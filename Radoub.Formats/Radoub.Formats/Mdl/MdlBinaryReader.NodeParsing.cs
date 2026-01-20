// MDL Binary Reader - Node parsing
// Partial class for node creation and property routing

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    private MdlNode ParseNode(uint nodeOffset)
    {
        // Bounds check
        if (nodeOffset + NodeHeaderSize > _modelData.Length)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ParseNode: offset {nodeOffset} + {NodeHeaderSize} > modelDataLen {_modelData.Length}");
            return new MdlNode { Name = "invalid", NodeType = MdlNodeType.Dummy };
        }

        using var nodeStream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(nodeStream, Encoding.ASCII);
        reader.BaseStream.Position = nodeOffset;

        // Log first 16 bytes at this position for debugging
        var debugBytes = new byte[Math.Min(16, _modelData.Length - (int)nodeOffset)];
        Array.Copy(_modelData, nodeOffset, debugBytes, 0, debugBytes.Length);
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] ParseNode at {nodeOffset}: first16bytes={BitConverter.ToString(debugBytes)}");

        // Read node header
        // Skip function pointers (6 uint32 = 24 bytes)
        reader.BaseStream.Position = nodeOffset + 24;

        var inheritColor = reader.ReadUInt32();
        var partNumber = reader.ReadInt32();

        // Node name (32 bytes)
        var nodeName = ReadFixedString(reader, 32);

        // Skip geometry header pointer and parent pointer
        reader.ReadUInt32(); // geometry header
        reader.ReadUInt32(); // parent node

        // Children array
        var childArrayOffset = reader.ReadUInt32();
        var childCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Convert pointer to buffer offset
        var childArrayBufferOffset = PointerToModelOffset(childArrayOffset);

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] Node '{nodeName}': childArrayPtr=0x{childArrayOffset:X8} -> bufferOffset={childArrayBufferOffset}, childCount={childCount}");

        // Controller arrays (these are also pointers)
        var controllerKeyOffset = reader.ReadUInt32();
        var controllerKeyCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated
        var controllerDataOffset = reader.ReadUInt32();
        var controllerDataCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Node flags
        var nodeFlags = reader.ReadUInt32();

        // Determine node type and create appropriate object
        MdlNode node = CreateNodeFromFlags(nodeFlags, nodeName);

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] ParseNode: name='{nodeName}', flags=0x{nodeFlags:X8}, type={node.NodeType}, childCount={childCount}");

        // Set common properties
        node.Name = nodeName;
        node.InheritColor = inheritColor != 0;

        // Parse node-type-specific data
        try
        {
            ParseNodeTypeData(node, nodeOffset, nodeFlags, reader);
        }
        catch (Exception ex)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ParseNodeTypeData failed for '{nodeName}' (flags=0x{nodeFlags:X8}): {ex.Message}");
        }

        // Parse controller data for position/rotation (convert pointer to buffer offset)
        var ctrlKeyBufferOffset = PointerToModelOffset(controllerKeyOffset);
        var ctrlDataBufferOffset = PointerToModelOffset(controllerDataOffset);
        if (controllerKeyCount > 0 && ctrlKeyBufferOffset != 0xFFFFFFFF && ctrlKeyBufferOffset != uint.MaxValue)
        {
            try
            {
                ParseControllers(node, ctrlKeyBufferOffset, (int)controllerKeyCount, ctrlDataBufferOffset);
            }
            catch (Exception ex)
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                    $"[MDL] ParseControllers failed for '{nodeName}': {ex.Message}");
            }
        }

        // Parse children (pointer already converted above)
        if (childCount > 0 && childArrayBufferOffset != 0xFFFFFFFF && childArrayBufferOffset != uint.MaxValue)
        {
            ParseChildren(node, childArrayBufferOffset, (int)childCount);
        }

        return node;
    }

    private MdlNode CreateNodeFromFlags(uint flags, string name)
    {
        // Determine most specific node type
        if ((flags & NodeFlagHasAABB) != 0)
            return new MdlAabbNode { NodeType = MdlNodeType.Aabb };
        if ((flags & NodeFlagHasDangly) != 0)
            return new MdlDanglyNode { NodeType = MdlNodeType.Dangly };
        if ((flags & NodeFlagHasAnim) != 0)
            return new MdlAnimNode { NodeType = MdlNodeType.Anim };
        if ((flags & NodeFlagHasSkin) != 0)
            return new MdlSkinNode { NodeType = MdlNodeType.Skin };
        if ((flags & NodeFlagHasMesh) != 0)
            return new MdlTrimeshNode { NodeType = MdlNodeType.Trimesh };
        if ((flags & NodeFlagHasLight) != 0)
            return new MdlLightNode { NodeType = MdlNodeType.Light };
        if ((flags & NodeFlagHasEmitter) != 0)
            return new MdlEmitterNode { NodeType = MdlNodeType.Emitter };
        if ((flags & NodeFlagHasReference) != 0)
            return new MdlReferenceNode { NodeType = MdlNodeType.Reference };

        return new MdlNode { NodeType = MdlNodeType.Dummy };
    }

    private void ParseNodeTypeData(MdlNode node, uint nodeOffset, uint flags, BinaryReader reader)
    {
        // Light node data starts at offset 0x70
        if ((flags & NodeFlagHasLight) != 0 && node is MdlLightNode light)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseLightNode(light, reader);
        }

        // Emitter node data starts at offset 0x70
        if ((flags & NodeFlagHasEmitter) != 0 && node is MdlEmitterNode emitter)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseEmitterNode(emitter, reader);
        }

        // Reference node data starts at offset 0x70
        if ((flags & NodeFlagHasReference) != 0 && node is MdlReferenceNode reference)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseReferenceNode(reference, reader);
        }

        // Mesh data starts at offset 0x70
        if ((flags & NodeFlagHasMesh) != 0 && node is MdlTrimeshNode mesh)
        {
            reader.BaseStream.Position = nodeOffset + NodeHeaderSize;
            ParseMeshNode(mesh, reader, flags);
        }
    }

    private void ParseChildren(MdlNode parent, uint arrayOffset, int count)
    {
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] ParseChildren START: parent='{parent.Name}', arrayOffset={arrayOffset}, count={count}, modelDataLen={_modelData.Length}");

        // Verify we can read the child array
        if (arrayOffset + count * 4 > _modelData.Length)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ParseChildren: Child array out of bounds! arrayOffset={arrayOffset}, count={count}, needed={arrayOffset + count * 4}, available={_modelData.Length}");
            return;
        }

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var childPointer = reader.ReadUInt32();

            // Convert pointer to buffer offset
            var childOffset = PointerToModelOffset(childPointer);

            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                $"[MDL] ParseChildren: parent='{parent.Name}', i={i}, childPtr=0x{childPointer:X8} -> offset={childOffset}, valid={childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length}");

            if (childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length)
            {
                var child = ParseNode(childOffset);
                child.Parent = parent;
                parent.Children.Add(child);
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
                    $"[MDL] ParseChildren: Added child '{child.Name}' to '{parent.Name}', parent now has {parent.Children.Count} children");
            }
            else
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                    $"[MDL] ParseChildren: SKIPPED invalid child pointer for '{parent.Name}'[{i}]");
            }
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.DEBUG,
            $"[MDL] ParseChildren END: parent='{parent.Name}' now has {parent.Children.Count} children");
    }

    private void ParseControllers(MdlNode node, uint keyOffset, int keyCount, uint dataOffset)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < keyCount; i++)
        {
            var keyPos = keyOffset + i * ControllerKeySize;
            if (keyPos + ControllerKeySize > _modelData.Length)
                break; // Out of bounds

            stream.Position = keyPos;

            var type = reader.ReadInt32();
            var rows = reader.ReadInt16();
            var keyDataOffset = reader.ReadInt16();
            var valueDataOffset = reader.ReadInt16();
            var columns = reader.ReadByte();
            reader.ReadByte(); // pad

            // Read controller data based on type
            // Type 8 = Position, Type 20 = Orientation
            var valuePos = dataOffset + valueDataOffset * 4;

            if (type == 8 && rows > 0) // Position
            {
                if (valuePos + 12 > _modelData.Length) continue;
                stream.Position = valuePos;
                node.Position = ReadVector3(reader);
            }
            else if (type == 20 && rows > 0) // Orientation
            {
                if (valuePos + 16 > _modelData.Length) continue;
                stream.Position = valuePos;
                var x = reader.ReadSingle();
                var y = reader.ReadSingle();
                var z = reader.ReadSingle();
                var w = reader.ReadSingle();
                node.Orientation = new Quaternion(x, y, z, w);
            }
            else if (type == 36 && rows > 0) // Scale
            {
                if (valuePos + 4 > _modelData.Length) continue;
                stream.Position = valuePos;
                node.Scale = reader.ReadSingle();
            }
            else if (type == 76 && rows > 0 && node is MdlLightNode light) // Light Color
            {
                if (valuePos + 12 > _modelData.Length) continue;
                stream.Position = valuePos;
                light.Color = ReadVector3(reader);
            }
            else if (type == 88 && rows > 0 && node is MdlLightNode light2) // Light Radius
            {
                if (valuePos + 4 > _modelData.Length) continue;
                stream.Position = valuePos;
                light2.Radius = reader.ReadSingle();
            }
        }
    }
}
