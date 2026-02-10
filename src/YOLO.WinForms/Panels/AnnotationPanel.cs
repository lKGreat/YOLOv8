using YOLO.WinForms.Controls;
using YOLO.WinForms.Forms;
using YOLO.WinForms.Models;
using YOLO.WinForms.Services;

namespace YOLO.WinForms.Panels;

/// <summary>
/// Main annotation workspace panel with image annotation, project management,
/// class editing, dataset generation, and keyboard shortcuts.
/// </summary>
public partial class AnnotationPanel : UserControl
{
    private readonly AnnotationProjectService _projectService = new();
    private readonly DatasetSplitService _datasetService = new();
    private readonly AnnotationCommandManager _cmdManager = new();

    private AnnotationProject? _project;
    private int _currentImageIndex = -1;
    private Image? _currentImage;

    // Reentrancy guards to prevent infinite event loops
    private bool _updatingSelection;
    private bool _navigating;

    /// <summary>Fired when status text should be updated in the main form.</summary>
    public event EventHandler<string>? StatusChanged;

    /// <summary>Fired when dataset is generated and ready for training. Passes the YAML path.</summary>
    public event Action<string>? DatasetReadyForTraining;

    /// <summary>Helper to get the parent form for AntdUI messages.</summary>
    private Form? ParentWindow => this.FindForm();

    public AnnotationPanel()
    {
        InitializeComponent();
        WireEvents();
        SetProjectLoaded(false);
    }

    // ════════════════════════════════════════════════════════════════
    // Wire events
    // ════════════════════════════════════════════════════════════════

    private void WireEvents()
    {
        // Project buttons
        btnNewProject.Click += BtnNewProject_Click;
        btnOpenProject.Click += BtnOpenProject_Click;
        btnSaveProject.Click += (s, e) => SaveProject();

        // Class management
        btnAddClass.Click += BtnAddClass_Click;
        btnRemoveClass.Click += BtnRemoveClass_Click;
        cboCurrentClass.SelectedIndexChanged += (s, e) =>
        {
            canvas.CurrentClassId = cboCurrentClass.SelectedIndex;
        };

        // Toolbar mode
        tsbSelect.Click += (s, e) => SetMode(CanvasMode.Select);
        tsbDrawRect.Click += (s, e) => SetMode(CanvasMode.DrawRect);

        // Toolbar zoom
        tsbZoomIn.Click += (s, e) => canvas.SetZoom(canvas.Zoom * 1.25f);
        tsbZoomOut.Click += (s, e) => canvas.SetZoom(canvas.Zoom / 1.25f);
        tsbFit.Click += (s, e) => canvas.FitToWindow();
        canvas.ZoomChanged += (s, zoom) => tslZoom.Text = $"{zoom:P0}";

        // Toolbar undo/redo
        tsbUndo.Click += (s, e) => PerformUndo();
        tsbRedo.Click += (s, e) => PerformRedo();

        // Toolbar navigation
        tsbPrevImage.Click += (s, e) => NavigateImage(-1);
        tsbNextImage.Click += (s, e) => NavigateImage(1);
        tsbMarkComplete.Click += (s, e) => MarkCompleteAndNext();

        // Canvas events
        canvas.AnnotationAdded += Canvas_AnnotationAdded;
        canvas.AnnotationDeleted += Canvas_AnnotationDeleted;
        canvas.AnnotationSelected += Canvas_AnnotationSelected;
        canvas.AnnotationChanged += Canvas_AnnotationChanged;

        // Image list
        lstImages.SelectedIndexChanged += LstImages_SelectedIndexChanged;
        cboImageFilter.SelectedIndexChanged += (s, e) => RefreshImageList();

        // Import
        btnImportImages.Click += BtnImportImages_Click;
        btnImportPdf.Click += BtnImportPdf_Click;

        // Annotations grid
        dgvAnnotations.SelectionChanged += DgvAnnotations_SelectionChanged;
        btnDeleteAnnotation.Click += (s, e) => DeleteSelectedAnnotation();

        // Dataset
        trkSplitRatio.ValueChanged += (s, e) =>
        {
            int v = trkSplitRatio.Value;
            lblSplitValue.Text = $"{v}% / {100 - v}%";
            if (_project != null) _project.SplitRatio = v / 100.0;
        };
        btnGenerateDataset.Click += BtnGenerateDataset_Click;
        btnGenerateAndTrain.Click += BtnGenerateAndTrain_Click;
        btnEditConfig.Click += BtnEditConfig_Click;
    }

