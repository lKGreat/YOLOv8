namespace YOLO.WinForms.Panels;

partial class InferencePanel
{
    private System.ComponentModel.IContainer components = null;

    // Note: Dispose is implemented in InferencePanel.cs to also dispose the loaded model

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        this.splitMain = new SplitContainer();
        this.panelConfigHeader = new AntdUI.Label();
        this.tableConfig = new TableLayoutPanel();
        this.lblWeights = new AntdUI.Label();
        this.txtWeights = new AntdUI.Input();
        this.btnBrowseWeights = new AntdUI.Button();
        this.lblVersion = new AntdUI.Label();
        this.cboVersion = new AntdUI.Select();
        this.lblVariant = new AntdUI.Label();
        this.cboVariant = new AntdUI.Select();
        this.lblNc = new AntdUI.Label();
        this.numNc = new AntdUI.InputNumber();
        this.lblImgSize = new AntdUI.Label();
        this.numImgSize = new AntdUI.InputNumber();
        this.lblConfThresh = new AntdUI.Label();
        this.txtConfThresh = new AntdUI.Input();
        this.lblIouThresh = new AntdUI.Label();
        this.txtIouThresh = new AntdUI.Input();
        this.panelInfButtons = new FlowLayoutPanel();
        this.btnLoadModel = new AntdUI.Button();
        this.btnSelectImage = new AntdUI.Button();
        this.btnSelectFolder = new AntdUI.Button();
        this.panelResultsHeader = new AntdUI.Label();
        this.splitImage = new SplitContainer();
        this.picOriginal = new PictureBox();
        this.picResult = new PictureBox();
        this.panelDetectionsHeader = new AntdUI.Label();
        this.dgvDetections = new DataGridView();
        this.colClass = new DataGridViewTextBoxColumn();
        this.colConfidence = new DataGridViewTextBoxColumn();
        this.colBBox = new DataGridViewTextBoxColumn();

        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitImage).BeginInit();
        this.splitImage.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.picOriginal).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.picResult).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).BeginInit();
        this.SuspendLayout();

        // ═══════════════════════════════════════════════════════
        // splitMain
        // ═══════════════════════════════════════════════════════
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Name = "splitMain";
        this.splitMain.Size = new Size(1200, 700);
        this.splitMain.SplitterDistance = 280;

        // ═══════════════════════════════════════════════════════
        // LEFT: Config panel
        // ═══════════════════════════════════════════════════════
        this.panelConfigHeader.Text = "Inference Configuration";
        this.panelConfigHeader.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.panelConfigHeader.Dock = DockStyle.Top;
        this.panelConfigHeader.Height = 36;
        this.panelConfigHeader.Padding = new Padding(8, 8, 0, 0);

        // tableConfig
        this.tableConfig.Dock = DockStyle.Fill;
        this.tableConfig.ColumnCount = 3;
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        this.tableConfig.RowCount = 8;
        for (int i = 0; i < 8; i++)
            this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        this.tableConfig.Padding = new Padding(6, 4, 6, 0);

        int row = 0;

        // Row 0: Weights
        this.lblWeights.Text = "Weights:";
        this.lblWeights.TextAlign = ContentAlignment.MiddleLeft;
        this.lblWeights.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblWeights, 0, row);
        this.txtWeights.Dock = DockStyle.Fill;
        this.txtWeights.PlaceholderText = "best.pt";
        this.tableConfig.Controls.Add(this.txtWeights, 1, row);
        this.btnBrowseWeights.Text = "...";
        this.btnBrowseWeights.Ghost = true;
        this.btnBrowseWeights.Size = new Size(32, 30);
        this.tableConfig.Controls.Add(this.btnBrowseWeights, 2, row);

        // Row 1: Version
        row = 1;
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.cboVersion, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVersion, 2);

        // Row 2: Variant
        row = 2;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.cboVariant, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVariant, 2);

        // Row 3: NC
        row = 3;
        this.lblNc.Text = "Classes:";
        this.lblNc.TextAlign = ContentAlignment.MiddleLeft;
        this.lblNc.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblNc, 0, row);
        this.numNc.Dock = DockStyle.Fill;
        this.numNc.Minimum = 1;
        this.numNc.Maximum = 10000;
        this.numNc.Value = 80;
        this.tableConfig.Controls.Add(this.numNc, 1, row);
        this.tableConfig.SetColumnSpan(this.numNc, 2);

        // Row 4: ImgSize
        row = 4;
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

        // Row 5: Conf
        row = 5;
        this.lblConfThresh.Text = "Conf:";
        this.lblConfThresh.TextAlign = ContentAlignment.MiddleLeft;
        this.lblConfThresh.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblConfThresh, 0, row);
        this.txtConfThresh.Dock = DockStyle.Fill;
        this.txtConfThresh.Text = "0.25";
        this.tableConfig.Controls.Add(this.txtConfThresh, 1, row);
        this.tableConfig.SetColumnSpan(this.txtConfThresh, 2);

        // Row 6: IoU
        row = 6;
        this.lblIouThresh.Text = "IoU:";
        this.lblIouThresh.TextAlign = ContentAlignment.MiddleLeft;
        this.lblIouThresh.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblIouThresh, 0, row);
        this.txtIouThresh.Dock = DockStyle.Fill;
        this.txtIouThresh.Text = "0.45";
        this.tableConfig.Controls.Add(this.txtIouThresh, 1, row);
        this.tableConfig.SetColumnSpan(this.txtIouThresh, 2);

        // panelInfButtons
        this.panelInfButtons.Dock = DockStyle.Bottom;
        this.panelInfButtons.FlowDirection = FlowDirection.TopDown;
        this.panelInfButtons.Height = 130;
        this.panelInfButtons.Padding = new Padding(8, 4, 8, 4);

        this.btnLoadModel.Text = "Load Model";
        this.btnLoadModel.Type = AntdUI.TTypeMini.Primary;
        this.btnLoadModel.Size = new Size(220, 34);

        this.btnSelectImage.Text = "Detect Image";
        this.btnSelectImage.Size = new Size(220, 34);
        this.btnSelectImage.Enabled = false;

        this.btnSelectFolder.Text = "Detect Folder";
        this.btnSelectFolder.Size = new Size(220, 34);
        this.btnSelectFolder.Enabled = false;

        this.panelInfButtons.Controls.Add(this.btnLoadModel);
        this.panelInfButtons.Controls.Add(this.btnSelectImage);
        this.panelInfButtons.Controls.Add(this.btnSelectFolder);

        // Assemble left panel
        this.splitMain.Panel1.Controls.Add(this.tableConfig);
        this.splitMain.Panel1.Controls.Add(this.panelInfButtons);
        this.splitMain.Panel1.Controls.Add(this.panelConfigHeader);

        // ═══════════════════════════════════════════════════════
        // RIGHT: Results
        // ═══════════════════════════════════════════════════════
        this.panelResultsHeader.Text = "Results";
        this.panelResultsHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelResultsHeader.Dock = DockStyle.Top;
        this.panelResultsHeader.Height = 30;
        this.panelResultsHeader.Padding = new Padding(8, 6, 0, 0);

        // splitImage
        this.splitImage.Dock = DockStyle.Fill;
        this.splitImage.Name = "splitImage";
        this.splitImage.SplitterDistance = 400;

        this.picOriginal.Dock = DockStyle.Fill;
        this.picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        this.picOriginal.BackColor = Color.FromArgb(40, 40, 40);
        this.picOriginal.BorderStyle = BorderStyle.FixedSingle;
        this.splitImage.Panel1.Controls.Add(this.picOriginal);

        this.picResult.Dock = DockStyle.Fill;
        this.picResult.SizeMode = PictureBoxSizeMode.Zoom;
        this.picResult.BackColor = Color.FromArgb(40, 40, 40);
        this.picResult.BorderStyle = BorderStyle.FixedSingle;
        this.splitImage.Panel2.Controls.Add(this.picResult);

        // Detections section
        this.panelDetectionsHeader.Text = "Detections";
        this.panelDetectionsHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelDetectionsHeader.Dock = DockStyle.Top;
        this.panelDetectionsHeader.Height = 28;
        this.panelDetectionsHeader.Padding = new Padding(4, 6, 0, 0);

        this.dgvDetections.Dock = DockStyle.Fill;
        this.dgvDetections.Font = new Font("Segoe UI", 9F);
        this.dgvDetections.ReadOnly = true;
        this.dgvDetections.AllowUserToAddRows = false;
        this.dgvDetections.AllowUserToDeleteRows = false;
        this.dgvDetections.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvDetections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        this.dgvDetections.RowHeadersVisible = false;
        this.dgvDetections.BackgroundColor = Color.FromArgb(250, 250, 250);
        this.dgvDetections.BorderStyle = BorderStyle.None;
        this.dgvDetections.EnableHeadersVisualStyles = false;
        this.dgvDetections.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);

        this.colClass.HeaderText = "Class";
        this.colClass.Name = "colClass";
        this.colClass.FillWeight = 30;

        this.colConfidence.HeaderText = "Confidence";
        this.colConfidence.Name = "colConfidence";
        this.colConfidence.FillWeight = 20;

        this.colBBox.HeaderText = "BBox (x1,y1,x2,y2)";
        this.colBBox.Name = "colBBox";
        this.colBBox.FillWeight = 50;

        this.dgvDetections.Columns.AddRange(new DataGridViewColumn[]
        {
            this.colClass, this.colConfidence, this.colBBox
        });

        var panelDetections = new System.Windows.Forms.Panel();
        panelDetections.Dock = DockStyle.Bottom;
        panelDetections.Height = 180;
        panelDetections.Controls.Add(this.dgvDetections);
        panelDetections.Controls.Add(this.panelDetectionsHeader);

        // Assemble right panel
        var panelResults = new System.Windows.Forms.Panel();
        panelResults.Dock = DockStyle.Fill;
        panelResults.Controls.Add(this.splitImage);
        panelResults.Controls.Add(panelDetections);
        panelResults.Controls.Add(this.panelResultsHeader);

        this.splitMain.Panel2.Controls.Add(panelResults);

        // ═══════════════════════════════════════════════════════
        // InferencePanel
        // ═══════════════════════════════════════════════════════
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Controls.Add(this.splitMain);
        this.Name = "InferencePanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitImage).EndInit();
        this.splitImage.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.picOriginal).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.picResult).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitMain;
    private AntdUI.Label panelConfigHeader;
    private TableLayoutPanel tableConfig;
    private AntdUI.Label lblWeights;
    private AntdUI.Input txtWeights;
    private AntdUI.Button btnBrowseWeights;
    private AntdUI.Label lblVersion;
    private AntdUI.Select cboVersion;
    private AntdUI.Label lblVariant;
    private AntdUI.Select cboVariant;
    private AntdUI.Label lblNc;
    private AntdUI.InputNumber numNc;
    private AntdUI.Label lblImgSize;
    private AntdUI.InputNumber numImgSize;
    private AntdUI.Label lblConfThresh;
    private AntdUI.Input txtConfThresh;
    private AntdUI.Label lblIouThresh;
    private AntdUI.Input txtIouThresh;
    private FlowLayoutPanel panelInfButtons;
    private AntdUI.Button btnLoadModel;
    private AntdUI.Button btnSelectImage;
    private AntdUI.Button btnSelectFolder;
    private AntdUI.Label panelResultsHeader;
    private SplitContainer splitImage;
    private PictureBox picOriginal;
    private PictureBox picResult;
    private AntdUI.Label panelDetectionsHeader;
    private DataGridView dgvDetections;
    private DataGridViewTextBoxColumn colClass;
    private DataGridViewTextBoxColumn colConfidence;
    private DataGridViewTextBoxColumn colBBox;
}
