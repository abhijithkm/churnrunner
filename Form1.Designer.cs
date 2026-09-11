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

    private Panel pnlConnection;
    private Label lblConnectionTitle;
    private Label lblServer;
    private TextBox txtServer;
    private Label lblDatabase;
    private TextBox txtDatabase;
    private RadioButton rbSqlAuth;
    private RadioButton rbWindowsAuth;
    private Label lblUsername;
    private TextBox txtUsername;
    private Label lblPassword;
    private TextBox txtPassword;
    private CheckBox chkTrustCert;
    private Button btnTestConnection;
    private Button btnSaveSettings;
    private Label lblConnIndicator;
    private LinkLabel lnkUpdateAvailable;
    private System.Windows.Forms.Timer connCheckTimer;

    private Panel pnlSplit;
    private Label lblSplitTitle;
    private Button btnGetCount;
    private Label lblLocationCountCaption;
    private TextBox txtLocationCount;
    private Label lblHalfAscCaption;
    private NumericUpDown numHalfAsc;
    private Label lblHalfDescCaption;
    private NumericUpDown numHalfDesc;

    private Panel pnlSchedule;
    private Label lblScheduleTitle;
    private Label lblScheduleCaption;
    private DateTimePicker dtpScheduleTime;
    private Button btnSetSchedule;
    private Button btnCancelSchedule;
    private Label lblScheduleStatus;
    private System.Windows.Forms.Timer scheduleTimer;

    private Panel pnlRecurring;
    private Label lblRecurringTitle;
    private CheckBox[] chkDays;
    private Label lblRecurringAt;
    private DateTimePicker dtpRecurringTime;
    private Button btnSaveRecurring;
    private Button btnRemoveRecurring;
    private Label lblRecurringStatus;

    private Button btnRunBoth;
    private Button btnStop;
    private Button btnClearLog;
    private TextBox txtLog;
    private Label lblStatus;
    private Label lblAscProgress;
    private ProgressBar pbAsc;
    private Label lblDescProgress;
    private ProgressBar pbDesc;

    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();

        // Palette
        var bgForm = Color.FromArgb(244, 246, 249);
        var cardBg = Color.White;
        var textDark = Color.FromArgb(30, 41, 59);
        var accent = Color.FromArgb(37, 99, 235);
        var borderLight = Color.FromArgb(226, 232, 240);
        var secondaryBg = Color.FromArgb(241, 245, 249);
        var dangerBg = Color.FromArgb(254, 242, 242);
        var dangerText = Color.FromArgb(220, 38, 38);
        var dangerBorder = Color.FromArgb(252, 165, 165);
        var logBg = Color.FromArgb(23, 23, 23);
        var logText = Color.FromArgb(226, 232, 240);
        var titleFont = new Font("Segoe UI", 10.5F, FontStyle.Bold);

        void StylePrimary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 0;
            b.BackColor = accent;
            b.ForeColor = Color.White;
            b.Cursor = Cursors.Hand;
        }

        void StyleSecondary(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = borderLight;
            b.BackColor = secondaryBg;
            b.ForeColor = textDark;
            b.Cursor = Cursors.Hand;
        }

        void StyleDanger(Button b)
        {
            b.FlatStyle = FlatStyle.Flat;
            b.FlatAppearance.BorderSize = 1;
            b.FlatAppearance.BorderColor = dangerBorder;
            b.BackColor = dangerBg;
            b.ForeColor = dangerText;
            b.Cursor = Cursors.Hand;
        }

        Label CardTitle(string text) => new Label
        {
            Text = text,
            Location = new Point(16, 12),
            AutoSize = true,
            Font = titleFont,
            ForeColor = textDark
        };

        pnlConnection = new Panel();
        lblConnectionTitle = CardTitle("SQL Server Connection");
        lblServer = new Label();
        txtServer = new TextBox();
        lblDatabase = new Label();
        txtDatabase = new TextBox();
        rbSqlAuth = new RadioButton();
        rbWindowsAuth = new RadioButton();
        lblUsername = new Label();
        txtUsername = new TextBox();
        lblPassword = new Label();
        txtPassword = new TextBox();
        chkTrustCert = new CheckBox();
        btnTestConnection = new Button();
        btnSaveSettings = new Button();
        lblConnIndicator = new Label();
        lnkUpdateAvailable = new LinkLabel();
        connCheckTimer = new System.Windows.Forms.Timer(components);

        pnlSplit = new Panel();
        lblSplitTitle = CardTitle("Location Split");
        btnGetCount = new Button();
        lblLocationCountCaption = new Label();
        txtLocationCount = new TextBox();
        lblHalfAscCaption = new Label();
        numHalfAsc = new NumericUpDown();
        lblHalfDescCaption = new Label();
        numHalfDesc = new NumericUpDown();

        pnlSchedule = new Panel();
        lblScheduleTitle = CardTitle("One-Time Schedule");
        lblScheduleCaption = new Label();
        dtpScheduleTime = new DateTimePicker();
        btnSetSchedule = new Button();
        btnCancelSchedule = new Button();
        lblScheduleStatus = new Label();
        scheduleTimer = new System.Windows.Forms.Timer(components);

        pnlRecurring = new Panel();
        lblRecurringTitle = CardTitle("Recurring Schedule");
        chkDays = new CheckBox[7];
        for (int i = 0; i < 7; i++) chkDays[i] = new CheckBox();
        lblRecurringAt = new Label();
        dtpRecurringTime = new DateTimePicker();
        btnSaveRecurring = new Button();
        btnRemoveRecurring = new Button();
        lblRecurringStatus = new Label();

        btnRunBoth = new Button();
        btnStop = new Button();
        btnClearLog = new Button();
        txtLog = new TextBox();
        lblStatus = new Label();
        lblAscProgress = new Label();
        pbAsc = new ProgressBar();
        lblDescProgress = new Label();
        pbDesc = new ProgressBar();

        SuspendLayout();

        // pnlConnection
        pnlConnection.BackColor = cardBg;
        pnlConnection.Location = new Point(12, 12);
        pnlConnection.Size = new Size(760, 196);
        pnlConnection.Controls.Add(lblConnectionTitle);
        pnlConnection.Controls.Add(lblServer);
        pnlConnection.Controls.Add(txtServer);
        pnlConnection.Controls.Add(lblDatabase);
        pnlConnection.Controls.Add(txtDatabase);
        pnlConnection.Controls.Add(rbSqlAuth);
        pnlConnection.Controls.Add(rbWindowsAuth);
        pnlConnection.Controls.Add(lblUsername);
        pnlConnection.Controls.Add(txtUsername);
        pnlConnection.Controls.Add(lblPassword);
        pnlConnection.Controls.Add(txtPassword);
        pnlConnection.Controls.Add(chkTrustCert);
        pnlConnection.Controls.Add(btnTestConnection);
        pnlConnection.Controls.Add(btnSaveSettings);
        pnlConnection.Controls.Add(lblConnIndicator);
        pnlConnection.Controls.Add(lnkUpdateAvailable);

        lblServer.Text = "Server:";
        lblServer.Location = new Point(15, 44);
        lblServer.AutoSize = true;

        txtServer.Location = new Point(110, 41);
        txtServer.Size = new Size(200, 23);
        txtServer.Text = "34.204.209.40";
        txtServer.BorderStyle = BorderStyle.FixedSingle;

        lblDatabase.Text = "Database:";
        lblDatabase.Location = new Point(330, 44);
        lblDatabase.AutoSize = true;

        txtDatabase.Location = new Point(410, 41);
        txtDatabase.Size = new Size(200, 23);
        txtDatabase.Text = "MisterCarWash";
        txtDatabase.BorderStyle = BorderStyle.FixedSingle;

        rbSqlAuth.Text = "SQL Login";
        rbSqlAuth.Location = new Point(15, 74);
        rbSqlAuth.AutoSize = true;
        rbSqlAuth.Checked = true;
        rbSqlAuth.CheckedChanged += AuthMode_CheckedChanged;

        rbWindowsAuth.Text = "Windows Auth";
        rbWindowsAuth.Location = new Point(120, 74);
        rbWindowsAuth.AutoSize = true;
        rbWindowsAuth.CheckedChanged += AuthMode_CheckedChanged;

        lblUsername.Text = "Username:";
        lblUsername.Location = new Point(15, 106);
        lblUsername.AutoSize = true;

        txtUsername.Location = new Point(110, 103);
        txtUsername.Size = new Size(200, 23);
        txtUsername.Text = "sa";
        txtUsername.BorderStyle = BorderStyle.FixedSingle;

        lblPassword.Text = "Password:";
        lblPassword.Location = new Point(330, 106);
        lblPassword.AutoSize = true;

        txtPassword.Location = new Point(410, 103);
        txtPassword.Size = new Size(200, 23);
        txtPassword.UseSystemPasswordChar = true;
        txtPassword.BorderStyle = BorderStyle.FixedSingle;

        chkTrustCert.Text = "Trust Server Certificate";
        chkTrustCert.Location = new Point(15, 132);
        chkTrustCert.AutoSize = true;
        chkTrustCert.Checked = true;

        btnTestConnection.Text = "Test Connection";
        btnTestConnection.Location = new Point(410, 130);
        btnTestConnection.Size = new Size(140, 27);
        btnTestConnection.Click += BtnTestConnection_Click;
        StyleSecondary(btnTestConnection);

        btnSaveSettings.Text = "Save Connection Settings";
        btnSaveSettings.Location = new Point(15, 162);
        btnSaveSettings.Size = new Size(200, 27);
        btnSaveSettings.Click += BtnSaveSettings_Click;
        StyleSecondary(btnSaveSettings);

        lblConnIndicator.Text = "● Unknown";
        lblConnIndicator.Location = new Point(600, 166);
        lblConnIndicator.AutoSize = true;
        lblConnIndicator.ForeColor = Color.Gray;
        lblConnIndicator.Font = new Font(lblConnIndicator.Font, FontStyle.Bold);

        lnkUpdateAvailable.Text = "";
        lnkUpdateAvailable.Location = new Point(444, 14);
        lnkUpdateAvailable.Size = new Size(300, 20);
        lnkUpdateAvailable.TextAlign = ContentAlignment.MiddleRight;
        lnkUpdateAvailable.LinkColor = accent;
        lnkUpdateAvailable.Font = new Font("Segoe UI", 9F);
        lnkUpdateAvailable.Visible = false;
        lnkUpdateAvailable.LinkClicked += LnkUpdateAvailable_LinkClicked;

        connCheckTimer.Interval = 10000;
        connCheckTimer.Tick += ConnCheckTimer_Tick;

        // pnlSplit
        pnlSplit.BackColor = cardBg;
        pnlSplit.Location = new Point(12, 216);
        pnlSplit.Size = new Size(760, 104);
        pnlSplit.Controls.Add(lblSplitTitle);
        pnlSplit.Controls.Add(btnGetCount);
        pnlSplit.Controls.Add(lblLocationCountCaption);
        pnlSplit.Controls.Add(txtLocationCount);
        pnlSplit.Controls.Add(lblHalfAscCaption);
        pnlSplit.Controls.Add(numHalfAsc);
        pnlSplit.Controls.Add(lblHalfDescCaption);
        pnlSplit.Controls.Add(numHalfDesc);

        btnGetCount.Text = "Get Count";
        btnGetCount.Location = new Point(15, 44);
        btnGetCount.Size = new Size(120, 27);
        btnGetCount.Click += BtnGetCount_Click;
        StyleSecondary(btnGetCount);

        lblLocationCountCaption.Text = "Total Pending:";
        lblLocationCountCaption.Location = new Point(150, 49);
        lblLocationCountCaption.AutoSize = true;

        txtLocationCount.Location = new Point(250, 46);
        txtLocationCount.Size = new Size(70, 23);
        txtLocationCount.ReadOnly = true;
        txtLocationCount.BorderStyle = BorderStyle.FixedSingle;

        lblHalfAscCaption.Text = "Half (ASC):";
        lblHalfAscCaption.Location = new Point(340, 49);
        lblHalfAscCaption.AutoSize = true;

        numHalfAsc.Location = new Point(430, 46);
        numHalfAsc.Size = new Size(80, 23);
        numHalfAsc.Maximum = 100000;
        numHalfAsc.Minimum = 0;
        numHalfAsc.BorderStyle = BorderStyle.FixedSingle;

        lblHalfDescCaption.Text = "Half (DESC):";
        lblHalfDescCaption.Location = new Point(530, 49);
        lblHalfDescCaption.AutoSize = true;

        numHalfDesc.Location = new Point(630, 46);
        numHalfDesc.Size = new Size(80, 23);
        numHalfDesc.Maximum = 100000;
        numHalfDesc.Minimum = 0;
        numHalfDesc.BorderStyle = BorderStyle.FixedSingle;

        // pnlSchedule (one-time)
        pnlSchedule.BackColor = cardBg;
        pnlSchedule.Location = new Point(12, 328);
        pnlSchedule.Size = new Size(376, 154);
        pnlSchedule.Controls.Add(lblScheduleTitle);
        pnlSchedule.Controls.Add(lblScheduleCaption);
        pnlSchedule.Controls.Add(dtpScheduleTime);
        pnlSchedule.Controls.Add(btnSetSchedule);
        pnlSchedule.Controls.Add(btnCancelSchedule);
        pnlSchedule.Controls.Add(lblScheduleStatus);

        lblScheduleCaption.Text = "Run at:";
        lblScheduleCaption.Location = new Point(15, 42);
        lblScheduleCaption.AutoSize = true;

        dtpScheduleTime.Format = DateTimePickerFormat.Custom;
        dtpScheduleTime.CustomFormat = "yyyy-MM-dd HH:mm";
        dtpScheduleTime.ShowUpDown = true;
        dtpScheduleTime.Location = new Point(85, 39);
        dtpScheduleTime.Size = new Size(150, 23);
        dtpScheduleTime.Value = DateTime.Now.AddMinutes(5);

        btnSetSchedule.Text = "Set Schedule";
        btnSetSchedule.Location = new Point(15, 72);
        btnSetSchedule.Size = new Size(110, 27);
        btnSetSchedule.Click += BtnSetSchedule_Click;
        StyleSecondary(btnSetSchedule);

        btnCancelSchedule.Text = "Cancel";
        btnCancelSchedule.Location = new Point(135, 72);
        btnCancelSchedule.Size = new Size(80, 27);
        btnCancelSchedule.Enabled = false;
        btnCancelSchedule.Click += BtnCancelSchedule_Click;
        StyleSecondary(btnCancelSchedule);

        lblScheduleStatus.Text = "Not scheduled.";
        lblScheduleStatus.Location = new Point(15, 106);
        lblScheduleStatus.Size = new Size(346, 40);
        lblScheduleStatus.AutoSize = false;

        scheduleTimer.Interval = 1000;
        scheduleTimer.Tick += ScheduleTimer_Tick;

        // pnlRecurring
        pnlRecurring.BackColor = cardBg;
        pnlRecurring.Location = new Point(396, 328);
        pnlRecurring.Size = new Size(376, 154);
        pnlRecurring.Controls.Add(lblRecurringTitle);

        string[] dayNames = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        for (int i = 0; i < 7; i++)
        {
            chkDays[i].Text = dayNames[i];
            chkDays[i].Location = new Point(15 + i * 46, 39);
            chkDays[i].Size = new Size(44, 20);
            pnlRecurring.Controls.Add(chkDays[i]);
        }

        pnlRecurring.Controls.Add(lblRecurringAt);
        pnlRecurring.Controls.Add(dtpRecurringTime);
        pnlRecurring.Controls.Add(btnSaveRecurring);
        pnlRecurring.Controls.Add(btnRemoveRecurring);
        pnlRecurring.Controls.Add(lblRecurringStatus);

        lblRecurringAt.Text = "at";
        lblRecurringAt.Location = new Point(15, 72);
        lblRecurringAt.AutoSize = true;

        dtpRecurringTime.Format = DateTimePickerFormat.Time;
        dtpRecurringTime.ShowUpDown = true;
        dtpRecurringTime.Location = new Point(45, 69);
        dtpRecurringTime.Size = new Size(90, 23);
        dtpRecurringTime.Value = DateTime.Today.AddHours(1);

        btnSaveRecurring.Text = "Save Recurring";
        btnSaveRecurring.Location = new Point(150, 67);
        btnSaveRecurring.Size = new Size(115, 27);
        btnSaveRecurring.Click += BtnSaveRecurring_Click;
        StyleSecondary(btnSaveRecurring);

        btnRemoveRecurring.Text = "Remove";
        btnRemoveRecurring.Location = new Point(270, 67);
        btnRemoveRecurring.Size = new Size(95, 27);
        btnRemoveRecurring.Enabled = false;
        btnRemoveRecurring.Click += BtnRemoveRecurring_Click;
        StyleSecondary(btnRemoveRecurring);

        lblRecurringStatus.Text = "Not set.";
        lblRecurringStatus.Location = new Point(15, 102);
        lblRecurringStatus.Size = new Size(346, 44);
        lblRecurringStatus.AutoSize = false;

        // run/log
        btnRunBoth.Text = "Run ASC + DESC (Parallel)";
        btnRunBoth.Location = new Point(12, 490);
        btnRunBoth.Size = new Size(220, 32);
        btnRunBoth.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        btnRunBoth.Click += BtnRunBoth_Click;
        StylePrimary(btnRunBoth);

        btnStop.Text = "Stop";
        btnStop.Location = new Point(242, 490);
        btnStop.Size = new Size(90, 32);
        btnStop.Enabled = false;
        btnStop.Click += BtnStop_Click;
        StyleDanger(btnStop);

        btnClearLog.Text = "Clear Log";
        btnClearLog.Location = new Point(652, 490);
        btnClearLog.Size = new Size(120, 32);
        btnClearLog.Click += (s, e) => txtLog.Clear();
        StyleSecondary(btnClearLog);

        lblStatus.Text = "Idle";
        lblStatus.Location = new Point(344, 498);
        lblStatus.AutoSize = true;
        lblStatus.ForeColor = textDark;
        lblStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        lblAscProgress.Text = "ASC: --";
        lblAscProgress.Location = new Point(12, 530);
        lblAscProgress.Size = new Size(110, 20);

        pbAsc.Location = new Point(126, 528);
        pbAsc.Size = new Size(646, 20);
        pbAsc.Minimum = 0;
        pbAsc.Maximum = 1;

        lblDescProgress.Text = "DESC: --";
        lblDescProgress.Location = new Point(12, 556);
        lblDescProgress.Size = new Size(110, 20);

        pbDesc.Location = new Point(126, 554);
        pbDesc.Size = new Size(646, 20);
        pbDesc.Minimum = 0;
        pbDesc.Maximum = 1;

        txtLog.Location = new Point(12, 584);
        txtLog.Size = new Size(760, 300);
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.ReadOnly = true;
        txtLog.Font = new Font("Consolas", 9F);
        txtLog.BackColor = logBg;
        txtLog.ForeColor = logText;
        txtLog.BorderStyle = BorderStyle.FixedSingle;
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        // Form1
        AutoScaleMode = AutoScaleMode.Font;
        BackColor = bgForm;
        ClientSize = new Size(784, 896);
        Controls.Add(pnlConnection);
        Controls.Add(pnlSplit);
        Controls.Add(pnlSchedule);
        Controls.Add(pnlRecurring);
        Controls.Add(btnRunBoth);
        Controls.Add(btnStop);
        Controls.Add(btnClearLog);
        Controls.Add(lblStatus);
        Controls.Add(lblAscProgress);
        Controls.Add(pbAsc);
        Controls.Add(lblDescProgress);
        Controls.Add(pbDesc);
        Controls.Add(txtLog);
        MinimumSize = new Size(800, 764);
        Text = "Mister Churn Populate Runner";
        ResumeLayout(false);
    }

    #endregion
}
