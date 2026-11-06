using System.Text.Json;

namespace ChurnRunner;

/// <summary>One finished run of both scripts.</summary>
/// <param name="Trigger">"Manual", "Scheduled" (in-app schedule) or "Windows task" (--auto).</param>
/// <param name="Result">"Done", "Stopped" or "Failed".</param>
/// <param name="PendingAfter">Locations still pending after the run; null when the check did not run.</param>
/// <param name="DuplicateLocations">Locations with more than one row for yesterday; null when the check did not run.</param>
public record RunRecord(
    DateTime Started,
    DateTime Finished,
    string Trigger,
    int HalfAsc,
    int HalfDesc,
    string Result,
    int? PendingAfter,
    int? DuplicateLocations);

/// <summary>Where the app keeps its files under %AppData%\ChurnRunner.</summary>
public static class AppPaths
{
    public static string Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChurnRunner");

    public static string LogsFolder => Path.Combine(Folder, "logs");
}

/// <summary>The last runs, newest first, kept in history.json.</summary>
public static class RunHistory
{
    private const int Keep = 50;
    private static readonly object Gate = new();
    private static string FilePath => Path.Combine(AppPaths.Folder, "history.json");

    public static List<RunRecord> Load()
    {
        lock (Gate)
        {
            try
            {
                if (!File.Exists(FilePath)) return new();
                return JsonSerializer.Deserialize<List<RunRecord>>(File.ReadAllText(FilePath)) ?? new();
            }
            catch
            {
                // A damaged file must not stop a run; start a fresh history instead.
                return new();
            }
        }
    }

    public static void Add(RunRecord record)
    {
        lock (Gate)
        {
            var list = Load();
            list.Insert(0, record);
            if (list.Count > Keep) list.RemoveRange(Keep, list.Count - Keep);
            Directory.CreateDirectory(AppPaths.Folder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}
