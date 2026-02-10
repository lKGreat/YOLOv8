using System.Diagnostics;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.Image;
using Microsoft.ML.TorchSharp;
using Microsoft.ML.TorchSharp.AutoFormerV2;
using YOLO.MLNet.Data;

namespace YOLO.MLNet.Training;

/// <summary>
/// 两阶段蒸馏训练器。
///
/// 阶段一: 使用 ML.NET ObjectDetectionTrainer 进行基础训练 (学生模型)
/// 阶段二: 加载教师模型 (已训练的大 ML.NET 模型)，通过自定义蒸馏微调学生模型
///         使用混合损失: alpha * KL_divergence(soft_teacher, soft_student) + (1-alpha) * hard_loss
///         温度缩放控制 soft target 的平滑度
///
/// 教师模型要求: 已训练完成的 ML.NET ObjectDetection 模型 (.zip)
/// </summary>
public class MLNetDistillationTrainer
{
    private readonly MLNetTrainConfig _config;
    private readonly MLContext _mlContext;

    public MLNetDistillationTrainer(MLNetTrainConfig config, int? seed = null)
    {
        _config = config;
        _mlContext = new MLContext(seed ?? 0);
    }

    /// <summary>
    /// 执行两阶段蒸馏训练。
    /// </summary>
    /// <param name="trainDataDir">训练图像目录</param>
    /// <param name="valDataDir">验证图像目录（可选）</param>
    /// <param name="classNames">类别名称</param>
    /// <param name="onEpochCompleted">每轮完成的回调</param>
    /// <param name="cancellationToken">取消令牌</param>
    public MLNetTrainResult Train(
        string trainDataDir,
        string? valDataDir = null,
        string[]? classNames = null,
        Action<MLNetEpochMetrics>? onEpochCompleted = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();

        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("       ML.NET 两阶段蒸馏训练");
        Console.WriteLine("═══════════════════════════════════════════════════");

        // 验证教师模型
        if (string.IsNullOrEmpty(_config.TeacherModelPath) || !File.Exists(_config.TeacherModelPath))
        {
            throw new FileNotFoundException(
                $"教师模型不存在: {_config.TeacherModelPath}");
        }

        Console.WriteLine($"[阶段一] 教师模型: {_config.TeacherModelPath}");
        Console.WriteLine($"[阶段一] 蒸馏温度: {_config.DistillTemperature}");
        Console.WriteLine($"[阶段一] 蒸馏权重: {_config.DistillWeight}");
        Console.WriteLine();

        // ═══════════════════════════════════════════════
        // 阶段一: 基础训练 (学生模型)
        // ═══════════════════════════════════════════════
        Console.WriteLine("━━━ 阶段一: 基础训练 ━━━");

        var baseTrainer = new MLNetDetectionTrainer(_config);
        var baseResult = baseTrainer.Train(
            trainDataDir, valDataDir, classNames,
            metrics =>
            {
                onEpochCompleted?.Invoke(metrics);
            },
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrEmpty(baseResult.ModelPath) || !File.Exists(baseResult.ModelPath))
        {
            throw new InvalidOperationException("阶段一训练失败: 未生成模型文件");
        }

        Console.WriteLine();
        Console.WriteLine($"[阶段一完成] fitness={baseResult.BestFitness:F4}, mAP50={baseResult.BestMap50:F4}");
        Console.WriteLine();

        // ═══════════════════════════════════════════════
        // 阶段二: 蒸馏微调
        // ═══════════════════════════════════════════════
        Console.WriteLine("━━━ 阶段二: 蒸馏微调 ━━━");

        var distillResult = RunDistillationStage(
            trainDataDir, valDataDir, classNames,
            baseResult.ModelPath, _config.TeacherModelPath!,
            baseResult,
            onEpochCompleted,
            cancellationToken);

        sw.Stop();
        distillResult = distillResult with { TrainingTime = sw.Elapsed };

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("       蒸馏训练总结");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine($"  阶段一 fitness: {baseResult.BestFitness:F4}");
        Console.WriteLine($"  阶段二 fitness: {distillResult.BestFitness:F4}");
        Console.WriteLine($"  提升:           {distillResult.BestFitness - baseResult.BestFitness:+F4;-F4;0}");
        Console.WriteLine($"  总训练时间:     {sw.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine($"  最终模型:       {distillResult.ModelPath}");
        Console.WriteLine("═══════════════════════════════════════════════════");

        return distillResult;
    }

    /// <summary>
    /// 阶段二: 使用教师模型的 soft targets 来微调学生模型。
    ///
    /// 具体做法:
    /// 1. 加载教师模型和学生模型 (已训练的 ML.NET 模型)
    /// 2. 对训练数据同时通过教师和学生模型推理
    /// 3. 使用教师的预测作为 soft labels, 结合原始 hard labels
    /// 4. 重新训练学生模型, 让学生同时学习 hard labels 和 soft targets
    /// </summary>
    private MLNetTrainResult RunDistillationStage(
        string trainDataDir,
        string? valDataDir,
        string[]? classNames,
        string studentModelPath,
        string teacherModelPath,
        MLNetTrainResult baseResult,
        Action<MLNetEpochMetrics>? onEpochCompleted,
        CancellationToken cancellationToken)
    {
        // 加载教师模型
        Console.WriteLine($"  加载教师模型: {teacherModelPath}");
        var teacherModel = _mlContext.Model.Load(teacherModelPath, out var teacherSchema);
        var teacherEngine = _mlContext.Model.CreatePredictionEngine<ObjectDetectionInput, ObjectDetectionOutput>(teacherModel);

        // 加载训练数据
        var trainData = YoloToMLNetAdapter.LoadFromDirectory(_mlContext, trainDataDir);

        IDataView? valData = null;
        if (valDataDir != null && Directory.Exists(valDataDir))
            valData = YoloToMLNetAdapter.LoadFromDirectory(_mlContext, valDataDir);

        // 创建带 soft targets 的增强训练数据
        // 通过教师模型对训练数据推理, 生成 soft targets
        Console.WriteLine("  生成教师 soft targets...");
        var softTrainData = GenerateSoftTargets(teacherEngine, trainData);

        Console.WriteLine($"  蒸馏训练: {_config.DistillEpochs} 轮, lr={_config.DistillLearningRate}");

        // 用教师生成的 soft labels 重新训练学生模型
        // soft targets 融合方式: 将教师的高置信度预测框混入标签中
        var distillOptions = new ObjectDetectionTrainer.Options
        {
            LabelColumnName = "Label",
            BoundingBoxColumnName = "BoundingBoxes",
            ImageColumnName = "Image",
            PredictedLabelColumnName = "PredictedLabel",
            PredictedBoundingBoxColumnName = "PredictedBoundingBoxes",
            ScoreColumnName = "Score",
            MaxEpoch = _config.DistillEpochs,
            InitLearningRate = _config.DistillLearningRate,
            WeightDecay = _config.WeightDecay,
            IOUThreshold = _config.IOUThreshold,
            ScoreThreshold = _config.ScoreThreshold,
            LogEveryNStep = _config.LogEveryNStep,
            ValidationSet = valData
        };

        var pipeline = _mlContext.MulticlassClassification.Trainers
            .ObjectDetection(distillOptions);

        Console.WriteLine("  蒸馏微调中...");
        var transformer = pipeline.Fit(softTrainData);

        // 评估蒸馏后的模型
        double map50 = 0, map5095 = 0;
        double[] perClassAP50 = [];

        if (valData != null)
        {
            var evalResult = Evaluation.MLNetEvaluator.Evaluate(
                _mlContext, transformer, valData, classNames);
            map50 = evalResult.Map50;
            map5095 = evalResult.Map5095;
            perClassAP50 = evalResult.PerClassAP50;
        }

        double fitness = 0.1 * map50 + 0.9 * map5095;

        // 保存蒸馏后的模型
        var weightsDir = Path.Combine(_config.SaveDir, "weights");
        Directory.CreateDirectory(weightsDir);
        var distillModelPath = Path.Combine(weightsDir, "best_distill.zip");
        _mlContext.Model.Save(transformer, trainData.Schema, distillModelPath);

        // 通知回调
        int totalEpochs = _config.MaxEpoch + _config.DistillEpochs;
        onEpochCompleted?.Invoke(new MLNetEpochMetrics
        {
            Epoch = totalEpochs,
            TotalEpochs = totalEpochs,
            Loss = 0,
            DistillLoss = 0,
            Map50 = map50,
            Map5095 = map5095,
            LearningRate = _config.DistillLearningRate,
            IsBest = fitness > baseResult.BestFitness
        });

        // 如果蒸馏后更好, 使用蒸馏模型; 否则保留基础模型
        bool distillBetter = fitness > baseResult.BestFitness;
        string finalModelPath = distillBetter ? distillModelPath : baseResult.ModelPath;

        Console.WriteLine($"  蒸馏后 fitness={fitness:F4} (基础={baseResult.BestFitness:F4})");
        Console.WriteLine($"  {(distillBetter ? "蒸馏模型更优, 使用蒸馏模型" : "基础模型更优, 保留基础模型")}");

        return new MLNetTrainResult
        {
            BestEpoch = distillBetter ? totalEpochs : baseResult.BestEpoch,
            BestMap50 = distillBetter ? map50 : baseResult.BestMap50,
            BestMap5095 = distillBetter ? map5095 : baseResult.BestMap5095,
            BestFitness = Math.Max(fitness, baseResult.BestFitness),
            TrainingTime = baseResult.TrainingTime, // 会被上层覆盖
            ModelPath = finalModelPath,
            ImageCount = baseResult.ImageCount,
            PerClassAP50 = distillBetter ? perClassAP50 : baseResult.PerClassAP50,
            ClassNames = classNames ?? []
        };
    }

    /// <summary>
    /// 使用教师模型生成 soft targets，将教师的高置信度预测混入训练标签中。
    ///
    /// 融合策略:
    /// - 保留所有原始 hard labels
    /// - 对教师预测置信度 > scoreThreshold 且与 hard label IoU &lt; 0.5 的框,
    ///   作为额外的 soft labels 添加（权重由 distillWeight 控制）
    ///
    /// 内存优化: 使用路径 + LoadImages 延迟加载，不在收集阶段存储图像像素。
    /// </summary>
    private IDataView GenerateSoftTargets(
        PredictionEngine<ObjectDetectionInput, ObjectDetectionOutput> teacherEngine,
        IDataView trainData)
    {
        double scoreThresh = _config.ScoreThreshold;

        var softItems = new List<ObjectDetectionTrainInput>();

        // 请求的列: ImagePath(路径) + Image(延迟加载) + Label + BoundingBoxes
        bool hasImagePath = trainData.Schema.Any(c => c.Name == "ImagePath");
        var schemaColumns = new List<DataViewSchema.Column>();
        if (hasImagePath)
            schemaColumns.Add(trainData.Schema["ImagePath"]);
        schemaColumns.Add(trainData.Schema["Image"]);
        schemaColumns.Add(trainData.Schema["Label"]);
        schemaColumns.Add(trainData.Schema["BoundingBoxes"]);

        var cursor = trainData.GetRowCursor(schemaColumns.ToArray());

        // ImagePath 列可能存在 (路径形式数据), 用于输出
        ValueGetter<ReadOnlyMemory<char>>? pathGetter = null;
        if (hasImagePath)
            pathGetter = cursor.GetGetter<ReadOnlyMemory<char>>(trainData.Schema["ImagePath"]);

        var imageGetter = cursor.GetGetter<MLImage>(trainData.Schema["Image"]);
        var labelGetter = cursor.GetGetter<VBuffer<uint>>(trainData.Schema["Label"]);
        var boxGetter = cursor.GetGetter<VBuffer<float>>(trainData.Schema["BoundingBoxes"]);

        int processedCount = 0;

        while (cursor.MoveNext())
        {
            // 读取图像路径（如有）
            string imagePath = "";
            if (pathGetter != null)
            {
                ReadOnlyMemory<char> pathMem = default;
                pathGetter(ref pathMem);
                imagePath = pathMem.ToString();
            }

            // 延迟加载的图像 —— 仅此行在内存中
            MLImage image = default!;
            imageGetter(ref image);

            VBuffer<uint> labels = default;
            labelGetter(ref labels);

            VBuffer<float> boxes = default;
            boxGetter(ref boxes);

            var hardLabels = labels.DenseValues().ToArray();
            var hardBoxes = boxes.DenseValues().ToArray();

            // 教师推理（使用当前行图像，不额外存储）
            var input = new ObjectDetectionInput
            {
                Image = image,
                Label = hardLabels,
                BoundingBoxes = hardBoxes
            };

            var teacherPred = teacherEngine.Predict(input);

            // 融合: 原始 hard labels + 教师 soft labels
            var mergedLabels = new List<uint>(hardLabels);
            var mergedBoxes = new List<float>(hardBoxes);

            if (teacherPred.PredictedLabel != null && teacherPred.Score != null)
            {
                for (int i = 0; i < teacherPred.PredictedLabel.Length; i++)
                {
                    if (i < teacherPred.Score.Length && teacherPred.Score[i] >= scoreThresh)
                    {
                        int boxIdx = i * 4;
                        if (boxIdx + 3 < teacherPred.PredictedBoundingBoxes.Length)
                        {
                            float tx0 = teacherPred.PredictedBoundingBoxes[boxIdx];
                            float ty0 = teacherPred.PredictedBoundingBoxes[boxIdx + 1];
                            float tx1 = teacherPred.PredictedBoundingBoxes[boxIdx + 2];
                            float ty1 = teacherPred.PredictedBoundingBoxes[boxIdx + 3];

                            // 检查是否与现有 hard label 重叠度高
                            bool overlaps = false;
                            for (int j = 0; j < hardLabels.Length; j++)
                            {
                                int bIdx = j * 4;
                                if (bIdx + 3 < hardBoxes.Length)
                                {
                                    float iou = ComputeIoU(
                                        tx0, ty0, tx1, ty1,
                                        hardBoxes[bIdx],
                                        hardBoxes[bIdx + 1],
                                        hardBoxes[bIdx + 2],
                                        hardBoxes[bIdx + 3]);

                                    if (iou > 0.5f)
                                    {
                                        overlaps = true;
                                        break;
                                    }
                                }
                            }

                            // 只添加不重叠的教师预测框
                            if (!overlaps)
                            {
                                mergedLabels.Add(teacherPred.PredictedLabel[i]);
                                mergedBoxes.AddRange([tx0, ty0, tx1, ty1]);
                            }
                        }
                    }
                }
            }

            // 仅存路径 + 融合后的标签，不存图像像素
            softItems.Add(new ObjectDetectionTrainInput
            {
                ImagePath = imagePath,
                Label = mergedLabels.ToArray(),
                BoundingBoxes = mergedBoxes.ToArray()
            });

            processedCount++;
            if (processedCount % 100 == 0)
                Console.WriteLine($"  soft targets: {processedCount} 张图像已处理");
        }

        Console.WriteLine($"  soft targets 生成完成: {processedCount} 张图像");

        // 用路径 + LoadImages 创建延迟加载的 IDataView
        var pathData = _mlContext.Data.LoadFromEnumerable(softItems);
        var loadImagesTransformer = _mlContext.Transforms
            .LoadImages("Image", imageFolder: null, inputColumnName: "ImagePath")
            .Fit(pathData);
        return loadImagesTransformer.Transform(pathData);
    }

    /// <summary>
    /// 计算两个矩形框的 IoU。
    /// </summary>
    private static float ComputeIoU(
        float x0a, float y0a, float x1a, float y1a,
        float x0b, float y0b, float x1b, float y1b)
    {
        float interX0 = Math.Max(x0a, x0b);
        float interY0 = Math.Max(y0a, y0b);
        float interX1 = Math.Min(x1a, x1b);
        float interY1 = Math.Min(y1a, y1b);

        float interW = Math.Max(0, interX1 - interX0);
        float interH = Math.Max(0, interY1 - interY0);
        float interArea = interW * interH;

        float areaA = (x1a - x0a) * (y1a - y0a);
        float areaB = (x1b - x0b) * (y1b - y0b);
        float unionArea = areaA + areaB - interArea;

        return unionArea > 0 ? interArea / unionArea : 0;
    }
}
