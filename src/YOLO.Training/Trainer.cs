using System.Diagnostics;
using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Core.Models;
using YOLO.Core.Utils;
using YOLO.Data.Augmentation;
using YOLO.Data.Datasets;
using YOLO.Inference;
using YOLO.Inference.Metrics;
using YOLO.Training.Loss;
using YOLO.Training.Optimizers;
using YOLO.Training.Schedulers;
using static TorchSharp.torch;

namespace YOLO.Training;

/// <summary>
/// Training configuration parameters.
/// </summary>
public record TrainConfig
{
    /// <summary>Model version, e.g. "v8", "v9", "v10"</summary>
    public string ModelVersion { get; init; } = "v8";
    public int Epochs { get; init; } = 100;
    public int BatchSize { get; init; } = 16;
    public int ImgSize { get; init; } = 640;
    public int NumClasses { get; init; } = 80;
    public string ModelVariant { get; init; } = "n";
    public string Optimizer { get; init; } = "auto";
    public double Lr0 { get; init; } = 0.01;
    public double Lrf { get; init; } = 0.01;
    public double Momentum { get; init; } = 0.937;
    public double WeightDecay { get; init; } = 0.0005;
    public double WarmupEpochs { get; init; } = 3.0;
    public double WarmupBiasLr { get; init; } = 0.1;
    public double WarmupMomentum { get; init; } = 0.8;
    public bool CosLR { get; init; } = false;
    public int CloseMosaic { get; init; } = 10;
    public int Patience { get; init; } = 100;
    public int Nbs { get; init; } = 64;
    public double MaxGradNorm { get; init; } = 10.0;
    public double BoxGain { get; init; } = 7.5;
    public double ClsGain { get; init; } = 0.5;
    public double DflGain { get; init; } = 1.5;
    public float MosaicProb { get; init; } = 1.0f;
    public float MixUpProb { get; init; } = 0.0f;
    public float HsvH { get; init; } = 0.015f;
    public float HsvS { get; init; } = 0.7f;
    public float HsvV { get; init; } = 0.4f;
    public float FlipLR { get; init; } = 0.5f;
    public float FlipUD { get; init; } = 0.0f;
    public float Scale { get; init; } = 0.5f;
    public float Translate { get; init; } = 0.1f;
    public string SaveDir { get; init; } = "runs/train";
    public int Seed { get; init; } = 0;

    // Distillation settings
    public string? TeacherModelPath { get; init; } = null;
    public string TeacherVariant { get; init; } = "l";
    public double DistillWeight { get; init; } = 1.0;
    public double DistillTemperature { get; init; } = 20.0;
    public string DistillMode { get; init; } = "logit";
}

/// <summary>
/// Per-epoch metrics reported via callback during training.
/// </summary>
public record EpochMetrics
{
    public int Epoch { get; init; }
    public int TotalEpochs { get; init; }
    public double BoxLoss { get; init; }
    public double ClsLoss { get; init; }
    public double DflLoss { get; init; }
    public double DistillLoss { get; init; }
    public double Map50 { get; init; }
    public double Map5095 { get; init; }
    public double Fitness { get; init; }
    public double LearningRate { get; init; }
    public bool IsBest { get; init; }
}

/// <summary>
/// Training result containing best metrics and timing.
/// </summary>
public record TrainResult
{
    public string ModelVersion { get; init; } = "v8";
    public string ModelVariant { get; init; } = "";
    public long ParamCount { get; init; }
    public int BestEpoch { get; init; }
    public double BestFitness { get; init; }
    public double BestMap50 { get; init; }
    public double BestMap5095 { get; init; }
    public TimeSpan TrainingTime { get; init; }
    public double[] PerClassAP50 { get; init; } = Array.Empty<double>();
    public string[] ClassNames { get; init; } = Array.Empty<string>();
}

/// <summary>
/// YOLO Trainer implementing the full training pipeline.
/// Uses ModelRegistry to create models and losses for any registered version (v8, v9, v10, ...).
///
/// Pipeline:
///   - Data loading with mosaic augmentation
///   - Forward pass through model
///   - Detection loss (version-specific via IDetectionLoss)
///   - Gradient accumulation, clipping, and optimizer step
///   - EMA parameter tracking
///   - LR warmup and scheduling (including momentum warmup)
///   - Validation after each epoch with mAP + fitness for best-model selection
///   - Early stopping
///   - Mosaic close (disable mosaic in last N epochs)
///   - Metrics CSV logging
/// </summary>
public class Trainer
{
    private readonly TrainConfig config;
    private readonly Device device;

