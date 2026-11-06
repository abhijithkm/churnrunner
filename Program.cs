namespace ChurnRunner;

static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        if (args.Contains("--auto", StringComparer.OrdinalIgnoreCase))
        {
            int exitCode = RunHeadless().GetAwaiter().GetResult();
            Environment.Exit(exitCode);
            return;
        }

        ApplicationConfiguration.Initialize();

        // --preview opens the window with sample numbers and no database or network calls.
        bool preview = args.Contains("--preview", StringComparer.OrdinalIgnoreCase);
        Application.Run(new Form1(preview));
    }

    private static async Task<int> RunHeadless()
    {
        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ChurnRunner", "logs");
        Directory.CreateDirectory(logDir);
        var logPath = Path.Combine(logDir, $"run_{DateTime.Now:yyyyMMdd_HHmmss}.log");

        void Log(string message)
        {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            File.AppendAllText(logPath, line + Environment.NewLine);
        }

        try
        {
            var settings = Settings.Load();
            if (settings is null)
            {
                Log("No saved settings found. Open the app once, fill in the connection, and click 'Save connection' before running with --auto.");
                return 1;
            }

            var runner = new ChurnJobRunner(settings);
            runner.OnLog += Log;

            Log("Auto run starting.");

            // Always compute a fresh split from the live pending count — a scheduled
            // run must not reuse whatever HalfAsc/HalfDesc happened to be on screen
            // the last time someone clicked "Save" in the GUI.
            var count = await runner.GetCount();
            Log($"Pending locations: {count.LocationCount} -> ASC {count.HalfAsc} / DESC {count.HalfDesc}");

            var started = DateTime.Now;
            try
            {
                await runner.RunBoth(count.HalfAsc, count.HalfDesc);
            }
            catch
            {
                RunHistory.Add(new RunRecord(started, DateTime.Now, "Windows task", count.HalfAsc, count.HalfDesc, "Failed", null, null));
                throw;
            }
            Log("Auto run completed OK.");

            AfterRunCheck? check = null;
            try
            {
                check = await runner.CheckAfterRun();
                Log($"After the run: {check.PendingLocations} location(s) still pending, {check.DuplicateLocations} location(s) with duplicate rows for yesterday.");
            }
            catch (Exception ex)
            {
                Log($"After-run check FAILED: {ex.Message}");
            }

            RunHistory.Add(new RunRecord(started, DateTime.Now, "Windows task", count.HalfAsc, count.HalfDesc, "Done",
                check?.PendingLocations, check?.DuplicateLocations));
            return 0;
        }
        catch (Exception ex)
        {
            Log($"Auto run FAILED: {ex}");
            return 1;
        }
    }
}
