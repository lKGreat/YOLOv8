using System.Diagnostics;
using TorchSharp;
using YOLOv8.Core.Models;
using YOLOv8.Data.Augmentation;
using YOLOv8.Data.Datasets;
using YOLOv8.Inference;
using YOLOv8.Inference.Metrics;
using YOLOv8.Training.Loss;
using YOLOv8.Training.Optimizers;
using YOLOv8.Training.Schedulers;
using static TorchSharp.torch;

namespace YOLOv8.Training;

/// <summary>
/// Training configuration parameters.
/// </summary>
public record TrainConfig
{
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
}

/// <summary>
/// Training result containing best metrics and timing.
/// </summary>
public record TrainResult
{
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
/// YOLOv8 Trainer implementing the full training pipeline:
///   - Data loading with mosaic augmentation
///   - Forward pass through model
///   - Detection loss (CIoU + DFL + BCE)
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

    public Trainer(TrainConfig config, Device? device = null)
    {
        this.config = config;
        this.device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);
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
    /// <returns>TrainResult with best metrics</returns>
    public TrainResult Train(string trainDataDir, string? valDataDir = null, string[]? classNames = null)
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
        Console.WriteLine($"Model: YOLOv8{config.ModelVariant}, Classes: {config.NumClasses}");
        Console.WriteLine($"Epochs: {config.Epochs}, BatchSize: {config.BatchSize}, ImgSize: {config.ImgSize}");

        // Create model
        using var model = new YOLOv8Model("yolov8", config.NumClasses, config.ModelVariant, device);
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

        // Create loss
        var loss = new DetectionLoss(config.NumClasses, regMax: 16, strides: null,
            config.BoxGain, config.ClsGain, config.DflGain);

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
        Console.WriteLine($"{"Epoch",>8} {"box",>10} {"cls",>10} {"dfl",>10} " +
            $"{"mAP50",>10} {"mAP50-95",>10} {"fitness",>10}");
        Console.WriteLine(new string('-', 72));

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

            double epochBoxLoss = 0, epochClsLoss = 0, epochDflLoss = 0;
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

                var (rawBox, rawCls, featureSizes) = model.ForwardTrain(imgs);

                var (totalLoss, lossItems) = loss.Compute(
                    rawBox, rawCls, featureSizes,
                    gtLbls, gtBoxes, mask, config.ImgSize);

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
                    Console.Write($"\r{epoch + 1,>5}/{config.Epochs,-3} " +
                        $"{epochBoxLoss / batchCount,10:F4} " +
                        $"{epochClsLoss / batchCount,10:F4} " +
                        $"{epochDflLoss / batchCount,10:F4} " +
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
            Console.Write($"\r{epoch + 1,>5}/{config.Epochs,-3} " +
                $"{avgBox,10:F4} {avgCls,10:F4} {avgDfl,10:F4} " +
                $"{currentMap50,10:F4} {currentMap5095,10:F4} {fitness,10:F4}");

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
                ema.ApplyTo(model);
                model.save(Path.Combine(weightsDir, "best.pt"));
                Console.Write(" *");
            }
            else
            {
                patienceCounter++;
            }

            Console.WriteLine();

            if (patienceCounter >= config.Patience)
            {
                Console.WriteLine($"  Early stopping after {epoch + 1} epochs (patience={config.Patience})");
                break;
            }

            // Save last model
            ema.ApplyTo(model);
            model.save(Path.Combine(weightsDir, "last.pt"));
        }

        sw.Stop();

        // Print training summary
        Console.WriteLine(new string('-', 72));
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
        Console.WriteLine($"  Model:         YOLOv8{config.ModelVariant}");
        Console.WriteLine($"  Parameters:    {totalParams:N0}");
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
    /// </summary>
    private (double map50, double map5095, double[] perClassAP50) Validate(
        YOLOv8Model model, ModelEMA ema, YOLODataset valDataset,
        int numClasses, int imgSize)
    {
        ema.ApplyTo(model);
        model.eval();

        var metric = new MAPMetric(numClasses);

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

        var (map50, map5095, perClassAP50) = metric.Compute();

        model.train();

        return (map50, map5095, perClassAP50);
    }
}