    // ════════════════════════════════════════════════════════════════
    // Keyboard shortcuts (ProcessCmdKey)
    // ════════════════════════════════════════════════════════════════

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        switch (keyData)
        {
            case Keys.R:
                SetMode(CanvasMode.DrawRect);
                return true;
            case Keys.V:
                SetMode(CanvasMode.Select);
                return true;
            case Keys.Delete:
                DeleteSelectedAnnotation();
                return true;
            case Keys.Control | Keys.S:
                SaveProject();
                return true;
            case Keys.Control | Keys.Z:
                PerformUndo();
                return true;
            case Keys.Control | Keys.Y:
                PerformRedo();
                return true;
            case Keys.Control | Keys.D0:
            case Keys.Control | Keys.NumPad0:
                canvas.FitToWindow();
                return true;
            case Keys.Left:
                NavigateImage(-1);
                return true;
            case Keys.Right:
                NavigateImage(1);
                return true;
            case Keys.Enter:
                MarkCompleteAndNext();
                return true;
            case Keys.Control | Keys.I:
                BtnImportImages_Click(this, EventArgs.Empty);
                return true;
            case Keys.Oemplus:
            case Keys.Add:
                canvas.SetZoom(canvas.Zoom * 1.25f);
                return true;
            case Keys.OemMinus:
            case Keys.Subtract:
                canvas.SetZoom(canvas.Zoom / 1.25f);
                return true;
            case Keys.Escape:
                SetMode(CanvasMode.Select);
                return true;
            case Keys.Space:
                canvas.SetSpaceHeld(true);
                return true;
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    protected override bool ProcessKeyMessage(ref Message m)
    {
        const int WM_KEYUP = 0x0101;
        if (m.Msg == WM_KEYUP && (Keys)m.WParam == Keys.Space)
        {
            canvas.SetSpaceHeld(false);
        }
        return base.ProcessKeyMessage(ref m);
    }

    // ════════════════════════════════════════════════════════════════
    // Project management
    // ════════════════════════════════════════════════════════════════

    private void BtnNewProject_Click(object? sender, EventArgs e)
    {
        string? projectName = null;
        var inputOk = false;

        if (ParentWindow != null)
        {
            var inputControl = new AntdUI.Input
            {
                PlaceholderText = "Enter project name...",
                Dock = DockStyle.Fill,
            };
            if (AntdUI.Modal.open(ParentWindow, "New Annotation Project", inputControl) == DialogResult.OK)
            {
                projectName = inputControl.Text;
                inputOk = true;
            }
        }
        else
        {
            using var nameDlg = new InputDialog("New Annotation Project", "Project name:");
            if (nameDlg.ShowDialog() == DialogResult.OK)
            {
                projectName = nameDlg.Value;
                inputOk = true;
            }
        }

        if (!inputOk || string.IsNullOrWhiteSpace(projectName))
            return;

        using var folderDlg = new FolderBrowserDialog
        {
            Description = "Select folder to create the project in",
            UseDescriptionForTitle = true
        };
        if (folderDlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            _project = _projectService.CreateProject(projectName, folderDlg.SelectedPath);
            LoadProjectUI();
            StatusChanged?.Invoke(this, $"Created project: {_project.Name}");
            ShowMessage($"Project '{_project.Name}' created.", AntdUI.TType.Success);
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to create project: {ex.Message}", AntdUI.TType.Error);
        }
    }

    private void BtnOpenProject_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "YOLO Annotation Project|*.yolo-anno|All Files|*.*",
            Title = "Open Annotation Project"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            _project = _projectService.OpenProject(dlg.FileName);
            LoadProjectUI();

            // Resume annotation
            int resumeIdx = _project.GetResumeIndex();
            if (_project.Images.Count > 0)
                NavigateToImage(resumeIdx);

            StatusChanged?.Invoke(this, $"Opened project: {_project.Name} ({_project.Images.Count} images)");
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to open project: {ex.Message}", AntdUI.TType.Error);
        }
    }

    private void SaveProject()
    {
        if (_project == null) return;
        try
        {
            _project.LastOpenedImageIndex = _currentImageIndex;
            _project.Save();
            StatusChanged?.Invoke(this, "Project saved.");
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to save: {ex.Message}", AntdUI.TType.Error);
        }
    }

    private void LoadProjectUI()
    {
        if (_project == null) return;
        SetProjectLoaded(true);
        _cmdManager.Clear();

        // Classes
        RefreshClassList();

        // Split ratio
        trkSplitRatio.Value = Math.Clamp((int)(_project.SplitRatio * 100), 50, 95);

        // Images
        RefreshImageList();

        UpdateStatusBar();
    }

    private void SetProjectLoaded(bool loaded)
    {
        btnSaveProject.Enabled = loaded;
        btnAddClass.Enabled = loaded;
        btnRemoveClass.Enabled = loaded;
        btnImportImages.Enabled = loaded;
        btnImportPdf.Enabled = loaded;
        btnGenerateDataset.Enabled = loaded;
        btnGenerateAndTrain.Enabled = loaded;
        btnEditConfig.Enabled = loaded;
        btnDeleteAnnotation.Enabled = loaded;
        toolbarPanel.Enabled = loaded;
    }

    // ════════════════════════════════════════════════════════════════
    // Class management
    // ════════════════════════════════════════════════════════════════

    private void BtnAddClass_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        string? className = null;
        var inputOk = false;

        if (ParentWindow != null)
        {
            var inputControl = new AntdUI.Input
            {
                PlaceholderText = "Enter class name...",
                Dock = DockStyle.Fill,
            };
            if (AntdUI.Modal.open(ParentWindow, "Add Class", inputControl) == DialogResult.OK)
            {
                className = inputControl.Text;
                inputOk = true;
            }
        }
        else
        {
            using var dlg = new InputDialog("Add Class", "Class name:");
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                className = dlg.Value;
                inputOk = true;
            }
        }

        if (!inputOk || string.IsNullOrWhiteSpace(className))
            return;

        _project.Classes.Add(className.Trim());
        RefreshClassList();
    }

    private void BtnRemoveClass_Click(object? sender, EventArgs e)
    {
        if (_project == null || lstClasses.SelectedIndex < 0) return;

        var classNameToRemove = _project.Classes[lstClasses.SelectedIndex];
        if (ParentWindow != null)
        {
            if (AntdUI.Modal.open(ParentWindow, "Confirm",
                $"Remove class '{classNameToRemove}'?", AntdUI.TType.Warn) != DialogResult.OK)
                return;
        }

        _project.Classes.RemoveAt(lstClasses.SelectedIndex);
        RefreshClassList();
    }

    private void RefreshClassList()
    {
        if (_project == null) return;

        lstClasses.Items.Clear();
        cboCurrentClass.Items.Clear();

        for (int i = 0; i < _project.Classes.Count; i++)
        {
            lstClasses.Items.Add($"[{i}] {_project.Classes[i]}");
            cboCurrentClass.Items.Add($"[{i}] {_project.Classes[i]}");
        }

        if (cboCurrentClass.Items.Count > 0 && cboCurrentClass.SelectedIndex < 0)
            cboCurrentClass.SelectedIndex = 0;

        canvas.ClassNames = _project.Classes;
    }

    // ════════════════════════════════════════════════════════════════
    // Image list management
    // ════════════════════════════════════════════════════════════════

    private void RefreshImageList()
    {
        if (_project == null) return;

        lstImages.BeginUpdate();
        lstImages.Items.Clear();

        var filter = cboImageFilter.SelectedIndex >= 0
            ? cboImageFilter.Items[cboImageFilter.SelectedIndex]?.ToString() ?? "All"
            : "All";
        var images = _project.Images.Select((img, idx) => (img, idx)).ToList();

        if (filter == "Completed")
            images = images.Where(x => x.img.IsCompleted).ToList();
        else if (filter == "Incomplete")
            images = images.Where(x => !x.img.IsCompleted).ToList();

        foreach (var (img, idx) in images)
        {
            var prefix = img.IsCompleted ? "[Done] " : "";
            var item = new ListViewItem($"{prefix}{img.FileName}")
            {
                Tag = idx  // store original index
            };
            lstImages.Items.Add(item);
        }

        lstImages.EndUpdate();

        // Update progress bar
        UpdateProgressBar();
    }

    private void LstImages_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_navigating) return;
        if (lstImages.SelectedItems.Count == 0) return;
        var item = lstImages.SelectedItems[0];
        if (item.Tag is int idx)
            NavigateToImage(idx);
    }

    // ════════════════════════════════════════════════════════════════
    // Image navigation
    // ════════════════════════════════════════════════════════════════

    private void NavigateImage(int delta)
    {
        if (_project == null || _project.Images.Count == 0) return;
        int newIdx = Math.Clamp(_currentImageIndex + delta, 0, _project.Images.Count - 1);
        NavigateToImage(newIdx);
    }

    private void NavigateToImage(int index)
    {
        if (_navigating) return;
        if (_project == null || index < 0 || index >= _project.Images.Count) return;

        _navigating = true;
        try
        {
            _currentImageIndex = index;
            _project.LastOpenedImageIndex = index;
            _cmdManager.Clear();

            // Load image
            _currentImage?.Dispose();
            _currentImage = null;

            var imgInfo = _project.Images[index];
            var imgPath = _project.GetImageAbsolutePath(imgInfo);

            if (File.Exists(imgPath))
            {
                try
                {
                    using var stream = File.OpenRead(imgPath);
                    _currentImage = Image.FromStream(stream);
                }
                catch (Exception ex)
                {
                    StatusChanged?.Invoke(this, $"Failed to load image: {ex.Message}");
                }
            }

            canvas.LoadImage(_currentImage, imgInfo.Annotations);
            RefreshAnnotationGrid();
            UpdateStatusBar();
            UpdateImageListSelection();

            tslImageIndex.Text = $"{index + 1} / {_project.Images.Count}";
        }
        finally
        {
            _navigating = false;
        }
    }

    private void UpdateImageListSelection()
    {
        foreach (ListViewItem item in lstImages.Items)
        {
            if (item.Tag is int idx && idx == _currentImageIndex)
            {
                item.Selected = true;
                item.EnsureVisible();
                return;
            }
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Canvas event handlers
    // ════════════════════════════════════════════════════════════════

    private void Canvas_AnnotationAdded(object? sender, RectAnnotation annotation)
    {
        if (_project == null || _currentImageIndex < 0) return;
        var imgInfo = _project.Images[_currentImageIndex];

        var cmd = new AddAnnotationCommand(imgInfo, annotation);
        _cmdManager.Execute(cmd);

        canvas.RefreshCanvas();
        RefreshAnnotationGrid();
        UpdateStatusBar();
    }

    private void Canvas_AnnotationDeleted(object? sender, RectAnnotation annotation)
    {
        if (_project == null || _currentImageIndex < 0) return;
        var imgInfo = _project.Images[_currentImageIndex];

        var cmd = new DeleteAnnotationCommand(imgInfo, annotation);
        _cmdManager.Execute(cmd);

        RefreshAnnotationGrid();
        UpdateStatusBar();
    }

    private void Canvas_AnnotationSelected(object? sender, RectAnnotation? annotation)
    {
        if (_updatingSelection) return;
        _updatingSelection = true;
        try
        {
            if (annotation == null)
            {
                dgvAnnotations.ClearSelection();
                return;
            }

            if (_project == null || _currentImageIndex < 0) return;
            var imgInfo = _project.Images[_currentImageIndex];
            int idx = imgInfo.Annotations.IndexOf(annotation);
            if (idx >= 0 && idx < dgvAnnotations.Rows.Count)
            {
                dgvAnnotations.ClearSelection();
                dgvAnnotations.Rows[idx].Selected = true;
            }
        }
        finally
        {
            _updatingSelection = false;
        }
    }

    private void Canvas_AnnotationChanged(object? sender, RectAnnotation annotation)
    {
        RefreshAnnotationGrid();
    }

    // ════════════════════════════════════════════════════════════════
    // Annotation grid
    // ════════════════════════════════════════════════════════════════

    private void RefreshAnnotationGrid()
    {
        dgvAnnotations.Rows.Clear();
        if (_project == null || _currentImageIndex < 0) return;

        var imgInfo = _project.Images[_currentImageIndex];
        for (int i = 0; i < imgInfo.Annotations.Count; i++)
        {
            var a = imgInfo.Annotations[i];
            string className = a.ClassId < _project.Classes.Count
                ? _project.Classes[a.ClassId]
                : $"class_{a.ClassId}";
            string bbox = $"{a.CX:F3}, {a.CY:F3}, {a.W:F3}, {a.H:F3}";
            dgvAnnotations.Rows.Add(className, bbox, i.ToString());
        }
    }

    private void DgvAnnotations_SelectionChanged(object? sender, EventArgs e)
    {
        if (_updatingSelection) return;
        if (dgvAnnotations.SelectedRows.Count == 0) return;
        if (_project == null || _currentImageIndex < 0) return;

        int rowIdx = dgvAnnotations.SelectedRows[0].Index;
        var imgInfo = _project.Images[_currentImageIndex];

        if (rowIdx >= 0 && rowIdx < imgInfo.Annotations.Count)
        {
            _updatingSelection = true;
            try
            {
                canvas.SelectedAnnotation = imgInfo.Annotations[rowIdx];
            }
            finally
            {
                _updatingSelection = false;
            }
        }
    }

    private void DeleteSelectedAnnotation()
    {
        if (canvas.SelectedAnnotation != null)
        {
            canvas.DeleteSelected();
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Undo / Redo
    // ════════════════════════════════════════════════════════════════

    private void PerformUndo()
    {
        _cmdManager.Undo();
        canvas.RefreshCanvas();
        RefreshAnnotationGrid();
    }

    private void PerformRedo()
    {
        _cmdManager.Redo();
        canvas.RefreshCanvas();
        RefreshAnnotationGrid();
    }

    // ════════════════════════════════════════════════════════════════
    // Mark complete
    // ════════════════════════════════════════════════════════════════

    private void MarkCompleteAndNext()
    {
        if (_project == null || _currentImageIndex < 0) return;

        var imgInfo = _project.Images[_currentImageIndex];
        imgInfo.IsCompleted = !imgInfo.IsCompleted;

        RefreshImageList();
        UpdateStatusBar();

        // Auto-navigate to next incomplete
        if (imgInfo.IsCompleted && _currentImageIndex < _project.Images.Count - 1)
        {
            NavigateImage(1);
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Import
    // ════════════════════════════════════════════════════════════════

    private void BtnImportImages_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        using var dlg = new OpenFileDialog
        {
            Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.gif;*.tiff;*.tif;*.webp|All Files|*.*",
            Title = "Import Images",
            Multiselect = true
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            int count = _projectService.ImportImages(_project, dlg.FileNames);
            RefreshImageList();
            UpdateStatusBar();
            StatusChanged?.Invoke(this, $"Imported {count} images.");
            ShowMessage($"Imported {count} images.", AntdUI.TType.Success);

            if (_currentImageIndex < 0 && _project.Images.Count > 0)
                NavigateToImage(0);
        }
        catch (Exception ex)
        {
            ShowMessage($"Import failed: {ex.Message}", AntdUI.TType.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BtnImportPdf_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        using var dlg = new OpenFileDialog
        {
            Filter = "PDF Files|*.pdf|All Files|*.*",
            Title = "Import PDF"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            StatusChanged?.Invoke(this, "Importing PDF pages...");
            int count = _projectService.ImportPdf(_project, dlg.FileName);
            RefreshImageList();
            UpdateStatusBar();
            StatusChanged?.Invoke(this, $"Imported {count} pages from PDF.");
            ShowMessage($"Imported {count} pages from PDF.", AntdUI.TType.Success);

            if (_currentImageIndex < 0 && _project.Images.Count > 0)
                NavigateToImage(0);
        }
        catch (Exception ex)
        {
            ShowMessage($"PDF import failed: {ex.Message}", AntdUI.TType.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Dataset generation
    // ════════════════════════════════════════════════════════════════

    private string? GenerateDatasetInternal()
    {
        if (_project == null) return null;

        if (_project.Classes.Count == 0)
        {
            ShowMessage("Please add at least one class before generating.", AntdUI.TType.Warn);
            return null;
        }

        if (_project.CompletedCount == 0)
        {
            ShowMessage("No completed images found. Mark images as complete first.", AntdUI.TType.Warn);
            return null;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            double ratio = (double)trkSplitRatio.Value / 100.0;
            _project.SplitRatio = ratio;

            // Export labels first
            _projectService.ExportYoloLabels(_project);

            // Split dataset
            var (trainCount, valCount) = _datasetService.SplitDataset(_project, ratio);

            // Generate YAML config
            var yamlPath = _datasetService.GenerateYamlConfig(_project);

            SaveProject();

            StatusChanged?.Invoke(this,
                $"Dataset generated: {trainCount} train, {valCount} val. Config: {yamlPath}");

            return yamlPath;
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to generate dataset: {ex.Message}", AntdUI.TType.Error);
            return null;
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void BtnGenerateDataset_Click(object? sender, EventArgs e)
    {
        var yamlPath = GenerateDatasetInternal();
        if (yamlPath != null)
        {
            ShowMessage("Dataset generated successfully!", AntdUI.TType.Success);
        }
    }

    /// <summary>
    /// Generate dataset and immediately switch to Training tab with the YAML path pre-filled.
    /// This is the key fix for the annotation -> training workflow.
    /// </summary>
    private void BtnGenerateAndTrain_Click(object? sender, EventArgs e)
    {
        var yamlPath = GenerateDatasetInternal();
        if (yamlPath != null)
        {
            ShowMessage("Dataset generated! Switching to Training...", AntdUI.TType.Success);
            // Fire event to switch to training tab with the generated YAML path
            DatasetReadyForTraining?.Invoke(yamlPath);
        }
    }

    private void BtnEditConfig_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        var yamlPath = Path.Combine(_project.DatasetFolder, "dataset.yaml");

        string text;
        if (File.Exists(yamlPath))
        {
            text = _datasetService.LoadConfigText(yamlPath);
        }
        else
        {
            text = $"# YOLO Dataset Configuration\npath: {_project.DatasetFolder.Replace('\\', '/')}\n" +
                   $"train: images/train\nval: images/val\n" +
                   $"nc: {_project.Classes.Count}\nnames: [{string.Join(", ", _project.Classes.Select(c => $"'{c}'"))}]\n";
        }

        using var editor = new ConfigEditorForm(text);
        if (editor.ShowDialog() == DialogResult.OK)
        {
            _datasetService.SaveConfigText(yamlPath, editor.YamlText);
            StatusChanged?.Invoke(this, "Config saved.");
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Mode
    // ════════════════════════════════════════════════════════════════

    private void SetMode(CanvasMode mode)
    {
        canvas.Mode = mode;
        tsbSelect.Type = mode == CanvasMode.Select ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
        tsbDrawRect.Type = mode == CanvasMode.DrawRect ? AntdUI.TTypeMini.Primary : AntdUI.TTypeMini.Default;
    }

    // ════════════════════════════════════════════════════════════════
    // Status / Progress
    // ════════════════════════════════════════════════════════════════

    private void UpdateStatusBar()
    {
        if (_project == null)
        {
            lblStatus.Text = "No project loaded";
            return;
        }

        int total = _project.Images.Count;
        int completed = _project.CompletedCount;
        double pct = total > 0 ? completed * 100.0 / total : 0;

        string imgStatus = _currentImageIndex >= 0
            ? $" | Image {_currentImageIndex + 1}/{total}"
            : "";

        string completeMarker = "";
        if (_currentImageIndex >= 0 && _currentImageIndex < _project.Images.Count)
        {
            completeMarker = _project.Images[_currentImageIndex].IsCompleted
                ? " [DONE]" : " [Pending]";
        }

        lblStatus.Text = $"{_project.Name} | {completed}/{total} completed ({pct:F0}%){imgStatus}{completeMarker}";
        UpdateProgressBar();
    }

    private void UpdateProgressBar()
    {
        if (_project == null || _project.Images.Count == 0)
        {
            progressBar.Value = 0;
            return;
        }
        progressBar.Value = (float)_project.CompletedCount / _project.Images.Count;
    }

    // ════════════════════════════════════════════════════════════════
    // AntdUI Message helper
    // ════════════════════════════════════════════════════════════════

    private void ShowMessage(string text, AntdUI.TType type)
    {
        var form = ParentWindow;
        if (form == null) return;

        switch (type)
        {
            case AntdUI.TType.Success:
                AntdUI.Message.success(form, text, Font);
                break;
            case AntdUI.TType.Error:
                AntdUI.Message.error(form, text, Font);
                break;
            case AntdUI.TType.Warn:
                AntdUI.Message.warn(form, text, Font);
                break;
            default:
                AntdUI.Message.info(form, text, Font);
                break;
        }
    }
}

// ════════════════════════════════════════════════════════════════════
// Simple input dialog (fallback when no parent form available)
// ════════════════════════════════════════════════════════════════════

/// <summary>
/// Simple single-line input dialog.
/// </summary>
internal class InputDialog : Form
{
    private readonly TextBox txtInput;
    private readonly Button btnOk;
    private readonly Button btnCancel;

    public string Value => txtInput.Text;

    public InputDialog(string title, string prompt)
    {
        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(350, 120);

        var lbl = new Label
        {
            Text = prompt,
            Location = new Point(12, 12),
            AutoSize = true
        };

        txtInput = new TextBox
        {
            Location = new Point(12, 36),
            Size = new Size(320, 25)
        };

        btnOk = new Button
        {
            Text = "OK",
            DialogResult = DialogResult.OK,
            Location = new Point(166, 75),
            Size = new Size(80, 30)
        };

        btnCancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new Point(252, 75),
            Size = new Size(80, 30)
        };

        AcceptButton = btnOk;
        CancelButton = btnCancel;

        Controls.AddRange([lbl, txtInput, btnOk, btnCancel]);
    }
}
