using System.Diagnostics;
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

    public MLNetDetectionTrainer(MLNetTrainConfig config, int? seed = null)
    {
        _config = config;
        _mlContext = new MLContext(seed ?? 0);
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
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        Console.WriteLine($"[ML.NET] 训练设备: 自动检测");
        Console.WriteLine($"[ML.NET] 模型架构: AutoFormerV2 (Vision Transformer)");
        Console.WriteLine($"[ML.NET] 训练轮数: {_config.MaxEpoch}, 学习率: {_config.InitLearningRate}");

        // 加载训练数据
        Console.WriteLine($"[ML.NET] 加载训练数据: {trainDataDir}");
        var trainData = YoloToMLNetAdapter.LoadFromDirectory(_mlContext, trainDataDir);
        int trainCount = YoloToMLNetAdapter.CountImages(trainDataDir);

        // 加载验证数据
        IDataView? valData = null;
        if (valDataDir != null && Directory.Exists(valDataDir))
        {
            Console.WriteLine($"[ML.NET] 加载验证数据: {valDataDir}");
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

            Console.WriteLine($"[ML.NET] 训练阶段 {completedEpochs + 1}-{completedEpochs + stageEpochs}/{_config.MaxEpoch}...");
            var transformer = pipeline.Fit(trainData);
            completedEpochs += stageEpochs;

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
                Console.WriteLine($"  * 保存最佳模型 (fitness={fitness:F4})");

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
            Console.WriteLine($"  Epoch {completedEpochs}/{_config.MaxEpoch}" +
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

        // 保存训练参数
        SaveTrainArgs(_config, trainCount, classNames);

        Console.WriteLine();
        Console.WriteLine("=== ML.NET 训练总结 ===");
        Console.WriteLine($"  架构:         AutoFormerV2");
        Console.WriteLine($"  最佳轮次:     {bestEpoch}");
        Console.WriteLine($"  最佳 fitness: {bestFitness:F4}");
        Console.WriteLine($"  mAP@0.5:      {bestMap50:F4}");
        Console.WriteLine($"  mAP@0.5-0.95: {bestMap5095:F4}");
        Console.WriteLine($"  训练时间:     {sw.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine($"  模型路径:     {bestModelPath}");

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
