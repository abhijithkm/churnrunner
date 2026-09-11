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

    private GroupBox grpConnection;
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
    private System.Windows.Forms.Timer connCheckTimer;

    private GroupBox grpSplit;
    private Button btnGetCount;
    private Label lblLocationCountCaption;
    private TextBox txtLocationCount;
    private Label lblHalfAscCaption;
    private NumericUpDown numHalfAsc;
    private Label lblHalfDescCaption;
    private NumericUpDown numHalfDesc;

    private GroupBox grpSchedule;
    private Label lblScheduleCaption;
    private DateTimePicker dtpScheduleTime;
    private Button btnSetSchedule;
    private Button btnCancelSchedule;
    private Label lblScheduleStatus;
    private System.Windows.Forms.Timer scheduleTimer;

    private GroupBox grpRecurring;
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
        grpConnection = new GroupBox();
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
        connCheckTimer = new System.Windows.Forms.Timer(components);

        grpSplit = new GroupBox();
        btnGetCount = new Button();
        lblLocationCountCaption = new Label();
        txtLocationCount = new TextBox();
        lblHalfAscCaption = new Label();
        numHalfAsc = new NumericUpDown();
        lblHalfDescCaption = new Label();
        numHalfDesc = new NumericUpDown();

        grpSchedule = new GroupBox();
        lblScheduleCaption = new Label();
        dtpScheduleTime = new DateTimePicker();
        btnSetSchedule = new Button();
        btnCancelSchedule = new Button();
        lblScheduleStatus = new Label();
        scheduleTimer = new System.Windows.Forms.Timer(components);

        grpRecurring = new GroupBox();
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

        // grpConnection
        grpConnection.Text = "SQL Server Connection";
        grpConnection.Location = new Point(12, 12);
        grpConnection.Size = new Size(760, 182);
        grpConnection.Controls.Add(lblServer);
        grpConnection.Controls.Add(txtServer);
        grpConnection.Controls.Add(lblDatabase);
        grpConnection.Controls.Add(txtDatabase);
        grpConnection.Controls.Add(rbSqlAuth);
        grpConnection.Controls.Add(rbWindowsAuth);
        grpConnection.Controls.Add(lblUsername);
        grpConnection.Controls.Add(txtUsername);
        grpConnection.Controls.Add(lblPassword);
        grpConnection.Controls.Add(txtPassword);
        grpConnection.Controls.Add(chkTrustCert);
        grpConnection.Controls.Add(btnTestConnection);
        grpConnection.Controls.Add(btnSaveSettings);

        lblServer.Text = "Server:";
        lblServer.Location = new Point(15, 30);
        lblServer.AutoSize = true;

        txtServer.Location = new Point(110, 27);
        txtServer.Size = new Size(200, 23);
        txtServer.Text = "34.204.209.40";

        lblDatabase.Text = "Database:";
        lblDatabase.Location = new Point(330, 30);
        lblDatabase.AutoSize = true;

        txtDatabase.Location = new Point(410, 27);
        txtDatabase.Size = new Size(200, 23);
        txtDatabase.Text = "MisterCarWash";

        rbSqlAuth.Text = "SQL Login";
        rbSqlAuth.Location = new Point(15, 60);
        rbSqlAuth.AutoSize = true;
        rbSqlAuth.Checked = true;
        rbSqlAuth.CheckedChanged += AuthMode_CheckedChanged;

        rbWindowsAuth.Text = "Windows Auth";
        rbWindowsAuth.Location = new Point(120, 60);
        rbWindowsAuth.AutoSize = true;
        rbWindowsAuth.CheckedChanged += AuthMode_CheckedChanged;

        lblUsername.Text = "Username:";
        lblUsername.Location = new Point(15, 92);
        lblUsername.AutoSize = true;

        txtUsername.Location = new Point(110, 89);
        txtUsername.Size = new Size(200, 23);
        txtUsername.Text = "sa";

        lblPassword.Text = "Password:";
        lblPassword.Location = new Point(330, 92);
        lblPassword.AutoSize = true;

        txtPassword.Location = new Point(410, 89);
        txtPassword.Size = new Size(200, 23);
        txtPassword.UseSystemPasswordChar = true;

        chkTrustCert.Text = "Trust Server Certificate";
        chkTrustCert.Location = new Point(15, 118);
        chkTrustCert.AutoSize = true;
        chkTrustCert.Checked = true;

        btnTestConnection.Text = "Test Connection";
        btnTestConnection.Location = new Point(410, 116);
        btnTestConnection.Size = new Size(140, 27);
        btnTestConnection.Click += BtnTestConnection_Click;

        btnSaveSettings.Text = "Save Connection Settings";
        btnSaveSettings.Location = new Point(15, 148);
        btnSaveSettings.Size = new Size(200, 27);
        btnSaveSettings.Click += BtnSaveSettings_Click;

        lblConnIndicator.Text = "● Unknown";
        lblConnIndicator.Location = new Point(600, 152);
        lblConnIndicator.AutoSize = true;
        lblConnIndicator.ForeColor = Color.Gray;
        lblConnIndicator.Font = new Font(lblConnIndicator.Font, FontStyle.Bold);
        grpConnection.Controls.Add(lblConnIndicator);

        connCheckTimer.Interval = 10000;
        connCheckTimer.Tick += ConnCheckTimer_Tick;

        // grpSplit
        grpSplit.Text = "Location Split";
        grpSplit.Location = new Point(12, 202);
        grpSplit.Size = new Size(760, 90);
        grpSplit.Controls.Add(btnGetCount);
        grpSplit.Controls.Add(lblLocationCountCaption);
        grpSplit.Controls.Add(txtLocationCount);
        grpSplit.Controls.Add(lblHalfAscCaption);
        grpSplit.Controls.Add(numHalfAsc);
        grpSplit.Controls.Add(lblHalfDescCaption);
        grpSplit.Controls.Add(numHalfDesc);

        btnGetCount.Text = "Get Count";
        btnGetCount.Location = new Point(15, 30);
        btnGetCount.Size = new Size(120, 27);
        btnGetCount.Click += BtnGetCount_Click;

        lblLocationCountCaption.Text = "Total Pending:";
        lblLocationCountCaption.Location = new Point(150, 35);
        lblLocationCountCaption.AutoSize = true;

        txtLocationCount.Location = new Point(250, 32);
        txtLocationCount.Size = new Size(70, 23);
        txtLocationCount.ReadOnly = true;

        lblHalfAscCaption.Text = "Half (ASC):";
        lblHalfAscCaption.Location = new Point(340, 35);
        lblHalfAscCaption.AutoSize = true;

        numHalfAsc.Location = new Point(430, 32);
        numHalfAsc.Size = new Size(80, 23);
        numHalfAsc.Maximum = 100000;
        numHalfAsc.Minimum = 0;

        lblHalfDescCaption.Text = "Half (DESC):";
        lblHalfDescCaption.Location = new Point(530, 35);
        lblHalfDescCaption.AutoSize = true;

        numHalfDesc.Location = new Point(630, 32);
        numHalfDesc.Size = new Size(80, 23);
        numHalfDesc.Maximum = 100000;
        numHalfDesc.Minimum = 0;

        // grpSchedule (one-time)
        grpSchedule.Text = "One-Time Schedule";
        grpSchedule.Location = new Point(12, 300);
        grpSchedule.Size = new Size(376, 140);
        grpSchedule.Controls.Add(lblScheduleCaption);
        grpSchedule.Controls.Add(dtpScheduleTime);
        grpSchedule.Controls.Add(btnSetSchedule);
        grpSchedule.Controls.Add(btnCancelSchedule);
        grpSchedule.Controls.Add(lblScheduleStatus);

        lblScheduleCaption.Text = "Run at:";
        lblScheduleCaption.Location = new Point(15, 28);
        lblScheduleCaption.AutoSize = true;

        dtpScheduleTime.Format = DateTimePickerFormat.Custom;
        dtpScheduleTime.CustomFormat = "yyyy-MM-dd HH:mm";
        dtpScheduleTime.ShowUpDown = true;
        dtpScheduleTime.Location = new Point(85, 25);
        dtpScheduleTime.Size = new Size(150, 23);
        dtpScheduleTime.Value = DateTime.Now.AddMinutes(5);

        btnSetSchedule.Text = "Set Schedule";
        btnSetSchedule.Location = new Point(15, 58);
        btnSetSchedule.Size = new Size(110, 27);
        btnSetSchedule.Click += BtnSetSchedule_Click;

        btnCancelSchedule.Text = "Cancel";
        btnCancelSchedule.Location = new Point(135, 58);
        btnCancelSchedule.Size = new Size(80, 27);
        btnCancelSchedule.Enabled = false;
        btnCancelSchedule.Click += BtnCancelSchedule_Click;

        lblScheduleStatus.Text = "Not scheduled.";
        lblScheduleStatus.Location = new Point(15, 92);
        lblScheduleStatus.Size = new Size(346, 40);
        lblScheduleStatus.AutoSize = false;

        scheduleTimer.Interval = 1000;
        scheduleTimer.Tick += ScheduleTimer_Tick;

        // grpRecurring
        grpRecurring.Text = "Recurring Schedule";
        grpRecurring.Location = new Point(396, 300);
        grpRecurring.Size = new Size(376, 140);

        string[] dayNames = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };
        for (int i = 0; i < 7; i++)
        {
            chkDays[i].Text = dayNames[i];
            chkDays[i].Location = new Point(15 + i * 46, 25);
            chkDays[i].Size = new Size(44, 20);
            grpRecurring.Controls.Add(chkDays[i]);
        }

        grpRecurring.Controls.Add(lblRecurringAt);
        grpRecurring.Controls.Add(dtpRecurringTime);
        grpRecurring.Controls.Add(btnSaveRecurring);
        grpRecurring.Controls.Add(btnRemoveRecurring);
        grpRecurring.Controls.Add(lblRecurringStatus);

        lblRecurringAt.Text = "at";
        lblRecurringAt.Location = new Point(15, 58);
        lblRecurringAt.AutoSize = true;

        dtpRecurringTime.Format = DateTimePickerFormat.Time;
        dtpRecurringTime.ShowUpDown = true;
        dtpRecurringTime.Location = new Point(45, 55);
        dtpRecurringTime.Size = new Size(90, 23);
        dtpRecurringTime.Value = DateTime.Today.AddHours(1);

        btnSaveRecurring.Text = "Save Recurring";
        btnSaveRecurring.Location = new Point(150, 53);
        btnSaveRecurring.Size = new Size(115, 27);
        btnSaveRecurring.Click += BtnSaveRecurring_Click;

        btnRemoveRecurring.Text = "Remove";
        btnRemoveRecurring.Location = new Point(270, 53);
        btnRemoveRecurring.Size = new Size(95, 27);
        btnRemoveRecurring.Enabled = false;
        btnRemoveRecurring.Click += BtnRemoveRecurring_Click;

        lblRecurringStatus.Text = "Not set.";
        lblRecurringStatus.Location = new Point(15, 88);
        lblRecurringStatus.Size = new Size(346, 44);
        lblRecurringStatus.AutoSize = false;

        // run/log
        btnRunBoth.Text = "Run ASC + DESC (Parallel)";
        btnRunBoth.Location = new Point(12, 448);
        btnRunBoth.Size = new Size(220, 32);
        btnRunBoth.Font = new Font(btnRunBoth.Font, FontStyle.Bold);
        btnRunBoth.Click += BtnRunBoth_Click;

        btnStop.Text = "Stop";
        btnStop.Location = new Point(242, 448);
        btnStop.Size = new Size(90, 32);
        btnStop.Enabled = false;
        btnStop.ForeColor = Color.DarkRed;
        btnStop.Click += BtnStop_Click;

        btnClearLog.Text = "Clear Log";
        btnClearLog.Location = new Point(652, 448);
        btnClearLog.Size = new Size(120, 32);
        btnClearLog.Click += (s, e) => txtLog.Clear();

        lblStatus.Text = "Idle";
        lblStatus.Location = new Point(344, 456);
        lblStatus.AutoSize = true;

        lblAscProgress.Text = "ASC: --";
        lblAscProgress.Location = new Point(12, 488);
        lblAscProgress.Size = new Size(110, 20);

        pbAsc.Location = new Point(126, 486);
        pbAsc.Size = new Size(646, 20);
        pbAsc.Minimum = 0;
        pbAsc.Maximum = 1;

        lblDescProgress.Text = "DESC: --";
        lblDescProgress.Location = new Point(12, 514);
        lblDescProgress.Size = new Size(110, 20);

        pbDesc.Location = new Point(126, 512);
        pbDesc.Size = new Size(646, 20);
        pbDesc.Minimum = 0;
        pbDesc.Maximum = 1;

        txtLog.Location = new Point(12, 542);
        txtLog.Size = new Size(760, 300);
        txtLog.Multiline = true;
        txtLog.ScrollBars = ScrollBars.Vertical;
        txtLog.ReadOnly = true;
        txtLog.Font = new Font("Consolas", 9F);
        txtLog.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;

        // Form1
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(784, 854);
        Controls.Add(grpConnection);
        Controls.Add(grpSplit);
        Controls.Add(grpSchedule);
        Controls.Add(grpRecurring);
        Controls.Add(btnRunBoth);
        Controls.Add(btnStop);
        Controls.Add(btnClearLog);
        Controls.Add(lblStatus);
        Controls.Add(lblAscProgress);
        Controls.Add(pbAsc);
        Controls.Add(lblDescProgress);
        Controls.Add(pbDesc);
        Controls.Add(txtLog);
        MinimumSize = new Size(800, 722);
        Text = "Mister Churn Populate Runner";
        ResumeLayout(false);
    }

    #endregion
}
