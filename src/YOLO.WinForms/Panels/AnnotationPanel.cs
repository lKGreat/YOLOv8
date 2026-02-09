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

    /// <summary>Fired when status text should be updated in the main form.</summary>
    public event EventHandler<string>? StatusChanged;

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
        // Detect Space key up
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
        using var nameDlg = new InputDialog("New Annotation Project", "Project name:");
        if (nameDlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(nameDlg.Value))
            return;

        using var folderDlg = new FolderBrowserDialog
        {
            Description = "Select folder to create the project in",
            UseDescriptionForTitle = true
        };
        if (folderDlg.ShowDialog() != DialogResult.OK) return;

        try
        {
            _project = _projectService.CreateProject(nameDlg.Value, folderDlg.SelectedPath);
            LoadProjectUI();
            StatusChanged?.Invoke(this, $"Created project: {_project.Name}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to create project: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show($"Failed to open project: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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
        grpClasses.Enabled = loaded;
        grpImages.Enabled = loaded;
        grpAnnotations.Enabled = loaded;
        grpDataset.Enabled = loaded;
        toolStrip.Enabled = loaded;
        btnImportImages.Enabled = loaded;
        btnImportPdf.Enabled = loaded;
    }

    // ════════════════════════════════════════════════════════════════
    // Class management
    // ════════════════════════════════════════════════════════════════

    private void BtnAddClass_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        using var dlg = new InputDialog("Add Class", "Class name:");
        if (dlg.ShowDialog() != DialogResult.OK || string.IsNullOrWhiteSpace(dlg.Value))
            return;

        _project.Classes.Add(dlg.Value.Trim());
        RefreshClassList();
    }

    private void BtnRemoveClass_Click(object? sender, EventArgs e)
    {
        if (_project == null || lstClasses.SelectedIndex < 0) return;

        var className = _project.Classes[lstClasses.SelectedIndex];
        if (MessageBox.Show($"Remove class '{className}'?", "Confirm",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

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

        var filter = cboImageFilter.SelectedItem?.ToString() ?? "All";
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
    }

    private void LstImages_SelectedIndexChanged(object? sender, EventArgs e)
    {
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
        if (_project == null || index < 0 || index >= _project.Images.Count) return;

        // Save current state
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
                // Load without locking the file
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

    private void UpdateImageListSelection()
    {
        // Select the current image in the list view
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
        // Highlight in grid
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
        if (dgvAnnotations.SelectedRows.Count == 0) return;
        if (_project == null || _currentImageIndex < 0) return;

        int rowIdx = dgvAnnotations.SelectedRows[0].Index;
        var imgInfo = _project.Images[_currentImageIndex];

        if (rowIdx >= 0 && rowIdx < imgInfo.Annotations.Count)
        {
            canvas.SelectedAnnotation = imgInfo.Annotations[rowIdx];
        }
    }

    private void DeleteSelectedAnnotation()
    {
        if (canvas.SelectedAnnotation != null)
        {
            var annotation = canvas.SelectedAnnotation;
            canvas.DeleteSelected();
            Canvas_AnnotationDeleted(this, annotation);
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

            if (_currentImageIndex < 0 && _project.Images.Count > 0)
                NavigateToImage(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Import failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            if (_currentImageIndex < 0 && _project.Images.Count > 0)
                NavigateToImage(0);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"PDF import failed: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    // ════════════════════════════════════════════════════════════════
    // Dataset generation
    // ════════════════════════════════════════════════════════════════

    private void BtnGenerateDataset_Click(object? sender, EventArgs e)
    {
        if (_project == null) return;

        if (_project.Classes.Count == 0)
        {
            MessageBox.Show("Please add at least one class before generating the dataset.",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_project.CompletedCount == 0)
        {
            MessageBox.Show("No completed images found. Mark some images as complete first.",
                "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            double ratio = trkSplitRatio.Value / 100.0;
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

            MessageBox.Show(
                $"Dataset generated successfully!\n\n" +
                $"Training images: {trainCount}\n" +
                $"Validation images: {valCount}\n" +
                $"Config: {yamlPath}",
                "Dataset Generated", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to generate dataset: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
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
            // Generate a preview
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
        tsbSelect.Checked = mode == CanvasMode.Select;
        tsbDrawRect.Checked = mode == CanvasMode.DrawRect;
    }

    // ════════════════════════════════════════════════════════════════
    // Status
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
    }
}

// ════════════════════════════════════════════════════════════════════
// Simple input dialog (reusable)
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
