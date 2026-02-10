namespace YOLO.WinForms.Panels;

partial class TrainingPanel
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
        this.lblVersion = new AntdUI.Label();
        this.cboVersion = new AntdUI.Select();
        this.lblVariant = new AntdUI.Label();
        this.cboVariant = new AntdUI.Select();
        this.lblDataset = new AntdUI.Label();
        this.txtDataset = new AntdUI.Input();
        this.btnBrowseDataset = new AntdUI.Button();
        this.lblEpochs = new AntdUI.Label();
        this.numEpochs = new AntdUI.InputNumber();
        this.lblBatch = new AntdUI.Label();
        this.numBatch = new AntdUI.InputNumber();
        this.lblImgSize = new AntdUI.Label();
        this.numImgSize = new AntdUI.InputNumber();
        this.lblLr0 = new AntdUI.Label();
        this.txtLr0 = new AntdUI.Input();
        this.lblOptimizer = new AntdUI.Label();
        this.cboOptimizer = new AntdUI.Select();
        this.lblSaveDir = new AntdUI.Label();
        this.txtSaveDir = new AntdUI.Input();
        this.lblDevice = new AntdUI.Label();
        this.cboDevice = new AntdUI.Select();
        this.chkCosLR = new AntdUI.Switch();
        this.lblCosLR = new AntdUI.Label();
        this.panelButtons = new FlowLayoutPanel();
        this.btnStart = new AntdUI.Button();
        this.btnStop = new AntdUI.Button();

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

        // ── splitMain ──────────────────────────────────────────
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Size = new Size(1200, 700);
        this.splitMain.Panel1MinSize = 200;
        this.splitMain.Panel2MinSize = 200;
        this.splitMain.SplitterDistance = 320;

        // ═══════════════════════════════════════════════════════
        // LEFT: Config panel
        // ═══════════════════════════════════════════════════════
        this.panelConfigHeader.Text = "Training Configuration";
        this.panelConfigHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.panelConfigHeader.Dock = DockStyle.Top;
        this.panelConfigHeader.Height = 36;
        this.panelConfigHeader.Padding = new Padding(8, 8, 0, 0);

        // tableConfig
        this.tableConfig.Dock = DockStyle.Fill;
        this.tableConfig.ColumnCount = 3;
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        this.tableConfig.RowCount = 12;
        for (int i = 0; i < 12; i++)
            this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        this.tableConfig.Padding = new Padding(6, 4, 6, 0);

        int row = 0;

        // Row 0: Version
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.cboVersion, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVersion, 2);

        // Row 1: Variant
        row = 1;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.cboVariant, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVariant, 2);

        // Row 2: Dataset
        row = 2;
        this.lblDataset.Text = "Dataset:";
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

        // Row 3: Epochs
        row = 3;
        this.lblEpochs.Text = "Epochs:";
        this.lblEpochs.TextAlign = ContentAlignment.MiddleLeft;
        this.lblEpochs.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblEpochs, 0, row);
        this.numEpochs.Dock = DockStyle.Fill;
        this.numEpochs.Minimum = 1;
        this.numEpochs.Maximum = 10000;
        this.numEpochs.Value = 100;
        this.tableConfig.Controls.Add(this.numEpochs, 1, row);
        this.tableConfig.SetColumnSpan(this.numEpochs, 2);

        // Row 4: Batch
        row = 4;
        this.lblBatch.Text = "Batch:";
        this.lblBatch.TextAlign = ContentAlignment.MiddleLeft;
        this.lblBatch.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblBatch, 0, row);
        this.numBatch.Dock = DockStyle.Fill;
        this.numBatch.Minimum = 1;
        this.numBatch.Maximum = 512;
        this.numBatch.Value = 16;
        this.tableConfig.Controls.Add(this.numBatch, 1, row);
        this.tableConfig.SetColumnSpan(this.numBatch, 2);

        // Row 5: ImgSize
        row = 5;
        this.lblImgSize.Text = "ImgSize:";
        this.lblImgSize.TextAlign = ContentAlignment.MiddleLeft;
        this.lblImgSize.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblImgSize, 0, row);
        this.numImgSize.Dock = DockStyle.Fill;
        this.numImgSize.Minimum = 32;
        this.numImgSize.Maximum = 2048;
        this.numImgSize.Value = 640;
        this.tableConfig.Controls.Add(this.numImgSize, 1, row);
        this.tableConfig.SetColumnSpan(this.numImgSize, 2);

        // Row 6: LR
        row = 6;
        this.lblLr0.Text = "LR:";
        this.lblLr0.TextAlign = ContentAlignment.MiddleLeft;
        this.lblLr0.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblLr0, 0, row);
        this.txtLr0.Dock = DockStyle.Fill;
        this.txtLr0.Text = "0.01";
        this.tableConfig.Controls.Add(this.txtLr0, 1, row);
        this.tableConfig.SetColumnSpan(this.txtLr0, 2);

        // Row 7: Optimizer
        row = 7;
        this.lblOptimizer.Text = "Optimizer:";
        this.lblOptimizer.TextAlign = ContentAlignment.MiddleLeft;
        this.lblOptimizer.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblOptimizer, 0, row);
        this.cboOptimizer.Dock = DockStyle.Fill;
        this.cboOptimizer.Items.AddRange(new object[] { "auto", "SGD", "AdamW", "Adam" });
        this.cboOptimizer.SelectedIndex = 0;
        this.tableConfig.Controls.Add(this.cboOptimizer, 1, row);
        this.tableConfig.SetColumnSpan(this.cboOptimizer, 2);

        // Row 8: SaveDir
        row = 8;
        this.lblSaveDir.Text = "Save Dir:";
        this.lblSaveDir.TextAlign = ContentAlignment.MiddleLeft;
        this.lblSaveDir.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblSaveDir, 0, row);
        this.txtSaveDir.Dock = DockStyle.Fill;
        this.txtSaveDir.Text = "runs/train/exp";
        this.tableConfig.Controls.Add(this.txtSaveDir, 1, row);
        this.tableConfig.SetColumnSpan(this.txtSaveDir, 2);

        // Row 9: Device
        row = 9;
        this.lblDevice.Text = "Device:";
        this.lblDevice.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDevice.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblDevice, 0, row);
        this.cboDevice.Dock = DockStyle.Fill;
        this.cboDevice.Items.Add("CPU");
        if (TorchSharp.torch.cuda.is_available())
        {
            int gpuCount = (int)TorchSharp.torch.cuda.device_count();
            for (int g = 0; g < gpuCount; g++)
                this.cboDevice.Items.Add($"CUDA:{g}");
        }
        this.cboDevice.SelectedIndex = this.cboDevice.Items.Count > 1 ? 1 : 0;
        this.tableConfig.Controls.Add(this.cboDevice, 1, row);
        this.tableConfig.SetColumnSpan(this.cboDevice, 2);

        // Row 10: Cosine LR
        row = 10;
        this.lblCosLR.Text = "Cosine LR:";
        this.lblCosLR.TextAlign = ContentAlignment.MiddleLeft;
        this.lblCosLR.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblCosLR, 0, row);
        this.chkCosLR.Dock = DockStyle.Left;
        this.chkCosLR.Checked = false;
        this.tableConfig.Controls.Add(this.chkCosLR, 1, row);

        // panelButtons
        this.panelButtons.Dock = DockStyle.Bottom;
        this.panelButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelButtons.Height = 48;
        this.panelButtons.Padding = new Padding(8, 6, 8, 6);

        this.btnStart.Text = "Start Training";
        this.btnStart.Type = AntdUI.TTypeMini.Primary;
        this.btnStart.Size = new Size(130, 36);

        this.btnStop.Text = "Stop";
        this.btnStop.Type = AntdUI.TTypeMini.Error;
        this.btnStop.Size = new Size(80, 36);
        this.btnStop.Enabled = false;

        this.panelButtons.Controls.Add(this.btnStart);
        this.panelButtons.Controls.Add(this.btnStop);

        // Assemble left panel
        this.splitMain.Panel1.Controls.Add(this.tableConfig);
        this.splitMain.Panel1.Controls.Add(this.panelButtons);
        this.splitMain.Panel1.Controls.Add(this.panelConfigHeader);

        // ═══════════════════════════════════════════════════════
        // RIGHT: Charts + Log
        // ═══════════════════════════════════════════════════════
        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.Orientation = Orientation.Horizontal;
        this.splitRight.Size = new Size(876, 700);
        this.splitRight.Panel1MinSize = 100;
        this.splitRight.Panel2MinSize = 100;
        this.splitRight.SplitterDistance = 350;

        // Chart panel
        this.panelChartHeader.Text = "Training Metrics";
        this.panelChartHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelChartHeader.Dock = DockStyle.Top;
        this.panelChartHeader.Height = 30;
        this.panelChartHeader.Padding = new Padding(8, 6, 0, 0);

        this.grpChart.Dock = DockStyle.Fill;
        this.grpChart.Padding = new Padding(4);

        this.splitRight.Panel1.Controls.Add(this.grpChart);
        this.splitRight.Panel1.Controls.Add(this.panelChartHeader);

        // Log panel
        this.panelLogHeader.Text = "Training Log";
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

        // ── TrainingPanel ─────────────────────────────────────
        this.Controls.Add(this.splitMain);
        this.Name = "TrainingPanel";
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
    private AntdUI.Label lblVersion;
    private AntdUI.Select cboVersion;
    private AntdUI.Label lblVariant;
    private AntdUI.Select cboVariant;
    private AntdUI.Label lblDataset;
    private AntdUI.Input txtDataset;
    private AntdUI.Button btnBrowseDataset;
    private AntdUI.Label lblEpochs;
    private AntdUI.InputNumber numEpochs;
    private AntdUI.Label lblBatch;
    private AntdUI.InputNumber numBatch;
    private AntdUI.Label lblImgSize;
    private AntdUI.InputNumber numImgSize;
    private AntdUI.Label lblLr0;
    private AntdUI.Input txtLr0;
    private AntdUI.Label lblOptimizer;
    private AntdUI.Select cboOptimizer;
    private AntdUI.Label lblSaveDir;
    private AntdUI.Input txtSaveDir;
    private AntdUI.Label lblDevice;
    private AntdUI.Select cboDevice;
    private AntdUI.Switch chkCosLR;
    private AntdUI.Label lblCosLR;
    private FlowLayoutPanel panelButtons;
    private AntdUI.Button btnStart;
    private AntdUI.Button btnStop;
    private SplitContainer splitRight;
    private AntdUI.Label panelChartHeader;
    private System.Windows.Forms.Panel grpChart;
    private AntdUI.Label panelLogHeader;
    private System.Windows.Forms.Panel grpLog;
    private RichTextBox txtLog;
}
