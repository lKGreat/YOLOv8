using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.TorchSharp;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using YOLO.Data.Datasets;
using YOLO.MLNet.Data;

namespace YOLO.MLNet.Training;

/// <summary>
/// Per-epoch 训练指标。
/// </summary>
public record MLNetEpochMetrics
{
    public int Epoch { get; init; }
    public int TotalEpochs { get; init; }
    public double Loss { get; init; }
    public double Map50 { get; init; }
    public double Map5095 { get; init; }
    public double LearningRate { get; init; }
    public bool IsBest { get; init; }
    public double DistillLoss { get; init; }
}

/// <summary>
/// ML.NET 目标检测训练结果。
/// </summary>
public record MLNetTrainResult
{
    public int BestEpoch { get; init; }
    public double BestMap50 { get; init; }
    public double BestMap5095 { get; init; }
    public double BestFitness { get; init; }
    public TimeSpan TrainingTime { get; init; }
    public string ModelPath { get; init; } = "";
    public int ImageCount { get; init; }
    public double[] PerClassAP50 { get; init; } = [];
    public string[] ClassNames { get; init; } = [];
}

/// <summary>
/// 封装 ML.NET ObjectDetectionTrainer 的核心训练器。
///
/// 提供:
///   - 基于 AutoFormerV2 的目标检测训练
///   - 进度回调 (onEpochCompleted)
///   - 训练完成后保存模型为 .zip
///   - 训练指标 CSV 日志
/// </summary>
public class MLNetDetectionTrainer
{
    private readonly MLNetTrainConfig _config;
    private readonly MLContext _mlContext;

    private readonly Action<string> _log;

    // Live progress state (best-effort parsed from ML.NET logs)
    private long _lastLogAtUtcTicks = DateTime.UtcNow.Ticks; // Interlocked.Read/Exchange
    private volatile string? _lastLog;
    private int _step = -1;        // Volatile.Read/Write, -1 表示未知
    private int _totalSteps = -1;  // Volatile.Read/Write, -1 表示未知
    private int _epoch = -1;       // Volatile.Read/Write, -1 表示未知
    private int _totalEpochs = -1; // Volatile.Read/Write, -1 表示未知

    // Live progress callback state (valid during Train())
    private volatile string _stage = "Fit";
    private volatile int _stageCompletedEpochs;
    private volatile int _stageTotalEpochs;
    private long _lastProgressReportTicks; // Stopwatch ticks (Interlocked.Read/Exchange)
    private Action<MLNetLiveProgress>? _onLiveProgress;
    private Stopwatch? _trainStopwatch;

    // NOTE: ML.NET 内部日志格式并不稳定，这里用多套正则“尽可能解析”
    private static readonly Regex StepRegex = new(
        @"\bStep\s*(?<s>\d+)\s*/\s*(?<t>\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex EpochRegex = new(
        @"\bEpoch\s*(?<e>\d+)\s*/\s*(?<t>\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex IterRegex = new(
        @"\bIter(?:ation)?\s*(?<s>\d+)\s*/\s*(?<t>\d+)\b",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public MLNetDetectionTrainer(MLNetTrainConfig config, int? seed = null, Action<string>? logSink = null)
    {
        _config = config;
        _mlContext = new MLContext(seed ?? 0);

        _log = logSink ?? Console.WriteLine;

        // 订阅 ML.NET 内部日志，转发到 Console 以便 GUI 日志面板可见
        // ObjectDetectionTrainer 的 Step/Loss/LR 日志走 ch.Info()，不走 Console
        _mlContext.Log += OnMLContextLog;
    }

    /// <summary>
    /// ML.NET 内部日志转发。过滤空消息和过于冗长的调试信息。
    /// </summary>
    private void OnMLContextLog(object? sender, LoggingEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(e.Message)) return;

        // 过滤极度冗长的 schema/data 信息，只保留训练相关日志
        var msg = e.Message.Trim();
        if (msg.StartsWith("Schema") || msg.StartsWith("Column") ||
            msg.Length > 500) return;

        var formatted = $"  [{e.Source}] {msg}";
        Interlocked.Exchange(ref _lastLogAtUtcTicks, DateTime.UtcNow.Ticks);
        _lastLog = formatted;

        TryParseProgress(msg);

        _log(formatted);

        // 尽可能将解析出的 step/epoch 实时推送给 UI（节流，避免高频刷 UI）
        ReportLiveProgressThrottled();
    }

