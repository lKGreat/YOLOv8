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
}

/// <summary>
/// YOLOv8 Trainer implementing the full training pipeline:
///   - Data loading with mosaic augmentation
///   - Forward pass through model
///   - Detection loss (CIoU + DFL + BCE)
///   - Gradient accumulation, clipping, and optimizer step
///   - EMA parameter tracking
///   - LR warmup and scheduling (including momentum warmup)
///   - Validation after each epoch with mAP for best-model selection
///   - Early stopping
///   - Mosaic close (disable mosaic in last N epochs)
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
    /// Run the full training loop.
    /// </summary>
    /// <param name="trainDataDir">Training images directory</param>
    /// <param name="valDataDir">Validation images directory (optional)</param>
    public void Train(string trainDataDir, string? valDataDir = null)
    {
        Console.WriteLine($"Training on device: {device}");
        Console.WriteLine($"Model: YOLOv8{config.ModelVariant}, Classes: {config.NumClasses}");
        Console.WriteLine($"Epochs: {config.Epochs}, BatchSize: {config.BatchSize}, ImgSize: {config.ImgSize}");

        // Create model
        using var model = new YOLOv8Model("yolov8", config.NumClasses, config.ModelVariant, device);
        model.train();

        // Count parameters
        long totalParams = model.parameters().Sum(p => p.numel());
        Console.WriteLine($"Total parameters: {totalParams:N0}");

        // Create augmentation pipelines
        var trainPipeline = AugmentationPipeline.CreateTrainPipeline(
            config.ImgSize, config.MosaicProb, config.MixUpProb,
            config.HsvH, config.HsvS, config.HsvV,
            config.FlipLR, config.FlipUD, config.Scale, config.Translate);

        // Create the no-mosaic pipeline for close_mosaic epochs
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
            model,
            config.Optimizer,
            config.Lr0,
            config.Momentum,
            config.WeightDecay,
            config.BatchSize,
            accumulate,
            config.Nbs,
            config.NumClasses,
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
        double bestMap = 0;
        double bestLoss = double.MaxValue;
        int patienceCounter = 0;
        bool mosaicClosed = false;

        // Create save directory
        var weightsDir = Path.Combine(config.SaveDir, "weights");
        Directory.CreateDirectory(weightsDir);

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

                // Move to device
                var imgs = images.to(device);
                var gtBoxes = gtBboxes.to(device);
                var gtLbls = gtLabels.to(device);
                var mask = maskGT.to(device);

                // Update LR and momentum
                scheduler.Step(epoch, globalIter);

                // Forward pass
                var (rawBox, rawCls, featureSizes) = model.ForwardTrain(imgs);

                // Compute loss
                var (totalLoss, lossItems) = loss.Compute(
                    rawBox, rawCls, featureSizes,
                    gtLbls, gtBoxes, mask, config.ImgSize);

                // Backward
                totalLoss.backward();

                // Optimizer step (with accumulation)
                if (globalIter - lastOptStep >= accumulate)
                {
                    // Gradient clipping
                    torch.nn.utils.clip_grad_norm_(model.parameters(), config.MaxGradNorm);

                    optimizer.step();
                    optimizer.zero_grad();

                    // EMA update (includes buffers)
                    ema.Update(model);

                    lastOptStep = globalIter;
                }

                // Track losses
                epochBoxLoss += lossItems[0].item<float>();
                epochClsLoss += lossItems[1].item<float>();
                epochDflLoss += lossItems[2].item<float>();
                batchCount++;

                if (batchCount % 10 == 0 || batchCount == batchesPerEpoch)
                {
                    Console.Write($"\rEpoch {epoch + 1}/{config.Epochs} " +
                        $"[{batchCount}/{batchesPerEpoch}] " +
                        $"box: {epochBoxLoss / batchCount:F4} " +
                        $"cls: {epochClsLoss / batchCount:F4} " +
                        $"dfl: {epochDflLoss / batchCount:F4}");
                }
            }

            Console.WriteLine();

            // Epoch summary
            double avgLoss = (epochBoxLoss + epochClsLoss + epochDflLoss) / Math.Max(batchCount, 1);
            Console.WriteLine($"  Epoch {epoch + 1} complete - " +
                $"avg box: {epochBoxLoss / Math.Max(batchCount, 1):F4}, " +
                $"avg cls: {epochClsLoss / Math.Max(batchCount, 1):F4}, " +
                $"avg dfl: {epochDflLoss / Math.Max(batchCount, 1):F4}");

            // === Validation ===
            double currentMap50 = 0;
            if (valDataset != null)
            {
                currentMap50 = Validate(model, ema, valDataset, config.NumClasses, config.ImgSize);
                Console.WriteLine($"  Val mAP@0.5: {currentMap50:F4}");
            }

            // Best model selection: prefer mAP if validation available, fallback to loss
            bool isBest;
            if (valDataset != null)
            {
                isBest = currentMap50 > bestMap;
                if (isBest) bestMap = currentMap50;
            }
            else
            {
                isBest = avgLoss < bestLoss;
                if (isBest) bestLoss = avgLoss;
            }

            if (isBest)
            {
                patienceCounter = 0;

                // Apply EMA parameters and save best model
                ema.ApplyTo(model);
                model.save(Path.Combine(weightsDir, "best.pt"));
                Console.WriteLine($"  Saved best model" +
                    (valDataset != null ? $" (mAP@0.5: {bestMap:F4})" : $" (loss: {bestLoss:F4})"));
            }
            else
            {
                patienceCounter++;
                if (patienceCounter >= config.Patience)
                {
                    Console.WriteLine($"  Early stopping after {epoch + 1} epochs");
                    break;
                }
            }

            // Save last model (with EMA)
            ema.ApplyTo(model);
            model.save(Path.Combine(weightsDir, "last.pt"));
        }

        Console.WriteLine("Training complete!");
        Console.WriteLine($"Results saved to {config.SaveDir}");
    }

    /// <summary>
    /// Run validation and compute mAP.
    /// </summary>
    private double Validate(YOLOv8Model model, ModelEMA ema, YOLODataset valDataset,
        int numClasses, int imgSize)
    {
        // Apply EMA parameters for evaluation
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

                // boxes: (B, 4, N) in xywh format scaled by stride
                // scores: (B, nc, N) after sigmoid
                long batch = boxes.shape[0];

                for (long b = 0; b < batch; b++)
                {
                    // Get predictions for this image
                    var imgBoxes = boxes[b]; // (4, N)
                    var imgScores = scores[b]; // (nc, N)

                    // Transpose to (N, 4) and (N, nc)
                    var boxesT = imgBoxes.T; // (N, 4)
                    var scoresT = imgScores.T; // (N, nc)

                    // Get max class score and class id per anchor
                    var (maxScores, maxClasses) = scoresT.max(dim: -1); // (N,), (N,)

                    // Filter by confidence
                    var confMask = maxScores > 0.001;
                    var filteredBoxes = boxesT[confMask]; // (M, 4) xywh
                    var filteredScores = maxScores[confMask]; // (M,)
                    var filteredClasses = maxClasses[confMask]; // (M,)

                    // Convert xywh to xyxy for metric
                    var predBoxesXyxy = Core.Utils.BboxUtils.Xywh2Xyxy(filteredBoxes);

                    // Get GT for this image
                    var gtMask = maskGT[b, .., 0].to(ScalarType.Bool);
                    var gtBoxesImg = gtBboxes[b][gtMask]; // (K, 4) xyxy normalized
                    var gtLabelsImg = gtLabels[b][gtMask]; // (K, 1)

                    // Scale GT to pixel coordinates
                    gtBoxesImg = gtBoxesImg * imgSize;
                    var gtClassesImg = gtLabelsImg[.., 0].to(ScalarType.Int64);

                    // Convert to arrays for metric
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

        var (map50, map5095, _) = metric.Compute();
        Console.WriteLine($"  Val mAP@0.5:0.95: {map5095:F4}");

        // Restore model to train mode (caller will set again if needed)
        model.train();

        return map50;
    }
}
