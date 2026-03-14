// MDL Binary Reader - Animation parsing
// Partial class for animation data parsing

using System.Text;

namespace Radoub.Formats.Mdl;

public partial class MdlBinaryReader
{
    private void ParseAnimations(MdlModel model, uint arrayOffset, int count)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);

        for (int i = 0; i < count; i++)
        {
            stream.Position = arrayOffset + i * 4;
            var animPointer = reader.ReadUInt32();

            // Convert animation pointer to buffer offset
            var animBufferOffset = PointerToModelOffset(animPointer);
            if (animBufferOffset != 0xFFFFFFFF && animBufferOffset != uint.MaxValue)
            {
                var anim = ParseAnimation(animBufferOffset);
                model.Animations.Add(anim);
            }
        }
    }

    private MdlAnimation ParseAnimation(uint offset)
    {
        using var stream = new MemoryStream(_modelData);
        using var reader = new BinaryReader(stream);
        stream.Position = offset;

        var anim = new MdlAnimation();

        // Skip function pointers (2 uint32)
        stream.Position += 8;

        // Animation name (64 bytes) - from geometry header portion
        anim.Name = ReadFixedString(reader, 64);

        // Skip to animation-specific fields at offset 0x70
        stream.Position = offset + GeometryHeaderSize;

        anim.Length = reader.ReadSingle();
        anim.TransitionTime = reader.ReadSingle();
        anim.AnimRoot = ReadFixedString(reader, 64);

        // Events array
        var eventsPointer = reader.ReadUInt32();
        var eventsCount = reader.ReadUInt32();
        reader.ReadUInt32(); // allocated

        // Convert events pointer to buffer offset
        var eventsBufferOffset = PointerToModelOffset(eventsPointer);
        if (eventsCount > 0 && eventsBufferOffset != 0xFFFFFFFF && eventsBufferOffset != uint.MaxValue)
        {
            // Sanity check: cap event count to prevent reading past buffer on corrupted data.
            // Real NWN animations rarely have more than ~10 events.
            const uint maxReasonableEvents = 1000;
            var clampedCount = eventsCount;
            if (eventsCount > maxReasonableEvents)
            {
                Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                    $"[MDL] Animation '{anim.Name}': eventsCount={eventsCount} exceeds reasonable limit, clamping to {maxReasonableEvents}");
                clampedCount = maxReasonableEvents;
            }

            for (uint i = 0; i < clampedCount; i++)
            {
                var eventOffset = eventsBufferOffset + i * 36;
                if (eventOffset + 36 > _modelData.Length)
                {
                    Logging.UnifiedLogger.LogApplication(Logging.LogLevel.WARN,
                        $"[MDL] Animation '{anim.Name}': event {i} at offset 0x{eventOffset:X8} would read past buffer — stopping");
                    break;
                }
                stream.Position = eventOffset;
                var time = reader.ReadSingle();
                var eventName = ReadFixedString(reader, 32);
                anim.Events.Add(new MdlAnimationEvent { Time = time, EventName = eventName });
            }
        }

        return anim;
    }
}
