using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Data.Datasets;
using YOLO.Training;
using YOLO.WinForms.Controls;
using YOLO.WinForms.Services;

namespace YOLO.WinForms.Panels;

/// <summary>
/// Training configuration and execution panel.
/// Provides UI for model selection, hyperparameters, start/stop training,
/// real-time metrics chart, and training log output.
/// </summary>
public partial class TrainingPanel : UserControl
{
    private readonly TrainingService trainingService = new();
    private MetricsChart? metricsChart;

    public event EventHandler<string>? StatusChanged;

    /// <summary>Helper to get the parent form for AntdUI messages.</summary>
    private Form? ParentWindow => this.FindForm();

    public TrainingPanel()
    {
        InitializeComponent();
        InitializeMetricsChart();
        PopulateModelVersions();
        WireEvents();
    }

    private void InitializeMetricsChart()
    {
        metricsChart = new MetricsChart();
        metricsChart.Dock = DockStyle.Fill;
        grpChart.Controls.Add(metricsChart);
    }

    private void PopulateModelVersions()
    {
        var versions = ModelRegistry.GetVersions();
        cboVersion.Items.Clear();
        foreach (var v in versions)
            cboVersion.Items.Add(v);

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

        var variants = ModelRegistry.GetVariants(version);
        cboVariant.Items.Clear();
        foreach (var v in variants)
            cboVariant.Items.Add(v);

        if (cboVariant.Items.Count > 0)
            cboVariant.SelectedIndex = 0;
    }

    private void WireEvents()
    {
        cboVersion.SelectedIndexChanged += (s, e) => UpdateVariants();
        btnBrowseDataset.Click += BtnBrowseDataset_Click;
        btnStart.Click += BtnStart_Click;
        btnStop.Click += BtnStop_Click;

        trainingService.LogMessage += (s, msg) => AppendLog(msg);

        // Real-time chart update
        trainingService.EpochCompleted += (s, m) =>
        {
            metricsChart?.AddEpoch(
                m.Epoch, m.BoxLoss, m.ClsLoss, m.DflLoss,
                m.Map50, m.Map5095, m.LearningRate);

            var bestMark = m.IsBest ? " *" : "";
            AppendLog($"Epoch {m.Epoch}/{m.TotalEpochs}  " +
                $"box={m.BoxLoss:F4}  cls={m.ClsLoss:F4}  dfl={m.DflLoss:F4}  " +
                $"mAP50={m.Map50:F4}  mAP50-95={m.Map5095:F4}  " +
                $"fitness={m.Fitness:F4}  lr={m.LearningRate:E2}{bestMark}");

            StatusChanged?.Invoke(this,
                $"Epoch {m.Epoch}/{m.TotalEpochs} | " +
                $"loss={m.BoxLoss + m.ClsLoss + m.DflLoss:F4} | " +
                $"mAP50={m.Map50:F4} | fitness={m.Fitness:F4}");
        };

        trainingService.TrainingCompleted += (s, result) =>
        {
            AppendLog($"\nTraining completed! Best fitness: {result.BestFitness:F4}");
            Invoke(() => SetTrainingState(false));
        };
        trainingService.TrainingFailed += (s, msg) =>
        {
            AppendLog($"\nERROR: {msg}");
            Invoke(() => SetTrainingState(false));
        };
    }

    /// <summary>
    /// Set the dataset YAML path from external (e.g. annotation panel).
    /// </summary>
    public void SetDatasetPath(string yamlPath)
    {
        if (InvokeRequired)
        {
            Invoke(() => SetDatasetPath(yamlPath));
            return;
        }
        txtDataset.Text = yamlPath;
    }

