using System.Text.Json;

namespace QuotaTray.Infra;

internal sealed class Settings
{
    public int RefreshIntervalMinutes { get; set; } = 10;

    public string? ChatGptEndpoint { get; set; }

    public string ChatGptJsonPath { get; set; } = "";

    public double? ChatGptMaxValue { get; set; }

    public bool ChatGptValueIsRemaining { get; set; }

    public string? GoEndpoint { get; set; }

    public string GoJsonPath5h { get; set; } = "";

    public string GoJsonPathWeek { get; set; } = "";

    public string GoJsonPathMonth { get; set; } = "";

    public string? GoWorkspaceId { get; set; }

    public double GoLimit5h { get; set; } = 12;

    public double GoLimitWeek { get; set; } = 30;

    public double GoLimitMonth { get; set; } = 60;

    public static Settings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                return JsonSerializer.Deserialize<Settings>(json) ?? new Settings();
            }
        }
        catch (Exception ex)
        {
            Logger.Log("Settings.Load error: " + ex.Message);
        }

        return new Settings();
    }

    public void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(AppPaths.SettingsFile, json);
        }
        catch (Exception ex)
        {
            Logger.Log("Settings.Save error: " + ex.Message);
        }
    }
}

internal static class JsonPath
{
    public static JsonElement? Get(JsonElement root, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var current = root;
        foreach (var part in path.Split('.'))
        {
            var name = part;
            var index = -1;
            var match = System.Text.RegularExpressions.Regex.Match(part, @"^(\w+)\[(\d+)\]$");
            if (match.Success)
            {
                name = match.Groups[1].Value;
                index = int.Parse(match.Groups[2].Value);
            }

            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(name, out var next))
            {
                return null;
            }

            current = next;
            if (index >= 0)
            {
                if (current.ValueKind != JsonValueKind.Array || index >= current.GetArrayLength())
                {
                    return null;
                }

                current = current[index];
            }
        }

        return current;
    }
}
