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
        if (cboVersion.SelectedItem is not string version) return;

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
        // Validate
        if (string.IsNullOrWhiteSpace(txtDataset.Text))
        {
            MessageBox.Show("Please select a dataset YAML file.", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!File.Exists(txtDataset.Text))
        {
            MessageBox.Show($"Dataset file not found: {txtDataset.Text}", "Validation",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        SetTrainingState(true);
        metricsChart?.Clear();
        txtLog.Clear();

        try
        {
            // Load dataset config
            var dataConfig = DatasetConfig.Load(txtDataset.Text);
            AppendLog($"Dataset: {dataConfig.Nc} classes, Train: {dataConfig.Train}");

            var config = new TrainConfig
            {
                ModelVersion = cboVersion.SelectedItem?.ToString() ?? "v8",
                ModelVariant = cboVariant.SelectedItem?.ToString() ?? "n",
                NumClasses = dataConfig.Nc,
                Epochs = (int)numEpochs.Value,
                BatchSize = (int)numBatch.Value,
                ImgSize = (int)numImgSize.Value,
                Lr0 = double.Parse(txtLr0.Text),
                Optimizer = cboOptimizer.SelectedItem?.ToString() ?? "auto",
                CosLR = chkCosLR.Checked,
                SaveDir = txtSaveDir.Text
            };

            StatusChanged?.Invoke(this,
                $"Training YOLO{config.ModelVersion}{config.ModelVariant}...");

            // Redirect console output to log
            var consoleWriter = new TextBoxConsoleWriter(this, txtLog);
            var originalOut = Console.Out;
            Console.SetOut(consoleWriter);

            var result = await trainingService.TrainAsync(
                config, dataConfig.Train, dataConfig.Val, dataConfig.Names.ToArray());

            Console.SetOut(originalOut);

            if (result != null)
            {
                StatusChanged?.Invoke(this,
                    $"Training complete: fitness={result.BestFitness:F4} mAP50={result.BestMap50:F4}");
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            StatusChanged?.Invoke(this, $"Training failed: {ex.Message}");
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
        grpConfig.Enabled = !isTraining;
    }

    private void AppendLog(string message)
    {
        if (InvokeRequired)
        {
            Invoke(() => AppendLog(message));
            return;
        }

        txtLog.AppendText(message + Environment.NewLine);
        txtLog.ScrollToCaret();
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

            textBox.AppendText(text);
            textBox.ScrollToCaret();
        }
    }
}
