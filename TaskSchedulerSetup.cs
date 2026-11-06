using System.Diagnostics;
using System.Globalization;

namespace ChurnRunner;

/// <summary>
/// Creates, removes and reads the Windows scheduled task that runs this app with
/// --auto, so the nightly run happens even when the window is closed. The task runs
/// as the current Windows user, which the saved password (DPAPI, current user) needs.
/// </summary>
public static class TaskSchedulerSetup
{
    public const string TaskName = "ChurnRunner nightly run";

    private static readonly string[] DayCodes = { "SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT" };

    public static (bool ok, string output) Create(IEnumerable<DayOfWeek> days, TimeSpan time)
    {
        string exe = Environment.ProcessPath ?? Application.ExecutablePath;
        return Run("/Create", "/F",
            "/TN", TaskName,
            "/TR", $"\"{exe}\" --auto",
            "/SC", "WEEKLY",
            "/D", string.Join(",", days.Select(d => DayCodes[(int)d])),
            "/ST", time.ToString(@"hh\:mm", CultureInfo.InvariantCulture));
    }

    public static (bool ok, string output) Delete() => Run("/Delete", "/F", "/TN", TaskName);

    /// <returns>The task's next run time as Windows reports it, or null when the task does not exist.</returns>
    public static string? NextRunTime()
    {
        var (ok, output) = Run("/Query", "/TN", TaskName, "/FO", "LIST");
        if (!ok) return null;

        var line = output.Split('\n').FirstOrDefault(l => l.TrimStart().StartsWith("Next Run Time", StringComparison.OrdinalIgnoreCase));
        if (line is null) return "";
        int colon = line.IndexOf(':');
        return colon < 0 ? "" : line[(colon + 1)..].Trim();
    }

    private static (bool ok, string output) Run(params string[] args)
    {
        var psi = new ProcessStartInfo("schtasks.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        using var p = Process.Start(psi)!;
        string stdout = p.StandardOutput.ReadToEnd();
        string stderr = p.StandardError.ReadToEnd();
        p.WaitForExit(15000);
        return (p.ExitCode == 0, (stdout + stderr).Trim());
    }
}
