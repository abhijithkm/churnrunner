using System.Diagnostics;

namespace ChurnRunner;

public partial class Form1 : Form
{
    private bool _connCheckInProgress;
    private CancellationTokenSource? _runCts;
    private DateTime? _scheduledRunTime;
    private bool _recurringEnabled;
    private DateTime? _lastRecurringTriggerDate;
    private Stopwatch? _runWatch;
    private readonly bool _preview;
    private DateTime? _lastConnTest;
    private bool _lastConnOk;
    private int? _processed;
    private int? _pending;
    private bool _processedRefreshing;
    private DateTime _lastProcessedRefresh;
    private readonly ToolTip _tips = new();
    private readonly AppUpdater _updater = new();

    public Form1() : this(preview: false) { }

    /// <param name="preview">
    /// Show the window filled with sample numbers and make no database or network
    /// calls, for checking the layout without touching the client's server.
    /// </param>
    public Form1(bool preview)
    {
        _preview = preview;
        InitializeComponent();
        AuthMode_CheckedChanged(this, EventArgs.Empty);
        lblRunTitle.Text = $"Churn for {DateTime.Today.AddDays(-1):dddd d MMMM}";
        ShowPendingCount(null);
        UpdateSideTiles();
        UpdateNextRunTile();

        if (preview)
        {
            ShowPreviewState();
            return;
        }

        bool hasSettings = LoadSettingsIntoForm();
        SetConnectionFieldsVisible(!hasSettings);
        UpdateTargetLabels();
        UpdateNextRunTile();
        UpdateLastRunTile();
        RefreshTaskStatus();
        _ = RefreshProcessedAsync();

        connCheckTimer.Start();
        scheduleTimer.Start();
        _ = CheckConnectionAsync();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        // Installed copies download the new version in the background and install it on restart.
        if (_updater.IsInstalled)
        {
            try
            {
                var version = await _updater.CheckAndDownloadAsync();
                if (version is null) return;
                lnkUpdateAvailable.Invoke(() =>
                {
                    lnkUpdateAvailable.Text = $"Version {version} is ready · restart to update";
                    lnkUpdateAvailable.Tag = null;
                    lnkUpdateAvailable.Visible = true;
                });
                Log($"Version {version} downloaded. Click \"restart to update\" in the header to install it.");
            }
            catch (Exception ex)
            {
                Log($"Update check FAILED: {ex.Message}");
            }
            return;
        }

        // A copy run straight from the zip only links to the release page.
        var update = await UpdateChecker.CheckForUpdateAsync();
        if (update is null) return;

        if (lnkUpdateAvailable.InvokeRequired)
        {
            lnkUpdateAvailable.Invoke(() => ShowUpdateAvailable(update));
            return;
        }
        ShowUpdateAvailable(update);
    }

    private void ShowUpdateAvailable(UpdateChecker.UpdateInfo update)
    {
        lnkUpdateAvailable.Text = $"Update available: v{update.LatestVersion}";
        lnkUpdateAvailable.Tag = update.Url;
        lnkUpdateAvailable.Visible = true;
    }

