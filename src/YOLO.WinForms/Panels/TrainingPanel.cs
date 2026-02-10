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
        PopulateTeacherVariants();
        WireEvents();

        // Keep tableConfig width synced with scroll container
        scrollConfig.Resize += (s, e) =>
        {
            var scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
            tableConfig.Width = scrollConfig.ClientSize.Width - scrollBarWidth;
        };
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

    /// <summary>
    /// Populate teacher variant combo with all variants for the selected version.
    /// Default to "l" (large) which is typically a good teacher model.
    /// </summary>
    private void PopulateTeacherVariants()
    {
        var version = GetSelectedText(cboVersion);
        if (string.IsNullOrEmpty(version)) return;

        var variants = ModelRegistry.GetVariants(version);
        cboTeacherVariant.Items.Clear();
        foreach (var v in variants)
            cboTeacherVariant.Items.Add(v);

        // Default teacher to "l" (large), or last variant if not available
        int defaultIdx = -1;
        for (int i = 0; i < cboTeacherVariant.Items.Count; i++)
        {
            if (cboTeacherVariant.Items[i]?.ToString() == "l")
            {
                defaultIdx = i;
                break;
            }
        }
        if (defaultIdx >= 0)
            cboTeacherVariant.SelectedIndex = defaultIdx;
        else if (cboTeacherVariant.Items.Count > 0)
            cboTeacherVariant.SelectedIndex = cboTeacherVariant.Items.Count - 1;
    }

    /// <summary>
    /// Toggle distillation panel visibility.
    /// </summary>
    private void SetDistillPanelVisible(bool visible)
    {
        panelDistill.Visible = visible;
    }

    private void WireEvents()
    {
        cboVersion.SelectedIndexChanged += (s, e) =>
        {
            UpdateVariants();
            PopulateTeacherVariants();
        };
        btnBrowseDataset.Click += BtnBrowseDataset_Click;
        btnBrowseTeacher.Click += BtnBrowseTeacher_Click;
        btnStart.Click += BtnStart_Click;
        btnStop.Click += BtnStop_Click;

        // Distillation toggle
        chkDistill.CheckedChanged += (s, e) => SetDistillPanelVisible(chkDistill.Checked);

        trainingService.LogMessage += (s, msg) => AppendLog(msg);

        // Real-time chart update
        trainingService.EpochCompleted += (s, m) =>
        {
            metricsChart?.AddEpoch(
                m.Epoch, m.BoxLoss, m.ClsLoss, m.DflLoss,
                m.Map50, m.Map5095, m.LearningRate);

            var bestMark = m.IsBest ? " *" : "";
            var distillStr = m.DistillLoss > 0 ? $"  distill={m.DistillLoss:F4}" : "";
            AppendLog($"Epoch {m.Epoch}/{m.TotalEpochs}  " +
                $"box={m.BoxLoss:F4}  cls={m.ClsLoss:F4}  dfl={m.DflLoss:F4}{distillStr}  " +
                $"mAP50={m.Map50:F4}  mAP50-95={m.Map5095:F4}  " +
                $"fitness={m.Fitness:F4}  lr={m.LearningRate:E2}{bestMark}");

            StatusChanged?.Invoke(this,
                $"Epoch {m.Epoch}/{m.TotalEpochs} | " +
                $"loss={m.BoxLoss + m.ClsLoss + m.DflLoss:F4} | " +
                $"mAP50={m.Map50:F4} | fitness={m.Fitness:F4}");
        };

        trainingService.TrainingCompleted += (s, result) =>
        {
            AppendLog(BuildTrainingReport(result));
            Invoke(() =>
            {
                SetTrainingState(false);
                ShowTrainingResultMessage(result);
            });
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

    private void BtnBrowseTeacher_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "Model Weights|*.pt;*.bin|All Files|*.*",
            Title = "选择教师模型权重文件"
        };

        if (dlg.ShowDialog() == DialogResult.OK)
            txtTeacher.Text = dlg.FileName;
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

            // Build distillation settings
            string? teacherPath = null;
            string teacherVariant = "l";
            double distillWeight = 1.0;
            double distillTemp = 20.0;
            string distillMode = "logit";

            if (chkDistill.Checked)
            {
                teacherPath = txtTeacher.Text?.Trim();
                if (string.IsNullOrWhiteSpace(teacherPath))
                {
                    ShowMessage("请选择教师模型权重文件", AntdUI.TType.Warn);
                    SetTrainingState(false);
                    return;
                }
                if (!File.Exists(teacherPath))
                {
                    ShowMessage($"教师模型文件不存在: {teacherPath}", AntdUI.TType.Warn);
                    SetTrainingState(false);
                    return;
                }
                teacherVariant = GetSelectedText(cboTeacherVariant) ?? "l";
                distillMode = GetSelectedText(cboDistillMode) ?? "logit";
                _ = double.TryParse(txtDistillWeight.Text, out distillWeight);
                _ = double.TryParse(txtDistillTemp.Text, out distillTemp);
            }

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
                SaveDir = txtSaveDir.Text,
                // Distillation
                TeacherModelPath = chkDistill.Checked ? teacherPath : null,
                TeacherVariant = teacherVariant,
                DistillWeight = distillWeight,
                DistillTemperature = distillTemp,
                DistillMode = distillMode
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

            var distillInfo = chkDistill.Checked
                ? $" (蒸馏: {distillMode}, 教师={config.TeacherVariant})"
                : "";
            AppendLog($"Model: YOLO{config.ModelVersion}{config.ModelVariant}, LR: {config.Lr0}, Optimizer: {config.Optimizer}");
            if (chkDistill.Checked)
            {
                AppendLog($"蒸馏模式: {distillMode}, 教师模型: YOLO{config.ModelVersion}{teacherVariant}");
                AppendLog($"教师权重: {teacherPath}");
                AppendLog($"蒸馏权重: {distillWeight}, 蒸馏温度: {distillTemp}");
            }
            StatusChanged?.Invoke(this,
                $"Training YOLO{config.ModelVersion}{config.ModelVariant} on {deviceStr}{distillInfo}...");

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
                string grade = GetModelGrade(result.BestFitness);
                StatusChanged?.Invoke(this,
                    $"Training complete: Grade={grade} fitness={result.BestFitness:F4} " +
                    $"mAP50={result.BestMap50:F4} mAP50-95={result.BestMap5095:F4}");
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
        btnBrowseTeacher.Enabled = !isTraining;

        if (isTraining)
        {
            btnStart.Loading = true;
        }
        else
        {
            btnStart.Loading = false;
        }
    }

    // ═════════════════════════════════════════════════════════
    // Training Quality Report
    // ═════════════════════════════════════════════════════════

    /// <summary>
    /// Build a comprehensive training quality report string.
    /// </summary>
    private static string BuildTrainingReport(YOLO.Training.TrainResult result)
    {
        var sb = new System.Text.StringBuilder();
        string grade = GetModelGrade(result.BestFitness);

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine("           TRAINING COMPLETE - MODEL REPORT");
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  Model Quality Grade:  {grade}");
        sb.AppendLine();
        sb.AppendLine("  ── Metrics ──────────────────────────────────────");
        sb.AppendLine($"  Fitness Score:        {result.BestFitness:F4}  (0.1*mAP50 + 0.9*mAP50-95)");
        sb.AppendLine($"  mAP@0.5:              {result.BestMap50:F4}  ({result.BestMap50 * 100:F1}%)");
        sb.AppendLine($"  mAP@0.5:0.95:         {result.BestMap5095:F4}  ({result.BestMap5095 * 100:F1}%)");
        sb.AppendLine();
        sb.AppendLine("  ── Training Info ────────────────────────────────");
        sb.AppendLine($"  Model:                YOLO{result.ModelVersion}{result.ModelVariant}");
        sb.AppendLine($"  Parameters:           {result.ParamCount:N0}");
        sb.AppendLine($"  Best Epoch:           {result.BestEpoch}");
        sb.AppendLine($"  Training Time:        {result.TrainingTime:hh\\:mm\\:ss}");

        // Per-class AP
        if (result.PerClassAP50.Length > 0 && result.ClassNames.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  ── Per-Class AP@0.5 ─────────────────────────────");
            int maxLen = result.ClassNames.Max(n => n.Length);
            for (int i = 0; i < result.PerClassAP50.Length && i < result.ClassNames.Length; i++)
            {
                double ap = result.PerClassAP50[i];
                string bar = new string('|', (int)(ap * 30));
                string pad = result.ClassNames[i].PadRight(maxLen);
                string apGrade = ap >= 0.75 ? "Excellent" : ap >= 0.5 ? "Good" : ap >= 0.3 ? "Fair" : "Poor";
                sb.AppendLine($"  {pad}  {ap:F4} ({ap * 100:F1}%)  {bar}  [{apGrade}]");
            }
        }

        sb.AppendLine();
        sb.AppendLine("  ── Quality Assessment ───────────────────────────");
        if (result.BestFitness >= 0.7)
            sb.AppendLine("  The model shows excellent detection performance.");
        else if (result.BestFitness >= 0.5)
            sb.AppendLine("  The model shows good performance. Consider more training data or epochs for improvement.");
        else if (result.BestFitness >= 0.3)
            sb.AppendLine("  The model shows fair performance. More training data, augmentation, or hyperparameter tuning recommended.");
        else
            sb.AppendLine("  The model needs significant improvement. Check data quality, increase epochs, or try a larger model variant.");

        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Get a letter grade based on the fitness score.
    /// </summary>
    private static string GetModelGrade(double fitness)
    {
        return fitness switch
        {
            >= 0.80 => "S  (Outstanding)",
            >= 0.70 => "A  (Excellent)",
            >= 0.60 => "B+ (Very Good)",
            >= 0.50 => "B  (Good)",
            >= 0.40 => "C+ (Above Average)",
            >= 0.30 => "C  (Average)",
            >= 0.20 => "D  (Below Average)",
            _ => "F  (Needs Improvement)"
        };
    }

    /// <summary>
    /// Show a summary message popup with the training results.
    /// </summary>
    private void ShowTrainingResultMessage(YOLO.Training.TrainResult result)
    {
        string grade = GetModelGrade(result.BestFitness);

        string summary = $"Training Complete!\n\n" +
                          $"Grade: {grade}\n" +
                          $"Fitness: {result.BestFitness:F4}\n" +
                          $"mAP@0.5: {result.BestMap50 * 100:F1}%\n" +
                          $"mAP@0.5:0.95: {result.BestMap5095 * 100:F1}%\n" +
                          $"Best Epoch: {result.BestEpoch}\n" +
                          $"Time: {result.TrainingTime:hh\\:mm\\:ss}";

        if (result.BestFitness >= 0.5)
            ShowMessage(summary, AntdUI.TType.Success);
        else
            ShowMessage(summary, AntdUI.TType.Warn);
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
