using System.Text.Json;
using System.Text.Json.Serialization;
using Radoub.Formats.Logging;

namespace Radoub.UI.Services;

public record QuickTokenSlot(int Slot, string? Token, string? Label);

public class QuickTokenService
{
    private readonly string _configPath;

    public QuickTokenService(string configPath)
    {
        _configPath = configPath;
    }

    public QuickTokenService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "quick-tokens.json"))
    {
    }

    public QuickTokenSlot[] Load()
    {
        var slots = new QuickTokenSlot[]
        {
            new(1, null, null),
            new(2, null, null),
            new(3, null, null)
        };

        try
        {
            if (!File.Exists(_configPath))
                return slots;

            var json = File.ReadAllText(_configPath);
            var data = JsonSerializer.Deserialize<QuickTokenFile>(json);
            if (data?.QuickSlots == null)
                return slots;

            foreach (var entry in data.QuickSlots)
            {
                if (entry.Slot >= 1 && entry.Slot <= 3)
                    slots[entry.Slot - 1] = new QuickTokenSlot(entry.Slot, entry.Token, entry.Label);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load quick-tokens.json: {ex.Message}");
        }

        return slots;
    }

    public void Save(QuickTokenSlot[] slots)
    {
        try
        {
            var dir = Path.GetDirectoryName(_configPath);
            if (dir != null && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var data = new QuickTokenFile
            {
                QuickSlots = slots
                    .Where(s => s.Token != null)
                    .Select(s => new QuickTokenFileEntry
                    {
                        Slot = s.Slot,
                        Token = s.Token!,
                        Label = s.Label ?? ""
                    })
                    .ToList()
            };

            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_configPath, json);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to save quick-tokens.json: {ex.Message}");
        }
    }

    private class QuickTokenFile
    {
        [JsonPropertyName("quickSlots")]
        public List<QuickTokenFileEntry> QuickSlots { get; set; } = new();
    }

    private class QuickTokenFileEntry
    {
        [JsonPropertyName("slot")]
        public int Slot { get; set; }

        [JsonPropertyName("token")]
        public string Token { get; set; } = "";

        [JsonPropertyName("label")]
        public string Label { get; set; } = "";
    }
}
