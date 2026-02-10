using YOLO.Data.Datasets;
using YOLO.MLNet.Evaluation;
using YOLO.MLNet.Training;
using YOLO.WinForms.Controls;
using YOLO.WinForms.Services;

namespace YOLO.WinForms.Panels;

/// <summary>
/// ML.NET 目标检测训练面板。
/// 提供 AutoFormerV2 训练配置、实时指标图表、训练日志输出，
/// 以及验证/测试/蒸馏功能。
/// </summary>
public partial class MLNetTrainingPanel : UserControl
{
    private readonly MLNetTrainingService _service = new();
    private MetricsChart? _metricsChart;

    public event EventHandler<string>? StatusChanged;

    private Form? ParentWindow => this.FindForm();

    public MLNetTrainingPanel()
    {
        InitializeComponent();
        InitializeMetricsChart();
        WireEvents();

        scrollConfig.Resize += (s, e) =>
        {
            var scrollBarWidth = SystemInformation.VerticalScrollBarWidth;
            tableConfig.Width = scrollConfig.ClientSize.Width - scrollBarWidth;
        };
    }

    private void InitializeMetricsChart()
    {
        _metricsChart = new MetricsChart();
        _metricsChart.Dock = DockStyle.Fill;
        grpChart.Controls.Add(_metricsChart);
    }

