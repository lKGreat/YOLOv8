using System.Diagnostics;
using System.Drawing.Drawing2D;
using YOLO.Core.Abstractions;
using YOLO.MLNet.Inference;
using YOLO.Runtime;
using YOLO.Runtime.Results;

namespace YOLO.WinForms.Panels;

/// <summary>
/// Model Test panel for loading .pt / .onnx models and testing detection
/// on images with dynamic parameter tuning, batch navigation, and
/// double-buffered rendering.
/// </summary>
public partial class ModelTestPanel : UserControl
{
    // ── State ────────────────────────────────────────────────
    private YoloInfer? _yoloInfer;
    private MLNetDetector? _mlnetDetector;
    private bool _isMLNetModel;
    private string? _currentModelPath;
    private string[]? _classNames;
    private bool _modelLoaded;

    // Batch state
    private string[] _batchFiles = [];
    private int _currentIndex = -1;

    // Current results
    private Bitmap? _originalImage;
    private Bitmap? _resultImage;
    private DetectionResult[] _currentDetections = [];
    private double _lastInferenceMs;

    // Zoom & pan
    private float _zoom = 1.0f;
    private PointF _panOffset = PointF.Empty;
    private Point _lastMousePos;
    private bool _isPanning;
    private const float ZoomStep = 0.15f;
    private const float MinZoom = 0.05f;
    private const float MaxZoom = 20.0f;

    // Compare mode
    private enum CompareMode { ResultOnly, SideBySide, Overlay }
    private CompareMode _compareMode = CompareMode.ResultOnly;

    // Color palette for drawing
    private static readonly Color[] DetectionColors =
    [
        Color.FromArgb(255, 56, 56), Color.FromArgb(255, 157, 151),
        Color.FromArgb(255, 112, 31), Color.FromArgb(255, 178, 29),
        Color.FromArgb(207, 210, 49), Color.FromArgb(72, 249, 10),
        Color.FromArgb(146, 204, 23), Color.FromArgb(61, 219, 134),
        Color.FromArgb(26, 147, 52), Color.FromArgb(0, 212, 187),
        Color.FromArgb(44, 153, 168), Color.FromArgb(0, 194, 255),
        Color.FromArgb(52, 69, 147), Color.FromArgb(100, 115, 255),
        Color.FromArgb(0, 24, 236), Color.FromArgb(132, 56, 255),
        Color.FromArgb(82, 0, 133), Color.FromArgb(203, 56, 255),
        Color.FromArgb(255, 149, 200), Color.FromArgb(255, 55, 199)
    ];

    public event EventHandler<string>? StatusChanged;

    private Form? ParentWindow => this.FindForm();

    public ModelTestPanel()
    {
        // Enable double buffering on the UserControl itself
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.UserPaint |
            ControlStyles.OptimizedDoubleBuffer,
            true);