    private void LnkUpdateAvailable_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (lnkUpdateAvailable.Tag is string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        if (!_updater.HasUpdateReady) return;
        if (_runCts is not null)
        {
            MessageBox.Show("A run is in progress. Restart to update once it has finished.", "Update not installed",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show("Close Churn Runner, install the new version and open it again?", "Restart to update",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm == DialogResult.Yes) _updater.ApplyAndRestart();
    }

    private void ConnCheckTimer_Tick(object? sender, EventArgs e) => _ = CheckConnectionAsync();

    private void BtnCheckConnection_Click(object? sender, EventArgs e) => _ = CheckConnectionAsync();

    private void SetConnIndicator(string text, Color color)
    {
        if (lblConnIndicator.InvokeRequired)
        {
            lblConnIndicator.Invoke(() => SetConnIndicator(text, color));
            return;
        }

        lblConnIndicator.Text = text;
        lblConnIndicator.ForeColor = color;
    }

    /// <summary>Shows how long ago the connection was last tested; green when it connected, red when it did not.</summary>
    private void RefreshConnIndicator()
    {
        if (_lastConnTest is not DateTime tested) return;
        if (_connCheckInProgress && lblConnIndicator.Text.StartsWith("● Testing")) return;

        var ago = DateTime.Now - tested;
        string when = ago.TotalSeconds < 5 ? "just now"
            : ago.TotalSeconds < 60 ? $"{(int)ago.TotalSeconds} sec ago"
            : ago.TotalMinutes < 60 ? $"{(int)ago.TotalMinutes} min ago"
            : $"at {tested:HH:mm}";
        SetConnIndicator($"● Tested {when}", _lastConnOk ? Theme.Good : Theme.Fault);
        _tips.SetToolTip(lblConnIndicator, _lastConnOk
            ? $"Connected to {txtServer.Text.Trim()} at {tested:HH:mm:ss}."
            : $"Could not connect to {txtServer.Text.Trim()} at {tested:HH:mm:ss}. Check the connection settings.");
    }

    private async Task CheckConnectionAsync()
    {
        if (_connCheckInProgress) return;
        _connCheckInProgress = true;

        try
        {
            SetConnIndicator("● Testing…", Theme.Waiting);

            var runner = new ChurnJobRunner(BuildSettingsFromForm());
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(runner.BuildConnectionString())
            {
                ConnectTimeout = 5
            };

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", conn) { CommandTimeout = 5 };
            await cmd.ExecuteScalarAsync();

            _lastConnTest = DateTime.Now;
            _lastConnOk = true;
            RefreshConnIndicator();
        }
        catch
        {
            _lastConnTest = DateTime.Now;
            _lastConnOk = false;
            RefreshConnIndicator();
        }
        finally
        {
            _connCheckInProgress = false;
        }
    }

    private Settings BuildSettingsFromForm()
    {
        var s = new Settings
        {
            Server = txtServer.Text.Trim(),
            Database = txtDatabase.Text.Trim(),
            WindowsAuth = rbWindowsAuth.Checked,
            Username = txtUsername.Text.Trim(),
            TrustServerCertificate = chkTrustCert.Checked,
            HalfAscOverride = (int)numHalfAsc.Value,
            HalfDescOverride = (int)numHalfDesc.Value,
            RecurringEnabled = _recurringEnabled,
            RecurringDays = string.Join(",", Enumerable.Range(0, 7).Where(i => chkDays[i].Checked).Select(i => ((DayOfWeek)i).ToString())),
            RecurringTime = dtpRecurringTime.Value.ToString("HH:mm")
        };
        s.SetPassword(txtPassword.Text);
        return s;
    }

    /// <returns>True when saved settings were found and loaded.</returns>
    private bool LoadSettingsIntoForm()
    {
        var s = Settings.Load();
        if (s is null) return false;

        txtServer.Text = s.Server;
        txtDatabase.Text = s.Database;
        rbWindowsAuth.Checked = s.WindowsAuth;
        rbSqlAuth.Checked = !s.WindowsAuth;
        txtUsername.Text = s.Username;
        chkTrustCert.Checked = s.TrustServerCertificate;
        try { txtPassword.Text = s.GetPassword(); } catch { /* different machine/user profile */ }
        if (s.HalfAscOverride is int a) numHalfAsc.Value = a;
        if (s.HalfDescOverride is int d) numHalfDesc.Value = d;

        if (s.RecurringEnabled && !string.IsNullOrWhiteSpace(s.RecurringDays))
        {
            var days = s.RecurringDays
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => Enum.TryParse<DayOfWeek>(x, out var d2) ? d2 : (DayOfWeek?)null)
                .Where(d2 => d2.HasValue)
                .Select(d2 => d2!.Value)
                .ToHashSet();

            for (int i = 0; i < 7; i++) chkDays[i].Checked = days.Contains((DayOfWeek)i);

            if (TimeSpan.TryParse(s.RecurringTime, out var t)) dtpRecurringTime.Value = DateTime.Today.Add(t);

            _recurringEnabled = true;
            foreach (var c in chkDays) c.Enabled = false;
            dtpRecurringTime.Enabled = false;
            btnSaveRecurring.Enabled = false;
            btnRemoveRecurring.Enabled = true;
        }
        UpdateRecurringStatusLabel();

        Log("Loaded saved connection settings.");
        return true;
    }

    private void AuthMode_CheckedChanged(object? sender, EventArgs e)
    {
        bool sqlAuth = rbSqlAuth.Checked;
        txtUsername.Enabled = sqlAuth;
        txtPassword.Enabled = sqlAuth;
        UpdateTargetLabels();
    }

    private void ConnectionFields_Changed(object? sender, EventArgs e) => UpdateTargetLabels();

    private void UpdateTargetLabels()
    {
        string server = txtServer.Text.Trim();
        string database = txtDatabase.Text.Trim();
        lblTarget.Text = $"{server}  ·  {database}";
        lblConnSummary.Text = rbSqlAuth.Checked
            ? $"{server} / {database}, signed in as {txtUsername.Text.Trim()}"
            : $"{server} / {database}, signed in with your Windows account";
    }

    private void BtnEditConnection_Click(object? sender, EventArgs e) => SetConnectionFieldsVisible(false);

    private void BtnConnection_Click(object? sender, EventArgs e) => SetConnectionFieldsVisible(!pnlConnectionCard.Visible);

    /// <summary>The connection card is opened from the header; it opens by itself when nothing is saved yet.</summary>
    private void SetConnectionFieldsVisible(bool visible) => pnlConnectionCard.Visible = visible;

    private void Log(string message)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(() => Log(message));
            return;
        }