    private void WireEvents()
    {
        btnBrowseDataset.Click += BtnBrowseDataset_Click;
        btnBrowseTeacher.Click += BtnBrowseTeacher_Click;
        btnStart.Click += BtnStart_Click;
        btnStop.Click += BtnStop_Click;
        btnValidate.Click += BtnValidate_Click;
        btnTest.Click += BtnTest_Click;

        chkDistill.CheckedChanged += (s, e) => panelDistill.Visible = chkDistill.Checked;

        _service.LogMessage += (s, msg) => AppendLog(msg);

        _service.LiveProgress += (s, p) =>
        {
            // 不刷屏到日志，只用于状态栏实时显示（epoch/step 尽力解析）
            var stepStr = p.Step is > 0
                ? (p.TotalSteps is > 0 ? $"{p.Step}/{p.TotalSteps}" : $"{p.Step}")
                : "未知";

            StatusChanged?.Invoke(this,
                $"{p.Stage} | Epoch {p.CompletedEpochs}/{p.TotalEpochs} | step {stepStr} | {p.Elapsed:hh\\:mm\\:ss}");
        };

        _service.EpochCompleted += (s, m) =>
        {
            _metricsChart?.AddEpoch(
                m.Epoch, m.Loss, 0, 0,
                m.Map50, m.Map5095, m.LearningRate);

            var distillStr = m.DistillLoss > 0 ? $"  distill={m.DistillLoss:F4}" : "";
            var bestMark = m.IsBest ? " *" : "";
            AppendLog($"Epoch {m.Epoch}/{m.TotalEpochs}  " +
                $"loss={m.Loss:F4}{distillStr}  " +
                $"mAP50={m.Map50:F4}  mAP50-95={m.Map5095:F4}  " +
                $"lr={m.LearningRate:E2}{bestMark}");

            StatusChanged?.Invoke(this,
                $"Epoch {m.Epoch}/{m.TotalEpochs} | " +
                $"mAP50={m.Map50:F4}");
        };

        _service.TrainingCompleted += (s, result) =>
        {
            AppendLog(BuildTrainingReport(result));
            Invoke(() =>
            {
                SetTrainingState(false);
                ShowTrainingResultMessage(result);
            });
        };

        _service.TrainingFailed += (s, msg) =>
        {
            AppendLog($"\nERROR: {msg}");
            Invoke(() => SetTrainingState(false));
        };

        _service.EvaluationCompleted += (s, evalResult) =>
        {
            var report = MLNetEvaluator.GenerateReport(evalResult);
            AppendLog(report);
            Invoke(() =>
            {
                btnValidate.Loading = false;
                btnTest.Loading = false;
            });
        };
    }

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
            Title = "选择数据集配置文件"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtDataset.Text = dlg.FileName;
    }

    private void BtnBrowseTeacher_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog
        {
            Filter = "ML.NET Models|*.zip|All Files|*.*",
            Title = "选择教师模型 (.zip)"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            txtTeacher.Text = dlg.FileName;
    }

    private async void BtnStart_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtDataset.Text))
        {
            ShowMessage("请选择数据集 YAML 文件。", AntdUI.TType.Warn);
            return;
        }

        if (!File.Exists(txtDataset.Text))
        {
            ShowMessage($"数据集文件不存在: {txtDataset.Text}", AntdUI.TType.Warn);
            return;
        }

        SetTrainingState(true);
        _metricsChart?.Clear();
        txtLog.Clear();

        try
        {
            var dataConfig = DatasetConfig.Load(txtDataset.Text);
            AppendLog($"数据集: {dataConfig.Nc} 类, 训练: {dataConfig.Train}");

            // 构建蒸馏设置
            bool useDistill = chkDistill.Checked;
            string? teacherPath = null;
            int distillEpochs = 10;
            double distillLr = 0.001;
            double distillTemp = 4.0;
            double distillWeight = 0.5;

            if (useDistill)
            {
                teacherPath = txtTeacher.Text?.Trim();
                if (string.IsNullOrWhiteSpace(teacherPath) || !File.Exists(teacherPath))
                {
                    ShowMessage("请选择有效的教师模型文件", AntdUI.TType.Warn);
                    SetTrainingState(false);
                    return;
                }
                _ = int.TryParse(numDistillEpochs.Value.ToString(), out distillEpochs);
                _ = double.TryParse(txtDistillLr.Text, out distillLr);
                _ = double.TryParse(txtDistillTemp.Text, out distillTemp);
                _ = double.TryParse(txtDistillWeight.Text, out distillWeight);
            }

            var config = new MLNetTrainConfig
            {
                MaxEpoch = (int)numEpochs.Value,
                InitLearningRate = double.Parse(txtLr0.Text),
                WeightDecay = double.Parse(txtWeightDecay.Text),
                IOUThreshold = double.Parse(txtIoU.Text),
                ScoreThreshold = double.Parse(txtScoreThresh.Text),
                SaveDir = txtSaveDir.Text,
                UseDistillation = useDistill,
                TeacherModelPath = useDistill ? teacherPath : null,
                DistillEpochs = distillEpochs,
                DistillLearningRate = distillLr,
                DistillTemperature = distillTemp,
                DistillWeight = distillWeight
            };

            AppendLog($"模型: AutoFormerV2 (ML.NET), LR: {config.InitLearningRate}");
            if (useDistill)
            {
                AppendLog($"蒸馏: 教师={teacherPath}");
                AppendLog($"蒸馏参数: 轮数={distillEpochs}, lr={distillLr}, T={distillTemp}, alpha={distillWeight}");
            }
            StatusChanged?.Invoke(this, $"训练中 AutoFormerV2 (ML.NET)...");

            // 重定向控制台输出
            var consoleWriter = new TextBoxConsoleWriter(this, txtLog);
            var originalOut = Console.Out;
            Console.SetOut(consoleWriter);
            try
            {
                var result = await _service.TrainAsync(
                    config, dataConfig.Train, dataConfig.Val,
                    dataConfig.Names.ToArray());

                if (result != null)
                {
                    StatusChanged?.Invoke(this,
                        $"训练完成: fitness={result.BestFitness:F4} " +
                        $"mAP50={result.BestMap50:F4}");
                }
            }
            finally
            {
                // 确保异常/取消时也能恢复 Console 输出
                Console.SetOut(originalOut);
            }
        }
        catch (Exception ex)
        {
            AppendLog($"Error: {ex.Message}");
            StatusChanged?.Invoke(this, $"训练失败: {ex.Message}");
            ShowMessage($"训练失败: {ex.Message}", AntdUI.TType.Error);
        }
        finally
        {
            SetTrainingState(false);
        }
    }

    private void BtnStop_Click(object? sender, EventArgs e)
    {
        _service.Cancel();
        AppendLog("正在停止训练...");
        StatusChanged?.Invoke(this, "正在停止...");
    }

    private async void BtnValidate_Click(object? sender, EventArgs e)
    {
        await RunEvaluation("val");
    }

    private async void BtnTest_Click(object? sender, EventArgs e)
    {
        await RunEvaluation("test");
    }

    private async Task RunEvaluation(string split)
    {
        if (string.IsNullOrWhiteSpace(txtDataset.Text) || !File.Exists(txtDataset.Text))
        {
            ShowMessage("请先选择数据集 YAML 文件。", AntdUI.TType.Warn);
            return;
        }

        // 查找最新的训练模型
        var weightsDir = Path.Combine(txtSaveDir.Text, "weights");
        var modelPath = Path.Combine(weightsDir, "best.zip");
        if (!File.Exists(modelPath))
        {
            modelPath = Path.Combine(weightsDir, "last.zip");
            if (!File.Exists(modelPath))
            {
                ShowMessage($"未找到训练好的模型。请先训练。", AntdUI.TType.Warn);
                return;
            }
        }

        var dataConfig = DatasetConfig.Load(txtDataset.Text);
        string evalDir = split == "test" && dataConfig.Test != null
            ? dataConfig.Test
            : dataConfig.Val;

        if (!Directory.Exists(evalDir))
        {
            ShowMessage($"{split} 数据目录不存在: {evalDir}", AntdUI.TType.Warn);
            return;
        }

        if (split == "val")
            btnValidate.Loading = true;
        else
            btnTest.Loading = true;

        AppendLog($"\n[{split.ToUpper()}] 评估模型: {modelPath}");
        AppendLog($"[{split.ToUpper()}] 数据目录: {evalDir}");
        StatusChanged?.Invoke(this, $"正在{(split == "val" ? "验证" : "测试")}...");

        await _service.EvaluateAsync(modelPath, evalDir, dataConfig.Names.ToArray());

        StatusChanged?.Invoke(this, $"{(split == "val" ? "验证" : "测试")}完成");
    }

    public void StopTraining()
    {
        if (_service.IsRunning)
            _service.Cancel();
    }

    private void SetTrainingState(bool isTraining)
    {
        btnStart.Enabled = !isTraining;
        btnStop.Enabled = isTraining;
        tableConfig.Enabled = !isTraining;
        btnBrowseDataset.Enabled = !isTraining;
        btnBrowseTeacher.Enabled = !isTraining;

        if (isTraining)
            btnStart.Loading = true;
        else
            btnStart.Loading = false;
    }

    // ── 训练报告 ──────────────────────────────────────

    private static string BuildTrainingReport(MLNetTrainResult result)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine("       ML.NET 训练完成 - 模型报告");
        sb.AppendLine("═══════════════════════════════════════════════════");
        sb.AppendLine();
        sb.AppendLine($"  模型架构:         AutoFormerV2");
        sb.AppendLine($"  Fitness:          {result.BestFitness:F4}");
        sb.AppendLine($"  mAP@0.5:          {result.BestMap50:F4} ({result.BestMap50 * 100:F1}%)");
        sb.AppendLine($"  mAP@0.5:0.95:     {result.BestMap5095:F4} ({result.BestMap5095 * 100:F1}%)");
        sb.AppendLine($"  最佳轮次:         {result.BestEpoch}");
        sb.AppendLine($"  训练时间:         {result.TrainingTime:hh\\:mm\\:ss}");
        sb.AppendLine($"  训练图像数:       {result.ImageCount}");
        sb.AppendLine($"  模型路径:         {result.ModelPath}");

        if (result.PerClassAP50.Length > 0 && result.ClassNames.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine("  ── Per-Class AP@0.5 ─────────────────────────");
            int maxLen = result.ClassNames.Max(n => n.Length);
            for (int i = 0; i < result.PerClassAP50.Length && i < result.ClassNames.Length; i++)
            {
                double ap = result.PerClassAP50[i];
                string bar = new string('|', (int)(ap * 30));
                string pad = result.ClassNames[i].PadRight(maxLen);
                sb.AppendLine($"  {pad}  {ap:F4} ({ap * 100:F1}%)  {bar}");
            }
        }

        sb.AppendLine("═══════════════════════════════════════════════════");
        return sb.ToString();
    }

    private void ShowTrainingResultMessage(MLNetTrainResult result)
    {
        string summary = $"ML.NET 训练完成!\n\n" +
                          $"Fitness: {result.BestFitness:F4}\n" +
                          $"mAP@0.5: {result.BestMap50 * 100:F1}%\n" +
                          $"mAP@0.5:0.95: {result.BestMap5095 * 100:F1}%\n" +
                          $"最佳轮次: {result.BestEpoch}\n" +
                          $"时间: {result.TrainingTime:hh\\:mm\\:ss}";

        if (result.BestFitness >= 0.5)
            ShowMessage(summary, AntdUI.TType.Success);
        else
            ShowMessage(summary, AntdUI.TType.Warn);
    }

    // ── 工具方法 ──────────────────────────────────────

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
    /// 控制台输出重定向到 RichTextBox。
    /// </summary>
    private class TextBoxConsoleWriter : System.IO.TextWriter
    {
        private readonly Control _owner;
        private readonly RichTextBox _textBox;
        public override System.Text.Encoding Encoding => System.Text.Encoding.UTF8;

        public TextBoxConsoleWriter(Control owner, RichTextBox textBox)
        {
            _owner = owner;
            _textBox = textBox;
        }

        public override void Write(char value) => AppendText(value.ToString());

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
            if (_owner.InvokeRequired)
            {
                try { _owner.Invoke(() => AppendText(text)); }
                catch (ObjectDisposedException) { }
                return;
            }

            _textBox.SuspendLayout();
            _textBox.AppendText(text);
            _textBox.SelectionStart = _textBox.TextLength;
            _textBox.SelectionLength = 0;
            _textBox.ScrollToCaret();
            _textBox.ResumeLayout();
        }
    }
}
