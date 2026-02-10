namespace YOLO.WinForms.Panels;

partial class ModelTestPanel
{
    private System.ComponentModel.IContainer components = null;

    // Note: Dispose is implemented in ModelTestPanel.cs to also dispose the inference engine

    #region Component Designer generated code

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        // ── Top-level containers ─────────────────────────────────
        this.splitMain = new SplitContainer();
        this.splitRight = new SplitContainer();

        // ── Left: Config sidebar ─────────────────────────────────
        this.panelLeftScroll = new Panel();
        this.panelLeftInner = new Panel();

        // Section 1: Model Config
        this.lblSectionModel = new AntdUI.Label();
        this.tableModel = new TableLayoutPanel();
        this.lblRecentModel = new AntdUI.Label();
        this.cboRecentModels = new AntdUI.Select();
        this.lblModelFile = new AntdUI.Label();
        this.txtModelFile = new AntdUI.Input();
        this.btnBrowseModel = new AntdUI.Button();
        this.lblVersion = new AntdUI.Label();
        this.cboVersion = new AntdUI.Select();
        this.lblVariant = new AntdUI.Label();
        this.cboVariant = new AntdUI.Select();
        this.lblNc = new AntdUI.Label();
        this.numNc = new AntdUI.InputNumber();
        this.lblClassNames = new AntdUI.Label();
        this.txtClassNames = new AntdUI.Input();
        this.btnBrowseClassNames = new AntdUI.Button();
        this.lblProvider = new AntdUI.Label();
        this.cboProvider = new AntdUI.Select();
        this.btnLoadModel = new AntdUI.Button();

        // Section 2: Inference Parameters
        this.lblSectionInference = new AntdUI.Label();
        this.tableInference = new TableLayoutPanel();
        this.lblConf = new AntdUI.Label();
        this.sliderConf = new AntdUI.Slider();
        this.lblConfValue = new AntdUI.Label();
        this.lblIoU = new AntdUI.Label();
        this.sliderIoU = new AntdUI.Slider();
        this.lblIoUValue = new AntdUI.Label();
        this.lblImgSize = new AntdUI.Label();
        this.cboImgSize = new AntdUI.Select();
        this.lblMaxDet = new AntdUI.Label();
        this.numMaxDet = new AntdUI.InputNumber();
        this.lblFp16 = new AntdUI.Label();
        this.swFp16 = new AntdUI.Switch();
        this.lblAutoRedetect = new AntdUI.Label();
        this.swAutoRedetect = new AntdUI.Switch();

        // Section 3: Action Buttons
        this.panelActions = new FlowLayoutPanel();
        this.btnSelectImage = new AntdUI.Button();
        this.btnSelectFolder = new AntdUI.Button();
        this.btnRedetect = new AntdUI.Button();
        this.btnSaveResult = new AntdUI.Button();
        this.btnClear = new AntdUI.Button();

        // ── Right top: Toolbar + Canvas ──────────────────────────
        this.panelToolbar = new FlowLayoutPanel();
        this.btnPrev = new AntdUI.Button();
        this.btnNext = new AntdUI.Button();
        this.lblImageCounter = new AntdUI.Label();
        this.divider1 = new AntdUI.Divider();
        this.btnZoomIn = new AntdUI.Button();
        this.btnZoomOut = new AntdUI.Button();
        this.btnFit = new AntdUI.Button();
        this.divider2 = new AntdUI.Divider();
        this.cboCompareMode = new AntdUI.Select();
        this.divider3 = new AntdUI.Divider();
        this.lblInferenceTime = new AntdUI.Label();
        this.canvas = new YOLO.WinForms.Controls.DoubleBufferedPanel();

