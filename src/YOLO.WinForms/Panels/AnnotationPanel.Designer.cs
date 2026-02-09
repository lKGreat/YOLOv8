namespace YOLO.WinForms.Panels;

partial class AnnotationPanel
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

        // ── Main layout: left | center | right ─────────────────────
        this.splitMain = new SplitContainer();
        this.splitRight = new SplitContainer();

        // ── Left sidebar ───────────────────────────────────────────
        this.grpProject = new GroupBox();
        this.btnNewProject = new Button();
        this.btnOpenProject = new Button();
        this.btnSaveProject = new Button();
        this.panelProjectButtons = new FlowLayoutPanel();

        this.grpClasses = new GroupBox();
        this.lstClasses = new ListBox();
        this.btnAddClass = new Button();
        this.btnRemoveClass = new Button();
        this.panelClassButtons = new FlowLayoutPanel();

        this.lblCurrentClass = new Label();
        this.cboCurrentClass = new ComboBox();

        // ── Center toolbar ─────────────────────────────────────────
        this.toolStrip = new ToolStrip();
        this.tsbSelect = new ToolStripButton();
        this.tsbDrawRect = new ToolStripButton();
        this.tsSep1 = new ToolStripSeparator();
        this.tsbZoomIn = new ToolStripButton();
        this.tsbZoomOut = new ToolStripButton();
        this.tsbFit = new ToolStripButton();
        this.tslZoom = new ToolStripLabel();
        this.tsSep2 = new ToolStripSeparator();
        this.tsbUndo = new ToolStripButton();
        this.tsbRedo = new ToolStripButton();
        this.tsSep3 = new ToolStripSeparator();
        this.tsbPrevImage = new ToolStripButton();
        this.tsbNextImage = new ToolStripButton();
        this.tsbMarkComplete = new ToolStripButton();
        this.tslImageIndex = new ToolStripLabel();

        // ── Center canvas ──────────────────────────────────────────
        this.canvas = new Controls.AnnotationCanvas();

        // ── Right sidebar ──────────────────────────────────────────
        this.grpImages = new GroupBox();
        this.lstImages = new ListView();
        this.panelImgFilter = new FlowLayoutPanel();
        this.cboImageFilter = new ComboBox();
        this.btnImportImages = new Button();
        this.btnImportPdf = new Button();

        this.grpAnnotations = new GroupBox();
        this.dgvAnnotations = new DataGridView();
        this.btnDeleteAnnotation = new Button();

        this.grpDataset = new GroupBox();
        this.lblSplitRatio = new Label();
        this.trkSplitRatio = new TrackBar();
        this.lblSplitValue = new Label();
        this.btnGenerateDataset = new Button();
        this.btnEditConfig = new Button();

        // ── Status bar ─────────────────────────────────────────────
        this.lblStatus = new Label();

        // ────────────────────────────────────────────────────────────
        // splitMain: Left panel | Right area
        // ────────────────────────────────────────────────────────────
        ((System.ComponentModel.ISupportInitialize)this.splitMain).BeginInit();
        this.splitMain.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.splitRight).BeginInit();
        this.splitRight.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.trkSplitRatio).BeginInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvAnnotations).BeginInit();
        this.SuspendLayout();

        // ── splitMain ──────────────────────────────────────────────
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Location = new Point(0, 0);
        this.splitMain.Name = "splitMain";
        this.splitMain.TabIndex = 0;

        // ── splitRight (center | right sidebar) ────────────────────
        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.FixedPanel = FixedPanel.Panel2;
        this.splitRight.Name = "splitRight";

        this.splitMain.Panel2.Controls.Add(this.splitRight);

        // ════════════════════════════════════════════════════════════
        // LEFT SIDEBAR (splitMain.Panel1)
        // ════════════════════════════════════════════════════════════

        // panelProjectButtons
        this.panelProjectButtons.AutoSize = true;
        this.panelProjectButtons.Dock = DockStyle.Top;
        this.panelProjectButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelProjectButtons.Padding = new Padding(2);

        // btnNewProject
        this.btnNewProject.Size = new Size(55, 28);
        this.btnNewProject.Text = "New";
        this.btnNewProject.FlatStyle = FlatStyle.Flat;
        this.btnNewProject.TabIndex = 0;

        // btnOpenProject
        this.btnOpenProject.Size = new Size(55, 28);
        this.btnOpenProject.Text = "Open";
        this.btnOpenProject.FlatStyle = FlatStyle.Flat;
        this.btnOpenProject.TabIndex = 1;

        // btnSaveProject
        this.btnSaveProject.Size = new Size(55, 28);
        this.btnSaveProject.Text = "Save";
        this.btnSaveProject.FlatStyle = FlatStyle.Flat;
        this.btnSaveProject.TabIndex = 2;

        this.panelProjectButtons.Controls.Add(this.btnNewProject);
        this.panelProjectButtons.Controls.Add(this.btnOpenProject);
        this.panelProjectButtons.Controls.Add(this.btnSaveProject);

        // grpProject
        this.grpProject.Dock = DockStyle.Top;
        this.grpProject.Height = 60;
        this.grpProject.Padding = new Padding(4);
        this.grpProject.Text = "Project";
        this.grpProject.Controls.Add(this.panelProjectButtons);

        // panelClassButtons
        this.panelClassButtons.AutoSize = true;
        this.panelClassButtons.Dock = DockStyle.Bottom;
        this.panelClassButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelClassButtons.Padding = new Padding(2);

        // btnAddClass
        this.btnAddClass.Size = new Size(75, 28);
        this.btnAddClass.Text = "Add";
        this.btnAddClass.FlatStyle = FlatStyle.Flat;

        // btnRemoveClass
        this.btnRemoveClass.Size = new Size(75, 28);
        this.btnRemoveClass.Text = "Remove";
        this.btnRemoveClass.FlatStyle = FlatStyle.Flat;

        this.panelClassButtons.Controls.Add(this.btnAddClass);
        this.panelClassButtons.Controls.Add(this.btnRemoveClass);

        // lstClasses
        this.lstClasses.Dock = DockStyle.Fill;
        this.lstClasses.IntegralHeight = false;
        this.lstClasses.Font = new Font("Segoe UI", 9.5F);

        // grpClasses
        this.grpClasses.Dock = DockStyle.Fill;
        this.grpClasses.Padding = new Padding(4);
        this.grpClasses.Text = "Classes";
        this.grpClasses.Controls.Add(this.lstClasses);
        this.grpClasses.Controls.Add(this.panelClassButtons);

        // lblCurrentClass + cboCurrentClass
        this.lblCurrentClass.Text = "Draw class:";
        this.lblCurrentClass.AutoSize = true;
        this.lblCurrentClass.Dock = DockStyle.Left;
        this.lblCurrentClass.TextAlign = ContentAlignment.MiddleLeft;
        this.lblCurrentClass.Padding = new Padding(4, 0, 0, 0);

        this.cboCurrentClass.Dock = DockStyle.Fill;
        this.cboCurrentClass.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboCurrentClass.Font = new Font("Segoe UI", 9.5F);

        var panelCurrentClass = new Panel();
        panelCurrentClass.Dock = DockStyle.Bottom;
        panelCurrentClass.Height = 32;
        panelCurrentClass.Controls.Add(this.cboCurrentClass);
        panelCurrentClass.Controls.Add(this.lblCurrentClass);

        this.splitMain.Panel1.Controls.Add(this.grpClasses);
        this.splitMain.Panel1.Controls.Add(panelCurrentClass);
        this.splitMain.Panel1.Controls.Add(this.grpProject);

        // ════════════════════════════════════════════════════════════
        // CENTER (splitRight.Panel1): toolbar + canvas
        // ════════════════════════════════════════════════════════════

        // toolStrip
        this.toolStrip.GripStyle = ToolStripGripStyle.Hidden;
        this.toolStrip.RenderMode = ToolStripRenderMode.Professional;

        this.tsbSelect.Text = "Select (V)";
        this.tsbSelect.CheckOnClick = true;
        this.tsbSelect.DisplayStyle = ToolStripItemDisplayStyle.Text;

        this.tsbDrawRect.Text = "Draw Rect (R)";
        this.tsbDrawRect.CheckOnClick = true;
        this.tsbDrawRect.Checked = true;
        this.tsbDrawRect.DisplayStyle = ToolStripItemDisplayStyle.Text;

        this.tsbZoomIn.Text = "Zoom +";
        this.tsbZoomIn.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tsbZoomOut.Text = "Zoom -";
        this.tsbZoomOut.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tsbFit.Text = "Fit";
        this.tsbFit.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tslZoom.Text = "100%";

        this.tsbUndo.Text = "Undo";
        this.tsbUndo.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tsbRedo.Text = "Redo";
        this.tsbRedo.DisplayStyle = ToolStripItemDisplayStyle.Text;

        this.tsbPrevImage.Text = "<< Prev";
        this.tsbPrevImage.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tsbNextImage.Text = "Next >>";
        this.tsbNextImage.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tsbMarkComplete.Text = "Mark Complete (Enter)";
        this.tsbMarkComplete.DisplayStyle = ToolStripItemDisplayStyle.Text;
        this.tslImageIndex.Text = "0 / 0";

        this.toolStrip.Items.AddRange(new ToolStripItem[]
        {
            this.tsbSelect, this.tsbDrawRect, this.tsSep1,
            this.tsbZoomIn, this.tsbZoomOut, this.tsbFit, this.tslZoom, this.tsSep2,
            this.tsbUndo, this.tsbRedo, this.tsSep3,
            this.tsbPrevImage, this.tslImageIndex, this.tsbNextImage, this.tsbMarkComplete
        });

        // canvas
        this.canvas.Dock = DockStyle.Fill;

        // lblStatus
        this.lblStatus.Dock = DockStyle.Bottom;
        this.lblStatus.Height = 24;
        this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        this.lblStatus.Font = new Font("Segoe UI", 9F);
        this.lblStatus.Text = "No project loaded";
        this.lblStatus.Padding = new Padding(6, 0, 0, 0);
        this.lblStatus.BackColor = Color.FromArgb(45, 45, 48);
        this.lblStatus.ForeColor = Color.FromArgb(200, 200, 200);

        this.splitRight.Panel1.Controls.Add(this.canvas);
        this.splitRight.Panel1.Controls.Add(this.toolStrip);
        this.splitRight.Panel1.Controls.Add(this.lblStatus);

        // ════════════════════════════════════════════════════════════
        // RIGHT SIDEBAR (splitRight.Panel2)
        // ════════════════════════════════════════════════════════════

        // panelImgFilter
        this.panelImgFilter.AutoSize = true;
        this.panelImgFilter.Dock = DockStyle.Top;
        this.panelImgFilter.FlowDirection = FlowDirection.LeftToRight;
        this.panelImgFilter.WrapContents = true;
        this.panelImgFilter.Padding = new Padding(2);

        this.cboImageFilter.Size = new Size(90, 25);
        this.cboImageFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        this.cboImageFilter.Items.AddRange(new object[] { "All", "Completed", "Incomplete" });
        this.cboImageFilter.SelectedIndex = 0;

        this.btnImportImages.Size = new Size(65, 25);
        this.btnImportImages.Text = "Images";
        this.btnImportImages.FlatStyle = FlatStyle.Flat;

        this.btnImportPdf.Size = new Size(50, 25);
        this.btnImportPdf.Text = "PDF";
        this.btnImportPdf.FlatStyle = FlatStyle.Flat;

        this.panelImgFilter.Controls.Add(this.cboImageFilter);
        this.panelImgFilter.Controls.Add(this.btnImportImages);
        this.panelImgFilter.Controls.Add(this.btnImportPdf);

        // lstImages
        this.lstImages.Dock = DockStyle.Fill;
        this.lstImages.View = View.List;
        this.lstImages.MultiSelect = false;
        this.lstImages.HideSelection = false;
        this.lstImages.FullRowSelect = true;
        this.lstImages.Font = new Font("Segoe UI", 9F);

        // grpImages
        this.grpImages.Dock = DockStyle.Fill;
        this.grpImages.Padding = new Padding(4);
        this.grpImages.Text = "Images";
        this.grpImages.Controls.Add(this.lstImages);
        this.grpImages.Controls.Add(this.panelImgFilter);

        // dgvAnnotations
        this.dgvAnnotations.Dock = DockStyle.Fill;
        this.dgvAnnotations.AllowUserToAddRows = false;
        this.dgvAnnotations.AllowUserToDeleteRows = false;
        this.dgvAnnotations.ReadOnly = true;
        this.dgvAnnotations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        this.dgvAnnotations.MultiSelect = false;
        this.dgvAnnotations.RowHeadersVisible = false;
        this.dgvAnnotations.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvAnnotations.BackgroundColor = SystemColors.Control;
        this.dgvAnnotations.Font = new Font("Segoe UI", 9F);
        this.dgvAnnotations.ColumnCount = 3;
        this.dgvAnnotations.Columns[0].Name = "Class";
        this.dgvAnnotations.Columns[0].FillWeight = 40;
        this.dgvAnnotations.Columns[1].Name = "BBox (cx,cy,w,h)";
        this.dgvAnnotations.Columns[1].FillWeight = 50;
        this.dgvAnnotations.Columns[2].Name = "#";
        this.dgvAnnotations.Columns[2].FillWeight = 10;

        // btnDeleteAnnotation
        this.btnDeleteAnnotation.Dock = DockStyle.Bottom;
        this.btnDeleteAnnotation.Height = 28;
        this.btnDeleteAnnotation.Text = "Delete Selected (Del)";
        this.btnDeleteAnnotation.FlatStyle = FlatStyle.Flat;

        // grpAnnotations
        this.grpAnnotations.Dock = DockStyle.Bottom;
        this.grpAnnotations.Height = 200;
        this.grpAnnotations.Padding = new Padding(4);
        this.grpAnnotations.Text = "Annotations";
        this.grpAnnotations.Controls.Add(this.dgvAnnotations);
        this.grpAnnotations.Controls.Add(this.btnDeleteAnnotation);

        // Dataset section
        this.lblSplitRatio.Text = "Train/Val Split:";
        this.lblSplitRatio.AutoSize = true;
        this.lblSplitRatio.Dock = DockStyle.Top;
        this.lblSplitRatio.Padding = new Padding(4, 4, 0, 0);

        this.trkSplitRatio.Dock = DockStyle.Top;
        this.trkSplitRatio.Minimum = 50;
        this.trkSplitRatio.Maximum = 95;
        this.trkSplitRatio.Value = 80;
        this.trkSplitRatio.TickFrequency = 5;
        this.trkSplitRatio.Height = 35;

        this.lblSplitValue.Text = "80% / 20%";
        this.lblSplitValue.AutoSize = true;
        this.lblSplitValue.Dock = DockStyle.Top;
        this.lblSplitValue.TextAlign = ContentAlignment.MiddleCenter;
        this.lblSplitValue.Padding = new Padding(4, 0, 0, 2);

        var panelDatasetButtons = new FlowLayoutPanel();
        panelDatasetButtons.AutoSize = true;
        panelDatasetButtons.Dock = DockStyle.Top;
        panelDatasetButtons.FlowDirection = FlowDirection.LeftToRight;
        panelDatasetButtons.Padding = new Padding(2);

        this.btnGenerateDataset.Size = new Size(100, 28);
        this.btnGenerateDataset.Text = "Generate";
        this.btnGenerateDataset.FlatStyle = FlatStyle.Flat;

        this.btnEditConfig.Size = new Size(90, 28);
        this.btnEditConfig.Text = "Edit Config";
        this.btnEditConfig.FlatStyle = FlatStyle.Flat;

        panelDatasetButtons.Controls.Add(this.btnGenerateDataset);
        panelDatasetButtons.Controls.Add(this.btnEditConfig);

        // grpDataset
        this.grpDataset.Dock = DockStyle.Bottom;
        this.grpDataset.Height = 155;
        this.grpDataset.Padding = new Padding(4);
        this.grpDataset.Text = "Dataset";
        // Add in reverse dock order so top items appear first
        this.grpDataset.Controls.Add(panelDatasetButtons);
        this.grpDataset.Controls.Add(this.lblSplitValue);
        this.grpDataset.Controls.Add(this.trkSplitRatio);
        this.grpDataset.Controls.Add(this.lblSplitRatio);

        // Assemble right panel
        this.splitRight.Panel2.Controls.Add(this.grpImages);
        this.splitRight.Panel2.Controls.Add(this.grpAnnotations);
        this.splitRight.Panel2.Controls.Add(this.grpDataset);

        // ── AnnotationPanel ────────────────────────────────────────
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Controls.Add(this.splitMain);
        this.Name = "AnnotationPanel";
        this.Size = new Size(1200, 700);

        ((System.ComponentModel.ISupportInitialize)this.splitMain).EndInit();
        this.splitMain.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.splitRight).EndInit();
        this.splitRight.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.trkSplitRatio).EndInit();
        ((System.ComponentModel.ISupportInitialize)this.dgvAnnotations).EndInit();
        this.ResumeLayout(false);

        // Defer SplitterDistance to the Load event, when the control has its real size.
        // Setting it here throws InvalidOperationException because the SplitContainer
        // width is still the default (~150px) before the form is shown.
        this.Load += (s, e) =>
        {
            this.splitMain.SplitterDistance = 200;
            this.splitRight.Panel2MinSize = 220;
            this.splitRight.SplitterDistance = Math.Max(
                this.splitRight.Panel1MinSize,
                this.splitRight.Width - this.splitRight.Panel2MinSize - this.splitRight.SplitterWidth);
        };
    }

    #endregion

    // Main layout
    private SplitContainer splitMain;
    private SplitContainer splitRight;

    // Left sidebar - Project
    private GroupBox grpProject;
    private Button btnNewProject;
    private Button btnOpenProject;
    private Button btnSaveProject;
    private FlowLayoutPanel panelProjectButtons;

    // Left sidebar - Classes
    private GroupBox grpClasses;
    private ListBox lstClasses;
    private Button btnAddClass;
    private Button btnRemoveClass;
    private FlowLayoutPanel panelClassButtons;
    private Label lblCurrentClass;
    private ComboBox cboCurrentClass;

    // Center - Toolbar
    private ToolStrip toolStrip;
    private ToolStripButton tsbSelect;
    private ToolStripButton tsbDrawRect;
    private ToolStripSeparator tsSep1;
    private ToolStripButton tsbZoomIn;
    private ToolStripButton tsbZoomOut;
    private ToolStripButton tsbFit;
    private ToolStripLabel tslZoom;
    private ToolStripSeparator tsSep2;
    private ToolStripButton tsbUndo;
    private ToolStripButton tsbRedo;
    private ToolStripSeparator tsSep3;
    private ToolStripButton tsbPrevImage;
    private ToolStripButton tsbNextImage;
    private ToolStripButton tsbMarkComplete;
    private ToolStripLabel tslImageIndex;

    // Center - Canvas
    private Controls.AnnotationCanvas canvas;

    // Center - Status bar
    private Label lblStatus;

    // Right sidebar - Images
    private GroupBox grpImages;
    private ListView lstImages;
    private FlowLayoutPanel panelImgFilter;
    private ComboBox cboImageFilter;
    private Button btnImportImages;
    private Button btnImportPdf;

    // Right sidebar - Annotations
    private GroupBox grpAnnotations;
    private DataGridView dgvAnnotations;
    private Button btnDeleteAnnotation;

    // Right sidebar - Dataset
    private GroupBox grpDataset;
    private Label lblSplitRatio;
    private TrackBar trkSplitRatio;
    private Label lblSplitValue;
    private Button btnGenerateDataset;
    private Button btnEditConfig;
}