        InitializeComponent();
        PopulateVersions();
        WireEvents();
    }

    // ═════════════════════════════════════════════════════════
    // Initialization
    // ═════════════════════════════════════════════════════════

    private void PopulateVersions()
    {
        var versions = ModelRegistry.GetVersions();
        foreach (var v in versions) cboVersion.Items.Add(v);
        if (cboVersion.Items.Count > 0)
        {
            cboVersion.SelectedIndex = 0;
            UpdateVariants();
        }
    }

    private void UpdateVariants()
    {
        var version = GetSelectedText(cboVersion);
        if (string.IsNullOrEmpty(version)) return;
        cboVariant.Items.Clear();
        foreach (var v in ModelRegistry.GetVariants(version))
            cboVariant.Items.Add(v);
        if (cboVariant.Items.Count > 0)
            cboVariant.SelectedIndex = 0;
    }

    private void WireEvents()
    {
        // Model config
        cboVersion.SelectedIndexChanged += (s, e) => UpdateVariants();
        btnBrowseModel.Click += BtnBrowseModel_Click;
        btnBrowseClassNames.Click += BtnBrowseClassNames_Click;
        txtModelFile.TextChanged += (s, e) =>
        {
            UpdatePtFieldsVisibility();
            TryAutoDetectArgsYaml(txtModelFile.Text?.Trim());
        };
        btnLoadModel.Click += BtnLoadModel_Click;

        // Recent models dropdown: refresh on expand
        cboRecentModels.SelectedIndexChanged += CboRecentModels_SelectedIndexChanged;
        cboRecentModels.EnabledChanged += (s, e) => { };
        // Refresh model list when the dropdown gets focus (click to expand)
        cboRecentModels.GotFocus += (s, e) => RefreshRecentModels();

        // Inference parameter changes
        sliderConf.ValueChanged += (s, val) =>
        {
            lblConfValue.Text = (sliderConf.Value / 100f).ToString("F2");
            OnParameterChanged();
        };
        sliderIoU.ValueChanged += (s, val) =>
        {
            lblIoUValue.Text = (sliderIoU.Value / 100f).ToString("F2");
            OnParameterChanged();
        };
        cboImgSize.SelectedIndexChanged += (s, e) => OnParameterChanged();
        numMaxDet.ValueChanged += (s, val) => OnParameterChanged();

        // Action buttons
        btnSelectImage.Click += BtnSelectImage_Click;
        btnSelectFolder.Click += BtnSelectFolder_Click;
        btnRedetect.Click += async (s, e) => await RedetectCurrentAsync();
        btnSaveResult.Click += BtnSaveResult_Click;
        btnClear.Click += (s, e) => ClearDisplay();

        // Navigation
        btnPrev.Click += (s, e) => NavigateBatch(-1);
        btnNext.Click += (s, e) => NavigateBatch(1);

        // Zoom
        btnZoomIn.Click += (s, e) => SetZoom(_zoom + ZoomStep);
        btnZoomOut.Click += (s, e) => SetZoom(_zoom - ZoomStep);
        btnFit.Click += (s, e) => FitToCanvas();

        // Compare mode
        cboCompareMode.SelectedIndexChanged += (s, e) =>
        {
            _compareMode = cboCompareMode.SelectedIndex switch
            {
                1 => CompareMode.SideBySide,
                2 => CompareMode.Overlay,
                _ => CompareMode.ResultOnly
            };
            canvas.Invalidate();
        };

        // Canvas events
        canvas.Paint += Canvas_Paint;
        canvas.MouseWheel += Canvas_MouseWheel;
        canvas.MouseDown += Canvas_MouseDown;
        canvas.MouseMove += Canvas_MouseMove;
        canvas.MouseUp += Canvas_MouseUp;
        canvas.Resize += (s, e) => canvas.Invalidate();

        // Drag and drop
        canvas.DragEnter += Canvas_DragEnter;
        canvas.DragDrop += Canvas_DragDrop;
    }

    // ═════════════════════════════════════════════════════════
    // .pt vs .onnx field visibility
    // ═════════════════════════════════════════════════════════

    private void UpdatePtFieldsVisibility()
    {
        string ext = Path.GetExtension(txtModelFile.Text ?? "").ToLowerInvariant();
        bool isPt = ext is ".pt" or ".bin";
        bool isZip = ext is ".zip";

        // Show version/variant/nc rows only for .pt models (not for .onnx or .zip)
        lblVersion.Visible = isPt;
        cboVersion.Visible = isPt;
        lblVariant.Visible = isPt;
        cboVariant.Visible = isPt;
        lblNc.Visible = isPt;
        numNc.Visible = isPt;
    }

    // ═════════════════════════════════════════════════════════
    // Recent Models Scanning
    // ═════════════════════════════════════════════════════════

    private void RefreshRecentModels()
    {
        var modelFiles = new List<string>();
        var modelExtensions = new[] { ".pt", ".onnx", ".bin" };

        // Scan common directories for model files
        var searchRoots = new List<string>();

        // 1. runs/ in current working directory
        var runsDir = Path.Combine(Directory.GetCurrentDirectory(), "runs");
        if (Directory.Exists(runsDir))
            searchRoots.Add(runsDir);

        // 2. runs/ relative to application directory
        var appRunsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "runs");
        if (Directory.Exists(appRunsDir) && appRunsDir != runsDir)
            searchRoots.Add(appRunsDir);

        // 3. If a model is already loaded, scan its parent directories
        if (!string.IsNullOrEmpty(_currentModelPath))
        {
            var parentDir = Path.GetDirectoryName(Path.GetDirectoryName(_currentModelPath));
            if (parentDir != null && Directory.Exists(parentDir))
                searchRoots.Add(parentDir);
        }

        foreach (var root in searchRoots)
        {
            try
            {
                var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
                    .Where(f => modelExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .Take(20);
                modelFiles.AddRange(files);
            }
            catch { /* ignore permission errors */ }
        }

        // Deduplicate and sort by last write time
        var uniqueFiles = modelFiles
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(f => File.GetLastWriteTime(f))
            .Take(20)
            .ToList();

        cboRecentModels.Items.Clear();
        foreach (var file in uniqueFiles)
        {
            // Show relative-ish path: parent/filename
            var dir = Path.GetFileName(Path.GetDirectoryName(file));
            var display = $"{dir}/{Path.GetFileName(file)}";
            cboRecentModels.Items.Add(new RecentModelItem(display, file));
        }

        if (cboRecentModels.Items.Count == 0)
        {
            cboRecentModels.Items.Add("No models found in runs/");
        }
    }

    private void CboRecentModels_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (cboRecentModels.SelectedIndex < 0 || cboRecentModels.SelectedIndex >= cboRecentModels.Items.Count)
            return;

        var item = cboRecentModels.Items[cboRecentModels.SelectedIndex];
        if (item is RecentModelItem rmi)
        {
            txtModelFile.Text = rmi.FullPath;
        }
    }

    /// <summary>
    /// Simple wrapper to store display text and full path in the dropdown.
    /// </summary>
    private sealed class RecentModelItem(string display, string fullPath)
    {
        public string FullPath { get; } = fullPath;
        public override string ToString() => display;
    }

    // ═════════════════════════════════════════════════════════
    // Auto-detect from args.yaml
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// When a .pt model file is selected, look for args.yaml in the parent
    /// or grandparent directory to auto-populate version, variant, and num_classes.
    /// </summary>
    private void TryAutoDetectArgsYaml(string? modelPath)
    {
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
            return;

        string ext = Path.GetExtension(modelPath).ToLowerInvariant();
        if (ext is not (".pt" or ".bin"))
            return; // ONNX models don't need this

        // Look for args.yaml: typically at ../args.yaml (weights/ -> parent)
        var weightsDir = Path.GetDirectoryName(modelPath);
        if (weightsDir == null) return;

        string? argsPath = null;
        var parentDir = Path.GetDirectoryName(weightsDir);

        // Check parent (runs/train/exp/args.yaml when model is in runs/train/exp/weights/)
        if (parentDir != null)
        {
            var candidate = Path.Combine(parentDir, "args.yaml");
            if (File.Exists(candidate))
                argsPath = candidate;
        }

        // Check same directory
        if (argsPath == null)
        {
            var candidate = Path.Combine(weightsDir, "args.yaml");
            if (File.Exists(candidate))
                argsPath = candidate;
        }

        if (argsPath == null) return;

        try
        {
            var argsDict = ParseSimpleYaml(File.ReadAllLines(argsPath));

            // Auto-fill num_classes
            if (argsDict.TryGetValue("num_classes", out var ncStr) && int.TryParse(ncStr, out int nc))
            {
                numNc.Value = nc;
            }

            // Auto-fill img_size
            if (argsDict.TryGetValue("img_size", out var imgStr) && int.TryParse(imgStr, out int imgSize))
            {
                // Find matching item in cboImgSize
                for (int i = 0; i < cboImgSize.Items.Count; i++)
                {
                    if (cboImgSize.Items[i]?.ToString() == imgSize.ToString())
                    {
                        cboImgSize.SelectedIndex = i;
                        break;
                    }
                }
            }

            // Auto-fill model variant from the "model" field (e.g. "yolov8n" -> variant = "n")
            if (argsDict.TryGetValue("model", out var modelStr) && modelStr.StartsWith("yolov8"))
            {
                var variant = modelStr["yolov8".Length..].Trim();
                if (!string.IsNullOrEmpty(variant))
                {
                    for (int i = 0; i < cboVariant.Items.Count; i++)
                    {
                        if (cboVariant.Items[i]?.ToString() == variant)
                        {
                            cboVariant.SelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            StatusChanged?.Invoke(this, $"Auto-detected config from {Path.GetFileName(argsPath)}");
        }
        catch { /* ignore parse errors */ }
    }

    /// <summary>
    /// Parse a simple YAML file (key: value pairs, no nesting) into a dictionary.
    /// </summary>
    private static Dictionary<string, string> ParseSimpleYaml(string[] lines)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            int colonIdx = trimmed.IndexOf(':');
            if (colonIdx > 0 && colonIdx < trimmed.Length - 1)
            {
                var key = trimmed[..colonIdx].Trim();
                var value = trimmed[(colonIdx + 1)..].Trim();
                dict[key] = value;
            }
        }
        return dict;
    }

    // ═════════════════════════════════════════════════════════
    // Model Loading
    // ═════════════════════════════════════════════════════════

    private void BtnBrowseModel_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "All Models|*.pt;*.onnx;*.bin;*.zip|YOLO Models|*.pt;*.onnx;*.bin|ML.NET Models|*.zip|All Files|*.*",
            Title = "Select Model File"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtModelFile.Text = dlg.FileName;
    }

    private void BtnBrowseClassNames_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Text Files|*.txt;*.names|YAML Files|*.yaml;*.yml|All Files|*.*",
            Title = "Select Class Names File"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtClassNames.Text = dlg.FileName;
    }

    private void BtnLoadModel_Click(object? sender, EventArgs e)
    {
        string modelPath = txtModelFile.Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(modelPath) || !File.Exists(modelPath))
        {
            ShowMessage("Please select a valid model file.", AntdUI.TType.Warn);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            btnLoadModel.Loading = true;
            StatusChanged?.Invoke(this, "Loading model...");

            // Dispose previous
            _yoloInfer?.Dispose();
            _yoloInfer = null;
            _mlnetDetector?.Dispose();
            _mlnetDetector = null;
            _isMLNetModel = false;
            _modelLoaded = false;

            // Load class names if specified
            _classNames = LoadClassNames(txtClassNames.Text?.Trim());

            string ext = Path.GetExtension(modelPath).ToLowerInvariant();
            bool isPt = ext is ".pt" or ".bin";
            bool isZip = ext is ".zip";

            if (isZip)
            {
                // ML.NET model (.zip)
                // Try to auto-load class names from args.yaml near the model
                if (_classNames == null)
                    _classNames = MLNetDetector.TryLoadClassNames(modelPath);

                _mlnetDetector = new MLNetDetector(modelPath, _classNames);
                _isMLNetModel = true;
            }
            else
            {
                // YOLO model (.pt / .onnx / .bin)
                var options = BuildYoloOptions(isPt);
                options.ClassNames = _classNames;
                _yoloInfer = new YoloInfer(modelPath, options);
            }

            _currentModelPath = modelPath;
            _modelLoaded = true;

            // Enable buttons
            btnSelectImage.Enabled = true;
            btnSelectFolder.Enabled = true;
            btnRedetect.Enabled = false; // no image yet

            string modelName = Path.GetFileName(modelPath);
            string modelType = isZip ? "ML.NET AutoFormerV2" : (GetSelectedText(cboProvider) ?? "Auto");
            lblModelInfo.Text = $"{modelName} | {modelType}";

            StatusChanged?.Invoke(this, $"Model loaded: {modelName}");
            ShowMessage($"Model loaded: {modelName}", AntdUI.TType.Success);
        }
        catch (Exception ex)
        {
            string errorMsg = ex.Message;

            // Provide actionable guidance for common errors
            if (errorMsg.Contains("Mismatched tensor shape", StringComparison.OrdinalIgnoreCase)
                || errorMsg.Contains("shape", StringComparison.OrdinalIgnoreCase))
            {
                string ext = Path.GetExtension(modelPath).ToLowerInvariant();
                if (ext is ".pt" or ".bin")
                {
                    errorMsg = "Model architecture mismatch: The selected Version, Variant, or Classes " +
                               "do not match the weights file. Please verify these settings match " +
                               "the training configuration. If args.yaml exists next to the weights, " +
                               "it will be auto-detected when you select the file.";
                }
            }
            else if (errorMsg.Contains("not found", StringComparison.OrdinalIgnoreCase)
                     || errorMsg.Contains("could not find", StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = $"Model file not found or inaccessible: {modelPath}";
            }

            ShowMessage($"Failed to load model: {errorMsg}", AntdUI.TType.Error);
            StatusChanged?.Invoke(this, $"Load failed: {errorMsg}");
        }
        finally
        {
            Cursor = Cursors.Default;
            btnLoadModel.Loading = false;
        }
    }

    private YoloOptions BuildYoloOptions(bool isPt)
    {
        var options = new YoloOptions
        {
            Confidence = sliderConf.Value / 100f,
            IoU = sliderIoU.Value / 100f,
            ImgSize = int.TryParse(GetSelectedText(cboImgSize), out int imgSz) ? imgSz : 640,
            MaxDetections = (int)numMaxDet.Value,
            HalfPrecision = swFp16.Checked,
        };

        // Device / provider
        string provider = GetSelectedText(cboProvider) ?? "Auto";
        switch (provider)
        {
            case "CPU":
                options.Device = DeviceType.Cpu;
                options.ExecutionProvider = ExecutionProviderType.CPU;
                break;
            case "CUDA":
                options.Device = DeviceType.Gpu;
                options.ExecutionProvider = ExecutionProviderType.CUDA;
                break;
            case "TensorRT":
                options.Device = DeviceType.Gpu;
                options.ExecutionProvider = ExecutionProviderType.TensorRT;
                break;
            case "DirectML":
                options.Device = DeviceType.Gpu;
                options.ExecutionProvider = ExecutionProviderType.DirectML;
                break;
            default:
                options.Device = DeviceType.Auto;
                break;
        }

        if (isPt)
        {
            options.ModelVersion = GetSelectedText(cboVersion) ?? "v8";
            options.ModelVariant = GetSelectedText(cboVariant) ?? "n";
            options.NumClasses = (int)numNc.Value;
        }

        return options;
    }

    private static string[]? LoadClassNames(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return null;

        string ext = Path.GetExtension(path).ToLowerInvariant();

        if (ext is ".yaml" or ".yml")
        {
            // Try to parse YAML with 'names' key
            var lines = File.ReadAllLines(path);
            var names = new List<string>();
            bool inNames = false;
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("names:"))
                {
                    inNames = true;
                    // Check if names are inline: names: [a, b, c]
                    var inline = trimmed["names:".Length..].Trim();
                    if (inline.StartsWith('[') && inline.EndsWith(']'))
                    {
                        var items = inline[1..^1].Split(',')
                            .Select(s => s.Trim().Trim('\'', '"'))
                            .Where(s => s.Length > 0);
                        return items.ToArray();
                    }
                    continue;
                }
                if (inNames)
                {
                    if (trimmed.StartsWith("- "))
                        names.Add(trimmed[2..].Trim().Trim('\'', '"'));
                    else if (trimmed.Contains(':') && char.IsDigit(trimmed[0]))
                    {
                        // format: 0: person
                        var val = trimmed[(trimmed.IndexOf(':') + 1)..].Trim().Trim('\'', '"');
                        names.Add(val);
                    }
                    else if (!string.IsNullOrWhiteSpace(trimmed))
                        break; // end of names block
                }
            }
            return names.Count > 0 ? names.ToArray() : null;
        }

        // .txt or .names: one class per line
        var textNames = File.ReadAllLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();
        return textNames.Length > 0 ? textNames : null;
    }

    // ═════════════════════════════════════════════════════════
    // Inference
    // ═════════════════════════════════════════════════════════

    private void BtnSelectImage_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp;*.webp|All Files|*.*",
            Title = "Select Image"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _batchFiles = [dlg.FileName];
        _currentIndex = 0;
        _ = RunInferenceAsync(dlg.FileName);
    }

    private void BtnSelectFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select image folder" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        _batchFiles = Directory.GetFiles(dlg.SelectedPath)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToArray();

        if (_batchFiles.Length == 0)
        {
            ShowMessage("No image files found in the selected folder.", AntdUI.TType.Warn);
            return;
        }

        _currentIndex = 0;
        StatusChanged?.Invoke(this, $"Loaded {_batchFiles.Length} images");
        _ = RunInferenceAsync(_batchFiles[0]);
    }

    private async Task RunInferenceAsync(string imagePath)
    {
        if (_yoloInfer == null && _mlnetDetector == null) return;

        try
        {
            Cursor = Cursors.WaitCursor;
            StatusChanged?.Invoke(this, $"Detecting: {Path.GetFileName(imagePath)}...");

            // Load original image
            using var origBmp = new Bitmap(imagePath);
            _originalImage?.Dispose();
            _originalImage = new Bitmap(origBmp);

            // Run inference with timing
            var sw = Stopwatch.StartNew();
            DetectionResult[] detections;
            if (_isMLNetModel && _mlnetDetector != null)
            {
                float confThresh = sliderConf.Value / 100f;
                detections = await Task.Run(() => _mlnetDetector.Detect(imagePath, confThresh));
            }
            else
            {
                detections = await Task.Run(() => _yoloInfer!.Detect(imagePath));
            }
            sw.Stop();
            _lastInferenceMs = sw.Elapsed.TotalMilliseconds;

            _currentDetections = detections;

            // Build result image with drawn detections
            _resultImage?.Dispose();
            _resultImage = DrawDetections(_originalImage, detections);

            // Update UI
            UpdateNavigationButtons();
            UpdateDetectionsGrid(detections);
            UpdateStatsBar(detections);
            lblInferenceTime.Text = $"{_lastInferenceMs:F1} ms";
            btnRedetect.Enabled = true;
            btnSaveResult.Enabled = true;

            // Fit and redraw canvas
            FitToCanvas();

            StatusChanged?.Invoke(this,
                $"{Path.GetFileName(imagePath)}: {detections.Length} detections ({_lastInferenceMs:F1}ms)");
        }
        catch (Exception ex)
        {
            ShowMessage($"Inference error: {ex.Message}", AntdUI.TType.Error);
            StatusChanged?.Invoke(this, $"Error: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private async Task RedetectCurrentAsync()
    {
        if (!_modelLoaded || _batchFiles.Length == 0 || _currentIndex < 0) return;

        // Rebuild the inference engine with new params
        try
        {
            string modelPath = _currentModelPath!;
            string ext = Path.GetExtension(modelPath).ToLowerInvariant();

            if (ext is ".zip")
            {
                // ML.NET model: just re-run with current params
                // (no need to rebuild, threshold is passed per-call)
            }
            else
            {
                bool isPt = ext is ".pt" or ".bin";
                var options = BuildYoloOptions(isPt);
                options.ClassNames = _classNames;

                _yoloInfer?.Dispose();
                _yoloInfer = new YoloInfer(modelPath, options);
            }

            await RunInferenceAsync(_batchFiles[_currentIndex]);
        }
        catch (Exception ex)
        {
            ShowMessage($"Re-detect failed: {ex.Message}", AntdUI.TType.Error);
        }
    }

    private void OnParameterChanged()
    {
        if (swAutoRedetect.Checked && _modelLoaded && _batchFiles.Length > 0 && _currentIndex >= 0)
        {
            _ = RedetectCurrentAsync();
        }
    }

    // ═════════════════════════════════════════════════════════
    // Batch Navigation
    // ═════════════════════════════════════════════════════════

    private async void NavigateBatch(int delta)
    {
        if (_batchFiles.Length == 0) return;

        int newIndex = _currentIndex + delta;
        if (newIndex < 0 || newIndex >= _batchFiles.Length) return;

        _currentIndex = newIndex;
        await RunInferenceAsync(_batchFiles[_currentIndex]);
    }

    private void UpdateNavigationButtons()
    {
        btnPrev.Enabled = _currentIndex > 0;
        btnNext.Enabled = _currentIndex < _batchFiles.Length - 1;
        lblImageCounter.Text = _batchFiles.Length > 0
            ? $"{_currentIndex + 1} / {_batchFiles.Length}"
            : "0 / 0";
    }

    // ═════════════════════════════════════════════════════════
    // Drawing Detections onto Bitmap
    // ═════════════════════════════════════════════════════════

    private Bitmap DrawDetections(Bitmap source, DetectionResult[] detections)
    {
        var bmp = new Bitmap(source);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        foreach (var det in detections)
        {
            var color = DetectionColors[det.ClassId % DetectionColors.Length];
            using var pen = new Pen(color, 2.5f);
            using var brush = new SolidBrush(Color.FromArgb(180, color));

            float x = det.X1, y = det.Y1;
            float w = det.Width, h = det.Height;
            g.DrawRectangle(pen, x, y, w, h);

            string label = det.ClassName
                ?? (_classNames != null && det.ClassId < _classNames.Length
                    ? _classNames[det.ClassId]
                    : $"#{det.ClassId}");
            label = $"{label} {det.Confidence:P0}";

            using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
            var labelSize = g.MeasureString(label, font);
            float ly = Math.Max(y - labelSize.Height - 2, 0);
            g.FillRectangle(brush, x, ly, labelSize.Width + 4, labelSize.Height);
            g.DrawString(label, font, Brushes.White, x + 2, ly);
        }

        return bmp;
    }

    // ═════════════════════════════════════════════════════════
    // Canvas Painting (Double-Buffered)
    // ═════════════════════════════════════════════════════════

    private void Canvas_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.Clear(canvas.BackColor);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.InterpolationMode = InterpolationMode.HighQualityBilinear;

        if (_originalImage == null && _resultImage == null) 
        {
            // Draw placeholder text
            using var font = new Font("Segoe UI", 14F, FontStyle.Regular);
            string text = _modelLoaded
                ? "Drag & drop an image here, or click 'Select Image'"
                : "Load a model to begin testing";
            var size = g.MeasureString(text, font);
            g.DrawString(text, font, new SolidBrush(Color.FromArgb(120, 120, 120)),
                (canvas.Width - size.Width) / 2,
                (canvas.Height - size.Height) / 2);
            return;
        }

        switch (_compareMode)
        {
            case CompareMode.ResultOnly:
                DrawImageOnCanvas(g, _resultImage ?? _originalImage!, canvas.ClientRectangle);
                break;

            case CompareMode.SideBySide:
                int halfWidth = canvas.Width / 2;
                var leftRect = new Rectangle(0, 0, halfWidth - 2, canvas.Height);
                var rightRect = new Rectangle(halfWidth + 2, 0, halfWidth - 2, canvas.Height);

                if (_originalImage != null)
                    DrawImageOnCanvas(g, _originalImage, leftRect);
                if (_resultImage != null)
                    DrawImageOnCanvas(g, _resultImage, rightRect);

                // Divider line
                using (var divPen = new Pen(Color.FromArgb(80, 80, 80), 2))
                    g.DrawLine(divPen, halfWidth, 0, halfWidth, canvas.Height);

                // Labels
                using (var labelFont = new Font("Segoe UI", 9F, FontStyle.Bold))
                {
                    g.DrawString("Original", labelFont, new SolidBrush(Color.FromArgb(200, 200, 200)), 8, 8);
                    g.DrawString("Result", labelFont, new SolidBrush(Color.FromArgb(200, 200, 200)), halfWidth + 8, 8);
                }
                break;

            case CompareMode.Overlay:
                // Draw original first, then overlay detections
                if (_originalImage != null)
                    DrawImageOnCanvas(g, _originalImage, canvas.ClientRectangle);

                // Draw detection boxes directly on canvas using zoom/pan transform
                if (_originalImage != null && _currentDetections.Length > 0)
                {
                    var imgRect = GetFittedRect(_originalImage, canvas.ClientRectangle);
                    float scaleX = imgRect.Width / (float)_originalImage.Width;
                    float scaleY = imgRect.Height / (float)_originalImage.Height;

                    foreach (var det in _currentDetections)
                    {
                        var color = DetectionColors[det.ClassId % DetectionColors.Length];
                        using var pen = new Pen(color, 2.5f);
                        using var brush = new SolidBrush(Color.FromArgb(140, color));

                        float dx = imgRect.X + det.X1 * scaleX;
                        float dy = imgRect.Y + det.Y1 * scaleY;
                        float dw = det.Width * scaleX;
                        float dh = det.Height * scaleY;
                        g.DrawRectangle(pen, dx, dy, dw, dh);

                        string label = det.ClassName
                            ?? (_classNames != null && det.ClassId < _classNames.Length
                                ? _classNames[det.ClassId]
                                : $"#{det.ClassId}");
                        label = $"{label} {det.Confidence:P0}";

                        using var font = new Font("Segoe UI", 9f, FontStyle.Bold);
                        var labelSize = g.MeasureString(label, font);
                        float ly = Math.Max(dy - labelSize.Height - 2, imgRect.Y);
                        g.FillRectangle(brush, dx, ly, labelSize.Width + 4, labelSize.Height);
                        g.DrawString(label, font, Brushes.White, dx + 2, ly);
                    }
                }
                break;
        }
    }

    private void DrawImageOnCanvas(Graphics g, Bitmap image, Rectangle bounds)
    {
        var destRect = GetFittedRect(image, bounds);
        g.DrawImage(image, destRect);
    }

    private RectangleF GetFittedRect(Bitmap image, Rectangle bounds)
    {
        float imgW = image.Width * _zoom;
        float imgH = image.Height * _zoom;

        // Fit within bounds maintaining aspect ratio
        float scale = Math.Min(bounds.Width / imgW, bounds.Height / imgH);
        float drawW = imgW * scale;
        float drawH = imgH * scale;

        float x = bounds.X + (bounds.Width - drawW) / 2 + _panOffset.X;
        float y = bounds.Y + (bounds.Height - drawH) / 2 + _panOffset.Y;

        return new RectangleF(x, y, drawW, drawH);
    }

    // ═════════════════════════════════════════════════════════
    // Zoom & Pan
    // ═════════════════════════════════════════════════════════

    private void SetZoom(float newZoom)
    {
        _zoom = Math.Clamp(newZoom, MinZoom, MaxZoom);
        canvas.Invalidate();
    }

    private void FitToCanvas()
    {
        _zoom = 1.0f;
        _panOffset = PointF.Empty;
        canvas.Invalidate();
    }

    private void Canvas_MouseWheel(object? sender, MouseEventArgs e)
    {
        float delta = e.Delta > 0 ? ZoomStep : -ZoomStep;
        SetZoom(_zoom + delta);
    }

    private void Canvas_MouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle || e.Button == MouseButtons.Left && ModifierKeys.HasFlag(Keys.Space))
        {
            _isPanning = true;
            _lastMousePos = e.Location;
            canvas.Cursor = Cursors.Hand;
        }
    }

    private void Canvas_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            _panOffset = new PointF(
                _panOffset.X + (e.X - _lastMousePos.X),
                _panOffset.Y + (e.Y - _lastMousePos.Y));
            _lastMousePos = e.Location;
            canvas.Invalidate();
        }
    }

    private void Canvas_MouseUp(object? sender, MouseEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            canvas.Cursor = Cursors.Default;
        }
    }

    // ═════════════════════════════════════════════════════════
    // Drag & Drop
    // ═════════════════════════════════════════════════════════

    private void Canvas_DragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
            e.Effect = DragDropEffects.Copy;
        else
            e.Effect = DragDropEffects.None;
    }

    private async void Canvas_DragDrop(object? sender, DragEventArgs e)
    {
        if (!_modelLoaded) return;

        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files == null || files.Length == 0) return;

        var imageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".webp" };
        var imageFiles = files
            .Where(f => imageExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
            .OrderBy(f => f)
            .ToArray();

        if (imageFiles.Length == 0)
        {
            ShowMessage("No valid image files found in the drop.", AntdUI.TType.Warn);
            return;
        }

        _batchFiles = imageFiles;
        _currentIndex = 0;
        await RunInferenceAsync(_batchFiles[0]);
    }

    // ═════════════════════════════════════════════════════════
    // Detections Grid & Stats
    // ═════════════════════════════════════════════════════════

    private void UpdateDetectionsGrid(DetectionResult[] detections)
    {
        dgvDetections.Rows.Clear();
        foreach (var det in detections)
        {
            string className = det.ClassName
                ?? (_classNames != null && det.ClassId < _classNames.Length
                    ? _classNames[det.ClassId]
                    : $"#{det.ClassId}");

            dgvDetections.Rows.Add(
                className,
                $"{det.Confidence:P1}",
                $"({det.X1:F0}, {det.Y1:F0}, {det.X2:F0}, {det.Y2:F0})",
                $"{det.Area:F0}"
            );
        }
    }

    private void UpdateStatsBar(DetectionResult[] detections)
    {
        lblTotalDetections.Text = $"Detections: {detections.Length}";

        // Class summary
        if (detections.Length > 0)
        {
            var classCounts = detections
                .GroupBy(d => d.ClassName
                    ?? (_classNames != null && d.ClassId < _classNames.Length
                        ? _classNames[d.ClassId]
                        : $"#{d.ClassId}"))
                .Select(g => $"{g.Key}: {g.Count()}")
                .Take(8);
            lblClassSummary.Text = string.Join(" | ", classCounts);
        }
        else
        {
            lblClassSummary.Text = "";
        }
    }

    // ═════════════════════════════════════════════════════════
    // Save Result
    // ═════════════════════════════════════════════════════════

    private void BtnSaveResult_Click(object? sender, EventArgs e)
    {
        if (_resultImage == null) return;

        using var dlg = new SaveFileDialog
        {
            Filter = "PNG|*.png|JPEG|*.jpg|BMP|*.bmp",
            DefaultExt = "png",
            FileName = _batchFiles.Length > 0
                ? Path.GetFileNameWithoutExtension(_batchFiles[_currentIndex]) + "_result"
                : "result"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var format = Path.GetExtension(dlg.FileName).ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => System.Drawing.Imaging.ImageFormat.Jpeg,
                ".bmp" => System.Drawing.Imaging.ImageFormat.Bmp,
                _ => System.Drawing.Imaging.ImageFormat.Png
            };
            _resultImage.Save(dlg.FileName, format);
            ShowMessage("Result saved.", AntdUI.TType.Success);
        }
    }

    // ═════════════════════════════════════════════════════════
    // Clear
    // ═════════════════════════════════════════════════════════

    private void ClearDisplay()
    {
        _originalImage?.Dispose();
        _originalImage = null;
        _resultImage?.Dispose();
        _resultImage = null;
        _currentDetections = [];
        _batchFiles = [];
        _currentIndex = -1;

        dgvDetections.Rows.Clear();
        lblTotalDetections.Text = "Detections: 0";
        lblClassSummary.Text = "";
        lblInferenceTime.Text = "";
        lblImageCounter.Text = "0 / 0";
        btnPrev.Enabled = false;
        btnNext.Enabled = false;
        btnRedetect.Enabled = false;
        btnSaveResult.Enabled = false;

        FitToCanvas();
    }

    // ═════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════

    private static string? GetSelectedText(AntdUI.Select select)
    {
        if (select.SelectedIndex < 0 || select.SelectedIndex >= select.Items.Count)
            return null;
        return select.Items[select.SelectedIndex]?.ToString();
    }

    private void ShowMessage(string text, AntdUI.TType type)
    {
        var form = ParentWindow;
        if (form == null) return;

        switch (type)
        {
            case AntdUI.TType.Success: AntdUI.Message.success(form, text, Font); break;
            case AntdUI.TType.Error: AntdUI.Message.error(form, text, Font); break;
            case AntdUI.TType.Warn: AntdUI.Message.warn(form, text, Font); break;
            default: AntdUI.Message.info(form, text, Font); break;
        }
    }

    // ═════════════════════════════════════════════════════════
    // Dispose
    // ═════════════════════════════════════════════════════════

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _yoloInfer?.Dispose();
            _yoloInfer = null;
            _mlnetDetector?.Dispose();
            _mlnetDetector = null;
            _originalImage?.Dispose();
            _originalImage = null;
            _resultImage?.Dispose();
            _resultImage = null;
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
