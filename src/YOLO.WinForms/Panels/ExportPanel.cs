using YOLO.Core.Abstractions;
using YOLO.Core.Utils;
using YOLO.Export;
using YOLO.WinForms.Services;

namespace YOLO.WinForms.Panels;

/// <summary>
/// Export panel for converting models to ONNX / TorchScript format.
/// </summary>
public partial class ExportPanel : UserControl
{
    private readonly ExportService exportService = new();

    public event EventHandler<string>? StatusChanged;

    private Form? ParentWindow => this.FindForm();

    public ExportPanel()
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
            using var dlg = new OpenFileDialog { Filter = "Model Weights|*.pt|All Files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtWeights.Text = dlg.FileName;
        };
        btnBrowseOutput.Click += (s, e) =>
        {
            using var dlg = new SaveFileDialog { Filter = "ONNX|*.onnx|TorchScript|*.pt|All|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK) txtOutput.Text = dlg.FileName;
        };
        btnExport.Click += BtnExport_Click;

        exportService.LogMessage += (s, msg) => AppendLog(msg);
    }

    private async void BtnExport_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtWeights.Text) || !File.Exists(txtWeights.Text))
        {
            ShowMessage("Please select a valid weights file.", AntdUI.TType.Warn);
            return;
        }

        btnExport.Enabled = false;
        btnExport.Loading = true;
        txtLog.Clear();

        try
        {
            string version = GetSelectedText(cboVersion) ?? "v8";
            string variant = GetSelectedText(cboVariant) ?? "n";
            int nc = (int)numNc.Value;

            AppendLog($"Loading model: YOLO{version}{variant} (nc={nc})...");
            StatusChanged?.Invoke(this, "Loading model...");

            using var model = ModelRegistry.Create(version, nc, variant);
            WeightLoader.SmartLoad(model, txtWeights.Text);

            string format = GetSelectedText(cboFormat)?.ToLower() switch
            {
                "onnx" => "onnx",
                "torchscript" => "torchscript",
                _ => "onnx"
            };

            string outputPath = string.IsNullOrWhiteSpace(txtOutput.Text)
                ? Path.ChangeExtension(txtWeights.Text, format == "onnx" ? ".onnx" : ".torchscript.pt")
                : txtOutput.Text;

            var config = new ExportConfig
            {
                Format = format,
                OutputPath = outputPath,
                ImgSize = (int)numImgSize.Value,
                Half = chkHalf.Checked,
                Simplify = chkSimplify.Checked,
                Dynamic = chkDynamic.Checked
            };

            var progress = new Progress<ExportProgress>(p =>
                AppendLog($"[{p.PercentComplete}%] {p.Stage}: {p.Message}"));

            StatusChanged?.Invoke(this, $"Exporting to {format}...");
            var result = await exportService.ExportAsync(model, config, progress);

            if (result?.Success == true)
            {
                StatusChanged?.Invoke(this,
                    $"Export complete: {result.FileSizeBytes / 1024.0 / 1024.0:F1} MB");
                ShowMessage("Export completed successfully!", AntdUI.TType.Success);
            }
            else
            {
                StatusChanged?.Invoke(this, "Export failed.");
                ShowMessage("Export failed.", AntdUI.TType.Error);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            StatusChanged?.Invoke(this, $"Export error: {ex.Message}");
            ShowMessage($"Export error: {ex.Message}", AntdUI.TType.Error);
        }
        finally
        {
            btnExport.Enabled = true;
            btnExport.Loading = false;
        }
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }
        txtLog.SuspendLayout();
        txtLog.AppendText(message + Environment.NewLine);
        txtLog.SelectionStart = txtLog.TextLength;
        txtLog.SelectionLength = 0;
        txtLog.ScrollToCaret();
        txtLog.ResumeLayout();
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
}
