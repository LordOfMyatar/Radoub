// MDL Binary Reader - Node parsing
// Partial class for node creation and property routing

using System.Numerics;
using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    // Maximum node tree depth to prevent stack overflow from circular references.
    // Real NWN models rarely exceed depth 10; 128 is generous.
    private const int MaxNodeDepth = 128;

    private MdlNode ParseNode(uint nodeOffset)
    {
        var visitedNodeOffsets = new HashSet<uint>();
        return ParseNodeInternal(nodeOffset, 0, visitedNodeOffsets);
    }

    private MdlNode ParseNodeInternal(uint nodeOffset, int depth, HashSet<uint> visitedNodeOffsets)
    {
        if (depth >= MaxNodeDepth)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] Node tree exceeded max depth {MaxNodeDepth} at offset 0x{nodeOffset:X8} — returning dummy node");
            return new MdlNode { Name = "depth_limit", NodeType = MdlNodeType.Dummy };
        }

        if (!visitedNodeOffsets.Add(nodeOffset))
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] Node tree circular reference detected at offset 0x{nodeOffset:X8} — returning dummy node");
            return new MdlNode { Name = "circular_ref", NodeType = MdlNodeType.Dummy };
        }

        return ParseNodeCore(nodeOffset, depth, visitedNodeOffsets);
    }

    private MdlNode ParseNodeCore(uint nodeOffset, int depth, HashSet<uint> visitedNodeOffsets)
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
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
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

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
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

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
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
            ParseChildren(node, childArrayBufferOffset, (int)childCount, depth, visitedNodeOffsets);
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

    private void ParseChildren(MdlNode parent, uint arrayOffset, int count, int parentDepth, HashSet<uint> visitedNodeOffsets)
    {
        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
            $"[MDL] ParseChildren START: parent='{parent.Name}', arrayOffset={arrayOffset}, count={count}, depth={parentDepth}, modelDataLen={_modelData.Length}");

        // Verify we can read the child array
        if (arrayOffset + count * 4 > _modelData.Length)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ParseChildren: Child array out of bounds! arrayOffset={arrayOffset}, count={count}, needed={arrayOffset + count * 4}, available={_modelData.Length}");
            return;
        }

        // Sanity check child count — corrupted data can have huge values
        if (count > 10000)
        {
            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                $"[MDL] ParseChildren: childCount={count} exceeds reasonable limit for '{parent.Name}' — clamping to 10000");
            count = 10000;
        }

        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var childPointer = reader.ReadUInt32();

            // Convert pointer to buffer offset
            var childOffset = PointerToModelOffset(childPointer);

            Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
                $"[MDL] ParseChildren: parent='{parent.Name}', i={i}, childPtr=0x{childPointer:X8} -> offset={childOffset}, valid={childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length}");

            if (childOffset != 0xFFFFFFFF && childOffset != uint.MaxValue && childOffset < _modelData.Length)
            {
                var child = ParseNodeInternal(childOffset, parentDepth + 1, visitedNodeOffsets);
                child.Parent = parent;
                parent.Children.Add(child);
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
                    $"[MDL] ParseChildren: Added child '{child.Name}' to '{parent.Name}', parent now has {parent.Children.Count} children");
            }
            else
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                    $"[MDL] ParseChildren: SKIPPED invalid child pointer for '{parent.Name}'[{i}]");
            }
        }

        Logging.UnifiedLogger.LogApplication(Logging.LogLevel.TRACE,
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

            if (rows <= 0) continue;

            // Times array immediately precedes values (NWN convention): one
            // float per row at dataOffset + keyDataOffset * 4. Values follow.
            var timePos = dataOffset + keyDataOffset * 4;
            var valuePos = dataOffset + valueDataOffset * 4;

            // Read time keys (#2124) — parse all rows for animation playback.
            // rows == 1 + no time stream = static bind pose (legacy behavior).
            float[]? times = null;
            if (rows > 1 && timePos + rows * 4 <= _modelData.Length)
            {
                times = new float[rows];
                stream.Position = timePos;
                for (int k = 0; k < rows; k++)
                    times[k] = reader.ReadSingle();
            }

            if (type == 8) // Position (Vector3)
            {
                if (valuePos + rows * 12 > _modelData.Length) continue;
                stream.Position = valuePos;
                var first = ReadVector3(reader);

                if (rows == 1 || times == null)
                {
                    node.Position = first;
                }
                else
                {
                    var values = new Vector3[rows];
                    values[0] = first;
                    for (int k = 1; k < rows; k++)
                        values[k] = ReadVector3(reader);

                    node.PositionTimes = times;
                    node.PositionValues = values;
                    node.Position = first; // bind pose fallback
                }
            }
            else if (type == 20) // Orientation (Quaternion, 16 bytes)
            {
                if (valuePos + rows * 16 > _modelData.Length) continue;
                stream.Position = valuePos;
                Quaternion ReadQuat()
                {
                    var x = reader.ReadSingle();
                    var y = reader.ReadSingle();
                    var z = reader.ReadSingle();
                    var w = reader.ReadSingle();
                    return new Quaternion(x, y, z, w);
                }

                var first = ReadQuat();
                if (rows == 1 || times == null)
                {
                    node.Orientation = first;
                }
                else
                {
                    var values = new Quaternion[rows];
                    values[0] = first;
                    for (int k = 1; k < rows; k++)
                        values[k] = ReadQuat();

                    node.OrientationTimes = times;
                    node.OrientationValues = values;
                    node.Orientation = first;
                }
            }
            else if (type == 36) // Scale (float)
            {
                if (valuePos + rows * 4 > _modelData.Length) continue;
                stream.Position = valuePos;
                var first = reader.ReadSingle();

                if (rows == 1 || times == null)
                {
                    node.Scale = first;
                }
                else
                {
                    var values = new float[rows];
                    values[0] = first;
                    for (int k = 1; k < rows; k++)
                        values[k] = reader.ReadSingle();

                    node.ScaleTimes = times;
                    node.ScaleValues = values;
                    node.Scale = first;
                }
            }
            else if (type == 76 && node is MdlLightNode light) // Light Color
            {
                if (valuePos + 12 > _modelData.Length) continue;
                stream.Position = valuePos;
                light.Color = ReadVector3(reader);
            }
            else if (type == 88 && node is MdlLightNode light2) // Light Radius
            {
                if (valuePos + 4 > _modelData.Length) continue;
                stream.Position = valuePos;
                light2.Radius = reader.ReadSingle();
            }
        }
    }
}
