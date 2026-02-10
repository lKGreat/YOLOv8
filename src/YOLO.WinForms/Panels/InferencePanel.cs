using System.Drawing.Drawing2D;
using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Core.Utils;
using YOLO.Inference;

namespace YOLO.WinForms.Panels;

/// <summary>
/// Inference panel for running object detection on images.
/// Supports single image and batch folder detection with visualization.
/// </summary>
public partial class InferencePanel : UserControl
{
    private YOLOModel? loadedModel;
    private Predictor? predictor;
    private string[]? classNames;

    public event EventHandler<string>? StatusChanged;

    private Form? ParentWindow => this.FindForm();

    public InferencePanel()
    {
        InitializeComponent();
        PopulateVersions();
        WireEvents();
    }

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
        cboVersion.SelectedIndexChanged += (s, e) => UpdateVariants();
        btnBrowseWeights.Click += (s, e) =>
        {
            using var dlg = new OpenFileDialog { Filter = "Model Weights|*.pt;*.bin|All Files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtWeights.Text = dlg.FileName;
        };
        btnLoadModel.Click += BtnLoadModel_Click;
        btnSelectImage.Click += BtnSelectImage_Click;
        btnSelectFolder.Click += BtnSelectFolder_Click;
    }

    private void BtnLoadModel_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtWeights.Text) || !File.Exists(txtWeights.Text))
        {
            ShowMessage("Please select a valid weights file.", AntdUI.TType.Warn);
            return;
        }

        try
        {
            Cursor = Cursors.WaitCursor;
            btnLoadModel.Loading = true;
            StatusChanged?.Invoke(this, "Loading model...");

            string version = GetSelectedText(cboVersion) ?? "v8";
            string variant = GetSelectedText(cboVariant) ?? "n";
            int nc = (int)numNc.Value;

            loadedModel?.Dispose();
            loadedModel = null;
            predictor = null;

            loadedModel = ModelRegistry.Create(version, nc, variant);
            WeightLoader.SmartLoad(loadedModel, txtWeights.Text);
            loadedModel.eval();

            float conf = float.Parse(txtConfThresh.Text);
            float iou = float.Parse(txtIouThresh.Text);

            predictor = new Predictor(loadedModel, (int)numImgSize.Value, conf, iou);
            classNames = Enumerable.Range(0, nc).Select(i => $"class_{i}").ToArray();

            btnSelectImage.Enabled = true;
            btnSelectFolder.Enabled = true;
            StatusChanged?.Invoke(this,
                $"Model loaded: YOLO{version}{variant} ({nc} classes)");
            ShowMessage($"Model loaded: YOLO{version}{variant}", AntdUI.TType.Success);
        }
        catch (Exception ex)
        {
            ShowMessage($"Failed to load model: {ex.Message}", AntdUI.TType.Error);
            StatusChanged?.Invoke(this, $"Load failed: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
            btnLoadModel.Loading = false;
        }
    }

    private async void BtnSelectImage_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Images|*.jpg;*.jpeg;*.png;*.bmp|All Files|*.*",
            Title = "Select Image"
        };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        await RunInferenceAsync(dlg.FileName);
    }

    private async void BtnSelectFolder_Click(object? sender, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "Select image folder" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        var extensions = new[] { ".jpg", ".jpeg", ".png", ".bmp" };
        var files = Directory.GetFiles(dlg.SelectedPath)
            .Where(f => extensions.Contains(Path.GetExtension(f).ToLower()))
            .OrderBy(f => f)
            .ToArray();

        StatusChanged?.Invoke(this, $"Processing {files.Length} images...");

        for (int i = 0; i < files.Length; i++)
        {
            StatusChanged?.Invoke(this, $"Image {i + 1}/{files.Length}: {Path.GetFileName(files[i])}");
            await RunInferenceAsync(files[i]);
            await Task.Delay(100);
        }

        StatusChanged?.Invoke(this, $"Batch complete: {files.Length} images processed.");
    }

    private async Task RunInferenceAsync(string imagePath)
    {
        if (predictor == null || classNames == null) return;

        try
        {
            Cursor = Cursors.WaitCursor;

            using var originalBmp = new Bitmap(imagePath);
            picOriginal.Image?.Dispose();
            picOriginal.Image = new Bitmap(originalBmp);

            var detections = await Task.Run(() =>
            {
                using var scope = torch.NewDisposeScope();
                return predictor.Predict(imagePath);
            });

            var resultBmp = DrawDetections(originalBmp, detections);
            picResult.Image?.Dispose();
            picResult.Image = resultBmp;

            dgvDetections.Rows.Clear();
            foreach (var det in detections)
            {
                string className = det.ClassId < classNames.Length
                    ? classNames[det.ClassId] : $"id={det.ClassId}";
                dgvDetections.Rows.Add(
                    className,
                    $"{det.Confidence:P1}",
                    $"({det.X1:F0}, {det.Y1:F0}, {det.X2:F0}, {det.Y2:F0})"
                );
            }

            StatusChanged?.Invoke(this,
                $"{Path.GetFileName(imagePath)}: {detections.Count} detections");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Inference error: {ex.Message}");
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private Bitmap DrawDetections(Bitmap source, IReadOnlyList<Detection> detections)
    {
        var bmp = new Bitmap(source);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var colors = new[]
        {
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
        };

        foreach (var det in detections)
        {
            var color = colors[det.ClassId % colors.Length];
            using var pen = new Pen(color, 2.5f);
            using var brush = new SolidBrush(Color.FromArgb(180, color));

            float x = det.X1, y = det.Y1;
            float w = det.X2 - det.X1, h = det.Y2 - det.Y1;
            g.DrawRectangle(pen, x, y, w, h);

            string label = det.ClassId < classNames!.Length
                ? $"{classNames[det.ClassId]} {det.Confidence:P0}"
                : $"#{det.ClassId} {det.Confidence:P0}";

            using var font = new Font("Segoe UI", 10f, FontStyle.Bold);
            var labelSize = g.MeasureString(label, font);
            float ly = Math.Max(y - labelSize.Height - 2, 0);
            g.FillRectangle(brush, x, ly, labelSize.Width + 4, labelSize.Height);
            g.DrawString(label, font, Brushes.White, x + 2, ly);
        }

        return bmp;
    }

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

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            loadedModel?.Dispose();
            loadedModel = null;
            predictor = null;
            components?.Dispose();
        }
        base.Dispose(disposing);
    }
}
