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
        this.grpConfig = new GroupBox();
        this.tableConfig = new TableLayoutPanel();
        this.lblWeights = new Label();
        this.txtWeights = new TextBox();
        this.btnBrowseWeights = new Button();
        this.lblVersion = new Label();
        this.cboVersion = new ComboBox();
        this.lblVariant = new Label();
        this.cboVariant = new ComboBox();
        this.lblNc = new Label();
        this.numNc = new NumericUpDown();
        this.lblImgSize = new Label();
        this.numImgSize = new NumericUpDown();
        this.lblConfThresh = new Label();
        this.txtConfThresh = new TextBox();
        this.lblIouThresh = new Label();
        this.txtIouThresh = new TextBox();
        this.panelInfButtons = new FlowLayoutPanel();
        this.btnLoadModel = new Button();
        this.btnSelectImage = new Button();
        this.btnSelectFolder = new Button();
        this.grpResults = new GroupBox();
        this.splitImage = new SplitContainer();
        this.picOriginal = new PictureBox();
        this.picResult = new PictureBox();
        this.grpDetections = new GroupBox();
        this.dgvDetections = new DataGridView();
        this.colClass = new DataGridViewTextBoxColumn();
        this.colConfidence = new DataGridViewTextBoxColumn();
        this.colBBox = new DataGridViewTextBoxColumn();

        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitImage).BeginInit();
        this.splitImage.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.numNc).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.picOriginal).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.picResult).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).BeginInit();
        this.SuspendLayout();

        // splitMain
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.SplitterDistance = 280;
        this.splitMain.Panel1.Controls.Add(this.grpConfig);
        this.splitMain.Panel2.Controls.Add(this.grpResults);

        // grpConfig
        this.grpConfig.Dock = DockStyle.Fill;
        this.grpConfig.Text = "Inference Configuration";
        this.grpConfig.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpConfig.Padding = new Padding(8);
        this.grpConfig.Controls.Add(this.tableConfig);
        this.grpConfig.Controls.Add(this.panelInfButtons);

        // tableConfig
        this.tableConfig.Dock = DockStyle.Fill;
        this.tableConfig.ColumnCount = 3;
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableConfig.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32F));
        this.tableConfig.RowCount = 8;
        for (int i = 0; i < 8; i++)
            this.tableConfig.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        this.tableConfig.Padding = new Padding(0, 4, 0, 0);

        int row = 0;

        // Row 0: Weights
        this.lblWeights.Text = "Weights:";
        this.lblWeights.TextAlign = ContentAlignment.MiddleLeft;
        this.lblWeights.Font = new Font("Segoe UI", 9F);
        this.lblWeights.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblWeights, 0, row);
        this.txtWeights.Dock = DockStyle.Fill;
        this.txtWeights.Font = new Font("Segoe UI", 9F);
        this.txtWeights.PlaceholderText = "best.pt";
        this.tableConfig.Controls.Add(this.txtWeights, 1, row);
        this.btnBrowseWeights.Text = "...";
        this.btnBrowseWeights.Size = new Size(28, 26);
        this.btnBrowseWeights.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.btnBrowseWeights, 2, row);

        // Row 1: Version
        row = 1;
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Font = new Font("Segoe UI", 9F);
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.cboVersion.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboVersion.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.cboVersion, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVersion, 2);

        // Row 2: Variant
        row = 2;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Font = new Font("Segoe UI", 9F);
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.cboVariant.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboVariant.Font = new Font("Segoe UI", 9F);
        this.tableConfig.Controls.Add(this.cboVariant, 1, row);
        this.tableConfig.SetColumnSpan(this.cboVariant, 2);

        // Row 3: NC
        row = 3;
        this.lblNc.Text = "Classes:";
        this.lblNc.TextAlign = ContentAlignment.MiddleLeft;
        this.lblNc.Font = new Font("Segoe UI", 9F);
        this.lblNc.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblNc, 0, row);
        this.numNc.Dock = DockStyle.Fill;
        this.numNc.Font = new Font("Segoe UI", 9F);
        this.numNc.Minimum = 1;
        this.numNc.Maximum = 10000;
        this.numNc.Value = 80;
        this.tableConfig.Controls.Add(this.numNc, 1, row);
        this.tableConfig.SetColumnSpan(this.numNc, 2);

        // Row 4: ImgSize
        row = 4;
        this.lblImgSize.Text = "ImgSize:";
        this.lblImgSize.TextAlign = ContentAlignment.MiddleLeft;
        this.lblImgSize.Font = new Font("Segoe UI", 9F);
        this.lblImgSize.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblImgSize, 0, row);
        this.numImgSize.Dock = DockStyle.Fill;
        this.numImgSize.Font = new Font("Segoe UI", 9F);
        this.numImgSize.Minimum = 32;
        this.numImgSize.Maximum = 2048;
        this.numImgSize.Increment = 32;
        this.numImgSize.Value = 640;
        this.tableConfig.Controls.Add(this.numImgSize, 1, row);
        this.tableConfig.SetColumnSpan(this.numImgSize, 2);

        // Row 5: Confidence
        row = 5;
        this.lblConfThresh.Text = "Conf:";
        this.lblConfThresh.TextAlign = ContentAlignment.MiddleLeft;
        this.lblConfThresh.Font = new Font("Segoe UI", 9F);
        this.lblConfThresh.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblConfThresh, 0, row);
        this.txtConfThresh.Dock = DockStyle.Fill;
        this.txtConfThresh.Font = new Font("Segoe UI", 9F);
        this.txtConfThresh.Text = "0.25";
        this.tableConfig.Controls.Add(this.txtConfThresh, 1, row);
        this.tableConfig.SetColumnSpan(this.txtConfThresh, 2);

        // Row 6: IoU
        row = 6;
        this.lblIouThresh.Text = "IoU:";
        this.lblIouThresh.TextAlign = ContentAlignment.MiddleLeft;
        this.lblIouThresh.Font = new Font("Segoe UI", 9F);
        this.lblIouThresh.Dock = DockStyle.Fill;
        this.tableConfig.Controls.Add(this.lblIouThresh, 0, row);
        this.txtIouThresh.Dock = DockStyle.Fill;
        this.txtIouThresh.Font = new Font("Segoe UI", 9F);
        this.txtIouThresh.Text = "0.45";
        this.tableConfig.Controls.Add(this.txtIouThresh, 1, row);
        this.tableConfig.SetColumnSpan(this.txtIouThresh, 2);

        // panelInfButtons
        this.panelInfButtons.Dock = DockStyle.Bottom;
        this.panelInfButtons.FlowDirection = FlowDirection.TopDown;
        this.panelInfButtons.Height = 120;
        this.panelInfButtons.Padding = new Padding(4);
        this.panelInfButtons.Controls.Add(this.btnLoadModel);
        this.panelInfButtons.Controls.Add(this.btnSelectImage);
        this.panelInfButtons.Controls.Add(this.btnSelectFolder);

        // btnLoadModel
        this.btnLoadModel.Text = "Load Model";
        this.btnLoadModel.Size = new Size(200, 32);
        this.btnLoadModel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.btnLoadModel.BackColor = Color.FromArgb(0, 120, 212);
        this.btnLoadModel.ForeColor = Color.White;
        this.btnLoadModel.FlatStyle = FlatStyle.Flat;
        this.btnLoadModel.FlatAppearance.BorderSize = 0;
        this.btnLoadModel.Cursor = Cursors.Hand;

        // btnSelectImage
        this.btnSelectImage.Text = "Detect Image";
        this.btnSelectImage.Size = new Size(200, 32);
        this.btnSelectImage.Font = new Font("Segoe UI", 9F);
        this.btnSelectImage.Enabled = false;
        this.btnSelectImage.Cursor = Cursors.Hand;

        // btnSelectFolder
        this.btnSelectFolder.Text = "Detect Folder";
        this.btnSelectFolder.Size = new Size(200, 32);
        this.btnSelectFolder.Font = new Font("Segoe UI", 9F);
        this.btnSelectFolder.Enabled = false;
        this.btnSelectFolder.Cursor = Cursors.Hand;

        // grpResults
        this.grpResults.Dock = DockStyle.Fill;
        this.grpResults.Text = "Results";
        this.grpResults.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpResults.Padding = new Padding(8);
        this.grpResults.Controls.Add(this.splitImage);
        this.grpResults.Controls.Add(this.grpDetections);

        // splitImage
        this.splitImage.Dock = DockStyle.Fill;
        this.splitImage.SplitterDistance = 400;
        this.splitImage.Panel1.Controls.Add(this.picOriginal);
        this.splitImage.Panel2.Controls.Add(this.picResult);

        // picOriginal
        this.picOriginal.Dock = DockStyle.Fill;
        this.picOriginal.SizeMode = PictureBoxSizeMode.Zoom;
        this.picOriginal.BackColor = Color.FromArgb(40, 40, 40);
        this.picOriginal.BorderStyle = BorderStyle.FixedSingle;

        // picResult
        this.picResult.Dock = DockStyle.Fill;
        this.picResult.SizeMode = PictureBoxSizeMode.Zoom;
        this.picResult.BackColor = Color.FromArgb(40, 40, 40);
        this.picResult.BorderStyle = BorderStyle.FixedSingle;

        // grpDetections
        this.grpDetections.Dock = DockStyle.Bottom;
        this.grpDetections.Text = "Detections";
        this.grpDetections.Height = 180;
        this.grpDetections.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        this.grpDetections.Padding = new Padding(4);
        this.grpDetections.Controls.Add(this.dgvDetections);

        // dgvDetections
        this.dgvDetections.Dock = DockStyle.Fill;
        this.dgvDetections.Font = new Font("Segoe UI", 9F);
        this.dgvDetections.ReadOnly = true;
        this.dgvDetections.AllowUserToAddRows = false;
        this.dgvDetections.AllowUserToDeleteRows = false;
        this.dgvDetections.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvDetections.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        this.dgvDetections.RowHeadersVisible = false;
        this.dgvDetections.Columns.AddRange(new DataGridViewColumn[]
        {
            this.colClass, this.colConfidence, this.colBBox
        });

        // colClass
        this.colClass.HeaderText = "Class";
        this.colClass.Name = "colClass";
        this.colClass.FillWeight = 30;

        // colConfidence
        this.colConfidence.HeaderText = "Confidence";
        this.colConfidence.Name = "colConfidence";
        this.colConfidence.FillWeight = 20;

        // colBBox
        this.colBBox.HeaderText = "Bounding Box (x1,y1,x2,y2)";
        this.colBBox.Name = "colBBox";
        this.colBBox.FillWeight = 50;

        // InferencePanel
        this.Controls.Add(this.splitMain);
        this.Name = "InferencePanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitImage).EndInit();
        this.splitImage.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.numNc).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.numImgSize).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.picOriginal).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.picResult).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    private SplitContainer splitMain;
    private GroupBox grpConfig;
    private TableLayoutPanel tableConfig;
    private Label lblWeights;
    private TextBox txtWeights;
    private Button btnBrowseWeights;
    private Label lblVersion;
    private ComboBox cboVersion;
    private Label lblVariant;
    private ComboBox cboVariant;
    private Label lblNc;
    private NumericUpDown numNc;
    private Label lblImgSize;
    private NumericUpDown numImgSize;
    private Label lblConfThresh;
    private TextBox txtConfThresh;
    private Label lblIouThresh;
    private TextBox txtIouThresh;
    private FlowLayoutPanel panelInfButtons;
    private Button btnLoadModel;
    private Button btnSelectImage;
    private Button btnSelectFolder;
    private GroupBox grpResults;
    private SplitContainer splitImage;
    private PictureBox picOriginal;
    private PictureBox picResult;
    private GroupBox grpDetections;
    private DataGridView dgvDetections;
    private DataGridViewTextBoxColumn colClass;
    private DataGridViewTextBoxColumn colConfidence;
    private DataGridViewTextBoxColumn colBBox;
}