    private void ReportLiveProgressThrottled()
    {
        var cb = _onLiveProgress;
        var sw = _trainStopwatch;
        if (cb == null || sw == null) return;

        long now = Stopwatch.GetTimestamp();
        long last = Interlocked.Read(ref _lastProgressReportTicks);

        // 约 5Hz 刷新上限（200ms）
        if (last != 0 && (now - last) < (Stopwatch.Frequency / 5))
            return;

        Interlocked.Exchange(ref _lastProgressReportTicks, now);
        cb(BuildSnapshot(_stage, _stageCompletedEpochs, _stageTotalEpochs, sw.Elapsed));
    }

    private void TryParseProgress(string msg)
    {
        // Step
        var mStep = StepRegex.Match(msg);
        if (mStep.Success)
        {
            if (int.TryParse(mStep.Groups["s"].Value, out var s)) Volatile.Write(ref _step, s);
            if (int.TryParse(mStep.Groups["t"].Value, out var t)) Volatile.Write(ref _totalSteps, t);
        }
        else
        {
            // 有些日志用 Iter/Iteration
            var mIter = IterRegex.Match(msg);
            if (mIter.Success)
            {
                if (int.TryParse(mIter.Groups["s"].Value, out var s)) Volatile.Write(ref _step, s);
                if (int.TryParse(mIter.Groups["t"].Value, out var t)) Volatile.Write(ref _totalSteps, t);
            }
        }

        // Epoch
        var mEpoch = EpochRegex.Match(msg);
        if (mEpoch.Success)
        {
            if (int.TryParse(mEpoch.Groups["e"].Value, out var e)) Volatile.Write(ref _epoch, e);
            if (int.TryParse(mEpoch.Groups["t"].Value, out var t)) Volatile.Write(ref _totalEpochs, t);
        }
    }

    private MLNetLiveProgress BuildSnapshot(
        string stage,
        int completedEpochs,
        int totalEpochs,
        TimeSpan elapsed)
    {
        // 优先用 ML.NET 日志解析到的 epoch/totalEpoch（如果它给了的话）
        var parsedEpoch = Volatile.Read(ref _epoch);
        var parsedTotalEpoch = Volatile.Read(ref _totalEpochs);

        int finalCompleted = parsedEpoch > 0 ? Math.Max(completedEpochs, parsedEpoch) : completedEpochs;
        int finalTotal = parsedTotalEpoch > 0 ? Math.Max(totalEpochs, parsedTotalEpoch) : totalEpochs;

        int step = Volatile.Read(ref _step);
        int totalStep = Volatile.Read(ref _totalSteps);

        var lastLogTicks = Interlocked.Read(ref _lastLogAtUtcTicks);
        var lastLogAtUtc = new DateTime(lastLogTicks, DateTimeKind.Utc);

        return new MLNetLiveProgress
        {
            Stage = stage,
            CompletedEpochs = finalCompleted,
            TotalEpochs = finalTotal,
            Step = step > 0 ? step : null,
            TotalSteps = totalStep > 0 ? totalStep : null,
            Elapsed = elapsed,
            LastLog = _lastLog,
            LastLogAtUtc = lastLogAtUtc
        };
    }

