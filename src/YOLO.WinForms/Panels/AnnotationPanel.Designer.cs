namespace YOLO.WinForms.Panels;

partial class AnnotationPanel
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

        // ── Main layout ──────────────────────────────────────────
        this.splitMain = new SplitContainer();
        this.splitRight = new SplitContainer();

        // ── Left sidebar ─────────────────────────────────────────
        this.panelProjectHeader = new AntdUI.Label();
        this.panelProjectButtons = new FlowLayoutPanel();
        this.btnNewProject = new AntdUI.Button();
        this.btnOpenProject = new AntdUI.Button();
        this.btnSaveProject = new AntdUI.Button();

        this.panelClassesHeader = new AntdUI.Label();
        this.lstClasses = new ListBox();
        this.panelClassButtons = new FlowLayoutPanel();
        this.btnAddClass = new AntdUI.Button();
        this.btnRemoveClass = new AntdUI.Button();

        this.lblCurrentClass = new AntdUI.Label();
        this.cboCurrentClass = new AntdUI.Select();

        // ── Center toolbar ───────────────────────────────────────
        this.toolbarPanel = new FlowLayoutPanel();
        this.tsbSelect = new AntdUI.Button();
        this.tsbDrawRect = new AntdUI.Button();
        this.toolSep1 = new AntdUI.Divider();
        this.tsbZoomIn = new AntdUI.Button();
        this.tsbZoomOut = new AntdUI.Button();
        this.tsbFit = new AntdUI.Button();
        this.tslZoom = new AntdUI.Label();
        this.toolSep2 = new AntdUI.Divider();
        this.tsbUndo = new AntdUI.Button();
        this.tsbRedo = new AntdUI.Button();
        this.toolSep3 = new AntdUI.Divider();
        this.tsbPrevImage = new AntdUI.Button();
        this.tslImageIndex = new AntdUI.Label();
        this.tsbNextImage = new AntdUI.Button();
        this.tsbMarkComplete = new AntdUI.Button();

        // ── Center canvas ────────────────────────────────────────
        this.canvas = new Controls.AnnotationCanvas();

        // ── Right sidebar ────────────────────────────────────────
        this.panelImagesHeader = new AntdUI.Label();
        this.panelImgFilter = new FlowLayoutPanel();
        this.cboImageFilter = new AntdUI.Select();
        this.btnImportImages = new AntdUI.Button();
        this.btnImportPdf = new AntdUI.Button();
        this.lstImages = new ListView();

        this.panelAnnotationsHeader = new AntdUI.Label();
        this.dgvAnnotations = new DataGridView();
        this.btnDeleteAnnotation = new AntdUI.Button();

        this.panelDatasetHeader = new AntdUI.Label();
        this.lblSplitRatio = new AntdUI.Label();
        this.trkSplitRatio = new AntdUI.Slider();
        this.lblSplitValue = new AntdUI.Label();
        this.panelDatasetButtons = new FlowLayoutPanel();
        this.btnGenerateDataset = new AntdUI.Button();
        this.btnGenerateAndTrain = new AntdUI.Button();
        this.btnEditConfig = new AntdUI.Button();

        // ── Progress & Status ────────────────────────────────────
        this.progressBar = new AntdUI.Progress();
        this.lblStatus = new AntdUI.Label();

        // ────────────────────────────────────────────────────────
        // NOTE: Do NOT use BeginInit/EndInit on SplitContainers here.
        // EndInit validates MinSize / SplitterDistance against the current
        // Width, which is still default (~150) at this point and causes
        // InvalidOperationException. Size-related properties are deferred
        // to the Load handler instead.
        this.splitMain.SuspendLayout();
        this.splitRight.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)this.dgvAnnotations).BeginInit();
        this.SuspendLayout();

        // ═══════════════════════════════════════════════════════
        // splitMain: Left panel | Right area
        // ═══════════════════════════════════════════════════════
        this.splitMain.Dock = DockStyle.Fill;
        this.splitMain.FixedPanel = FixedPanel.Panel1;
        this.splitMain.Name = "splitMain";
        // MinSize values are set in the deferred Load handler to avoid
        // InvalidOperationException when the control width is still default.

        this.splitRight.Dock = DockStyle.Fill;
        this.splitRight.FixedPanel = FixedPanel.Panel2;
        this.splitRight.Name = "splitRight";

        this.splitMain.Panel2.Controls.Add(this.splitRight);

        // ═══════════════════════════════════════════════════════
        // LEFT SIDEBAR
        // ═══════════════════════════════════════════════════════

        // -- Project section --
        this.panelProjectHeader.Text = "Project";
        this.panelProjectHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelProjectHeader.Dock = DockStyle.Top;
        this.panelProjectHeader.Height = 28;
        this.panelProjectHeader.Padding = new Padding(6, 6, 0, 0);

        this.panelProjectButtons.AutoSize = true;
        this.panelProjectButtons.Dock = DockStyle.Top;
        this.panelProjectButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelProjectButtons.Padding = new Padding(4, 2, 4, 6);
        this.panelProjectButtons.WrapContents = true;

        this.btnNewProject.Text = "New";
        this.btnNewProject.Type = AntdUI.TTypeMini.Primary;
        this.btnNewProject.Size = new Size(58, 32);

        this.btnOpenProject.Text = "Open";
        this.btnOpenProject.Size = new Size(58, 32);

        this.btnSaveProject.Text = "Save";
        this.btnSaveProject.Size = new Size(58, 32);

        this.panelProjectButtons.Controls.Add(this.btnNewProject);
        this.panelProjectButtons.Controls.Add(this.btnOpenProject);
        this.panelProjectButtons.Controls.Add(this.btnSaveProject);

        // -- Classes section --
        this.panelClassesHeader.Text = "Classes";
        this.panelClassesHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelClassesHeader.Dock = DockStyle.Top;
        this.panelClassesHeader.Height = 28;
        this.panelClassesHeader.Padding = new Padding(6, 6, 0, 0);

        this.lstClasses.Dock = DockStyle.Fill;
        this.lstClasses.IntegralHeight = false;
        this.lstClasses.Font = new Font("Segoe UI", 9.5F);
        this.lstClasses.BorderStyle = BorderStyle.None;
        this.lstClasses.BackColor = Color.FromArgb(250, 250, 250);

        this.panelClassButtons.AutoSize = true;
        this.panelClassButtons.Dock = DockStyle.Bottom;
        this.panelClassButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelClassButtons.Padding = new Padding(4, 2, 4, 2);

        this.btnAddClass.Text = "Add";
        this.btnAddClass.Type = AntdUI.TTypeMini.Primary;
        this.btnAddClass.Ghost = true;
        this.btnAddClass.Size = new Size(70, 30);

        this.btnRemoveClass.Text = "Remove";
        this.btnRemoveClass.Type = AntdUI.TTypeMini.Error;
        this.btnRemoveClass.Ghost = true;
        this.btnRemoveClass.Size = new Size(78, 30);

        this.panelClassButtons.Controls.Add(this.btnAddClass);
        this.panelClassButtons.Controls.Add(this.btnRemoveClass);

        // -- Draw class selector --
        this.lblCurrentClass.Text = "Draw class:";
        this.lblCurrentClass.AutoSize = true;
        this.lblCurrentClass.Dock = DockStyle.Left;
        this.lblCurrentClass.TextAlign = ContentAlignment.MiddleLeft;
        this.lblCurrentClass.Padding = new Padding(6, 0, 0, 0);
        this.lblCurrentClass.Font = new Font("Segoe UI", 9F);

        this.cboCurrentClass.Dock = DockStyle.Fill;

        var panelCurrentClass = new System.Windows.Forms.Panel();
        panelCurrentClass.Dock = DockStyle.Bottom;
        panelCurrentClass.Height = 36;
        panelCurrentClass.Padding = new Padding(4, 2, 4, 4);
        panelCurrentClass.Controls.Add(this.cboCurrentClass);
        panelCurrentClass.Controls.Add(this.lblCurrentClass);

        // -- Assemble left sidebar --
        var panelClassesContent = new System.Windows.Forms.Panel();
        panelClassesContent.Dock = DockStyle.Fill;
        panelClassesContent.Controls.Add(this.lstClasses);
        panelClassesContent.Controls.Add(this.panelClassButtons);
        panelClassesContent.Controls.Add(this.panelClassesHeader);

        this.splitMain.Panel1.Controls.Add(panelClassesContent);
        this.splitMain.Panel1.Controls.Add(panelCurrentClass);
        this.splitMain.Panel1.Controls.Add(this.panelProjectButtons);
        this.splitMain.Panel1.Controls.Add(this.panelProjectHeader);

        // ═══════════════════════════════════════════════════════
        // CENTER (splitRight.Panel1): toolbar + canvas + status
        // ═══════════════════════════════════════════════════════

        // -- Toolbar --
        this.toolbarPanel.Dock = DockStyle.Top;
        this.toolbarPanel.Height = 40;
        this.toolbarPanel.FlowDirection = FlowDirection.LeftToRight;
        this.toolbarPanel.WrapContents = false;
        this.toolbarPanel.Padding = new Padding(4, 4, 4, 0);
        this.toolbarPanel.BackColor = Color.FromArgb(250, 250, 250);

        this.tsbSelect.Text = "Select (V)";
        this.tsbSelect.Ghost = true;
        this.tsbSelect.Size = new Size(82, 32);

        this.tsbDrawRect.Text = "Draw (R)";
        this.tsbDrawRect.Type = AntdUI.TTypeMini.Primary;
        this.tsbDrawRect.Size = new Size(76, 32);

        this.toolSep1.Vertical = true;
        this.toolSep1.Size = new Size(12, 32);

        this.tsbZoomIn.Text = "+";
        this.tsbZoomIn.Ghost = true;
        this.tsbZoomIn.Shape = AntdUI.TShape.Circle;
        this.tsbZoomIn.Size = new Size(32, 32);

        this.tsbZoomOut.Text = "-";
        this.tsbZoomOut.Ghost = true;
        this.tsbZoomOut.Shape = AntdUI.TShape.Circle;
        this.tsbZoomOut.Size = new Size(32, 32);

        this.tsbFit.Text = "Fit";
        this.tsbFit.Ghost = true;
        this.tsbFit.Size = new Size(42, 32);

        this.tslZoom.Text = "100%";
        this.tslZoom.AutoSize = true;
        this.tslZoom.TextAlign = ContentAlignment.MiddleCenter;
        this.tslZoom.Padding = new Padding(2, 8, 2, 0);
        this.tslZoom.Font = new Font("Segoe UI", 8.5F);
        this.tslZoom.ForeColor = Color.FromArgb(120, 120, 120);

        this.toolSep2.Vertical = true;
        this.toolSep2.Size = new Size(12, 32);

        this.tsbUndo.Text = "Undo";
        this.tsbUndo.Ghost = true;
        this.tsbUndo.Size = new Size(52, 32);

        this.tsbRedo.Text = "Redo";
        this.tsbRedo.Ghost = true;
        this.tsbRedo.Size = new Size(52, 32);

        this.toolSep3.Vertical = true;
        this.toolSep3.Size = new Size(12, 32);

        this.tsbPrevImage.Text = "<<";
        this.tsbPrevImage.Ghost = true;
        this.tsbPrevImage.Size = new Size(36, 32);

        this.tslImageIndex.Text = "0 / 0";
        this.tslImageIndex.AutoSize = true;
        this.tslImageIndex.TextAlign = ContentAlignment.MiddleCenter;
        this.tslImageIndex.Padding = new Padding(2, 8, 2, 0);
        this.tslImageIndex.Font = new Font("Segoe UI", 9F);

        this.tsbNextImage.Text = ">>";
        this.tsbNextImage.Ghost = true;
        this.tsbNextImage.Size = new Size(36, 32);

        this.tsbMarkComplete.Text = "Mark Done";
        this.tsbMarkComplete.Type = AntdUI.TTypeMini.Success;
        this.tsbMarkComplete.Ghost = true;
        this.tsbMarkComplete.Size = new Size(90, 32);

        this.toolbarPanel.Controls.AddRange(new Control[]
        {
            this.tsbSelect, this.tsbDrawRect, this.toolSep1,
            this.tsbZoomIn, this.tsbZoomOut, this.tsbFit, this.tslZoom, this.toolSep2,
            this.tsbUndo, this.tsbRedo, this.toolSep3,
            this.tsbPrevImage, this.tslImageIndex, this.tsbNextImage, this.tsbMarkComplete
        });

        // -- Canvas --
        this.canvas.Dock = DockStyle.Fill;

        // -- Progress --
        this.progressBar.Dock = DockStyle.Bottom;
        this.progressBar.Height = 4;
        this.progressBar.Value = 0F;
        this.progressBar.Shape = AntdUI.TShapeProgress.Round;
        this.progressBar.Back = Color.FromArgb(240, 240, 240);

        // -- Status --
        this.lblStatus.Dock = DockStyle.Bottom;
        this.lblStatus.Height = 26;
        this.lblStatus.TextAlign = ContentAlignment.MiddleLeft;
        this.lblStatus.Font = new Font("Segoe UI", 9F);
        this.lblStatus.Text = "No project loaded";
        this.lblStatus.Padding = new Padding(8, 0, 0, 0);
        this.lblStatus.BackColor = Color.FromArgb(248, 248, 248);
        this.lblStatus.ForeColor = Color.FromArgb(100, 100, 100);

        this.splitRight.Panel1.Controls.Add(this.canvas);
        this.splitRight.Panel1.Controls.Add(this.toolbarPanel);
        this.splitRight.Panel1.Controls.Add(this.progressBar);
        this.splitRight.Panel1.Controls.Add(this.lblStatus);

        // ═══════════════════════════════════════════════════════
        // RIGHT SIDEBAR (splitRight.Panel2)
        // ═══════════════════════════════════════════════════════

        // -- Images section --
        this.panelImagesHeader.Text = "Images";
        this.panelImagesHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelImagesHeader.Dock = DockStyle.Top;
        this.panelImagesHeader.Height = 28;
        this.panelImagesHeader.Padding = new Padding(6, 6, 0, 0);

        this.panelImgFilter.AutoSize = true;
        this.panelImgFilter.Dock = DockStyle.Top;
        this.panelImgFilter.FlowDirection = FlowDirection.LeftToRight;
        this.panelImgFilter.WrapContents = true;
        this.panelImgFilter.Padding = new Padding(4, 0, 4, 4);

        this.cboImageFilter.Size = new Size(98, 30);
        this.cboImageFilter.Items.AddRange(new object[] { "All", "Completed", "Incomplete" });
        this.cboImageFilter.SelectedIndex = 0;

        this.btnImportImages.Text = "Images";
        this.btnImportImages.Ghost = true;
        this.btnImportImages.Size = new Size(68, 30);

        this.btnImportPdf.Text = "PDF";
        this.btnImportPdf.Ghost = true;
        this.btnImportPdf.Size = new Size(48, 30);

        this.panelImgFilter.Controls.Add(this.cboImageFilter);
        this.panelImgFilter.Controls.Add(this.btnImportImages);
        this.panelImgFilter.Controls.Add(this.btnImportPdf);

        this.lstImages.Dock = DockStyle.Fill;
        this.lstImages.View = View.List;
        this.lstImages.MultiSelect = false;
        this.lstImages.HideSelection = false;
        this.lstImages.FullRowSelect = true;
        this.lstImages.Font = new Font("Segoe UI", 9F);
        this.lstImages.BorderStyle = BorderStyle.None;
        this.lstImages.BackColor = Color.FromArgb(250, 250, 250);

        var panelImagesList = new System.Windows.Forms.Panel();
        panelImagesList.Dock = DockStyle.Fill;
        panelImagesList.Controls.Add(this.lstImages);
        panelImagesList.Controls.Add(this.panelImgFilter);
        panelImagesList.Controls.Add(this.panelImagesHeader);

        // -- Annotations section --
        this.panelAnnotationsHeader.Text = "Annotations";
        this.panelAnnotationsHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelAnnotationsHeader.Dock = DockStyle.Top;
        this.panelAnnotationsHeader.Height = 28;
        this.panelAnnotationsHeader.Padding = new Padding(6, 6, 0, 0);

        this.dgvAnnotations.Dock = DockStyle.Fill;
        this.dgvAnnotations.AllowUserToAddRows = false;
        this.dgvAnnotations.AllowUserToDeleteRows = false;
        this.dgvAnnotations.ReadOnly = true;
        this.dgvAnnotations.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        this.dgvAnnotations.MultiSelect = false;
        this.dgvAnnotations.RowHeadersVisible = false;
        this.dgvAnnotations.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvAnnotations.BackgroundColor = Color.FromArgb(250, 250, 250);
        this.dgvAnnotations.BorderStyle = BorderStyle.None;
        this.dgvAnnotations.Font = new Font("Segoe UI", 9F);
        this.dgvAnnotations.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 245);
        this.dgvAnnotations.EnableHeadersVisualStyles = false;
        this.dgvAnnotations.ColumnCount = 3;
        this.dgvAnnotations.Columns[0].Name = "Class";
        this.dgvAnnotations.Columns[0].FillWeight = 35;
        this.dgvAnnotations.Columns[1].Name = "BBox";
        this.dgvAnnotations.Columns[1].FillWeight = 50;
        this.dgvAnnotations.Columns[2].Name = "#";
        this.dgvAnnotations.Columns[2].FillWeight = 15;

        this.btnDeleteAnnotation.Dock = DockStyle.Bottom;
        this.btnDeleteAnnotation.Height = 32;
        this.btnDeleteAnnotation.Text = "Delete Selected (Del)";
        this.btnDeleteAnnotation.Type = AntdUI.TTypeMini.Error;
        this.btnDeleteAnnotation.Ghost = true;

        var panelAnnotations = new System.Windows.Forms.Panel();
        panelAnnotations.Dock = DockStyle.Bottom;
        panelAnnotations.Height = 200;
        panelAnnotations.Controls.Add(this.dgvAnnotations);
        panelAnnotations.Controls.Add(this.btnDeleteAnnotation);
        panelAnnotations.Controls.Add(this.panelAnnotationsHeader);

        // -- Dataset section --
        this.panelDatasetHeader.Text = "Dataset";
        this.panelDatasetHeader.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        this.panelDatasetHeader.Dock = DockStyle.Top;
        this.panelDatasetHeader.Height = 28;
        this.panelDatasetHeader.Padding = new Padding(6, 6, 0, 0);

        this.lblSplitRatio.Text = "Train / Val Split:";
        this.lblSplitRatio.AutoSize = true;
        this.lblSplitRatio.Dock = DockStyle.Top;
        this.lblSplitRatio.Padding = new Padding(6, 2, 0, 0);
        this.lblSplitRatio.Font = new Font("Segoe UI", 9F);

        this.trkSplitRatio.Dock = DockStyle.Top;
        this.trkSplitRatio.MinValue = 50;
        this.trkSplitRatio.MaxValue = 95;
        this.trkSplitRatio.Value = 80;
        this.trkSplitRatio.Height = 30;

        this.lblSplitValue.Text = "80% / 20%";
        this.lblSplitValue.AutoSize = true;
        this.lblSplitValue.Dock = DockStyle.Top;
        this.lblSplitValue.TextAlign = ContentAlignment.MiddleCenter;
        this.lblSplitValue.Padding = new Padding(6, 0, 0, 2);
        this.lblSplitValue.Font = new Font("Segoe UI", 9F);
        this.lblSplitValue.ForeColor = Color.FromArgb(100, 100, 100);

        this.panelDatasetButtons.AutoSize = true;
        this.panelDatasetButtons.Dock = DockStyle.Top;
        this.panelDatasetButtons.FlowDirection = FlowDirection.LeftToRight;
        this.panelDatasetButtons.WrapContents = true;
        this.panelDatasetButtons.Padding = new Padding(4, 2, 4, 4);

        this.btnGenerateDataset.Text = "Generate";
        this.btnGenerateDataset.Size = new Size(85, 32);

        this.btnGenerateAndTrain.Text = "Generate & Train";
        this.btnGenerateAndTrain.Type = AntdUI.TTypeMini.Primary;
        this.btnGenerateAndTrain.Size = new Size(124, 32);

        this.btnEditConfig.Text = "Config";
        this.btnEditConfig.Ghost = true;
        this.btnEditConfig.Size = new Size(66, 32);

        this.panelDatasetButtons.Controls.Add(this.btnGenerateDataset);
        this.panelDatasetButtons.Controls.Add(this.btnGenerateAndTrain);
        this.panelDatasetButtons.Controls.Add(this.btnEditConfig);

        var panelDataset = new System.Windows.Forms.Panel();
        panelDataset.Dock = DockStyle.Bottom;
        panelDataset.Height = 160;
        // Add in reverse dock order
        panelDataset.Controls.Add(this.panelDatasetButtons);
        panelDataset.Controls.Add(this.lblSplitValue);
        panelDataset.Controls.Add(this.trkSplitRatio);
        panelDataset.Controls.Add(this.lblSplitRatio);
        panelDataset.Controls.Add(this.panelDatasetHeader);

        // Assemble right panel
        this.splitRight.Panel2.Controls.Add(panelImagesList);
        this.splitRight.Panel2.Controls.Add(panelAnnotations);
        this.splitRight.Panel2.Controls.Add(panelDataset);

        // ═══════════════════════════════════════════════════════
        // AnnotationPanel
        // ═══════════════════════════════════════════════════════
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.Controls.Add(this.splitMain);
        this.Name = "AnnotationPanel";
        this.Size = new Size(1200, 700);

        this.splitMain.ResumeLayout(false);
        this.splitRight.ResumeLayout(false);
        ((System.ComponentModel.ISupportInitialize)this.dgvAnnotations).EndInit();
        this.ResumeLayout(false);

        // Defer all SplitterDistance / MinSize until the control has its real layout size.
        // Use BeginInvoke so it runs after the AntdUI.Tabs container finishes layout.
        this.Load += (s, e) =>
        {
            BeginInvoke(() =>
            {
                try
                {
                    this.splitMain.Panel1MinSize = 160;
                    this.splitMain.Panel2MinSize = 200;
                    if (this.splitMain.Width > 400)
                        this.splitMain.SplitterDistance = 200;

                    this.splitRight.Panel1MinSize = 200;
                    this.splitRight.Panel2MinSize = 220;
                    int rightAvail = this.splitRight.Width - 220 - this.splitRight.SplitterWidth;
                    if (rightAvail > 200)
                        this.splitRight.SplitterDistance = rightAvail;
                }
                catch (InvalidOperationException) { /* control not yet sized */ }
            });
        };
    }

    #endregion

    // Main layout
    private SplitContainer splitMain;
    private SplitContainer splitRight;

    // Left sidebar - Project
    private AntdUI.Label panelProjectHeader;
    private FlowLayoutPanel panelProjectButtons;
    private AntdUI.Button btnNewProject;
    private AntdUI.Button btnOpenProject;
    private AntdUI.Button btnSaveProject;

    // Left sidebar - Classes
    private AntdUI.Label panelClassesHeader;
    private ListBox lstClasses;
    private FlowLayoutPanel panelClassButtons;
    private AntdUI.Button btnAddClass;
    private AntdUI.Button btnRemoveClass;
    private AntdUI.Label lblCurrentClass;
    private AntdUI.Select cboCurrentClass;

    // Center - Toolbar
    private FlowLayoutPanel toolbarPanel;
    private AntdUI.Button tsbSelect;
    private AntdUI.Button tsbDrawRect;
    private AntdUI.Divider toolSep1;
    private AntdUI.Button tsbZoomIn;
    private AntdUI.Button tsbZoomOut;
    private AntdUI.Button tsbFit;
    private AntdUI.Label tslZoom;
    private AntdUI.Divider toolSep2;
    private AntdUI.Button tsbUndo;
    private AntdUI.Button tsbRedo;
    private AntdUI.Divider toolSep3;
    private AntdUI.Button tsbPrevImage;
    private AntdUI.Button tsbNextImage;
    private AntdUI.Button tsbMarkComplete;
    private AntdUI.Label tslImageIndex;

    // Center - Canvas
    private Controls.AnnotationCanvas canvas;

    // Center - Status / Progress
    private AntdUI.Progress progressBar;
    private AntdUI.Label lblStatus;

    // Right sidebar - Images
    private AntdUI.Label panelImagesHeader;
    private FlowLayoutPanel panelImgFilter;
    private AntdUI.Select cboImageFilter;
    private AntdUI.Button btnImportImages;
    private AntdUI.Button btnImportPdf;
    private ListView lstImages;

    // Right sidebar - Annotations
    private AntdUI.Label panelAnnotationsHeader;
    private DataGridView dgvAnnotations;
    private AntdUI.Button btnDeleteAnnotation;

    // Right sidebar - Dataset
    private AntdUI.Label panelDatasetHeader;
    private AntdUI.Label lblSplitRatio;
    private AntdUI.Slider trkSplitRatio;
    private AntdUI.Label lblSplitValue;
    private FlowLayoutPanel panelDatasetButtons;
    private AntdUI.Button btnGenerateDataset;
    private AntdUI.Button btnGenerateAndTrain;
    private AntdUI.Button btnEditConfig;
}