        // ── Right bottom: Detections + Stats ─────────────────────
        this.panelStatsHeader = new AntdUI.Label();
        this.dgvDetections = new DataGridView();
        this.colClass = new DataGridViewTextBoxColumn();
        this.colConfidence = new DataGridViewTextBoxColumn();
        this.colBBox = new DataGridViewTextBoxColumn();
        this.colArea = new DataGridViewTextBoxColumn();
        this.panelStatsBar = new FlowLayoutPanel();
        this.lblTotalDetections = new AntdUI.Label();
        this.lblModelInfo = new AntdUI.Label();
        this.lblClassSummary = new AntdUI.Label();

        // ── Begin Init ───────────────────────────────────────────
        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitRight).BeginInit();
        this.splitRight.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).BeginInit();
        this.SuspendLayout();

        // ═══════════════════════════════════════════════════════
        // splitMain (Horizontal: Left Config | Right Results)
        // ═══════════════════════════════════════════════════════
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Name = "splitMain";
        this.splitMain.Size = new Size(1200, 700);
        this.splitMain.SplitterDistance = 300;

        // ═══════════════════════════════════════════════════════
        // LEFT SIDEBAR (scrollable)
        // ═══════════════════════════════════════════════════════
        this.panelLeftScroll.Dock = DockStyle.Fill;
        this.panelLeftScroll.AutoScroll = true;
        this.panelLeftScroll.Padding = new Padding(0);

        this.panelLeftInner.Dock = DockStyle.Top;
        this.panelLeftInner.AutoSize = true;
        this.panelLeftInner.AutoSizeMode = AutoSizeMode.GrowAndShrink;

        // ── Section 1: Model Configuration ───────────────────
        this.lblSectionModel.Text = "Model Configuration";
        this.lblSectionModel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.lblSectionModel.Dock = DockStyle.Top;
        this.lblSectionModel.Height = 36;
        this.lblSectionModel.Padding = new Padding(8, 8, 0, 0);

        this.tableModel.Dock = DockStyle.Top;
        this.tableModel.AutoSize = true;
        this.tableModel.ColumnCount = 3;
        this.tableModel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
        this.tableModel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableModel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 36F));
        this.tableModel.RowCount = 8;
        for (int i = 0; i < 8; i++)
            this.tableModel.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        this.tableModel.Padding = new Padding(6, 0, 6, 0);

        int row = 0;

        // Row 0: Recent Models dropdown
        this.lblRecentModel.Text = "Recent:";
        this.lblRecentModel.TextAlign = ContentAlignment.MiddleLeft;
        this.lblRecentModel.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblRecentModel, 0, row);
        this.cboRecentModels.Dock = DockStyle.Fill;
        this.cboRecentModels.PlaceholderText = "Click to scan for models...";
        this.tableModel.Controls.Add(this.cboRecentModels, 1, row);
        this.tableModel.SetColumnSpan(this.cboRecentModels, 2);

        // Row 1: Model File
        row = 1;
        this.lblModelFile.Text = "Model:";
        this.lblModelFile.TextAlign = ContentAlignment.MiddleLeft;
        this.lblModelFile.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblModelFile, 0, row);
        this.txtModelFile.Dock = DockStyle.Fill;
        this.txtModelFile.PlaceholderText = "best.pt / model.onnx";
        this.tableModel.Controls.Add(this.txtModelFile, 1, row);
        this.btnBrowseModel.Text = "...";
        this.btnBrowseModel.Ghost = true;
        this.btnBrowseModel.Size = new Size(32, 30);
        this.tableModel.Controls.Add(this.btnBrowseModel, 2, row);

        // Row 2: Version
        row = 2;
        this.lblVersion.Text = "Version:";
        this.lblVersion.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVersion.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblVersion, 0, row);
        this.cboVersion.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.cboVersion, 1, row);
        this.tableModel.SetColumnSpan(this.cboVersion, 2);

        // Row 3: Variant
        row = 3;
        this.lblVariant.Text = "Variant:";
        this.lblVariant.TextAlign = ContentAlignment.MiddleLeft;
        this.lblVariant.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblVariant, 0, row);
        this.cboVariant.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.cboVariant, 1, row);
        this.tableModel.SetColumnSpan(this.cboVariant, 2);

        // Row 4: Num Classes
        row = 4;
        this.lblNc.Text = "Classes:";
        this.lblNc.TextAlign = ContentAlignment.MiddleLeft;
        this.lblNc.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblNc, 0, row);
        this.numNc.Dock = DockStyle.Fill;
        this.numNc.Minimum = 1;
        this.numNc.Maximum = 10000;
        this.numNc.Value = 80;
        this.tableModel.Controls.Add(this.numNc, 1, row);
        this.tableModel.SetColumnSpan(this.numNc, 2);

        // Row 5: Class Names
        row = 5;
        this.lblClassNames.Text = "Labels:";
        this.lblClassNames.TextAlign = ContentAlignment.MiddleLeft;
        this.lblClassNames.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblClassNames, 0, row);
        this.txtClassNames.Dock = DockStyle.Fill;
        this.txtClassNames.PlaceholderText = "classes.txt (optional)";
        this.tableModel.Controls.Add(this.txtClassNames, 1, row);
        this.btnBrowseClassNames.Text = "...";
        this.btnBrowseClassNames.Ghost = true;
        this.btnBrowseClassNames.Size = new Size(32, 30);
        this.tableModel.Controls.Add(this.btnBrowseClassNames, 2, row);

        // Row 6: Execution Provider
        row = 6;
        this.lblProvider.Text = "Device:";
        this.lblProvider.TextAlign = ContentAlignment.MiddleLeft;
        this.lblProvider.Dock = DockStyle.Fill;
        this.tableModel.Controls.Add(this.lblProvider, 0, row);
        this.cboProvider.Dock = DockStyle.Fill;
        this.cboProvider.Items.AddRange(new object[] { "Auto", "CPU", "CUDA", "TensorRT", "DirectML" });
        this.cboProvider.SelectedIndex = 0;
        this.tableModel.Controls.Add(this.cboProvider, 1, row);
        this.tableModel.SetColumnSpan(this.cboProvider, 2);

        // Row 7: Load Model button
        row = 7;
        this.btnLoadModel.Text = "Load Model";
        this.btnLoadModel.Type = AntdUI.TTypeMini.Primary;
        this.btnLoadModel.Dock = DockStyle.Fill;
        this.btnLoadModel.Margin = new Padding(4, 4, 4, 4);
        this.tableModel.Controls.Add(this.btnLoadModel, 0, row);
        this.tableModel.SetColumnSpan(this.btnLoadModel, 3);

        // ── Section 2: Inference Parameters ──────────────────
        this.lblSectionInference.Text = "Inference Parameters";
        this.lblSectionInference.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
        this.lblSectionInference.Dock = DockStyle.Top;
        this.lblSectionInference.Height = 36;
        this.lblSectionInference.Padding = new Padding(8, 8, 0, 0);

        this.tableInference.Dock = DockStyle.Top;
        this.tableInference.AutoSize = true;
        this.tableInference.ColumnCount = 3;
        this.tableInference.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 72F));
        this.tableInference.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        this.tableInference.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 50F));
        this.tableInference.RowCount = 6;
        for (int i = 0; i < 6; i++)
            this.tableInference.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
        this.tableInference.Padding = new Padding(6, 0, 6, 0);

        // Row 0: Confidence
        row = 0;
        this.lblConf.Text = "Conf:";
        this.lblConf.TextAlign = ContentAlignment.MiddleLeft;
        this.lblConf.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblConf, 0, row);
        this.sliderConf.Dock = DockStyle.Fill;
        this.sliderConf.Value = 25;
        this.tableInference.Controls.Add(this.sliderConf, 1, row);
        this.lblConfValue.Text = "0.25";
        this.lblConfValue.TextAlign = ContentAlignment.MiddleCenter;
        this.lblConfValue.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblConfValue, 2, row);

        // Row 1: IoU
        row = 1;
        this.lblIoU.Text = "IoU:";
        this.lblIoU.TextAlign = ContentAlignment.MiddleLeft;
        this.lblIoU.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblIoU, 0, row);
        this.sliderIoU.Dock = DockStyle.Fill;
        this.sliderIoU.Value = 45;
        this.tableInference.Controls.Add(this.sliderIoU, 1, row);
        this.lblIoUValue.Text = "0.45";
        this.lblIoUValue.TextAlign = ContentAlignment.MiddleCenter;
        this.lblIoUValue.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblIoUValue, 2, row);

        // Row 2: Image Size
        row = 2;
        this.lblImgSize.Text = "ImgSize:";
        this.lblImgSize.TextAlign = ContentAlignment.MiddleLeft;
        this.lblImgSize.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblImgSize, 0, row);
        this.cboImgSize.Dock = DockStyle.Fill;
        this.cboImgSize.Items.AddRange(new object[] { "320", "416", "512", "640", "1024", "1280" });
        this.cboImgSize.SelectedIndex = 3; // 640
        this.tableInference.Controls.Add(this.cboImgSize, 1, row);
        this.tableInference.SetColumnSpan(this.cboImgSize, 2);

        // Row 3: Max Detections
        row = 3;
        this.lblMaxDet.Text = "MaxDet:";
        this.lblMaxDet.TextAlign = ContentAlignment.MiddleLeft;
        this.lblMaxDet.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblMaxDet, 0, row);
        this.numMaxDet.Dock = DockStyle.Fill;
        this.numMaxDet.Minimum = 1;
        this.numMaxDet.Maximum = 1000;
        this.numMaxDet.Value = 300;
        this.tableInference.Controls.Add(this.numMaxDet, 1, row);
        this.tableInference.SetColumnSpan(this.numMaxDet, 2);

        // Row 4: FP16
        row = 4;
        this.lblFp16.Text = "FP16:";
        this.lblFp16.TextAlign = ContentAlignment.MiddleLeft;
        this.lblFp16.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblFp16, 0, row);
        this.swFp16.Dock = DockStyle.Left;
        this.tableInference.Controls.Add(this.swFp16, 1, row);

        // Row 5: Auto Re-detect
        row = 5;
        this.lblAutoRedetect.Text = "Auto:";
        this.lblAutoRedetect.TextAlign = ContentAlignment.MiddleLeft;
        this.lblAutoRedetect.Dock = DockStyle.Fill;
        this.tableInference.Controls.Add(this.lblAutoRedetect, 0, row);
        this.swAutoRedetect.Checked = true;
        this.swAutoRedetect.Dock = DockStyle.Left;
        this.tableInference.Controls.Add(this.swAutoRedetect, 1, row);

        // ── Section 3: Action Buttons ────────────────────────
        this.panelActions.Dock = DockStyle.Top;
        this.panelActions.FlowDirection = FlowDirection.TopDown;
        this.panelActions.AutoSize = true;
        this.panelActions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        this.panelActions.Padding = new Padding(8, 8, 8, 8);

        this.btnSelectImage.Text = "Select Image";
        this.btnSelectImage.Size = new Size(260, 36);
        this.btnSelectImage.Enabled = false;

        this.btnSelectFolder.Text = "Select Folder";
        this.btnSelectFolder.Size = new Size(260, 36);
        this.btnSelectFolder.Enabled = false;

        this.btnRedetect.Text = "Re-detect";
        this.btnRedetect.Size = new Size(260, 36);
        this.btnRedetect.Ghost = true;
        this.btnRedetect.Enabled = false;

        this.btnSaveResult.Text = "Save Result";
        this.btnSaveResult.Size = new Size(260, 36);
        this.btnSaveResult.Ghost = true;
        this.btnSaveResult.Enabled = false;

        this.btnClear.Text = "Clear";
        this.btnClear.Size = new Size(260, 36);
        this.btnClear.Ghost = true;

        this.panelActions.Controls.Add(this.btnSelectImage);
        this.panelActions.Controls.Add(this.btnSelectFolder);
        this.panelActions.Controls.Add(this.btnRedetect);
        this.panelActions.Controls.Add(this.btnSaveResult);
        this.panelActions.Controls.Add(this.btnClear);

        // ── Assemble left sidebar (reverse dock order) ───────
        // Controls added bottom-to-top: actions, inference table, inference header, model table, model header
        this.panelLeftInner.Controls.Add(this.panelActions);
        this.panelLeftInner.Controls.Add(this.tableInference);
        this.panelLeftInner.Controls.Add(this.lblSectionInference);
        this.panelLeftInner.Controls.Add(this.tableModel);
        this.panelLeftInner.Controls.Add(this.lblSectionModel);

        this.panelLeftScroll.Controls.Add(this.panelLeftInner);
        this.splitMain.Panel1.Controls.Add(this.panelLeftScroll);

        // ═══════════════════════════════════════════════════════
        // RIGHT AREA (splitRight: Image top | Stats bottom)
        // ═══════════════════════════════════════════════════════
        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.Orientation = Orientation.Horizontal;
        this.splitRight.FixedPanel = FixedPanel.Panel2;
        this.splitRight.SplitterDistance = 450;
        this.splitRight.Name = "splitRight";

        // ── Toolbar ──────────────────────────────────────────
        this.panelToolbar.Dock = DockStyle.Top;
        this.panelToolbar.Height = 40;
        this.panelToolbar.FlowDirection = FlowDirection.LeftToRight;
        this.panelToolbar.WrapContents = false;
        this.panelToolbar.Padding = new Padding(6, 4, 6, 4);
        this.panelToolbar.BackColor = Color.FromArgb(248, 248, 248);

        this.btnPrev.Text = "<";
        this.btnPrev.Ghost = true;
        this.btnPrev.Size = new Size(34, 30);
        this.btnPrev.Enabled = false;

        this.btnNext.Text = ">";
        this.btnNext.Ghost = true;
        this.btnNext.Size = new Size(34, 30);
        this.btnNext.Enabled = false;

        this.lblImageCounter.Text = "0 / 0";
        this.lblImageCounter.TextAlign = ContentAlignment.MiddleCenter;
        this.lblImageCounter.Size = new Size(80, 30);
        this.lblImageCounter.Padding = new Padding(4, 0, 4, 0);

        this.divider1.Vertical = true;
        this.divider1.Size = new Size(16, 30);

        this.btnZoomIn.Text = "+";
        this.btnZoomIn.Ghost = true;
        this.btnZoomIn.Size = new Size(34, 30);

        this.btnZoomOut.Text = "-";
        this.btnZoomOut.Ghost = true;
        this.btnZoomOut.Size = new Size(34, 30);

        this.btnFit.Text = "Fit";
        this.btnFit.Ghost = true;
        this.btnFit.Size = new Size(44, 30);

        this.divider2.Vertical = true;
        this.divider2.Size = new Size(16, 30);

        this.cboCompareMode.Size = new Size(120, 30);
        this.cboCompareMode.Items.AddRange(new object[] { "Result Only", "Side by Side", "Overlay" });
        this.cboCompareMode.SelectedIndex = 0;

        this.divider3.Vertical = true;
        this.divider3.Size = new Size(16, 30);

        this.lblInferenceTime.Text = "";
        this.lblInferenceTime.TextAlign = ContentAlignment.MiddleLeft;
        this.lblInferenceTime.Size = new Size(140, 30);
        this.lblInferenceTime.ForeColor = Color.FromArgb(22, 119, 255);
        this.lblInferenceTime.Font = new Font("Segoe UI", 9F, FontStyle.Bold);

        this.panelToolbar.Controls.Add(this.btnPrev);
        this.panelToolbar.Controls.Add(this.btnNext);
        this.panelToolbar.Controls.Add(this.lblImageCounter);
        this.panelToolbar.Controls.Add(this.divider1);
        this.panelToolbar.Controls.Add(this.btnZoomIn);
        this.panelToolbar.Controls.Add(this.btnZoomOut);
        this.panelToolbar.Controls.Add(this.btnFit);
        this.panelToolbar.Controls.Add(this.divider2);
        this.panelToolbar.Controls.Add(this.cboCompareMode);
        this.panelToolbar.Controls.Add(this.divider3);
        this.panelToolbar.Controls.Add(this.lblInferenceTime);

        // ── Canvas (double-buffered) ─────────────────────────
        this.canvas.Dock = DockStyle.Fill;
        this.canvas.BackColor = Color.FromArgb(30, 30, 30);
        this.canvas.AllowDrop = true;

        // Assemble right top
        this.splitRight.Panel1.Controls.Add(this.canvas);
        this.splitRight.Panel1.Controls.Add(this.panelToolbar);

        // ── Bottom: Stats & Detections ───────────────────────
        this.panelStatsHeader.Text = "Detections";
        this.panelStatsHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelStatsHeader.Dock = DockStyle.Top;
        this.panelStatsHeader.Height = 28;
        this.panelStatsHeader.Padding = new Padding(8, 6, 0, 0);

        // DataGridView
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
        this.colClass.FillWeight = 25;

        this.colConfidence.HeaderText = "Confidence";
        this.colConfidence.Name = "colConfidence";
        this.colConfidence.FillWeight = 20;

        this.colBBox.HeaderText = "BBox (x1,y1,x2,y2)";
        this.colBBox.Name = "colBBox";
        this.colBBox.FillWeight = 35;

        this.colArea.HeaderText = "Area (px)";
        this.colArea.Name = "colArea";
        this.colArea.FillWeight = 20;

        this.dgvDetections.Columns.AddRange(new DataGridViewColumn[]
        {
            this.colClass, this.colConfidence, this.colBBox, this.colArea
        });

        // Stats bar
        this.panelStatsBar.Dock = DockStyle.Bottom;
        this.panelStatsBar.Height = 28;
        this.panelStatsBar.FlowDirection = FlowDirection.LeftToRight;
        this.panelStatsBar.BackColor = Color.FromArgb(245, 245, 245);
        this.panelStatsBar.Padding = new Padding(8, 4, 8, 0);

        this.lblTotalDetections.Text = "Detections: 0";
        this.lblTotalDetections.AutoSize = true;
        this.lblTotalDetections.Font = new Font("Segoe UI", 8.5F);
        this.lblTotalDetections.Padding = new Padding(0, 0, 16, 0);

        this.lblModelInfo.Text = "";
        this.lblModelInfo.AutoSize = true;
        this.lblModelInfo.Font = new Font("Segoe UI", 8.5F);
        this.lblModelInfo.ForeColor = Color.FromArgb(100, 100, 100);
        this.lblModelInfo.Padding = new Padding(0, 0, 16, 0);

        this.lblClassSummary.Text = "";
        this.lblClassSummary.AutoSize = true;
        this.lblClassSummary.Font = new Font("Segoe UI", 8.5F);
        this.lblClassSummary.ForeColor = Color.FromArgb(100, 100, 100);

        this.panelStatsBar.Controls.Add(this.lblTotalDetections);
        this.panelStatsBar.Controls.Add(this.lblModelInfo);
        this.panelStatsBar.Controls.Add(this.lblClassSummary);

        // Assemble right bottom
        this.splitRight.Panel2.Controls.Add(this.dgvDetections);
        this.splitRight.Panel2.Controls.Add(this.panelStatsBar);
        this.splitRight.Panel2.Controls.Add(this.panelStatsHeader);

        // Add splitRight to splitMain right panel
        this.splitMain.Panel2.Controls.Add(this.splitRight);

        // ═══════════════════════════════════════════════════════
        // ModelTestPanel
        // ═══════════════════════════════════════════════════════
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Controls.Add(this.splitMain);
        this.Name = "ModelTestPanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitRight).EndInit();
        this.splitRight.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.dgvDetections).EndInit();
        this.ResumeLayout(false);
    }

    #endregion

    // ── Top-level containers ─────────────────────────────────
    private SplitContainer splitMain;
    private SplitContainer splitRight;

    // ── Left sidebar ─────────────────────────────────────────
    private Panel panelLeftScroll;
    private Panel panelLeftInner;

    // Section 1: Model Config
    private AntdUI.Label lblSectionModel;
    private TableLayoutPanel tableModel;
    private AntdUI.Label lblRecentModel;
    private AntdUI.Select cboRecentModels;
    private AntdUI.Label lblModelFile;
    private AntdUI.Input txtModelFile;
    private AntdUI.Button btnBrowseModel;
    private AntdUI.Label lblVersion;
    private AntdUI.Select cboVersion;
    private AntdUI.Label lblVariant;
    private AntdUI.Select cboVariant;
    private AntdUI.Label lblNc;
    private AntdUI.InputNumber numNc;
    private AntdUI.Label lblClassNames;
    private AntdUI.Input txtClassNames;
    private AntdUI.Button btnBrowseClassNames;
    private AntdUI.Label lblProvider;
    private AntdUI.Select cboProvider;
    private AntdUI.Button btnLoadModel;

    // Section 2: Inference Parameters
    private AntdUI.Label lblSectionInference;
    private TableLayoutPanel tableInference;
    private AntdUI.Label lblConf;
    private AntdUI.Slider sliderConf;
    private AntdUI.Label lblConfValue;
    private AntdUI.Label lblIoU;
    private AntdUI.Slider sliderIoU;
    private AntdUI.Label lblIoUValue;
    private AntdUI.Label lblImgSize;
    private AntdUI.Select cboImgSize;
    private AntdUI.Label lblMaxDet;
    private AntdUI.InputNumber numMaxDet;
    private AntdUI.Label lblFp16;
    private AntdUI.Switch swFp16;
    private AntdUI.Label lblAutoRedetect;
    private AntdUI.Switch swAutoRedetect;

    // Section 3: Action Buttons
    private FlowLayoutPanel panelActions;
    private AntdUI.Button btnSelectImage;
    private AntdUI.Button btnSelectFolder;
    private AntdUI.Button btnRedetect;
    private AntdUI.Button btnSaveResult;
    private AntdUI.Button btnClear;

    // ── Right: Toolbar ───────────────────────────────────────
    private FlowLayoutPanel panelToolbar;
    private AntdUI.Button btnPrev;
    private AntdUI.Button btnNext;
    private AntdUI.Label lblImageCounter;
    private AntdUI.Divider divider1;
    private AntdUI.Button btnZoomIn;
    private AntdUI.Button btnZoomOut;
    private AntdUI.Button btnFit;
    private AntdUI.Divider divider2;
    private AntdUI.Select cboCompareMode;
    private AntdUI.Divider divider3;
    private AntdUI.Label lblInferenceTime;

    // ── Right: Canvas ────────────────────────────────────────
    private YOLO.WinForms.Controls.DoubleBufferedPanel canvas;

    // ── Right: Detections & Stats ────────────────────────────
    private AntdUI.Label panelStatsHeader;
    private DataGridView dgvDetections;
    private DataGridViewTextBoxColumn colClass;
    private DataGridViewTextBoxColumn colConfidence;
    private DataGridViewTextBoxColumn colBBox;
    private DataGridViewTextBoxColumn colArea;
    private FlowLayoutPanel panelStatsBar;
    private AntdUI.Label lblTotalDetections;
    private AntdUI.Label lblModelInfo;
    private AntdUI.Label lblClassSummary;
}