    /// <summary>
    /// Initialize the v8 loss factory in the ModelRegistry.
    /// Called once at startup to wire loss creation without circular dependencies.
    /// </summary>
    public static void RegisterLossFactories()
    {
        // Register v8 loss factory
        if (ModelRegistry.IsRegistered("v8") && !ModelRegistry.HasLossFactory("v8"))
        {
            ModelRegistry.RegisterLoss("v8", (nc, boxGain, clsGain, dflGain) =>
                new DetectionLoss(nc, regMax: 16, strides: null, boxGain, clsGain, dflGain));
        }
    }

    public Trainer(TrainConfig config, Device? device = null)
    {
        this.config = config;
        this.device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);

        // Ensure model registrations are loaded
        YOLOv8Model.EnsureRegistered();
        RegisterLossFactories();
    }

    /// <summary>
    /// Compute ultralytics fitness score: 0.1 * mAP50 + 0.9 * mAP50-95
    /// </summary>
    public static double ComputeFitness(double map50, double map5095)
        => 0.1 * map50 + 0.9 * map5095;

    /// <summary>
    /// Run the full training loop.
    /// </summary>
    /// <param name="trainDataDir">Training images directory</param>
    /// <param name="valDataDir">Validation images directory (optional)</param>
    /// <param name="classNames">Optional class names for per-class AP display</param>
    /// <param name="onEpochCompleted">Optional callback invoked after each epoch with live metrics</param>
    /// <returns>TrainResult with best metrics</returns>
    public TrainResult Train(string trainDataDir, string? valDataDir = null,
        string[]? classNames = null, Action<EpochMetrics>? onEpochCompleted = null)
    {
        var sw = Stopwatch.StartNew();

        // Set seed for reproducibility
        if (config.Seed > 0)
        {
            torch.manual_seed(config.Seed);
            if (torch.cuda.is_available())
                torch.cuda.manual_seed(config.Seed);
            Console.WriteLine($"Random seed: {config.Seed}");
        }

        Console.WriteLine($"Training on device: {device}");
        Console.WriteLine($"Model: YOLO{config.ModelVersion}{config.ModelVariant}, Classes: {config.NumClasses}");
        Console.WriteLine($"Epochs: {config.Epochs}, BatchSize: {config.BatchSize}, ImgSize: {config.ImgSize}");

        // Create model via registry
        using var model = ModelRegistry.Create(config.ModelVersion, config.NumClasses, config.ModelVariant, device);
        model.train();

        // Count parameters
        long totalParams = model.parameters().Sum(p => p.numel());
        Console.WriteLine($"Total parameters: {totalParams:N0}");

        // Create metrics logger
        using var logger = new TrainingMetricsLogger(config.SaveDir);
        logger.SaveArgs(config.ModelVariant, totalParams, config);

        // Create augmentation pipelines
        var trainPipeline = AugmentationPipeline.CreateTrainPipeline(
            config.ImgSize, config.MosaicProb, config.MixUpProb,
            config.HsvH, config.HsvS, config.HsvV,
            config.FlipLR, config.FlipUD, config.Scale, config.Translate);

        var noMosaicPipeline = trainPipeline.WithoutMosaic();

        // Create dataset
        var trainDataset = new YOLODataset(trainDataDir, config.ImgSize, trainPipeline, useMosaic: true);
        trainDataset.CacheLabels();
        Console.WriteLine($"Training samples: {trainDataset.Count}");

        // Create validation dataset if provided
        YOLODataset? valDataset = null;
        if (valDataDir != null && Directory.Exists(valDataDir))
        {
            var valPipeline = AugmentationPipeline.CreateValPipeline(config.ImgSize);
            valDataset = new YOLODataset(valDataDir, config.ImgSize, valPipeline, useMosaic: false);
            valDataset.CacheLabels();
            Console.WriteLine($"Validation samples: {valDataset.Count}");

            // If val exists but has 0 GT boxes, mAP is guaranteed to be 0.0.
            // For tiny datasets (even 1 image), fall back to evaluating on train (val-style pipeline)
            // so users can confirm the model can overfit/learn.
            var (valTotalBoxes, _, _) = valDataset.GetLabelStats();
            if (valTotalBoxes == 0)
            {
                Console.WriteLine("  WARNING: Validation set has 0 GT boxes. mAP will always be 0.0.");
                Console.WriteLine("  Fallback: using TRAIN set (val pipeline) to compute mAP for visibility.");

                var trainValPipeline = AugmentationPipeline.CreateValPipeline(config.ImgSize);
                var trainAsVal = new YOLODataset(trainDataDir, config.ImgSize, trainValPipeline, useMosaic: false);
                trainAsVal.CacheLabels();
                valDataset = trainAsVal;
                Console.WriteLine($"  Fallback validation samples: {valDataset.Count}");
            }
        }

        int batchesPerEpoch = (trainDataset.Count + config.BatchSize - 1) / config.BatchSize;

        // Gradient accumulation
        int accumulate = Math.Max((int)Math.Round((double)config.Nbs / config.BatchSize), 1);

        // Create optimizer with 3-group parameter separation
        var optimizer = OptimizerFactory.Create(
            model, config.Optimizer, config.Lr0, config.Momentum, config.WeightDecay,
            config.BatchSize, accumulate, config.Nbs, config.NumClasses,
            (long)batchesPerEpoch * config.Epochs);

        // Create scheduler with momentum warmup
        var scheduler = new WarmupLRScheduler(optimizer, config.Epochs, batchesPerEpoch,
            config.Lrf, config.CosLR, config.WarmupEpochs, config.WarmupBiasLr,
            config.WarmupMomentum, config.Momentum);

        // Create EMA (updates both parameters and buffers)
        using var ema = new ModelEMA(model);

        // Create loss via registry
        var loss = ModelRegistry.CreateLoss(config.ModelVersion, config.NumClasses,
            config.BoxGain, config.ClsGain, config.DflGain);

        // === Knowledge distillation setup ===
        bool useDistillation = !string.IsNullOrEmpty(config.TeacherModelPath) &&
                               File.Exists(config.TeacherModelPath);
        YOLOModel? teacher = null;
        DistillationLoss? distillLoss = null;
        bool useFeatureDistill = config.DistillMode == "feature" || config.DistillMode == "both";

        if (useDistillation)
        {
            Console.WriteLine();
            Console.WriteLine($"=== Knowledge Distillation ===");
            Console.WriteLine($"  Teacher: YOLO{config.ModelVersion}{config.TeacherVariant}");
            Console.WriteLine($"  Weights: {config.TeacherModelPath}");
            Console.WriteLine($"  Mode:    {config.DistillMode}");
            Console.WriteLine($"  Weight:  {config.DistillWeight}");
            Console.WriteLine($"  Temp:    {config.DistillTemperature}");

            teacher = ModelRegistry.Create(config.ModelVersion, config.NumClasses, config.TeacherVariant, device);

            // Smart load: auto-detects Python PyTorch or TorchSharp format
            var loadResult = WeightLoader.SmartLoad(teacher, config.TeacherModelPath!);
            if (loadResult.MissingCount > 0)
            {
                Console.WriteLine($"  Warning: {loadResult.MissingCount} parameters not loaded from checkpoint");
            }

            teacher.eval();

            // Freeze all teacher parameters
            foreach (var p in teacher.parameters())
                p.requires_grad = false;

            long teacherParams = teacher.parameters().Sum(p => p.numel());
            Console.WriteLine($"  Teacher params: {teacherParams:N0}");

            // Create adaptation layers for feature distillation
            long[]? studentCh = useFeatureDistill ? model.FeatureChannels : null;
            long[]? teacherCh = useFeatureDistill ? teacher.FeatureChannels : null;

            distillLoss = new DistillationLoss(
                "distill_loss",
                temperature: config.DistillTemperature,
                mode: config.DistillMode,
                studentChannels: studentCh,
                teacherChannels: teacherCh,
                device: device);

            // Add distillation adapter parameters to optimizer
            if (useFeatureDistill)
            {
                foreach (var p in distillLoss.parameters())
                    p.requires_grad = true;
            }

            Console.WriteLine();
        }

        // Training loop
        int globalIter = 0;
        double bestFitness = 0;
        double bestMap50 = 0;
        double bestMap5095 = 0;
        int bestEpoch = 0;
        double bestLoss = double.MaxValue;
        int patienceCounter = 0;
        bool mosaicClosed = false;
        double[] bestPerClassAP50 = Array.Empty<double>();

        // Create save directory
        var weightsDir = Path.Combine(config.SaveDir, "weights");
        Directory.CreateDirectory(weightsDir);

        // Print header
        Console.WriteLine();
        if (useDistillation)
        {
            Console.WriteLine(string.Format("{0,8} {1,10} {2,10} {3,10} {4,10} {5,10} {6,10} {7,10}",
                "Epoch", "box", "cls", "dfl", "distill", "mAP50", "mAP50-95", "fitness"));
            Console.WriteLine(new string('-', 84));
        }
        else
        {
            Console.WriteLine(string.Format("{0,8} {1,10} {2,10} {3,10} {4,10} {5,10} {6,10}",
                "Epoch", "box", "cls", "dfl", "mAP50", "mAP50-95", "fitness"));
            Console.WriteLine(new string('-', 72));
        }

        for (int epoch = 0; epoch < config.Epochs; epoch++)
        {
            model.train();

            // Close mosaic in last N epochs
            if (!mosaicClosed && epoch >= config.Epochs - config.CloseMosaic)
            {
                Console.WriteLine($"  Closing mosaic augmentation at epoch {epoch + 1}");
                trainDataset.SetPipeline(noMosaicPipeline, useMosaic: false);
                mosaicClosed = true;
            }

            double epochBoxLoss = 0, epochClsLoss = 0, epochDflLoss = 0, epochDistillLoss = 0;
            int batchCount = 0;
            int lastOptStep = 0;

            foreach (var (images, gtBboxes, gtLabels, maskGT) in
                trainDataset.GetBatches(config.BatchSize, shuffle: true))
            {
                using var scope = torch.NewDisposeScope();

                globalIter++;

                var imgs = images.to(device);
                var gtBoxes = gtBboxes.to(device);
                var gtLbls = gtLabels.to(device);
                var mask = maskGT.to(device);

                scheduler.Step(epoch, globalIter);

                // Student forward pass
                Tensor rawBox, rawCls;
                (long h, long w)[] featureSizes;
                Tensor[]? studentFeats = null;

                if (useDistillation && useFeatureDistill)
                {
                    var result = model.ForwardTrainWithFeatures(imgs);
                    rawBox = result.rawBox;
                    rawCls = result.rawCls;
                    featureSizes = result.featureSizes;
                    studentFeats = result.neckFeatures;
                }
                else
                {
                    (rawBox, rawCls, featureSizes) = model.ForwardTrain(imgs);
                }

                // Detection loss
                var (totalLoss, lossItems) = loss.Compute(
                    rawBox, rawCls, featureSizes,
                    gtLbls, gtBoxes, mask, config.ImgSize);

                // Distillation loss
                if (useDistillation && teacher != null && distillLoss != null)
                {
                    Tensor tRawBox, tRawCls;
                    Tensor[]? teacherFeats = null;

                    using (torch.no_grad())
                    {
                        if (useFeatureDistill)
                        {
                            var tResult = teacher.ForwardTrainWithFeatures(imgs);
                            tRawBox = tResult.rawBox;
                            tRawCls = tResult.rawCls;
                            teacherFeats = tResult.neckFeatures;
                        }
                        else
                        {
                            (tRawBox, tRawCls, _) = teacher.ForwardTrain(imgs);
                        }
                    }

                    var (dLoss, dItem) = distillLoss.Compute(
                        rawBox, rawCls, tRawBox, tRawCls,
                        studentFeats, teacherFeats);

                    totalLoss = totalLoss + dLoss * config.DistillWeight;
                    epochDistillLoss += dItem.item<float>();
                }

                totalLoss.backward();

                if (globalIter - lastOptStep >= accumulate)
                {
                    torch.nn.utils.clip_grad_norm_(model.parameters(), config.MaxGradNorm);
                    optimizer.step();
                    optimizer.zero_grad();
                    ema.Update(model);
                    lastOptStep = globalIter;
                }

                epochBoxLoss += lossItems[0].item<float>();
                epochClsLoss += lossItems[1].item<float>();
                epochDflLoss += lossItems[2].item<float>();
                batchCount++;

                if (batchCount % 10 == 0 || batchCount == batchesPerEpoch)
                {
                    var distillStr = useDistillation
                        ? $" distill: {epochDistillLoss / batchCount:F4}"
                        : "";
                    Console.Write($"\r{epoch + 1,5}/{config.Epochs,-3} " +
                        $"{epochBoxLoss / batchCount,10:F4} " +
                        $"{epochClsLoss / batchCount,10:F4} " +
                        $"{epochDflLoss / batchCount,10:F4}" +
                        $"{distillStr} " +
                        $"[{batchCount}/{batchesPerEpoch}]         ");
                }
            }

            double avgBox = epochBoxLoss / Math.Max(batchCount, 1);
            double avgCls = epochClsLoss / Math.Max(batchCount, 1);
            double avgDfl = epochDflLoss / Math.Max(batchCount, 1);
            double avgLoss = avgBox + avgCls + avgDfl;

            // Validation
            double currentMap50 = 0, currentMap5095 = 0;
            double[] perClassAP50 = Array.Empty<double>();
            if (valDataset != null)
            {
                (currentMap50, currentMap5095, perClassAP50) = Validate(model, ema, valDataset,
                    config.NumClasses, config.ImgSize);
            }

            double fitness = ComputeFitness(currentMap50, currentMap5095);

            // Get current LR from optimizer
            double currentLr = optimizer.ParamGroups.First().LearningRate;

            // Log to CSV
            logger.LogEpoch(epoch + 1, avgBox, avgCls, avgDfl,
                currentMap50, currentMap5095, fitness, currentLr);

            // Print epoch line
            double avgDistill = epochDistillLoss / Math.Max(batchCount, 1);
            if (useDistillation)
            {
                Console.Write($"\r{epoch + 1,5}/{config.Epochs,-3} " +
                    $"{avgBox,10:F4} {avgCls,10:F4} {avgDfl,10:F4} {avgDistill,10:F4} " +
                    $"{currentMap50,10:F4} {currentMap5095,10:F4} {fitness,10:F4}");
            }
            else
            {
                Console.Write($"\r{epoch + 1,5}/{config.Epochs,-3} " +
                    $"{avgBox,10:F4} {avgCls,10:F4} {avgDfl,10:F4} " +
                    $"{currentMap50,10:F4} {currentMap5095,10:F4} {fitness,10:F4}");
            }

            // Best model selection by fitness (or loss if no val)
            bool isBest;
            if (valDataset != null)
            {
                isBest = fitness > bestFitness;
                if (isBest)
                {
                    bestFitness = fitness;
                    bestMap50 = currentMap50;
                    bestMap5095 = currentMap5095;
                    bestEpoch = epoch + 1;
                    bestPerClassAP50 = perClassAP50;
                }
            }
            else
            {
                isBest = avgLoss < bestLoss;
                if (isBest)
                {
                    bestLoss = avgLoss;
                    bestEpoch = epoch + 1;
                }
            }

            if (isBest)
            {
                patienceCounter = 0;
                SaveModelWithEMA(model, ema, Path.Combine(weightsDir, "best.pt"));
                Console.Write(" *");
            }
            else
            {
                patienceCounter++;
            }

            Console.WriteLine();

            // Invoke per-epoch callback for real-time UI updates
            onEpochCompleted?.Invoke(new EpochMetrics
            {
                Epoch = epoch + 1,
                TotalEpochs = config.Epochs,
                BoxLoss = avgBox,
                ClsLoss = avgCls,
                DflLoss = avgDfl,
                DistillLoss = avgDistill,
                Map50 = currentMap50,
                Map5095 = currentMap5095,
                Fitness = fitness,
                LearningRate = currentLr,
                IsBest = isBest
            });

            if (patienceCounter >= config.Patience)
            {
                Console.WriteLine($"  Early stopping after {epoch + 1} epochs (patience={config.Patience})");
                break;
            }

            // Save last model (with EMA weights, without modifying training model)
            SaveModelWithEMA(model, ema, Path.Combine(weightsDir, "last.pt"));
        }

        sw.Stop();

        // Dispose teacher model if used
        teacher?.Dispose();
        distillLoss?.Dispose();

        // Print training summary
        Console.WriteLine(new string('-', useDistillation ? 84 : 72));
        Console.WriteLine();

        // Print per-class AP if available
        if (bestPerClassAP50.Length > 0 && classNames != null && classNames.Length > 0)
        {
            Console.WriteLine("=== Per-Class AP@0.5 (best epoch) ===");
            for (int c = 0; c < Math.Min(bestPerClassAP50.Length, classNames.Length); c++)
            {
                if (bestPerClassAP50[c] > 0 || c < config.NumClasses)
                {
                    Console.WriteLine($"  {classNames[c],-20} {bestPerClassAP50[c]:F4}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine("=== Training Summary ===");
        Console.WriteLine($"  Model:         YOLO{config.ModelVersion}{config.ModelVariant}");
        Console.WriteLine($"  Parameters:    {totalParams:N0}");
        if (useDistillation)
        {
            Console.WriteLine($"  Teacher:       YOLO{config.ModelVersion}{config.TeacherVariant} ({config.DistillMode} distillation)");
        }
        Console.WriteLine($"  Best epoch:    {bestEpoch}");
        Console.WriteLine($"  Best fitness:  {bestFitness:F4}");
        Console.WriteLine($"  Best mAP@0.5:  {bestMap50:F4}");
        Console.WriteLine($"  Best mAP50-95: {bestMap5095:F4}");
        Console.WriteLine($"  Training time: {sw.Elapsed:hh\\:mm\\:ss}");
        Console.WriteLine($"  Results:       {config.SaveDir}");
        Console.WriteLine();

        // Print history table
        logger.PrintSummary();

        return new TrainResult
        {
            ModelVersion = config.ModelVersion,
            ModelVariant = config.ModelVariant,
            ParamCount = totalParams,
            BestEpoch = bestEpoch,
            BestFitness = bestFitness,
            BestMap50 = bestMap50,
            BestMap5095 = bestMap5095,
            TrainingTime = sw.Elapsed,
            PerClassAP50 = bestPerClassAP50,
            ClassNames = classNames ?? Array.Empty<string>()
        };
    }

    /// <summary>
    /// Run validation and compute mAP.
    /// Uses EMA weights for evaluation but preserves the training model state.
    /// </summary>
    private (double map50, double map5095, double[] perClassAP50) Validate(
        YOLOModel model, ModelEMA ema, YOLODataset valDataset,
        int numClasses, int imgSize)
    {
        // Save training state before applying EMA (so training is not disrupted)
        var savedState = SaveModelState(model);

        ema.ApplyTo(model);
        model.eval();

        var metric = new MAPMetric(numClasses);

        int totalPredictions = 0;
        int totalGTs = 0;
        float maxConf = 0f;

        using (torch.no_grad())
        {
            foreach (var (images, gtBboxes, gtLabels, maskGT) in
                valDataset.GetBatches(config.BatchSize, shuffle: false))
            {
                using var scope = torch.NewDisposeScope();

                var imgs = images.to(device);
                var (boxes, scores, _) = model.forward(imgs);

                long batch = boxes.shape[0];

                for (long b = 0; b < batch; b++)
                {
                    var boxesT = boxes[b].T;
                    var scoresT = scores[b].T;

                    var (maxScores, maxClasses) = scoresT.max(dim: -1);

                    var confMask = maxScores > 0.001;
                    var filteredBoxes = boxesT[confMask];
                    var filteredScores = maxScores[confMask];
                    var filteredClasses = maxClasses[confMask];

                    var predBoxesXyxy = Core.Utils.BboxUtils.Xywh2Xyxy(filteredBoxes);

                    var gtMask = maskGT[b, .., 0].to(ScalarType.Bool);
                    var gtBoxesImg = gtBboxes[b][gtMask];
                    var gtLabelsImg = gtLabels[b][gtMask];

                    gtBoxesImg = gtBoxesImg * imgSize;
                    var gtClassesImg = gtLabelsImg[.., 0].to(ScalarType.Int64);

                    int numPred = (int)predBoxesXyxy.shape[0];
                    int numGT = (int)gtBoxesImg.shape[0];

                    totalPredictions += numPred;
                    totalGTs += numGT;
                    if (numPred > 0)
                    {
                        var batchMaxConf = filteredScores.max().cpu().item<float>();
                        if (batchMaxConf > maxConf) maxConf = batchMaxConf;
                    }

                    var predBoxArr = new float[numPred, 4];
                    var predScoreArr = new float[numPred];
                    var predClassArr = new int[numPred];
                    var gtBoxArr = new float[numGT, 4];
                    var gtClassArr = new int[numGT];

                    if (numPred > 0)
                    {
                        var predData = predBoxesXyxy.cpu().data<float>().ToArray();
                        var scoreData = filteredScores.cpu().data<float>().ToArray();
                        var classData = filteredClasses.cpu().data<long>().ToArray();

                        for (int i = 0; i < numPred; i++)
                        {
                            predBoxArr[i, 0] = predData[i * 4 + 0];
                            predBoxArr[i, 1] = predData[i * 4 + 1];
                            predBoxArr[i, 2] = predData[i * 4 + 2];
                            predBoxArr[i, 3] = predData[i * 4 + 3];
                            predScoreArr[i] = scoreData[i];
                            predClassArr[i] = (int)classData[i];
                        }
                    }

                    if (numGT > 0)
                    {
                        var gtData = gtBoxesImg.cpu().data<float>().ToArray();
                        var gtClsData = gtClassesImg.cpu().data<long>().ToArray();

                        for (int i = 0; i < numGT; i++)
                        {
                            gtBoxArr[i, 0] = gtData[i * 4 + 0];
                            gtBoxArr[i, 1] = gtData[i * 4 + 1];
                            gtBoxArr[i, 2] = gtData[i * 4 + 2];
                            gtBoxArr[i, 3] = gtData[i * 4 + 3];
                            gtClassArr[i] = (int)gtClsData[i];
                        }
                    }

                    metric.Update(predBoxArr, predScoreArr, predClassArr, gtBoxArr, gtClassArr);
                }
            }
        }

        // Diagnostic: warn if validation has no GTs or no predictions
        if (totalGTs == 0)
        {
            Console.Write(" [WARN: 0 GT boxes in val]");
        }
        else if (totalPredictions == 0)
        {
            Console.Write($" [WARN: 0 preds>0.001, GTs={totalGTs}]");
        }

        var (map50, map5095, perClassAP50) = metric.Compute();

        // Restore training state (undo EMA overwrite)
        RestoreModelState(model, savedState);
        model.train();

        return (map50, map5095, perClassAP50);
    }

    /// <summary>
    /// Save a snapshot of all model parameters and buffers (for later restoration).
    /// </summary>
    private static Dictionary<string, Tensor> SaveModelState(nn.Module model)
    {
        var state = new Dictionary<string, Tensor>();
        using var _ = torch.no_grad();

        foreach (var (name, param) in model.named_parameters())
        {
            state[name] = param.detach().clone();
        }
        foreach (var (name, buffer) in model.named_buffers())
        {
            state[$"__buffer__{name}"] = buffer.detach().clone();
        }

        return state;
    }

    /// <summary>
    /// Restore model parameters and buffers from a saved snapshot, then dispose the snapshot.
    /// </summary>
    private static void RestoreModelState(nn.Module model, Dictionary<string, Tensor> savedState)
    {
        using var _ = torch.no_grad();

        foreach (var (name, param) in model.named_parameters())
        {
            if (savedState.TryGetValue(name, out var saved))
                param.copy_(saved);
        }
        foreach (var (name, buffer) in model.named_buffers())
        {
            if (savedState.TryGetValue($"__buffer__{name}", out var saved))
                buffer.copy_(saved);
        }

        // Dispose all saved tensors
        foreach (var t in savedState.Values)
            t.Dispose();
        savedState.Clear();
    }

    /// <summary>
    /// Save model to disk using EMA weights, without permanently modifying the training model.
    /// </summary>
    private static void SaveModelWithEMA(nn.Module model, ModelEMA ema, string path)
    {
        var savedState = SaveModelState(model);
        ema.ApplyTo(model);
        model.save(path);
        RestoreModelState(model, savedState);
    }
}
