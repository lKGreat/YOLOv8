namespace YOLO.WinForms.Panels;

partial class MLNetTrainingPanel
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
            components.Dispose();
        base.Dispose(disposing);
    }

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.splitMain = new SplitContainer();

        // Config panel
        this.panelConfigHeader = new AntdUI.Label();
        this.tableConfig = new TableLayoutPanel();
        this.lblDataset = new AntdUI.Label();
        this.txtDataset = new AntdUI.Input();
        this.btnBrowseDataset = new AntdUI.Button();
        this.lblEpochs = new AntdUI.Label();
        this.numEpochs = new AntdUI.InputNumber();
        this.lblLr0 = new AntdUI.Label();
        this.txtLr0 = new AntdUI.Input();
        this.lblWeightDecay = new AntdUI.Label();
        this.txtWeightDecay = new AntdUI.Input();
        this.lblIoU = new AntdUI.Label();
        this.txtIoU = new AntdUI.Input();
        this.lblScoreThresh = new AntdUI.Label();
        this.txtScoreThresh = new AntdUI.Input();
        this.lblSaveDir = new AntdUI.Label();
        this.txtSaveDir = new AntdUI.Input();

        // Distillation controls
        this.chkDistill = new AntdUI.Switch();
        this.lblDistill = new AntdUI.Label();
        this.panelDistill = new System.Windows.Forms.Panel();
        this.lblTeacher = new AntdUI.Label();
        this.txtTeacher = new AntdUI.Input();
        this.btnBrowseTeacher = new AntdUI.Button();
        this.lblDistillEpochs = new AntdUI.Label();
        this.numDistillEpochs = new AntdUI.InputNumber();
        this.lblDistillLr = new AntdUI.Label();
        this.txtDistillLr = new AntdUI.Input();
        this.lblDistillTemp = new AntdUI.Label();
        this.txtDistillTemp = new AntdUI.Input();
        this.lblDistillWeight = new AntdUI.Label();
        this.txtDistillWeight = new AntdUI.Input();

        this.panelButtons = new FlowLayoutPanel();
        this.btnStart = new AntdUI.Button();
        this.btnStop = new AntdUI.Button();
        this.btnValidate = new AntdUI.Button();
        this.btnTest = new AntdUI.Button();

        // Right panel
        this.splitRight = new SplitContainer();
        this.panelChartHeader = new AntdUI.Label();
        this.grpChart = new System.Windows.Forms.Panel();
        this.panelLogHeader = new AntdUI.Label();
        this.grpLog = new System.Windows.Forms.Panel();
        this.txtLog = new RichTextBox();

        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitRight).BeginInit();
        this.splitRight.SuspendLayout();
        this.SuspendLayout();

        // ── splitMain ──────────────────────────────────
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Size = new Size(1200, 700);
        this.splitMain.Panel1MinSize = 200;
        this.splitMain.Panel2MinSize = 200;
        this.splitMain.SplitterDistance = 320;

        // ═══════════════════════════════════════════════
        // LEFT: Config panel
        // ═══════════════════════════════════════════════
        this.panelConfigHeader.Text = "ML.NET 目标检测训练";
        this.panelConfigHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.panelConfigHeader.Dock = DockStyle.Top;
        this.panelConfigHeader.Height = 36;
        this.panelConfigHeader.Padding = new Padding(8, 8, 0, 0);

        // tableConfig
        this.tableConfig.ColumnCount = 3;
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        this.tableConfig.RowCount = 9;
        for (int i = 0; i < 8; i++)
            this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        // Row 8: distill panel
        this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 220F));
        this.tableConfig.Padding = new Padding(6, 4, 6, 0);

        int row = 0;

        // Row 0: Dataset
        this.lblDataset.Text = "数据集:";
        this.lblDataset.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDataset.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblDataset, 0, row);
        this.txtDataset.Dock = DockStyle.Fill;
        this.txtDataset.PlaceholderText = "dataset.yaml";
        this.tableConfig.Controls.Add(this.txtDataset, 1, row);
        this.btnBrowseDataset.Text = "...";
        this.btnBrowseDataset.Size = new Size(32, 30);
        this.btnBrowseDataset.Ghost = true;
        this.tableConfig.Controls.Add(this.btnBrowseDataset, 2, row);

        // Row 1: Epochs
        row = 1;
        this.lblEpochs.Text = "训练轮数:";
        this.lblEpochs.TextAlign = ContentAlignment.MiddleLeft;
        this.lblEpochs.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblEpochs, 0, row);
        this.numEpochs.Dock = DockStyle.Fill;
        this.numEpochs.Minimum = 1;
        this.numEpochs.Maximum = 1000;
        this.numEpochs.Value = 20;
        this.tableConfig.Controls.Add(this.numEpochs, 1, row);
        this.tableConfig.SetColumnSpan(this.numEpochs, 2);

        // Row 2: LR
        row = 2;
        this.lblLr0.Text = "学习率:";
        this.lblLr0.TextAlign = ContentAlignment.MiddleLeft;
        this.lblLr0.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblLr0, 0, row);
        this.txtLr0.Dock = DockStyle.Fill;
        this.txtLr0.Text = "0.01";
        this.tableConfig.Controls.Add(this.txtLr0, 1, row);
        this.tableConfig.SetColumnSpan(this.txtLr0, 2);

        // Row 3: Weight Decay
        row = 3;
        this.lblWeightDecay.Text = "权重衰减:";
        this.lblWeightDecay.TextAlign = ContentAlignment.MiddleLeft;
        this.lblWeightDecay.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblWeightDecay, 0, row);
        this.txtWeightDecay.Dock = DockStyle.Fill;
        this.txtWeightDecay.Text = "0.0005";
        this.tableConfig.Controls.Add(this.txtWeightDecay, 1, row);
        this.tableConfig.SetColumnSpan(this.txtWeightDecay, 2);

        // Row 4: IoU Threshold
        row = 4;
        this.lblIoU.Text = "IoU 阈值:";
        this.lblIoU.TextAlign = ContentAlignment.MiddleLeft;
        this.lblIoU.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblIoU, 0, row);
        this.txtIoU.Dock = DockStyle.Fill;
        this.txtIoU.Text = "0.5";
        this.tableConfig.Controls.Add(this.txtIoU, 1, row);
        this.tableConfig.SetColumnSpan(this.txtIoU, 2);

        // Row 5: Score Threshold
        row = 5;
        this.lblScoreThresh.Text = "得分阈值:";
        this.lblScoreThresh.TextAlign = ContentAlignment.MiddleLeft;
        this.lblScoreThresh.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblScoreThresh, 0, row);
        this.txtScoreThresh.Dock = DockStyle.Fill;
        this.txtScoreThresh.Text = "0.5";
        this.tableConfig.Controls.Add(this.txtScoreThresh, 1, row);
        this.tableConfig.SetColumnSpan(this.txtScoreThresh, 2);

        // Row 6: SaveDir
        row = 6;
        this.lblSaveDir.Text = "保存目录:";
        this.lblSaveDir.TextAlign = ContentAlignment.MiddleLeft;
        this.lblSaveDir.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblSaveDir, 0, row);
        this.txtSaveDir.Dock = DockStyle.Fill;
        this.txtSaveDir.Text = "runs/mlnet-train/exp";
        this.tableConfig.Controls.Add(this.txtSaveDir, 1, row);
        this.tableConfig.SetColumnSpan(this.txtSaveDir, 2);

        // Row 7: Distillation toggle
        row = 7;
        this.lblDistill.Text = "蒸馏:";
        this.lblDistill.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDistill.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblDistill, 0, row);
        this.chkDistill.Size = new Size(50, 28);
        this.chkDistill.Anchor = AnchorStyles.Left;
        this.chkDistill.Checked = false;
        this.tableConfig.Controls.Add(this.chkDistill, 1, row);

        // ── Distillation detail panel ──────────────────
        this.panelDistill.Dock = DockStyle.Fill;
        this.panelDistill.Visible = false;

        var tableDistill = new TableLayoutPanel();
        tableDistill.Dock = DockStyle.Fill;
        tableDistill.ColumnCount = 3;
        tableDistill.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        tableDistill.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableDistill.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        tableDistill.RowCount = 5;
        for (int i = 0; i < 5; i++)
            tableDistill.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        tableDistill.Padding = new Padding(6, 0, 6, 0);

        // Distill Row 0: Teacher model path
        this.lblTeacher.Text = "教师模型:";
        this.lblTeacher.TextAlign = ContentAlignment.MiddleLeft;
        this.lblTeacher.Dock = DockStyle.Fill;
        tableDistill.Controls.Add(this.lblTeacher, 0, 0);
        this.txtTeacher.Dock = DockStyle.Fill;
        this.txtTeacher.PlaceholderText = "选择教师模型 (.zip)";
        tableDistill.Controls.Add(this.txtTeacher, 1, 0);
        this.btnBrowseTeacher.Text = "...";
        this.btnBrowseTeacher.Size = new Size(32, 30);
        this.btnBrowseTeacher.Ghost = true;
        tableDistill.Controls.Add(this.btnBrowseTeacher, 2, 0);

        // Distill Row 1: Epochs
        this.lblDistillEpochs.Text = "蒸馏轮数:";
        this.lblDistillEpochs.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDistillEpochs.Dock = DockStyle.Fill;
        tableDistill.Controls.Add(this.lblDistillEpochs, 0, 1);
        this.numDistillEpochs.Dock = DockStyle.Fill;
        this.numDistillEpochs.Minimum = 1;
        this.numDistillEpochs.Maximum = 500;
        this.numDistillEpochs.Value = 10;
        tableDistill.Controls.Add(this.numDistillEpochs, 1, 1);
        tableDistill.SetColumnSpan(this.numDistillEpochs, 2);

        // Distill Row 2: LR
        this.lblDistillLr.Text = "蒸馏学习率:";
        this.lblDistillLr.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDistillLr.Dock = DockStyle.Fill;
        tableDistill.Controls.Add(this.lblDistillLr, 0, 2);
        this.txtDistillLr.Dock = DockStyle.Fill;
        this.txtDistillLr.Text = "0.001";
        tableDistill.Controls.Add(this.txtDistillLr, 1, 2);
        tableDistill.SetColumnSpan(this.txtDistillLr, 2);

        // Distill Row 3: Weight
        this.lblDistillWeight.Text = "蒸馏权重:";
        this.lblDistillWeight.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDistillWeight.Dock = DockStyle.Fill;
        tableDistill.Controls.Add(this.lblDistillWeight, 0, 3);
        this.txtDistillWeight.Dock = DockStyle.Fill;
        this.txtDistillWeight.Text = "0.5";
        tableDistill.Controls.Add(this.txtDistillWeight, 1, 3);
        tableDistill.SetColumnSpan(this.txtDistillWeight, 2);

        // Distill Row 4: Temperature
        this.lblDistillTemp.Text = "蒸馏温度:";
        this.lblDistillTemp.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDistillTemp.Dock = DockStyle.Fill;
        tableDistill.Controls.Add(this.lblDistillTemp, 0, 4);
        this.txtDistillTemp.Dock = DockStyle.Fill;
        this.txtDistillTemp.Text = "4.0";
        tableDistill.Controls.Add(this.txtDistillTemp, 1, 4);
        tableDistill.SetColumnSpan(this.txtDistillTemp, 2);

        this.panelDistill.Controls.Add(tableDistill);
        this.panelDistill.Size = new Size(320, 210);

        // Row 8: embedded distill panel
        row = 8;
        this.tableConfig.Controls.Add(this.panelDistill, 0, row);
        this.tableConfig.SetColumnSpan(this.panelDistill, 3);

        // panelButtons
        this.panelButtons.Dock = DockStyle.Bottom;
        this.panelButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelButtons.Height = 48;
        this.panelButtons.Padding = new Padding(8, 6, 8, 6);

        this.btnStart.Text = "开始训练";
        this.btnStart.Type = AntdUI.TTypeMini.Primary;
        this.btnStart.Size = new Size(100, 36);

        this.btnStop.Text = "停止";
        this.btnStop.Type = AntdUI.TTypeMini.Error;
        this.btnStop.Size = new Size(70, 36);
        this.btnStop.Enabled = false;

        this.btnValidate.Text = "验证";
        this.btnValidate.Type = AntdUI.TTypeMini.Default;
        this.btnValidate.Size = new Size(70, 36);

        this.btnTest.Text = "测试";
        this.btnTest.Type = AntdUI.TTypeMini.Default;
        this.btnTest.Size = new Size(70, 36);

        this.panelButtons.Controls.Add(this.btnStart);
        this.panelButtons.Controls.Add(this.btnStop);
        this.panelButtons.Controls.Add(this.btnValidate);
        this.panelButtons.Controls.Add(this.btnTest);

        // Scrollable config container
        this.scrollConfig = new System.Windows.Forms.Panel();
        this.scrollConfig.Dock = DockStyle.Fill;
        this.scrollConfig.AutoScroll = true;

        this.tableConfig.Dock = DockStyle.None;
        this.tableConfig.AutoSize = true;
        this.tableConfig.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.tableConfig.MinimumSize = new Size(300, 0);

        this.scrollConfig.Controls.Add(this.tableConfig);

        // Assemble left panel
        this.splitMain.Panel1.Controls.Add(this.scrollConfig);
        this.splitMain.Panel1.Controls.Add(this.panelButtons);
        this.splitMain.Panel1.Controls.Add(this.panelConfigHeader);

        // ═══════════════════════════════════════════════
        // RIGHT: Charts + Log
        // ═══════════════════════════════════════════════
        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.Orientation = Orientation.Horizontal;
        this.splitRight.Size = new Size(876, 700);
        this.splitRight.Panel1MinSize = 100;
        this.splitRight.Panel2MinSize = 100;
        this.splitRight.SplitterDistance = 350;

        // Chart panel
        this.panelChartHeader.Text = "训练指标";
        this.panelChartHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelChartHeader.Dock = DockStyle.Top;
        this.panelChartHeader.Height = 30;
        this.panelChartHeader.Padding = new Padding(8, 6, 0, 0);

        this.grpChart.Dock = DockStyle.Fill;
        this.grpChart.Padding = new Padding(4);

        this.splitRight.Panel1.Controls.Add(this.grpChart);
        this.splitRight.Panel1.Controls.Add(this.panelChartHeader);

        // Log panel
        this.panelLogHeader.Text = "训练日志";
        this.panelLogHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelLogHeader.Dock = DockStyle.Top;
        this.panelLogHeader.Height = 30;
        this.panelLogHeader.Padding = new Padding(8, 6, 0, 0);

        this.grpLog.Dock = DockStyle.Fill;
        this.grpLog.Padding = new Padding(4);

        this.txtLog.Dock = DockStyle.Fill;
        this.txtLog.Font = new Font("Cascadia Code", 9F);
        this.txtLog.ReadOnly = true;
        this.txtLog.BackColor = Color.FromArgb(30, 30, 30);
        this.txtLog.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtLog.BorderStyle = BorderStyle.None;
        this.txtLog.WordWrap = false;
        this.grpLog.Controls.Add(this.txtLog);

        this.splitRight.Panel2.Controls.Add(this.grpLog);
        this.splitRight.Panel2.Controls.Add(this.panelLogHeader);

        this.splitMain.Panel2.Controls.Add(this.splitRight);

        // ── MLNetTrainingPanel ─────────────────────────
        this.Controls.Add(this.splitMain);
        this.Name = "MLNetTrainingPanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitRight).EndInit();
        this.splitRight.ResumeLayout(false);
        this.ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitMain;
    private AntdUI.Label panelConfigHeader;
    private TableLayoutPanel tableConfig;
    private AntdUI.Label lblDataset;
    private AntdUI.Input txtDataset;
    private AntdUI.Button btnBrowseDataset;
    private AntdUI.Label lblEpochs;
    private AntdUI.InputNumber numEpochs;
    private AntdUI.Label lblLr0;
    private AntdUI.Input txtLr0;
    private AntdUI.Label lblWeightDecay;
    private AntdUI.Input txtWeightDecay;
    private AntdUI.Label lblIoU;
    private AntdUI.Input txtIoU;
    private AntdUI.Label lblScoreThresh;
    private AntdUI.Input txtScoreThresh;
    private AntdUI.Label lblSaveDir;
    private AntdUI.Input txtSaveDir;

    // Distillation
    private AntdUI.Switch chkDistill;
    private AntdUI.Label lblDistill;
    private System.Windows.Forms.Panel panelDistill;
    private AntdUI.Label lblTeacher;
    private AntdUI.Input txtTeacher;
    private AntdUI.Button btnBrowseTeacher;
    private AntdUI.Label lblDistillEpochs;
    private AntdUI.InputNumber numDistillEpochs;
    private AntdUI.Label lblDistillLr;
    private AntdUI.Input txtDistillLr;
    private AntdUI.Label lblDistillTemp;
    private AntdUI.Input txtDistillTemp;
    private AntdUI.Label lblDistillWeight;
    private AntdUI.Input txtDistillWeight;

    private System.Windows.Forms.Panel scrollConfig;
    private FlowLayoutPanel panelButtons;
    private AntdUI.Button btnStart;
    private AntdUI.Button btnStop;
    private AntdUI.Button btnValidate;
    private AntdUI.Button btnTest;
    private SplitContainer splitRight;
    private AntdUI.Label panelChartHeader;
    private System.Windows.Forms.Panel grpChart;
    private AntdUI.Label panelLogHeader;
    private System.Windows.Forms.Panel grpLog;
    private RichTextBox txtLog;
}
