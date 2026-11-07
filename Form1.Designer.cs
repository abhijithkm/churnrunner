namespace ChurnRunner;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    // Header
    private Label lblTarget;
    private Label lblConnIndicator;
    private LinkLabel lnkUpdateAvailable;
    private ThemedButton btnCheckConnection;
    private System.Windows.Forms.Timer connCheckTimer;

    // Run (hero)
    private Label lblRunTitle;
    private ThemedButton btnGetCount;
    private NumericUpDown numHalfAsc;
    private NumericUpDown numHalfDesc;
    private MeetTrack trackRun;
    private ThemedButton btnRunBoth;
    private ThemedButton btnStop;
    private Label lblStatus;
    private Label lblRunMeta;

    // Tiles
    private Label lblPendingValue;
    private Label lblPendingNote;
    private Label lblAscValue;
    private Label lblAscNote;
    private Label lblDescValue;
    private Label lblDescNote;
    private Label lblNextValue;
    private Label lblNextNote;
    private Label lblLastValue;
    private Label lblLastNote;

    // Connection
    private ThemedButton btnConnection;
    private Card pnlConnectionCard;
    private ThemedButton btnEditConnection;
    private Label lblConnSummary;
    private TableLayoutPanel pnlConnectionFields;
    private TextBox txtServer;
    private TextBox txtDatabase;
    private RadioButton rbSqlAuth;
    private RadioButton rbWindowsAuth;
    private TextBox txtUsername;
    private TextBox txtPassword;
    private CheckBox chkTrustCert;
    private ThemedButton btnTestConnection;
    private ThemedButton btnSaveSettings;

    // Schedule
    private DateTimePicker dtpScheduleTime;
    private ThemedButton btnSetSchedule;
    private ThemedButton btnCancelSchedule;
    private Label lblScheduleStatus;
    private System.Windows.Forms.Timer scheduleTimer;
    private CheckBox[] chkDays;
    private DateTimePicker dtpRecurringTime;
    private ThemedButton btnSaveRecurring;
    private ThemedButton btnRemoveRecurring;
    private Label lblRecurringStatus;

    // Activity
    private ThemedButton btnClearLog;
    private RichTextBox txtLog;
    private ThemedButton btnHistory;
    private ThemedButton btnOpenLogs;

    // Windows task
    private ThemedButton btnAddTask;
    private ThemedButton btnRemoveTask;
    private Label lblTaskStatus;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        connCheckTimer = new System.Windows.Forms.Timer(components) { Interval = 10000 };
        connCheckTimer.Tick += ConnCheckTimer_Tick;
        scheduleTimer = new System.Windows.Forms.Timer(components) { Interval = 1000 };
        scheduleTimer.Tick += ScheduleTimer_Tick;

        SuspendLayout();

        AutoScaleMode = AutoScaleMode.Font;
        Font = Theme.Body();
        BackColor = Theme.Canvas;
        ForeColor = Theme.Ink;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(24, 4, 24, 22),
            BackColor = Theme.Canvas
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.Controls.Add(BuildHeader(), 0, 0);
        root.Controls.Add(BuildHero(), 0, 1);
        root.Controls.Add(BuildTiles(), 0, 2);
        root.Controls.Add(BuildBody(), 0, 3);

        ClientSize = new Size(1200, 920);
        MinimumSize = new Size(1080, 860);
        StartPosition = FormStartPosition.CenterScreen;
        Text = "Churn Runner";
        Controls.Add(root);
        ResumeLayout(false);
    }

    private Control BuildHeader()
    {
        var header = NewGrid(3);
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        header.Margin = new Padding(0, 10, 0, 16);

        var logo = new IconChip(Theme.GlyphSync, Color.White, Theme.Primary, 42, 15F) { Margin = new Padding(0, 0, 12, 0), Anchor = AnchorStyles.Left };

        var titles = NewFlow(FlowDirection.TopDown);
        titles.Anchor = AnchorStyles.Left;
        titles.Controls.Add(new Label { Text = "Churn Runner", Font = Theme.Display(16F), AutoSize = true, Margin = Padding.Empty });
        lblTarget = new Label { Font = Theme.Body(9F), ForeColor = Theme.Slate, AutoSize = true, Margin = new Padding(1, 0, 0, 0) };
        titles.Controls.Add(lblTarget);

        var right = NewFlow(FlowDirection.LeftToRight);
        right.Anchor = AnchorStyles.Right;
        lnkUpdateAvailable = new LinkLabel
        {
            AutoSize = true,
            Visible = false,
            LinkColor = Theme.Violet,
            ActiveLinkColor = Theme.Primary,
            Font = Theme.Body(9F, FontStyle.Bold),
            Margin = new Padding(0, 10, 16, 0)
        };
        lnkUpdateAvailable.LinkClicked += LnkUpdateAvailable_LinkClicked;
        lblConnIndicator = new Label
        {
            Text = "● Not tested yet",
            ForeColor = Theme.Waiting,
            AutoSize = true,
            Margin = new Padding(0, 10, 12, 0)
        };
        btnCheckConnection = new ThemedButton("", ButtonKind.Secondary, Theme.GlyphRefresh) { Size = new Size(38, 38), Margin = Padding.Empty };
        btnCheckConnection.Click += BtnCheckConnection_Click;
        right.Controls.Add(lnkUpdateAvailable);
        right.Controls.Add(lblConnIndicator);
        btnConnection = new ThemedButton("Connection", ButtonKind.Secondary, Theme.GlyphPlug) { Height = 38, Margin = new Padding(0, 0, 8, 0) };
        btnConnection.Click += BtnConnection_Click;
        right.Controls.Add(btnConnection);
        right.Controls.Add(btnCheckConnection);

        header.Controls.Add(logo, 0, 0);
        header.Controls.Add(titles, 1, 0);
        header.Controls.Add(right, 2, 0);
        return header;
    }

    private Control BuildHero()
    {
        var card = new Card
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            BackColor = Theme.HeroFill,
            BorderColor = Theme.HeroBorder,
            Radius = 16,
            Padding = new Padding(26, 20, 26, 22),
            Margin = Padding.Empty
        };
        var grid = NewGrid(1);
        grid.Dock = DockStyle.Top;
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var titleRow = NewGrid(2);
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        titleRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var titles = NewFlow(FlowDirection.TopDown);
        lblRunTitle = new Label { Font = Theme.Display(17F), AutoSize = true, Margin = Padding.Empty };
        titles.Controls.Add(lblRunTitle);
        titles.Controls.Add(new Label
        {
            Text = "Refresh the count, check how the locations are split, then start both runs.",
            ForeColor = Theme.Slate,
            AutoSize = true,
            Margin = new Padding(1, 4, 0, 0)
        });
        btnGetCount = new ThemedButton("Refresh count", ButtonKind.Ghost, Theme.GlyphRefresh) { Anchor = AnchorStyles.Top | AnchorStyles.Right, Margin = new Padding(0, 4, 0, 0) };
        btnGetCount.Click += BtnGetCount_Click;
        titleRow.Controls.Add(titles, 0, 0);
        titleRow.Controls.Add(btnGetCount, 1, 0);

        var splitRow = NewGrid(3);
        splitRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        splitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        splitRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        splitRow.Margin = new Padding(0, 18, 0, 0);
        numHalfAsc = NewHalfInput(Theme.Asc);
        numHalfDesc = NewHalfInput(Theme.Desc);
        var ascSide = NewFlow(FlowDirection.LeftToRight);
        ascSide.Controls.Add(SideLabel("ASC takes", Theme.Asc));
        ascSide.Controls.Add(numHalfAsc);
        var descSide = NewFlow(FlowDirection.LeftToRight);
        descSide.Anchor = AnchorStyles.Right;
        descSide.Controls.Add(SideLabel("DESC takes", Theme.Desc));
        descSide.Controls.Add(numHalfDesc);
        splitRow.Controls.Add(ascSide, 0, 0);
        splitRow.Controls.Add(new Label
        {
            Text = "ASC works from the top of the location list, DESC from the bottom. They meet at the marker.",
            ForeColor = Theme.Slate,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = Padding.Empty
        }, 1, 0);
        splitRow.Controls.Add(descSide, 2, 0);

        trackRun = new MeetTrack { Dock = DockStyle.Top, BackColor = Theme.HeroFill, Margin = new Padding(0, 14, 0, 0) };

        var actions = NewGrid(2);
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        actions.Margin = new Padding(0, 16, 0, 0);
        var buttons = NewFlow(FlowDirection.LeftToRight);
        btnRunBoth = new ThemedButton("Start both runs", ButtonKind.Primary, Theme.GlyphPlay) { Height = 40, Margin = new Padding(0, 0, 10, 0) };
        btnRunBoth.Click += BtnRunBoth_Click;
        btnStop = new ThemedButton("Stop", ButtonKind.Danger, Theme.GlyphStop) { Height = 40, Enabled = false, Margin = Padding.Empty };
        btnStop.Click += BtnStop_Click;
        lblStatus = new Label
        {
            Text = "Idle",
            Font = Theme.Body(9F, FontStyle.Bold),
            ForeColor = Theme.Slate,
            BackColor = Theme.WaitingTint,
            AutoSize = true,
            Padding = new Padding(10, 5, 10, 5),
            Margin = new Padding(16, 9, 0, 0)
        };
        buttons.Controls.Add(btnRunBoth);
        buttons.Controls.Add(btnStop);
        buttons.Controls.Add(lblStatus);
        lblRunMeta = new Label { Font = Theme.Mono(9F), ForeColor = Theme.Slate, AutoSize = true, Anchor = AnchorStyles.Right, Margin = new Padding(0, 12, 0, 0) };
        actions.Controls.Add(buttons, 0, 0);
        actions.Controls.Add(lblRunMeta, 1, 0);

        grid.Controls.Add(titleRow, 0, 0);
        grid.Controls.Add(splitRow, 0, 1);
        grid.Controls.Add(trackRun, 0, 2);
        grid.Controls.Add(actions, 0, 3);
        card.Controls.Add(grid);
        return card;
    }

    private Control BuildTiles()
    {
        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5,
            RowCount = 1,
            AutoSize = true,
            Margin = new Padding(0, 16, 0, 0),
            Padding = Padding.Empty
        };
        for (int i = 0; i < 5; i++) row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));

        row.Controls.Add(Tile(Theme.GlyphPin, Theme.Primary, Theme.PrimaryTint, "Locations processed", out lblPendingValue, out lblPendingNote, new Padding(0, 0, 8, 0)), 0, 0);
        row.Controls.Add(Tile(Theme.GlyphUp, Theme.Asc, Theme.AscTint, "ASC run", out lblAscValue, out lblAscNote, new Padding(8, 0, 8, 0)), 1, 0);
        row.Controls.Add(Tile(Theme.GlyphDown, Theme.Desc, Theme.DescTint, "DESC run", out lblDescValue, out lblDescNote, new Padding(8, 0, 8, 0)), 2, 0);
        row.Controls.Add(Tile(Theme.GlyphCalendar, Theme.Violet, Theme.VioletTint, "Next scheduled run", out lblNextValue, out lblNextNote, new Padding(8, 0, 8, 0)), 3, 0);
        row.Controls.Add(Tile(Theme.GlyphClock, Theme.Good, Theme.GoodTint, "Last run", out lblLastValue, out lblLastNote, new Padding(8, 0, 0, 0)), 4, 0);
        return row;
    }

    private static Control Tile(string glyph, Color color, Color tint, string caption, out Label value, out Label note, Padding margin)
    {
        var card = new Card { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(16, 16, 16, 16), Margin = margin };
        var grid = NewGrid(2);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var text = NewFlow(FlowDirection.TopDown);
        value = new Label { Text = "—", Font = Theme.Display(16F), AutoSize = true, Margin = Padding.Empty };
        var captionLabel = new Label { Text = caption, ForeColor = Theme.Ink, AutoSize = true, Margin = new Padding(1, 2, 0, 0) };
        note = new Label { ForeColor = Theme.Slate, Font = Theme.Body(8.75F), AutoSize = true, MaximumSize = new Size(200, 0), Margin = new Padding(1, 2, 0, 0) };
        text.Controls.Add(value);
        text.Controls.Add(captionLabel);
        text.Controls.Add(note);

        grid.Controls.Add(new IconChip(glyph, color, tint) { Anchor = AnchorStyles.Top | AnchorStyles.Left, Margin = new Padding(0, 2, 14, 0) }, 0, 0);
        grid.Controls.Add(text, 1, 0);
        card.Controls.Add(grid);
        return card;
    }

    private Control BuildBody()
    {
        var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = new Padding(0, 16, 0, 0) };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 420));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        body.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Settings column scrolls if the connection fields are open on a short window.
        var left = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Margin = new Padding(0, 0, 16, 0),
            Padding = Padding.Empty
        };
        left.Controls.Add(BuildConnectionCard());
        left.Controls.Add(BuildScheduleCard());

        body.Controls.Add(left, 0, 0);
        body.Controls.Add(BuildLogCard(), 1, 0);
        return body;
    }

    private Control BuildConnectionCard()
    {
        var card = SettingsCard(new Padding(0, 0, 0, 16));
        pnlConnectionCard = card;
        card.Visible = false;
        var grid = NewGrid(1);
        grid.Dock = DockStyle.None;
        grid.MinimumSize = new Size(SettingsContentWidth, 0);
        grid.MaximumSize = new Size(SettingsContentWidth, 0);
        grid.Location = new Point(card.Padding.Left, card.Padding.Top);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var head = NewGrid(2);
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        btnEditConnection = new ThemedButton("Close", ButtonKind.Secondary) { Height = 30, Anchor = AnchorStyles.Right, Margin = Padding.Empty };
        btnEditConnection.Click += BtnEditConnection_Click;
        head.Controls.Add(CardTitle(Theme.GlyphPlug, "Connection"), 0, 0);
        head.Controls.Add(btnEditConnection, 1, 0);

        lblConnSummary = new Label { ForeColor = Theme.Slate, AutoSize = true, MaximumSize = new Size(346, 0), Margin = new Padding(0, 8, 0, 0) };

        pnlConnectionFields = NewGrid(2);
        pnlConnectionFields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
        pnlConnectionFields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        pnlConnectionFields.Margin = new Padding(0, 12, 0, 0);

        txtServer = NewTextBox("34.204.209.40");
        txtServer.TextChanged += ConnectionFields_Changed;
        txtDatabase = NewTextBox("MisterCarWash");
        txtDatabase.TextChanged += ConnectionFields_Changed;
        rbSqlAuth = new RadioButton { Text = "SQL login", AutoSize = true, Checked = true, Margin = new Padding(0, 4, 14, 0) };
        rbSqlAuth.CheckedChanged += AuthMode_CheckedChanged;
        rbWindowsAuth = new RadioButton { Text = "Windows account", AutoSize = true, Margin = new Padding(0, 4, 0, 0) };
        rbWindowsAuth.CheckedChanged += AuthMode_CheckedChanged;
        var auth = NewFlow(FlowDirection.LeftToRight);
        auth.Controls.Add(rbSqlAuth);
        auth.Controls.Add(rbWindowsAuth);
        txtUsername = NewTextBox("sa");
        txtUsername.TextChanged += ConnectionFields_Changed;
        txtPassword = NewTextBox("");
        txtPassword.UseSystemPasswordChar = true;
        chkTrustCert = new CheckBox { Text = "Trust server certificate", AutoSize = true, Checked = true, Margin = new Padding(0, 6, 0, 0) };
        btnTestConnection = new ThemedButton("Test connection", ButtonKind.Secondary) { Margin = new Padding(0, 0, 8, 0) };
        btnTestConnection.Click += BtnTestConnection_Click;
        btnSaveSettings = new ThemedButton("Save connection", ButtonKind.Primary) { Margin = Padding.Empty };
        btnSaveSettings.Click += BtnSaveSettings_Click;
        var connButtons = NewFlow(FlowDirection.LeftToRight);
        connButtons.Margin = new Padding(0, 12, 0, 0);
        connButtons.Controls.Add(btnTestConnection);
        connButtons.Controls.Add(btnSaveSettings);

        AddField(pnlConnectionFields, "Server", txtServer);
        AddField(pnlConnectionFields, "Database", txtDatabase);
        AddField(pnlConnectionFields, "Sign in", auth);
        AddField(pnlConnectionFields, "Username", txtUsername);
        AddField(pnlConnectionFields, "Password", txtPassword);
        AddField(pnlConnectionFields, "", chkTrustCert);
        AddField(pnlConnectionFields, "", connButtons);

        grid.Controls.Add(head, 0, 0);
        grid.Controls.Add(lblConnSummary, 0, 1);
        grid.Controls.Add(pnlConnectionFields, 0, 2);
        card.Controls.Add(grid);
        return card;
    }

    private Control BuildScheduleCard()
    {
        var card = SettingsCard(Padding.Empty);
        var grid = NewGrid(1);
        grid.Dock = DockStyle.None;
        grid.MinimumSize = new Size(SettingsContentWidth, 0);
        grid.MaximumSize = new Size(SettingsContentWidth, 0);
        grid.Location = new Point(card.Padding.Left, card.Padding.Top);
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        dtpScheduleTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "ddd d MMM  HH:mm",
            ShowUpDown = true,
            Width = 150,
            Value = DateTime.Now.AddMinutes(5),
            Margin = new Padding(0, 6, 8, 0)
        };
        btnSetSchedule = new ThemedButton("Schedule", ButtonKind.Secondary) { Margin = new Padding(0, 0, 8, 0) };
        btnSetSchedule.Click += BtnSetSchedule_Click;
        btnCancelSchedule = new ThemedButton("Cancel", ButtonKind.Secondary) { Enabled = false, Margin = Padding.Empty };
        btnCancelSchedule.Click += BtnCancelSchedule_Click;
        var onceRow = NewFlow(FlowDirection.LeftToRight);
        onceRow.Margin = new Padding(0, 6, 0, 0);
        onceRow.Controls.Add(dtpScheduleTime);
        onceRow.Controls.Add(btnSetSchedule);
        onceRow.Controls.Add(btnCancelSchedule);
        lblScheduleStatus = StatusLine("Not scheduled.");

        chkDays = new CheckBox[7];
        string[] dayNames = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        var days = NewFlow(FlowDirection.LeftToRight);
        days.Margin = new Padding(0, 6, 0, 0);
        for (int i = 0; i < 7; i++)
        {
            chkDays[i] = NewDayToggle(dayNames[i]);
            days.Controls.Add(chkDays[i]);
        }

        dtpRecurringTime = new DateTimePicker
        {
            Format = DateTimePickerFormat.Custom,
            CustomFormat = "HH:mm",
            ShowUpDown = true,
            Width = 72,
            Value = DateTime.Today.AddHours(1),
            Margin = new Padding(0, 6, 8, 0)
        };
        btnSaveRecurring = new ThemedButton("Save", ButtonKind.Secondary) { Margin = new Padding(0, 0, 8, 0) };
        btnSaveRecurring.Click += BtnSaveRecurring_Click;
        btnRemoveRecurring = new ThemedButton("Remove", ButtonKind.Secondary) { Enabled = false, Margin = Padding.Empty };
        btnRemoveRecurring.Click += BtnRemoveRecurring_Click;
        var weeklyRow = NewFlow(FlowDirection.LeftToRight);
        weeklyRow.Margin = new Padding(0, 8, 0, 0);
        weeklyRow.Controls.Add(new Label { Text = "at", AutoSize = true, Margin = new Padding(0, 9, 8, 0) });
        weeklyRow.Controls.Add(dtpRecurringTime);
        weeklyRow.Controls.Add(btnSaveRecurring);
        weeklyRow.Controls.Add(btnRemoveRecurring);
        lblRecurringStatus = StatusLine("Not set.");

        grid.Controls.Add(CardTitle(Theme.GlyphCalendar, "Schedule"), 0, 0);
        grid.Controls.Add(SubTitle("Run once"), 0, 1);
        grid.Controls.Add(onceRow, 0, 2);
        grid.Controls.Add(lblScheduleStatus, 0, 3);
        grid.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Theme.Line, Margin = new Padding(0, 14, 0, 4) }, 0, 4);
        grid.Controls.Add(SubTitle("Every week"), 0, 5);
        grid.Controls.Add(days, 0, 6);
        grid.Controls.Add(weeklyRow, 0, 7);
        grid.Controls.Add(lblRecurringStatus, 0, 8);
        grid.Controls.Add(new Panel { Height = 1, Dock = DockStyle.Top, BackColor = Theme.Line, Margin = new Padding(0, 14, 0, 4) }, 0, 9);
        grid.Controls.Add(SubTitle("When the app is closed"), 0, 10);
        btnAddTask = new ThemedButton("Add Windows task", ButtonKind.Secondary) { Margin = new Padding(0, 0, 8, 0) };
        btnAddTask.Click += BtnAddTask_Click;
        btnRemoveTask = new ThemedButton("Remove", ButtonKind.Secondary) { Enabled = false, Margin = Padding.Empty };
        btnRemoveTask.Click += BtnRemoveTask_Click;
        var taskRow = NewFlow(FlowDirection.LeftToRight);
        taskRow.Margin = new Padding(0, 6, 0, 0);
        taskRow.Controls.Add(btnAddTask);
        taskRow.Controls.Add(btnRemoveTask);
        grid.Controls.Add(taskRow, 0, 11);
        lblTaskStatus = StatusLine("Not set up. Uses the days and time above.");
        grid.Controls.Add(lblTaskStatus, 0, 12);
        card.Controls.Add(grid);
        return card;
    }

    private Control BuildLogCard()
    {
        var card = new Card { Dock = DockStyle.Fill, Padding = new Padding(18, 16, 8, 14), Margin = Padding.Empty };
        var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
        grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var head = NewGrid(2);
        head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        head.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        btnHistory = new ThemedButton("History", ButtonKind.Secondary, Theme.GlyphClock) { Height = 30, Margin = new Padding(0, 0, 8, 0) };
        btnHistory.Click += BtnHistory_Click;
        btnOpenLogs = new ThemedButton("Open logs folder", ButtonKind.Secondary) { Height = 30, Margin = new Padding(0, 0, 8, 0) };
        btnOpenLogs.Click += BtnOpenLogs_Click;
        btnClearLog = new ThemedButton("Clear", ButtonKind.Secondary) { Height = 30, Margin = new Padding(0, 0, 10, 0) };
        btnClearLog.Click += (s, e) => txtLog.Clear();
        head.Controls.Add(CardTitle(Theme.GlyphList, "Activity"), 0, 0);
        var logButtons = NewFlow(FlowDirection.LeftToRight);
        logButtons.Anchor = AnchorStyles.Right;
        logButtons.Controls.Add(btnHistory);
        logButtons.Controls.Add(btnOpenLogs);
        logButtons.Controls.Add(btnClearLog);
        head.Controls.Add(logButtons, 1, 0);

        txtLog = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Panel,
            ForeColor = Theme.Ink,
            Font = Theme.Mono(9F),
            DetectUrls = false,
            ScrollBars = RichTextBoxScrollBars.Vertical,
            Margin = new Padding(0, 12, 0, 0)
        };

        grid.Controls.Add(head, 0, 0);
        grid.Controls.Add(txtLog, 0, 1);
        card.Controls.Add(grid);
        return card;
    }

    private const int SettingsCardWidth = 384;
    private const int SettingsContentWidth = SettingsCardWidth - 36;

    /// <summary>A fixed-width card for the settings column; its height follows its content.</summary>
    private static Card SettingsCard(Padding margin) => new()
    {
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(SettingsCardWidth, 0),
        MaximumSize = new Size(SettingsCardWidth, 0),
        Margin = margin
    };

    private static TableLayoutPanel NewGrid(int columns) => new()
    {
        ColumnCount = columns,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Dock = DockStyle.Fill,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static FlowLayoutPanel NewFlow(FlowDirection direction) => new()
    {
        FlowDirection = direction,
        WrapContents = false,
        AutoSize = true,
        AutoSizeMode = AutoSizeMode.GrowAndShrink,
        Margin = Padding.Empty,
        Padding = Padding.Empty
    };

    private static Control CardTitle(string glyph, string text)
    {
        var row = NewFlow(FlowDirection.LeftToRight);
        row.Anchor = AnchorStyles.Left;
        row.Controls.Add(new Label { Text = glyph, Font = Theme.Icons(11F), ForeColor = Theme.Slate, AutoSize = true, Margin = new Padding(0, 4, 8, 0) });
        row.Controls.Add(new Label { Text = text, Font = Theme.Heading(11.5F), AutoSize = true, Margin = Padding.Empty });
        return row;
    }

    private static Label SubTitle(string text) => new()
    {
        Text = text,
        Font = Theme.Body(9F, FontStyle.Bold),
        ForeColor = Theme.Slate,
        AutoSize = true,
        Margin = new Padding(0, 12, 0, 0)
    };

    private static Label StatusLine(string text) => new()
    {
        Text = text,
        ForeColor = Theme.Slate,
        AutoSize = true,
        MaximumSize = new Size(346, 0),
        Margin = new Padding(0, 8, 0, 0)
    };

    private static Label SideLabel(string text, Color color) => new()
    {
        Text = text,
        Font = Theme.Body(9.5F, FontStyle.Bold),
        ForeColor = color,
        AutoSize = true,
        Margin = new Padding(0, 5, 8, 0)
    };

    private static TextBox NewTextBox(string text) => new()
    {
        Text = text,
        BorderStyle = BorderStyle.FixedSingle,
        Dock = DockStyle.Fill,
        Margin = new Padding(0, 3, 0, 3)
    };

    private NumericUpDown NewHalfInput(Color color)
    {
        var n = new NumericUpDown
        {
            Minimum = 0,
            Maximum = 100000,
            Width = 84,
            Font = Theme.Mono(10F, FontStyle.Bold),
            ForeColor = color,
            TextAlign = HorizontalAlignment.Right,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = Padding.Empty
        };
        n.ValueChanged += SplitValue_Changed;
        return n;
    }

    private static CheckBox NewDayToggle(string text)
    {
        var c = new CheckBox
        {
            Text = text,
            Appearance = Appearance.Button,
            FlatStyle = FlatStyle.Flat,
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(42, 32),
            BackColor = Theme.VioletTint,
            ForeColor = Theme.Violet,
            Font = Theme.Body(9F, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 6, 0),
            UseVisualStyleBackColor = false
        };
        c.FlatAppearance.BorderColor = Theme.VioletTint;
        c.FlatAppearance.CheckedBackColor = Theme.Violet;
        c.CheckedChanged += (s, e) => c.ForeColor = c.Checked ? Color.White : Theme.Violet;
        return c;
    }

    private static void AddField(TableLayoutPanel grid, string caption, Control field)
    {
        int row = grid.RowCount++;
        grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        grid.Controls.Add(new Label
        {
            Text = caption,
            ForeColor = Theme.Slate,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = Padding.Empty
        }, 0, row);
        grid.Controls.Add(field, 1, row);
    }

    #endregion
}