    /// <summary>
    /// 运行完整的 ML.NET 目标检测训练流程。
    /// </summary>
    /// <param name="trainDataDir">训练图像目录</param>
    /// <param name="valDataDir">验证图像目录（可选）</param>
    /// <param name="classNames">类别名称数组</param>
    /// <param name="onEpochCompleted">每轮完成的回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>训练结果</returns>
    public MLNetTrainResult Train(
        string trainDataDir,
        string? valDataDir = null,
        string[]? classNames = null,
        Action<MLNetEpochMetrics>? onEpochCompleted = null,
        Action<MLNetLiveProgress>? onLiveProgress = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _trainStopwatch = sw;
        _onLiveProgress = onLiveProgress;
        Interlocked.Exchange(ref _lastProgressReportTicks, 0);

        _log($"[ML.NET] 训练设备: 自动检测");
        _log($"[ML.NET] 模型架构: AutoFormerV2 (Vision Transformer)");
        _log($"[ML.NET] 训练轮数: {_config.MaxEpoch}, 学习率: {_config.InitLearningRate}");

        // 加载训练数据
        _log($"[ML.NET] 加载训练数据: {trainDataDir}");
        var trainData = YoloToMLNetAdapter.LoadFromDirectory(_mlContext, trainDataDir);
        int trainCount = YoloToMLNetAdapter.CountImages(trainDataDir);

        // 加载验证数据
        IDataView? valData = null;
        if (valDataDir != null && Directory.Exists(valDataDir))
        {
            _log($"[ML.NET] 加载验证数据: {valDataDir}");
            valData = YoloToMLNetAdapter.LoadFromDirectory(_mlContext, valDataDir);
        }

        // 创建保存目录
        var weightsDir = Path.Combine(_config.SaveDir, "weights");
        Directory.CreateDirectory(weightsDir);

        // 初始化 CSV 日志
        var csvPath = Path.Combine(_config.SaveDir, "results.csv");
        using var csvWriter = new StreamWriter(csvPath, false);
        csvWriter.WriteLine("epoch,loss,map50,map5095,fitness,lr");

        // 配置 ObjectDetectionTrainer
        var trainerOptions = new ObjectDetectionTrainer.Options
        {
            LabelColumnName = "Label",
            BoundingBoxColumnName = "BoundingBoxes",
            ImageColumnName = "Image",
            PredictedLabelColumnName = "PredictedLabel",
            PredictedBoundingBoxColumnName = "PredictedBoundingBoxes",
            ScoreColumnName = "Score",
            MaxEpoch = _config.MaxEpoch,
            InitLearningRate = _config.InitLearningRate,
            WeightDecay = _config.WeightDecay,
            IOUThreshold = _config.IOUThreshold,
            ScoreThreshold = _config.ScoreThreshold,
            LogEveryNStep = _config.LogEveryNStep,
            ValidationSet = valData
        };

        // 逐轮训练: 分步执行以提供进度回调
        // ML.NET ObjectDetectionTrainer.Fit 是一体化的,
        // 所以我们分拆成多个小 epoch 轮次来提供进度反馈
        double bestFitness = 0;
        double bestMap50 = 0;
        double bestMap5095 = 0;
        int bestEpoch = 0;
        double[] bestPerClassAP50 = [];
        string bestModelPath = "";

        // 阶段式训练: 每次训练一定轮数, 评估, 保存最优
        int epochsPerStage = Math.Max(1, _config.MaxEpoch / 5); // 分 5 个阶段
        int completedEpochs = 0;

        ObjectDetectionTransformer? bestTransformer = null;

        while (completedEpochs < _config.MaxEpoch)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int stageEpochs = Math.Min(epochsPerStage, _config.MaxEpoch - completedEpochs);

            var stageOptions = new ObjectDetectionTrainer.Options
            {
                LabelColumnName = trainerOptions.LabelColumnName,
                BoundingBoxColumnName = trainerOptions.BoundingBoxColumnName,
                ImageColumnName = trainerOptions.ImageColumnName,
                PredictedLabelColumnName = trainerOptions.PredictedLabelColumnName,
                PredictedBoundingBoxColumnName = trainerOptions.PredictedBoundingBoxColumnName,
                ScoreColumnName = trainerOptions.ScoreColumnName,
                MaxEpoch = stageEpochs,
                InitLearningRate = _config.InitLearningRate,
                WeightDecay = _config.WeightDecay,
                IOUThreshold = _config.IOUThreshold,
                ScoreThreshold = _config.ScoreThreshold,
                LogEveryNStep = _config.LogEveryNStep,
                ValidationSet = valData
            };

            // 创建并训练
            var pipeline = _mlContext.MulticlassClassification.Trainers
                .ObjectDetection(stageOptions);

            _log($"[ML.NET] 训练阶段 {completedEpochs + 1}-{completedEpochs + stageEpochs}/{_config.MaxEpoch}...");
            _log($"[ML.NET] Fit() 执行中 (AutoFormerV2 ViT 训练较慢，请耐心等待)...");

            _stage = "Fit";
            _stageCompletedEpochs = completedEpochs;
            _stageTotalEpochs = _config.MaxEpoch;
            ReportLiveProgressThrottled();

            // Fit() 会长时间阻塞：启动“心跳”线程，避免 UI 看起来卡死
            using var heartCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var stageStartCompleted = completedEpochs;
            var stageTotal = _config.MaxEpoch;
            var heartTask = Task.Run(async () =>
            {
                var proc = Process.GetCurrentProcess();
                var lastBeat = DateTime.UtcNow;
                while (!heartCts.IsCancellationRequested)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), heartCts.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }

                    var now = DateTime.UtcNow;
                    if (now - lastBeat < TimeSpan.FromSeconds(1.8))
                        continue;
                    lastBeat = now;

                    // 如果最近已有训练日志在刷，就不额外刷心跳，避免刷屏
                    var lastLogTicks = Interlocked.Read(ref _lastLogAtUtcTicks);
                    var lastLogAtUtc = new DateTime(lastLogTicks, DateTimeKind.Utc);
                    bool recentlyActive = (now - lastLogAtUtc) <= TimeSpan.FromSeconds(2.2);

                    var snap = BuildSnapshot(
                        stage: "Fit",
                        completedEpochs: stageStartCompleted,
                        totalEpochs: stageTotal,
                        elapsed: sw.Elapsed);

                    onLiveProgress?.Invoke(snap);

                    if (recentlyActive) continue;

                    proc.Refresh();
                    double wsMb = proc.WorkingSet64 / 1024.0 / 1024.0;
                    double gcMb = GC.GetTotalMemory(false) / 1024.0 / 1024.0;

                    var stepStr = snap.Step is > 0
                        ? (snap.TotalSteps is > 0 ? $"{snap.Step}/{snap.TotalSteps}" : $"{snap.Step}")
                        : "未知";

                    _log($"[ML.NET] 心跳  已运行 {sw.Elapsed:hh\\:mm\\:ss}  " +
                         $"进度 {snap.CompletedEpochs}/{snap.TotalEpochs}  step={stepStr}  " +
                         $"RAM(ws={wsMb:F0}MB,gc={gcMb:F0}MB)");
                }
            }, heartCts.Token);

            var transformer = pipeline.Fit(trainData);
            heartCts.Cancel();
            try { heartTask.Wait(TimeSpan.FromSeconds(1)); } catch { /* ignore */ }
            completedEpochs += stageEpochs;

            _stage = "Validate";
            _stageCompletedEpochs = completedEpochs;
            _stageTotalEpochs = _config.MaxEpoch;
            ReportLiveProgressThrottled();

            // 在验证集上评估
            double stageMap50 = 0, stageMap5095 = 0;
            double[] perClassAP50 = [];

            if (valData != null)
            {
                var evalResult = Evaluation.MLNetEvaluator.Evaluate(
                    _mlContext, transformer, valData, classNames);
                stageMap50 = evalResult.Map50;
                stageMap5095 = evalResult.Map5095;
                perClassAP50 = evalResult.PerClassAP50;
            }

            double fitness = 0.1 * stageMap50 + 0.9 * stageMap5095;
            bool isBest = fitness > bestFitness;

            if (isBest)
            {
                bestFitness = fitness;
                bestMap50 = stageMap50;
                bestMap5095 = stageMap5095;
                bestEpoch = completedEpochs;
                bestPerClassAP50 = perClassAP50;

                // 保存最佳模型
                bestModelPath = Path.Combine(weightsDir, "best.zip");
                SaveModel(transformer, trainData.Schema, bestModelPath);
                _log($"  * 保存最佳模型 (fitness={fitness:F4})");

                bestTransformer = transformer;
            }

            // 保存最新模型
            var lastModelPath = Path.Combine(weightsDir, "last.zip");
            SaveModel(transformer, trainData.Schema, lastModelPath);

            // CSV 日志
            csvWriter.WriteLine($"{completedEpochs},0,{stageMap50:F4},{stageMap5095:F4},{fitness:F4},{_config.InitLearningRate}");
            csvWriter.Flush();

            // 回调
            var mark = isBest ? " *" : "";
            _log($"  Epoch {completedEpochs}/{_config.MaxEpoch}" +
                $"  mAP50={stageMap50:F4}  mAP50-95={stageMap5095:F4}" +
                $"  fitness={fitness:F4}{mark}");

            onEpochCompleted?.Invoke(new MLNetEpochMetrics
            {
                Epoch = completedEpochs,
                TotalEpochs = _config.MaxEpoch,
                Loss = 0,
                Map50 = stageMap50,
                Map5095 = stageMap5095,
                LearningRate = _config.InitLearningRate,
                IsBest = isBest
            });
        }

        sw.Stop();
        _stage = "Complete";
        _stageCompletedEpochs = Math.Min(_config.MaxEpoch, completedEpochs);
        _stageTotalEpochs = _config.MaxEpoch;
        ReportLiveProgressThrottled();
        _onLiveProgress = null;
        _trainStopwatch = null;

        // 保存训练参数
        SaveTrainArgs(_config, trainCount, classNames);

        _log("");
        _log("=== ML.NET 训练总结 ===");
        _log($"  架构:         AutoFormerV2");
        _log($"  最佳轮次:     {bestEpoch}");
        _log($"  最佳 fitness: {bestFitness:F4}");
        _log($"  mAP@0.5:      {bestMap50:F4}");
        _log($"  mAP@0.5-0.95: {bestMap5095:F4}");
        _log($"  训练时间:     {sw.Elapsed:hh\\:mm\\:ss}");
        _log($"  模型路径:     {bestModelPath}");

        return new MLNetTrainResult
        {
            BestEpoch = bestEpoch,
            BestMap50 = bestMap50,
            BestMap5095 = bestMap5095,
            BestFitness = bestFitness,
            TrainingTime = sw.Elapsed,
            ModelPath = bestModelPath,
            ImageCount = trainCount,
            PerClassAP50 = bestPerClassAP50,
            ClassNames = classNames ?? []
        };
    }

    /// <summary>
    /// 保存训练好的 ML.NET 模型到 .zip 文件。
    /// </summary>
    public void SaveModel(ObjectDetectionTransformer transformer, DataViewSchema schema, string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        _mlContext.Model.Save(transformer, schema, path);
    }

    /// <summary>
    /// 加载已保存的 ML.NET 模型。
    /// </summary>
    public ITransformer LoadModel(string modelPath, out DataViewSchema schema)
    {
        return _mlContext.Model.Load(modelPath, out schema);
    }

    /// <summary>
    /// 保存训练参数到 args.yaml。
    /// </summary>
    private void SaveTrainArgs(MLNetTrainConfig config, int trainCount, string[]? classNames)
    {
        var argsPath = Path.Combine(config.SaveDir, "args.yaml");
        Directory.CreateDirectory(config.SaveDir);

        using var writer = new StreamWriter(argsPath);
        writer.WriteLine($"model: AutoFormerV2");
        writer.WriteLine($"framework: ML.NET");
        writer.WriteLine($"max_epoch: {config.MaxEpoch}");
        writer.WriteLine($"init_lr: {config.InitLearningRate}");
        writer.WriteLine($"weight_decay: {config.WeightDecay}");
        writer.WriteLine($"iou_threshold: {config.IOUThreshold}");
        writer.WriteLine($"score_threshold: {config.ScoreThreshold}");
        writer.WriteLine($"img_size: {config.ImgSize}");
        writer.WriteLine($"train_images: {trainCount}");
        if (config.UseDistillation)
        {
            writer.WriteLine($"distillation: true");
            writer.WriteLine($"teacher_model: {config.TeacherModelPath}");
            writer.WriteLine($"distill_epochs: {config.DistillEpochs}");
            writer.WriteLine($"distill_temperature: {config.DistillTemperature}");
            writer.WriteLine($"distill_weight: {config.DistillWeight}");
        }
        if (classNames != null)
        {
            writer.WriteLine($"num_classes: {classNames.Length}");
            writer.WriteLine("names:");
            for (int i = 0; i < classNames.Length; i++)
                writer.WriteLine($"  {i}: {classNames[i]}");
        }
    }

    /// <summary>
    /// 获取内部 MLContext 实例。
    /// </summary>
    public MLContext Context => _mlContext;
}