        Color color =
            message.Contains("FAILED", StringComparison.OrdinalIgnoreCase) || message.Contains("ERROR", StringComparison.OrdinalIgnoreCase) ? Theme.Fault :
            message.StartsWith("[ASC]", StringComparison.Ordinal) ? Theme.Asc :
            message.StartsWith("[DESC]", StringComparison.Ordinal) ? Theme.Desc :
            Theme.Ink;

        AppendLog($"{DateTime.Now:HH:mm:ss}  ", Theme.Waiting);
        WriteLogFile(message);
        AppendLog(message + Environment.NewLine, color);
        txtLog.ScrollToCaret();
    }

    /// <summary>Keeps every log line in %AppData%\ChurnRunner\logs\app_yyyyMMdd.log, one file per day.</summary>
    private void WriteLogFile(string message)
    {
        if (_preview) return;
        try
        {
            Directory.CreateDirectory(AppPaths.LogsFolder);
            File.AppendAllText(Path.Combine(AppPaths.LogsFolder, $"app_{DateTime.Now:yyyyMMdd}.log"),
                $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}{Environment.NewLine}");
        }
        catch
        {
            // The on-screen log still has the line; a locked or missing log folder must not stop a run.
        }
    }

    private void AppendLog(string text, Color color)
    {
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.SelectionLength = 0;
        txtLog.SelectionColor = color;
        txtLog.AppendText(text);
    }

    private void SetStatus(string text, Color color, Color tint)
    {
        lblStatus.Text = text;
        lblStatus.ForeColor = color;
        lblStatus.BackColor = tint;
    }

    private void SetButtonsEnabled(bool enabled)
    {
        btnTestConnection.Enabled = enabled;
        btnGetCount.Enabled = enabled;
        btnRunBoth.Enabled = enabled;
        btnSaveSettings.Enabled = enabled;
    }

    private void ShowPendingCount(int? count)
    {
        _pending = count;
        UpdateProcessedTile();
    }

    /// <summary>
    /// First tile: locations with yesterday's churn row written today, out of processed plus
    /// still pending. Either number can be missing until it has been read from the server.
    /// </summary>
    private void UpdateProcessedTile()
    {
        var yesterday = DateTime.Today.AddDays(-1);
        if (_processed is int done && _pending is int left)
        {
            lblPendingValue.Text = $"{done:N0} / {done + left:N0}";
            lblPendingNote.Text = left == 0 ? $"All done for {yesterday:ddd d MMM}" : $"{left:N0} still to process";
        }
        else if (_processed is int doneOnly)
        {
            lblPendingValue.Text = doneOnly.ToString("N0");
            lblPendingNote.Text = "Refresh the count for the total";
        }
        else if (_pending is int leftOnly)
        {
            lblPendingValue.Text = leftOnly.ToString("N0");
            lblPendingNote.Text = "Still to process";
        }
        else
        {
            lblPendingValue.Text = "—";
            lblPendingNote.Text = "Refresh the count.";
        }
    }

    /// <summary>Reads how many locations are processed so far (script\check1.sql). Read-only.</summary>
    private async Task RefreshProcessedAsync(ChurnJobRunner? runner = null, bool logResult = false)
    {
        if (_preview || _processedRefreshing) return;
        _processedRefreshing = true;
        _lastProcessedRefresh = DateTime.Now;
        try
        {
            runner ??= new ChurnJobRunner(BuildSettingsFromForm());
            _processed = await runner.GetProcessedCount();
            UpdateProcessedTile();
            if (logResult) Log($"Locations processed so far for {DateTime.Today.AddDays(-1):ddd d MMM}: {_processed}");
        }
        catch (Exception ex)
        {
            if (logResult) Log($"Processed count FAILED: {ex.Message}");
        }
        finally
        {
            _processedRefreshing = false;
        }
    }

    private void SplitValue_Changed(object? sender, EventArgs e)
    {
        if (_runWatch is not null) return;
        trackRun.SetSplit((int)numHalfAsc.Value, (int)numHalfDesc.Value);
        UpdateSideTiles();
    }

    private void SetProgress(bool asc, int cur, int total)
    {
        if (trackRun.InvokeRequired)
        {
            trackRun.Invoke(() => SetProgress(asc, cur, total));
            return;
        }

        if (asc) trackRun.SetAsc(cur, total);
        else trackRun.SetDesc(cur, total);
        UpdateSideTiles();
        UpdateRunMeta();
    }

    private void UpdateSideTiles()
    {
        lblAscValue.Text = $"{trackRun.AscDone:N0} / {trackRun.AscTotal:N0}";
        lblAscNote.Text = trackRun.AscTotal == 0 ? "Not split yet" : "From the top";
        lblDescValue.Text = $"{trackRun.DescDone:N0} / {trackRun.DescTotal:N0}";
        lblDescNote.Text = trackRun.DescTotal == 0 ? "Not split yet" : "From the bottom";
    }

    private void UpdateNextRunTile()
    {
        DateTime? next = _scheduledRunTime;
        string note = "One-time, in this app";

        if (_recurringEnabled && NextRecurringRun() is DateTime weekly && (next is null || weekly < next))
        {
            next = weekly;
            note = "Weekly, in this app";
        }

        if (next is DateTime n)
        {
            lblNextValue.Text = n.Date == DateTime.Today ? $"Today {n:HH:mm}" : $"{n:ddd HH:mm}";
            lblNextNote.Text = note;
        }
        else
        {
            lblNextValue.Text = "None";
            lblNextNote.Text = "Nothing scheduled";
        }
    }

    private void UpdateRunMeta()
    {
        if (_runWatch is null) return;

        var elapsed = _runWatch.Elapsed;
        int done = trackRun.Done;
        int left = trackRun.Total - done;
        string text = $"{(int)elapsed.TotalMinutes:00}:{elapsed.Seconds:00} elapsed";

        if (done > 0)
        {
            var perLocation = elapsed / done;
            var remaining = perLocation * left;
            text += $"  ·  {perLocation.TotalSeconds:0.0} s per location";
            if (left > 0)
                text += remaining.TotalMinutes < 1 ? "  ·  under a minute left" : $"  ·  about {Math.Ceiling(remaining.TotalMinutes):0} min left";
        }

        lblRunMeta.Text = text;
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        if (_runCts is null) return;
        Log("Stop requested — cancelling running EXEC(s). The in-flight location's write may be " +
            "partially applied; check that location's row in CustomerUnlimitedData if it matters.");
        _runCts.Cancel();
        btnStop.Enabled = false;
        SetStatus("Stopping…", Theme.Slate, Theme.WaitingTint);
    }

    private void BtnSaveSettings_Click(object? sender, EventArgs e)
    {
        try
        {
            BuildSettingsFromForm().Save();
            Log("Connection saved (password encrypted with DPAPI, current Windows user only).");
            SetConnectionFieldsVisible(false);
            _ = CheckConnectionAsync();
        }
        catch (Exception ex)
        {
            Log($"Save FAILED: {ex.Message}");
        }
    }

    private async void BtnTestConnection_Click(object? sender, EventArgs e)
    {
        SetButtonsEnabled(false);
        try
        {
            Log("Testing connection...");
            var runner = new ChurnJobRunner(BuildSettingsFromForm());
            using var conn = new Microsoft.Data.SqlClient.SqlConnection(runner.BuildConnectionString());
            await conn.OpenAsync();
            Log($"Connected OK. Server version: {conn.ServerVersion}");
        }
        catch (Exception ex)
        {
            Log($"Connection FAILED: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
            _ = CheckConnectionAsync();
        }
    }

    private async void BtnGetCount_Click(object? sender, EventArgs e)
    {
        SetButtonsEnabled(false);
        try
        {
            var runner = new ChurnJobRunner(BuildSettingsFromForm());
            var result = await runner.GetCount();

            ShowPendingCount(result.LocationCount);
            numHalfAsc.Value = result.HalfAsc;
            numHalfDesc.Value = result.HalfDesc;

            Log($"Pending locations: {result.LocationCount} -> ASC {result.HalfAsc} / DESC {result.HalfDesc}");
            await RefreshProcessedAsync(runner, logResult: true);
        }
        catch (Exception ex)
        {
            Log($"Get Count FAILED: {ex.Message}");
        }
        finally
        {
            SetButtonsEnabled(true);
        }
    }

    private async void BtnRunBoth_Click(object? sender, EventArgs e)
    {
        int halfAsc = (int)numHalfAsc.Value;
        int halfDesc = (int)numHalfDesc.Value;

        var confirm = MessageBox.Show(
            $"Start ASC ({halfAsc} locations) and DESC ({halfDesc} locations) in parallel on {txtServer.Text}/{txtDatabase.Text}?",
            "Start both runs",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        await RunBothFlow(halfAsc, halfDesc, "Manual");
    }

    private async Task RunBothFlow(int halfAsc, int halfDesc, string trigger)
    {
        SetButtonsEnabled(false);
        btnStop.Enabled = true;
        SetStatus("Running", Theme.Primary, Theme.PrimaryTint);
        trackRun.SetAsc(0, halfAsc);
        trackRun.SetDesc(0, halfDesc);
        UpdateSideTiles();
        _runWatch = Stopwatch.StartNew();
        var sw = _runWatch;
        var started = DateTime.Now;
        string result = "Failed";
        AfterRunCheck? check = null;
        UpdateRunMeta();

        var runner = new ChurnJobRunner(BuildSettingsFromForm());
        runner.OnLog += Log;

        _runCts = new CancellationTokenSource();

        try
        {
            await runner.RunBoth(
                halfAsc, halfDesc, _runCts.Token,
                (cur, total) => SetProgress(true, cur, total),
                (cur, total) => SetProgress(false, cur, total));
            Log($"Both runs completed in {sw.Elapsed:mm\\:ss}.");
            result = "Done";
            check = await RunAfterRunCheck(runner);
            if (check is null) SetStatus("Done", Theme.Good, Theme.GoodTint);
            else if (check.DuplicateLocations > 0) SetStatus($"Done · {check.DuplicateLocations} duplicated", Theme.Fault, Theme.FaultTint);
            else if (check.PendingLocations > 0) SetStatus($"Done · {check.PendingLocations} left", Theme.Desc, Theme.DescTint);
            else SetStatus("Done · all processed", Theme.Good, Theme.GoodTint);
        }
        catch (OperationCanceledException)
        {
            result = "Stopped";
            Log($"Run stopped by user after {sw.Elapsed:mm\\:ss}.");
            SetStatus("Stopped", Theme.Slate, Theme.WaitingTint);
        }
        catch (Exception)
        {
            Log($"One or both runs FAILED after {sw.Elapsed:mm\\:ss}. See log above.");
            SetStatus("Failed", Theme.Fault, Theme.FaultTint);
        }
        finally
        {
            UpdateRunMeta();
            _runWatch = null;
            RecordRun(new RunRecord(started, DateTime.Now, trigger, halfAsc, halfDesc, result, check?.PendingLocations, check?.DuplicateLocations));
            SetButtonsEnabled(true);
            btnStop.Enabled = false;
            _runCts?.Dispose();
            _runCts = null;
        }
    }

    private void BtnSetSchedule_Click(object? sender, EventArgs e)
    {
        var target = dtpScheduleTime.Value;
        if (target <= DateTime.Now)
        {
            MessageBox.Show("Pick a date and time in the future.", "Schedule not set",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _scheduledRunTime = target;
        dtpScheduleTime.Enabled = false;
        btnSetSchedule.Enabled = false;
        btnCancelSchedule.Enabled = true;
        lblScheduleStatus.Text = $"Scheduled for {target:ddd d MMM HH:mm}.";
        UpdateNextRunTile();
        Log($"Schedule set for {target:yyyy-MM-dd HH:mm:ss}. Keep this app open — it runs Get Count then Run Both automatically at that time.");
    }

    private void BtnCancelSchedule_Click(object? sender, EventArgs e)
    {
        _scheduledRunTime = null;
        dtpScheduleTime.Enabled = true;
        btnSetSchedule.Enabled = true;
        btnCancelSchedule.Enabled = false;
        lblScheduleStatus.Text = "Not scheduled.";
        UpdateNextRunTile();
        Log("Schedule cancelled.");
    }

    private async void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
        if (_runWatch is not null && DateTime.Now - _lastProcessedRefresh >= TimeSpan.FromSeconds(30))
            _ = RefreshProcessedAsync();

        UpdateRunMeta();
        RefreshConnIndicator();

        if (_scheduledRunTime is DateTime target)
        {
            var remaining = target - DateTime.Now;
            if (remaining <= TimeSpan.Zero)
            {
                _scheduledRunTime = null;
                dtpScheduleTime.Enabled = true;
                btnSetSchedule.Enabled = true;
                btnCancelSchedule.Enabled = false;
                lblScheduleStatus.Text = "Not scheduled.";
                UpdateNextRunTile();

                if (_runCts != null)
                {
                    Log("Scheduled run skipped: a run is already in progress.");
                }
                else
                {
                    Log("Scheduled time reached — starting auto run (Get Count, then Run Both).");
                    await RunScheduledJob();
                }
            }
            else
            {
                lblScheduleStatus.Text =
                    $"Scheduled for {target:ddd d MMM HH:mm} (in {(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}).";
            }
        }

        if (_recurringEnabled)
        {
            var now = DateTime.Now;
            bool dayMatches = chkDays[(int)now.DayOfWeek].Checked;
            bool timeMatches = now.Hour == dtpRecurringTime.Value.Hour && now.Minute == dtpRecurringTime.Value.Minute;

            if (dayMatches && timeMatches && _lastRecurringTriggerDate != now.Date)
            {
                _lastRecurringTriggerDate = now.Date;

                if (_runCts != null)
                {
                    Log("Recurring run skipped: a run is already in progress.");
                }
                else
                {
                    Log($"Recurring schedule triggered ({now.DayOfWeek} {dtpRecurringTime.Value:HH:mm}) — starting auto run.");
                    await RunScheduledJob();
                }
            }

            UpdateRecurringStatusLabel();
            UpdateNextRunTile();
        }
    }

    private void BtnSaveRecurring_Click(object? sender, EventArgs e)
    {
        if (!chkDays.Any(c => c.Checked))
        {
            MessageBox.Show("Select at least one day.", "Weekly schedule not saved",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _recurringEnabled = true;
        _lastRecurringTriggerDate = null;

        foreach (var c in chkDays) c.Enabled = false;
        dtpRecurringTime.Enabled = false;
        btnSaveRecurring.Enabled = false;
        btnRemoveRecurring.Enabled = true;

        BuildSettingsFromForm().Save();
        UpdateRecurringStatusLabel();
        UpdateNextRunTile();

        var dayList = string.Join(",", Enumerable.Range(0, 7).Where(i => chkDays[i].Checked).Select(i => (DayOfWeek)i));
        Log($"Recurring schedule saved: {dayList} at {dtpRecurringTime.Value:HH:mm}. Keep this app open for it to fire.");
    }

    private void BtnRemoveRecurring_Click(object? sender, EventArgs e)
    {
        _recurringEnabled = false;
        _lastRecurringTriggerDate = null;

        foreach (var c in chkDays) { c.Checked = false; c.Enabled = true; }
        dtpRecurringTime.Enabled = true;
        btnSaveRecurring.Enabled = true;
        btnRemoveRecurring.Enabled = false;

        BuildSettingsFromForm().Save();
        lblRecurringStatus.Text = "Not set.";
        UpdateNextRunTile();
        Log("Recurring schedule removed.");
    }

    private DateTime? NextRecurringRun()
    {
        var days = Enumerable.Range(0, 7).Where(i => chkDays[i].Checked).Select(i => (DayOfWeek)i).ToList();
        if (days.Count == 0) return null;

        var time = dtpRecurringTime.Value.TimeOfDay;
        var now = DateTime.Now;
        for (int add = 0; add < 8; add++)
        {
            var candidateDate = now.Date.AddDays(add);
            if (!days.Contains(candidateDate.DayOfWeek)) continue;
            var candidate = candidateDate + time;
            if (candidate > now) return candidate;
        }
        return null;
    }

    private void UpdateRecurringStatusLabel()
    {
        if (!_recurringEnabled)
        {
            lblRecurringStatus.Text = "Not set.";
            return;
        }

        var days = Enumerable.Range(0, 7).Where(i => chkDays[i].Checked).Select(i => (DayOfWeek)i).ToList();
        if (days.Count == 0)
        {
            lblRecurringStatus.Text = "Not set.";
            return;
        }

        var time = dtpRecurringTime.Value.TimeOfDay;
        var now = DateTime.Now;
        var dayList = string.Join(", ", days.OrderBy(d => (int)d).Select(d => d.ToString().Substring(0, 3)));
        lblRecurringStatus.Text = NextRecurringRun() is DateTime n
            ? $"Runs {dayList} at {time:hh\\:mm}. Next: {n:ddd d MMM HH:mm} (in {(int)(n - now).TotalHours:00}:{(n - now).Minutes:00}:{(n - now).Seconds:00})."
            : $"Runs {dayList} at {time:hh\\:mm}.";
    }

    private async Task RunScheduledJob()
    {
        SetButtonsEnabled(false);

        CountResult result;
        try
        {
            var runner = new ChurnJobRunner(BuildSettingsFromForm());
            result = await runner.GetCount();

            ShowPendingCount(result.LocationCount);
            numHalfAsc.Value = result.HalfAsc;
            numHalfDesc.Value = result.HalfDesc;

            Log($"Pending locations: {result.LocationCount} -> ASC {result.HalfAsc} / DESC {result.HalfDesc}");
            await RefreshProcessedAsync(runner, logResult: true);
        }
        catch (Exception ex)
        {
            Log($"Auto run: Get Count FAILED: {ex.Message}");
            SetButtonsEnabled(true);
            return;
        }

        await RunBothFlow(result.HalfAsc, result.HalfDesc, "Scheduled");
    }

    /// <summary>Read-only check after a finished run: what is still pending and any duplicated rows for yesterday.</summary>
    private async Task<AfterRunCheck?> RunAfterRunCheck(ChurnJobRunner runner)
    {
        SetStatus("Checking results…", Theme.Primary, Theme.PrimaryTint);
        try
        {
            var check = await runner.CheckAfterRun();
            ShowPendingCount(check.PendingLocations);
            await RefreshProcessedAsync(runner, logResult: true);
            Log($"After the run: {check.PendingLocations} location(s) still pending, {check.DuplicateLocations} location(s) with duplicate rows for yesterday.");
            if (check.PendingLocations > 0)
                Log("Some locations are still pending. Refresh the count and start both runs again to finish them.");
            if (check.DuplicateLocations > 0)
                Log("Duplicate rows found for yesterday. Check CustomerUnlimitedData before the next run; an interrupted earlier run can leave a second row per location.");
            return check;
        }
        catch (Exception ex)
        {
            Log($"After-run check FAILED: {ex.Message}");
            return null;
        }
    }

    private void RecordRun(RunRecord record)
    {
        try { RunHistory.Add(record); }
        catch (Exception ex) { Log($"Saving run history FAILED: {ex.Message}"); }
        UpdateLastRunTile();
    }

    private void UpdateLastRunTile()
    {
        var last = RunHistory.Load().FirstOrDefault();
        if (last is null)
        {
            lblLastValue.Text = "None";
            lblLastNote.Text = "No runs recorded yet";
            return;
        }
        ShowLastRun(last);
    }

    private void ShowLastRun(RunRecord last)
    {
        var took = last.Finished - last.Started;
        lblLastValue.Text = $"{(int)took.TotalMinutes} min";
        lblLastValue.ForeColor = last.Result switch { "Done" => Theme.Ink, "Failed" => Theme.Fault, _ => Theme.Slate };
        string left = last.PendingAfter is int n && n > 0 ? $", {n} left" : "";
        lblLastNote.Text = $"{last.Result} · {last.Started:ddd HH:mm}{left}";
    }

    private void BtnHistory_Click(object? sender, EventArgs e)
    {
        using var history = new HistoryForm(RunHistory.Load());
        history.ShowDialog(this);
    }

    private void BtnOpenLogs_Click(object? sender, EventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LogsFolder);
        Process.Start(new ProcessStartInfo(AppPaths.LogsFolder) { UseShellExecute = true });
    }

    private void BtnAddTask_Click(object? sender, EventArgs e)
    {
        var days = Enumerable.Range(0, 7).Where(i => chkDays[i].Checked).Select(i => (DayOfWeek)i).ToList();
        if (days.Count == 0)
        {
            MessageBox.Show("Select at least one day under Every week first.", "Windows task not added",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (Settings.Load() is null)
        {
            MessageBox.Show("Save the connection first. The Windows task uses the saved connection.", "Windows task not added",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var time = dtpRecurringTime.Value.TimeOfDay;
        string dayList = string.Join(", ", days.Select(d => d.ToString()[..3]));
        var confirm = MessageBox.Show(
            $"Add a Windows scheduled task that runs both scripts on {dayList} at {time:hh\\:mm}, even when this window is closed?" + Environment.NewLine + Environment.NewLine +
            "It runs as your Windows account while you are signed in. If the in-app weekly schedule is also saved, remove it so the run does not start twice.",
            "Add Windows task", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (confirm != DialogResult.Yes) return;

        var (ok, output) = TaskSchedulerSetup.Create(days, time);
        Log(ok ? $"Windows task added: {dayList} at {time:hh\\:mm}." : $"Adding the Windows task FAILED: {output}");
        RefreshTaskStatus();
    }

    private void BtnRemoveTask_Click(object? sender, EventArgs e)
    {
        var (ok, output) = TaskSchedulerSetup.Delete();
        Log(ok ? "Windows task removed." : $"Removing the Windows task FAILED: {output}");
        RefreshTaskStatus();
    }

    private void RefreshTaskStatus()
    {
        string? next;
        try { next = TaskSchedulerSetup.NextRunTime(); }
        catch { next = null; }

        btnRemoveTask.Enabled = next is not null;
        btnAddTask.Text = next is null ? "Add Windows task" : "Update Windows task";
        lblTaskStatus.Text = next is null
            ? "Not set up. Uses the days and time above."
            : string.IsNullOrEmpty(next) ? "Set up." : $"Set up. Next run: {next}.";
    }

    /// <summary>Sample state for <c>--preview</c>: a run partway through, no real connection.</summary>
    private void ShowPreviewState()
    {
        UpdateTargetLabels();
        _lastConnTest = DateTime.Now.AddSeconds(-15);
        _lastConnOk = true;
        RefreshConnIndicator();
        ShowLastRun(new RunRecord(DateTime.Today.AddHours(1), DateTime.Today.AddHours(1).AddMinutes(24), "Windows task", 285, 284, "Done", 0, 0));
        btnRemoveTask.Enabled = true;
        btnAddTask.Text = "Update Windows task";
        lblTaskStatus.Text = $"Set up. Next run: {DateTime.Today.AddDays(1).AddHours(1):dd/MM/yyyy HH:mm:ss}.";

        _processed = 219;
        ShowPendingCount(350);
        numHalfAsc.Value = 285;
        numHalfDesc.Value = 284;
        trackRun.SetAsc(121, 285);
        trackRun.SetDesc(98, 284);
        UpdateSideTiles();

        SetButtonsEnabled(false);
        btnStop.Enabled = true;
        SetStatus("Running", Theme.Primary, Theme.PrimaryTint);
        lblRunMeta.Text = "09:24 elapsed  ·  2.6 s per location  ·  about 15 min left";

        for (int i = 1; i <= 5; i++) chkDays[i].Checked = true;
        _recurringEnabled = true;
        UpdateRecurringStatusLabel();
        UpdateNextRunTile();

        Log("Pending locations: 569 -> ASC 285 / DESC 284");
        Log("[ASC] starting usp_MisterChurnPopulate_Asc.sql (Half=285)");
        Log("[DESC] starting usp_MisterChurnPopulate_Desc.sql (Half=284)");
        Log("[ASC] [121/285] Processing location (A6652138-69C4-477C-A1F1-0E9AE65B43CA)");
        Log("[DESC] [98/284] Processing location (5DEF20DE-5F47-43DC-8F26-EC23453D7F82)");
        Log("Connection FAILED: example of how an error line looks.");
    }
}
