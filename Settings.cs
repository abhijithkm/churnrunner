using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChurnRunner;

public class Settings
{
    public string Server { get; set; } = "";
    public string Database { get; set; } = "";
    public bool WindowsAuth { get; set; }
    public string Username { get; set; } = "";
    public string ProtectedPassword { get; set; } = ""; // DPAPI, base64
    public bool TrustServerCertificate { get; set; } = true;
    public int? HalfAscOverride { get; set; }
    public int? HalfDescOverride { get; set; }

    public bool RecurringEnabled { get; set; }
    public string RecurringDays { get; set; } = ""; // comma-separated DayOfWeek names, e.g. "Friday,Saturday"
    public string RecurringTime { get; set; } = "01:00"; // HH:mm, 24h

    private static string SettingsPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ChurnRunner", "settings.json");

    public static Settings? Load()
    {
        if (!File.Exists(SettingsPath)) return null;
        var json = File.ReadAllText(SettingsPath);
        return JsonSerializer.Deserialize<Settings>(json);
    }

    public void Save()
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
    }

    public void SetPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            ProtectedPassword = "";
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var protectedBytes = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        ProtectedPassword = Convert.ToBase64String(protectedBytes);
    }

    public string GetPassword()
    {
        if (string.IsNullOrEmpty(ProtectedPassword)) return "";
        var protectedBytes = Convert.FromBase64String(ProtectedPassword);
        var bytes = ProtectedData.Unprotect(protectedBytes, null, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
