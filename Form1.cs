using System.Diagnostics;

namespace ChurnRunner;

public partial class Form1 : Form
{
    private bool _connCheckInProgress;
    private CancellationTokenSource? _runCts;
    private DateTime? _scheduledRunTime;
    private bool _recurringEnabled;
    private DateTime? _lastRecurringTriggerDate;

    public Form1()
    {
        InitializeComponent();
        AuthMode_CheckedChanged(this, EventArgs.Empty);
        LoadSettingsIntoForm();

        connCheckTimer.Start();
        scheduleTimer.Start();
        _ = CheckConnectionAsync();
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
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
        lnkUpdateAvailable.Text = $"⬆ Update available: v{update.LatestVersion}";
        lnkUpdateAvailable.Tag = update.Url;
        lnkUpdateAvailable.Visible = true;
    }

    private void LnkUpdateAvailable_LinkClicked(object? sender, LinkLabelLinkClickedEventArgs e)
    {
        if (lnkUpdateAvailable.Tag is string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
    }

    private void ConnCheckTimer_Tick(object? sender, EventArgs e) => _ = CheckConnectionAsync();

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

    private async Task CheckConnectionAsync()
    {
        if (_connCheckInProgress) return;
        _connCheckInProgress = true;

        try
        {
            SetConnIndicator("● Checking...", Color.Gray);

            var runner = new ChurnJobRunner(BuildSettingsFromForm());
            var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(runner.BuildConnectionString())
            {
                ConnectTimeout = 5
            };

            using var conn = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString);
            await conn.OpenAsync();
            using var cmd = new Microsoft.Data.SqlClient.SqlCommand("SELECT 1", conn) { CommandTimeout = 5 };
            await cmd.ExecuteScalarAsync();

            SetConnIndicator($"● Connected ({DateTime.Now:HH:mm:ss})", Color.Green);
        }
        catch
        {
            SetConnIndicator($"● Disconnected ({DateTime.Now:HH:mm:ss})", Color.Red);
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

    private void LoadSettingsIntoForm()
    {
        var s = Settings.Load();
        if (s is null) return;

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
    }

    private void AuthMode_CheckedChanged(object? sender, EventArgs e)
    {
        bool sqlAuth = rbSqlAuth.Checked;
        txtUsername.Enabled = sqlAuth;
        txtPassword.Enabled = sqlAuth;
    }

    private void Log(string message)
    {
        if (txtLog.InvokeRequired)
        {
            txtLog.Invoke(() => Log(message));
            return;
        }

        txtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}{Environment.NewLine}");
    }

    private void SetStatus(string text) => lblStatus.Text = text;

    private void SetButtonsEnabled(bool enabled)
    {
        btnTestConnection.Enabled = enabled;
        btnGetCount.Enabled = enabled;
        btnRunBoth.Enabled = enabled;
        btnSaveSettings.Enabled = enabled;
    }

    private void SetProgress(ProgressBar bar, Label label, string side, int cur, int total)
    {
        if (bar.InvokeRequired)
        {
            bar.Invoke(() => SetProgress(bar, label, side, cur, total));
            return;
        }

        bar.Maximum = Math.Max(total, 1);
        bar.Value = Math.Clamp(cur, 0, bar.Maximum);
        label.Text = $"{side}: {cur}/{total}";
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        if (_runCts is null) return;
        Log("Stop requested — cancelling running EXEC(s). The in-flight location's write may be " +
            "partially applied; check that location's row in CustomerUnlimitedData if it matters.");
        _runCts.Cancel();
        btnStop.Enabled = false;
    }

    private void BtnSaveSettings_Click(object? sender, EventArgs e)
    {
        try
        {
            BuildSettingsFromForm().Save();
            Log("Connection settings saved (password encrypted with DPAPI, current Windows user only).");
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

            txtLocationCount.Text = result.LocationCount.ToString();
            numHalfAsc.Value = result.HalfAsc;
            numHalfDesc.Value = result.HalfDesc;

            Log($"Pending locations: {result.LocationCount} -> ASC {result.HalfAsc} / DESC {result.HalfDesc}");
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
            $"Run ASC with @Half={halfAsc} and DESC with @Half={halfDesc} in parallel against {txtServer.Text}/{txtDatabase.Text}?",
            "Confirm Run",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        await RunBothFlow(halfAsc, halfDesc);
    }

    private async Task RunBothFlow(int halfAsc, int halfDesc)
    {
        SetButtonsEnabled(false);
        btnStop.Enabled = true;
        SetStatus("Running...");
        SetProgress(pbAsc, lblAscProgress, "ASC", 0, halfAsc);
        SetProgress(pbDesc, lblDescProgress, "DESC", 0, halfDesc);
        var sw = Stopwatch.StartNew();

        var runner = new ChurnJobRunner(BuildSettingsFromForm());
        runner.OnLog += Log;

        _runCts = new CancellationTokenSource();

        try
        {
            await runner.RunBoth(
                halfAsc, halfDesc, _runCts.Token,
                (cur, total) => SetProgress(pbAsc, lblAscProgress, "ASC", cur, total),
                (cur, total) => SetProgress(pbDesc, lblDescProgress, "DESC", cur, total));
            Log($"Both runs completed in {sw.Elapsed:mm\\:ss}.");
            SetStatus("Done");
        }
        catch (OperationCanceledException)
        {
            Log($"Run stopped by user after {sw.Elapsed:mm\\:ss}.");
            SetStatus("Stopped");
        }
        catch (Exception)
        {
            Log($"One or both runs FAILED after {sw.Elapsed:mm\\:ss}. See log above.");
            SetStatus("Failed");
        }
        finally
        {
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
            MessageBox.Show("Pick a date/time in the future.", "Invalid schedule",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _scheduledRunTime = target;
        dtpScheduleTime.Enabled = false;
        btnSetSchedule.Enabled = false;
        btnCancelSchedule.Enabled = true;
        lblScheduleStatus.Text = $"Scheduled for {target:yyyy-MM-dd HH:mm:ss}.";
        Log($"Schedule set for {target:yyyy-MM-dd HH:mm:ss}. Keep this app open — it runs Get Count then Run Both automatically at that time.");
    }

    private void BtnCancelSchedule_Click(object? sender, EventArgs e)
    {
        _scheduledRunTime = null;
        dtpScheduleTime.Enabled = true;
        btnSetSchedule.Enabled = true;
        btnCancelSchedule.Enabled = false;
        lblScheduleStatus.Text = "Not scheduled.";
        Log("Schedule cancelled.");
    }

    private async void ScheduleTimer_Tick(object? sender, EventArgs e)
    {
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
                    $"Scheduled for {target:yyyy-MM-dd HH:mm:ss} (in {(int)remaining.TotalHours:00}:{remaining.Minutes:00}:{remaining.Seconds:00}).";
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
        }
    }

    private void BtnSaveRecurring_Click(object? sender, EventArgs e)
    {
        if (!chkDays.Any(c => c.Checked))
        {
            MessageBox.Show("Select at least one day.", "Invalid schedule",
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
        Log("Recurring schedule removed.");
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
        DateTime? next = null;
        for (int add = 0; add < 8; add++)
        {
            var candidateDate = now.Date.AddDays(add);
            if (!days.Contains(candidateDate.DayOfWeek)) continue;
            var candidate = candidateDate + time;
            if (candidate > now) { next = candidate; break; }
        }

        var dayList = string.Join(",", days.OrderBy(d => (int)d).Select(d => d.ToString().Substring(0, 3)));
        lblRecurringStatus.Text = next is DateTime n
            ? $"Runs {dayList} at {time:hh\\:mm}. Next: {n:yyyy-MM-dd HH:mm} (in {(int)(n - now).TotalHours:00}:{(n - now).Minutes:00}:{(n - now).Seconds:00})."
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

            txtLocationCount.Text = result.LocationCount.ToString();
            numHalfAsc.Value = result.HalfAsc;
            numHalfDesc.Value = result.HalfDesc;

            Log($"Pending locations: {result.LocationCount} -> ASC {result.HalfAsc} / DESC {result.HalfDesc}");
        }
        catch (Exception ex)
        {
            Log($"Auto run: Get Count FAILED: {ex.Message}");
            SetButtonsEnabled(true);
            return;
        }

        await RunBothFlow(result.HalfAsc, result.HalfDesc);
    }
}