    private void BtnBrowseDataset_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "YAML Files|*.yaml;*.yml|All Files|*.*",
            Title = "Select Dataset Configuration"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
            txtDataset.Text = dlg.FileName;
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtDataset.Text))
        {
            ShowMessage("Please select a dataset YAML file.", AntdUI.TType.Warn);
            return;
        }

        if (!File.Exists(txtDataset.Text))
        {
            ShowMessage($"Dataset file not found: {txtDataset.Text}", AntdUI.TType.Warn);
            return;
        }

        SetTrainingState(true);
        metricsChart?.Clear();
        txtLog.Clear();

        try
        {
            var dataConfig = DatasetConfig.Load(txtDataset.Text);
            AppendLog($"Dataset: {dataConfig.Nc} classes, Train: {dataConfig.Train}");

            var config = new TrainConfig
            {
                ModelVersion = GetSelectedText(cboVersion) ?? "v8",
                ModelVariant = GetSelectedText(cboVariant) ?? "n",
                NumClasses = dataConfig.Nc,
                Epochs = (int)numEpochs.Value,
                BatchSize = (int)numBatch.Value,
                ImgSize = (int)numImgSize.Value,
                Lr0 = double.Parse(txtLr0.Text),
                Optimizer = GetSelectedText(cboOptimizer) ?? "auto",
                CosLR = chkCosLR.Checked,
                SaveDir = txtSaveDir.Text
            };

            // Determine device
            torch.Device? trainDevice = null;
            var deviceStr = GetSelectedText(cboDevice);
            if (deviceStr != null && deviceStr.StartsWith("CUDA"))
            {
                var parts = deviceStr.Split(':');
                int deviceIdx = parts.Length > 1 && int.TryParse(parts[1], out int idx) ? idx : 0;
                trainDevice = torch.device(DeviceType.CUDA, deviceIdx);
            }
            else
            {
                trainDevice = torch.CPU;
            }

            StatusChanged?.Invoke(this,
                $"Training YOLO{config.ModelVersion}{config.ModelVariant} on {deviceStr}...");

            // Redirect console output to log
            var consoleWriter = new TextBoxConsoleWriter(this, txtLog);
            var originalOut = Console.Out;
            Console.SetOut(consoleWriter);

            var result = await trainingService.TrainAsync(
                config, dataConfig.Train, dataConfig.Val, dataConfig.Names.ToArray(),
                device: trainDevice);

            Console.SetOut(originalOut);

            if (result != null)
            {
                StatusChanged?.Invoke(this,
                    $"Training complete: fitness={result.BestFitness:F4} mAP50={result.BestMap50:F4}");
                ShowMessage("Training completed successfully!", AntdUI.TType.Success);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            StatusChanged?.Invoke(this, $"Training failed: {ex.Message}");
            ShowMessage($"Training failed: {ex.Message}", AntdUI.TType.Error);
        }
        finally
        {
            SetTrainingState(false);
        }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        trainingService.Cancel();
        AppendLog("Stopping training...");
        StatusChanged?.Invoke(this, "Stopping...");
    }

    public void StopTraining()
    {
        if (trainingService.IsRunning)
            trainingService.Cancel();
    }

    private void SetTrainingState(bool isTraining)
    {
        btnStart.Enabled = !isTraining;
        btnStop.Enabled = isTraining;
        tableConfig.Enabled = !isTraining;
        btnBrowseDataset.Enabled = !isTraining;

        if (isTraining)
        {
            btnStart.Loading = true;
        }
        else
        {
            btnStart.Loading = false;
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

    /// <summary>
    /// Helper to safely get selected text from AntdUI.Select.
    /// </summary>
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

    /// <summary>
    /// Helper to redirect Console.Write/WriteLine to the RichTextBox.
    /// </summary>
    private class TextBoxConsoleWriter : System.IO.TextWriter
    {
        private readonly Control owner;
        private readonly RichTextBox textBox;
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public TextBoxConsoleWriter(Control owner, RichTextBox textBox)
        {
            this.owner = owner;
            this.textBox = textBox;
        }

        public override void Write(char value)
        {
            AppendText(value.ToString());
        }

        public override void Write(string? value)
        {
            if (value != null) AppendText(value);
        }

        public override void WriteLine(string? value)
        {
            AppendText((value ?? "") + Environment.NewLine);
        }

        private void AppendText(string text)
        {
            if (owner.InvokeRequired)
            {
                try { owner.Invoke(() => AppendText(text)); }
                catch (ObjectDisposedException) { }
                return;
            }

            textBox.SuspendLayout();
            textBox.AppendText(text);
            textBox.SelectionStart = textBox.TextLength;
            textBox.SelectionLength = 0;
            textBox.ScrollToCaret();
            textBox.ResumeLayout();
        }
    }
}
