namespace YOLO.WinForms.Panels;

partial class TrainingPanel
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

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.splitMain = new SplitContainer();
        this.grpConfig = new GroupBox();
        this.tableConfig = new TableLayoutPanel();
        this.lblVersion = new Label();
        this.cboVersion = new ComboBox();
        this.lblVariant = new Label();
        this.cboVariant = new ComboBox();
        this.lblDataset = new Label();
        this.txtDataset = new TextBox();
        this.btnBrowseDataset = new Button();
        this.lblEpochs = new Label();
        this.numEpochs = new NumericUpDown();
        this.lblBatch = new Label();
        this.numBatch = new NumericUpDown();
        this.lblImgSize = new Label();
        this.numImgSize = new NumericUpDown();
        this.lblLr0 = new Label();
        this.txtLr0 = new TextBox();
        this.lblOptimizer = new Label();
        this.cboOptimizer = new ComboBox();
        this.lblSaveDir = new Label();
        this.txtSaveDir = new TextBox();
        this.chkCosLR = new CheckBox();
        this.panelButtons = new FlowLayoutPanel();
        this.btnStart = new Button();
        this.btnStop = new Button();
        this.splitRight = new SplitContainer();
        this.grpChart = new GroupBox();
        this.grpLog = new GroupBox();
        this.txtLog = new RichTextBox();

        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitRight).BeginInit();
        this.splitRight.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.numEpochs).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numBatch).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).BeginInit();
        this.SuspendLayout();

        // splitMain
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Location = new Point(0, 0);
        this.splitMain.Name = "splitMain";
        this.splitMain.Size = new Size(1200, 700);
        this.splitMain.Panel1MinSize = 200;
        this.splitMain.Panel2MinSize = 200;
        this.splitMain.SplitterDistance = 320;
        this.splitMain.Panel1.Controls.Add(this.grpConfig);
        this.splitMain.Panel2.Controls.Add(this.splitRight);

        // grpConfig
        this.grpConfig.Dock = DockStyle.Fill;
        this.grpConfig.Text = "Training Configuration";
        this.grpConfig.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpConfig.Padding = new Padding(8);
        this.grpConfig.Controls.Add(this.tableConfig);
        this.grpConfig.Controls.Add(this.panelButtons);

        // tableConfig
        this.tableConfig.Dock = DockStyle.Fill;
        this.tableConfig.ColumnCount = 3;
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        this.tableConfig.RowCount = 13;
        for (int i = 0; i < 13; i++)
            this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        this.tableConfig.Padding = new Padding(0, 4, 0, 0);

        // --- Row 0: Version ---
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Font = new Font("Segoe UI", 9F);
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVersion, 0, 0);

        this.cboVersion.Dock = DockStyle.Fill;
        this.cboVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboVersion.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.cboVersion, 1, 0);
        this.tableConfig.SetColumnSpan(this.cboVersion, 2);

        // --- Row 1: Variant ---
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Font = new Font("Segoe UI", 9F);
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVariant, 0, 1);

        this.cboVariant.Dock = DockStyle.Fill;
        this.cboVariant.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboVariant.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.cboVariant, 1, 1);
        this.tableConfig.SetColumnSpan(this.cboVariant, 2);

        // --- Row 2: Dataset ---
        this.lblDataset.Text = "Dataset:";
        this.lblDataset.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDataset.Font = new Font("Segoe UI", 9F);
        this.lblDataset.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblDataset, 0, 2);

        this.txtDataset.Dock = DockStyle.Fill;
        this.txtDataset.Font = new Font("Segoe UI", 9F);
        this.txtDataset.PlaceholderText = "coco128.yaml";
        this.tableConfig.Controls.Add(this.txtDataset, 1, 2);

        this.btnBrowseDataset.Text = "...";
        this.btnBrowseDataset.Size = new Size(28, 26);
        this.btnBrowseDataset.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.btnBrowseDataset, 2, 2);

        // --- Row 3: Epochs ---
        this.lblEpochs.Text = "Epochs:";
        this.lblEpochs.TextAlign = ContentAlignment.MiddleLeft;
        this.lblEpochs.Font = new Font("Segoe UI", 9F);
        this.lblEpochs.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblEpochs, 0, 3);

        this.numEpochs.Dock = DockStyle.Fill;
        this.numEpochs.Font = new Font("Segoe UI", 9F);
        this.numEpochs.Minimum = 1;
        this.numEpochs.Maximum = 10000;
        this.numEpochs.Value = 100;
        this.tableConfig.Controls.Add(this.numEpochs, 1, 3);
        this.tableConfig.SetColumnSpan(this.numEpochs, 2);

        // --- Row 4: Batch ---
        this.lblBatch.Text = "Batch:";
        this.lblBatch.TextAlign = ContentAlignment.MiddleLeft;
        this.lblBatch.Font = new Font("Segoe UI", 9F);
        this.lblBatch.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblBatch, 0, 4);

        this.numBatch.Dock = DockStyle.Fill;
        this.numBatch.Font = new Font("Segoe UI", 9F);
        this.numBatch.Minimum = 1;
        this.numBatch.Maximum = 512;
        this.numBatch.Value = 16;
        this.tableConfig.Controls.Add(this.numBatch, 1, 4);
        this.tableConfig.SetColumnSpan(this.numBatch, 2);

        // --- Row 5: ImgSize ---
        this.lblImgSize.Text = "ImgSize:";
        this.lblImgSize.TextAlign = ContentAlignment.MiddleLeft;
        this.lblImgSize.Font = new Font("Segoe UI", 9F);
        this.lblImgSize.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblImgSize, 0, 5);

        this.numImgSize.Dock = DockStyle.Fill;
        this.numImgSize.Font = new Font("Segoe UI", 9F);
        this.numImgSize.Minimum = 32;
        this.numImgSize.Maximum = 2048;
        this.numImgSize.Increment = 32;
        this.numImgSize.Value = 640;
        this.tableConfig.Controls.Add(this.numImgSize, 1, 5);
        this.tableConfig.SetColumnSpan(this.numImgSize, 2);

        // --- Row 6: Learning Rate ---
        this.lblLr0.Text = "LR:";
        this.lblLr0.TextAlign = ContentAlignment.MiddleLeft;
        this.lblLr0.Font = new Font("Segoe UI", 9F);
        this.lblLr0.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblLr0, 0, 6);

        this.txtLr0.Dock = DockStyle.Fill;
        this.txtLr0.Font = new Font("Segoe UI", 9F);
        this.txtLr0.Text = "0.01";
        this.tableConfig.Controls.Add(this.txtLr0, 1, 6);
        this.tableConfig.SetColumnSpan(this.txtLr0, 2);

        // --- Row 7: Optimizer ---
        this.lblOptimizer.Text = "Optimizer:";
        this.lblOptimizer.TextAlign = ContentAlignment.MiddleLeft;
        this.lblOptimizer.Font = new Font("Segoe UI", 9F);
        this.lblOptimizer.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblOptimizer, 0, 7);

        this.cboOptimizer.Dock = DockStyle.Fill;
        this.cboOptimizer.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboOptimizer.Font = new Font("Segoe UI", 9F);
        this.cboOptimizer.Items.AddRange(new object[] { "auto", "SGD", "AdamW", "Adam" });
        this.cboOptimizer.SelectedIndex = 0;
        this.tableConfig.Controls.Add(this.cboOptimizer, 1, 7);
        this.tableConfig.SetColumnSpan(this.cboOptimizer, 2);

        // --- Row 8: Save Dir ---
        this.lblSaveDir.Text = "Save Dir:";
        this.lblSaveDir.TextAlign = ContentAlignment.MiddleLeft;
        this.lblSaveDir.Font = new Font("Segoe UI", 9F);
        this.lblSaveDir.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblSaveDir, 0, 8);

        this.txtSaveDir.Dock = DockStyle.Fill;
        this.txtSaveDir.Font = new Font("Segoe UI", 9F);
        this.txtSaveDir.Text = "runs/train/exp";
        this.tableConfig.Controls.Add(this.txtSaveDir, 1, 8);
        this.tableConfig.SetColumnSpan(this.txtSaveDir, 2);

        // --- Row 9: Device ---
        this.lblDevice = new Label();
        this.lblDevice.Text = "Device:";
        this.lblDevice.TextAlign = ContentAlignment.MiddleLeft;
        this.lblDevice.Font = new Font("Segoe UI", 9F);
        this.lblDevice.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblDevice, 0, 9);

        this.cboDevice = new ComboBox();
        this.cboDevice.Dock = DockStyle.Fill;
        this.cboDevice.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboDevice.Font = new Font("Segoe UI", 9F);
        this.cboDevice.Items.Add("CPU");
        if (TorchSharp.torch.cuda.is_available())
        {
            int gpuCount = (int)TorchSharp.torch.cuda.device_count();
            for (int g = 0; g < gpuCount; g++)
                this.cboDevice.Items.Add($"CUDA:{g}");
        }
        this.cboDevice.SelectedIndex = this.cboDevice.Items.Count > 1 ? 1 : 0;
        this.tableConfig.Controls.Add(this.cboDevice, 1, 9);
        this.tableConfig.SetColumnSpan(this.cboDevice, 2);

        // --- Row 10: Cosine LR ---
        this.chkCosLR.Text = "Cosine LR Schedule";
        this.chkCosLR.Font = new Font("Segoe UI", 9F);
        this.chkCosLR.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.chkCosLR, 0, 10);
        this.tableConfig.SetColumnSpan(this.chkCosLR, 3);

        // panelButtons
        this.panelButtons.Dock = DockStyle.Bottom;
        this.panelButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelButtons.Height = 44;
        this.panelButtons.Padding = new Padding(4);
        this.panelButtons.Controls.Add(this.btnStart);
        this.panelButtons.Controls.Add(this.btnStop);

        // btnStart
        this.btnStart.Text = "Start Training";
        this.btnStart.Size = new Size(130, 34);
        this.btnStart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.btnStart.BackColor = Color.FromArgb(0, 120, 212);
        this.btnStart.ForeColor = Color.White;
        this.btnStart.FlatStyle = FlatStyle.Flat;
        this.btnStart.FlatAppearance.BorderSize = 0;
        this.btnStart.Cursor = Cursors.Hand;

        // btnStop
        this.btnStop.Text = "Stop";
        this.btnStop.Size = new Size(80, 34);
        this.btnStop.Font = new Font("Segoe UI", 9F);
        this.btnStop.Enabled = false;
        this.btnStop.FlatStyle = FlatStyle.Flat;

        // splitRight
        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.Orientation = Orientation.Horizontal;
        this.splitRight.Size = new Size(876, 700);
        this.splitRight.Panel1MinSize = 100;
        this.splitRight.Panel2MinSize = 100;
        this.splitRight.SplitterDistance = 350;
        this.splitRight.Panel1.Controls.Add(this.grpChart);
        this.splitRight.Panel2.Controls.Add(this.grpLog);

        // grpChart
        this.grpChart.Dock = DockStyle.Fill;
        this.grpChart.Text = "Training Metrics";
        this.grpChart.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpChart.Padding = new Padding(8);

        // grpLog
        this.grpLog.Dock = DockStyle.Fill;
        this.grpLog.Text = "Training Log";
        this.grpLog.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpLog.Padding = new Padding(8);
        this.grpLog.Controls.Add(this.txtLog);

        // txtLog
        this.txtLog.Dock = DockStyle.Fill;
        this.txtLog.Font = new Font("Cascadia Code", 9F);
        this.txtLog.ReadOnly = true;
        this.txtLog.BackColor = Color.FromArgb(30, 30, 30);
        this.txtLog.ForeColor = Color.FromArgb(220, 220, 220);
        this.txtLog.BorderStyle = BorderStyle.None;
        this.txtLog.WordWrap = false;

        // TrainingPanel
        this.Controls.Add(this.splitMain);
        this.Name = "TrainingPanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitRight).EndInit();
        this.splitRight.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.numEpochs).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numBatch).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitMain;
    private GroupBox grpConfig;
    private TableLayoutPanel tableConfig;
    private Label lblVersion;
    private ComboBox cboVersion;
    private Label lblVariant;
    private ComboBox cboVariant;
    private Label lblDataset;
    private TextBox txtDataset;
    private Button btnBrowseDataset;
    private Label lblEpochs;
    private NumericUpDown numEpochs;
    private Label lblBatch;
    private NumericUpDown numBatch;
    private Label lblImgSize;
    private NumericUpDown numImgSize;
    private Label lblLr0;
    private TextBox txtLr0;
    private Label lblOptimizer;
    private ComboBox cboOptimizer;
    private Label lblSaveDir;
    private TextBox txtSaveDir;
    private CheckBox chkCosLR;
    private Label lblDevice;
    private ComboBox cboDevice;
    private FlowLayoutPanel panelButtons;
    private Button btnStart;
    private Button btnStop;
    private SplitContainer splitRight;
    private GroupBox grpChart;
    private GroupBox grpLog;
    private RichTextBox txtLog;
}
