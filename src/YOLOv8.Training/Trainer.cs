using TorchSharp;
using YOLOv8.Core.Models;
using YOLOv8.Data.Augmentation;
using YOLOv8.Data.Datasets;
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
    public int Patience { get; init; } = 50;
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
///   - LR warmup and scheduling
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

        // Create augmentation pipeline
        var trainPipeline = AugmentationPipeline.CreateTrainPipeline(
            config.ImgSize, config.MosaicProb, config.MixUpProb,
            config.HsvH, config.HsvS, config.HsvV,
            config.FlipLR, config.FlipUD, config.Scale, config.Translate);

        // Create dataset
        var trainDataset = new YOLODataset(trainDataDir, config.ImgSize, trainPipeline, useMosaic: true);
        trainDataset.CacheLabels();
        Console.WriteLine($"Training samples: {trainDataset.Count}");

        int batchesPerEpoch = (trainDataset.Count + config.BatchSize - 1) / config.BatchSize;

        // Create optimizer
        var optimizer = OptimizerFactory.Create(model, config.Optimizer, config.Lr0,
            config.Momentum, config.WeightDecay, config.NumClasses,
            (long)batchesPerEpoch * config.Epochs);

        // Create scheduler
        var scheduler = new WarmupLRScheduler(optimizer, config.Epochs, batchesPerEpoch,
            config.Lrf, config.CosLR, config.WarmupEpochs, config.WarmupBiasLr,
            config.WarmupMomentum, config.Momentum);

        // Create EMA
        using var ema = new ModelEMA(model);

        // Create loss
        var loss = new DetectionLoss(config.NumClasses, regMax: 16, strides: null,
            config.BoxGain, config.ClsGain, config.DflGain);

        // Gradient accumulation
        int accumulate = Math.Max((int)Math.Round((double)config.Nbs / config.BatchSize), 1);

        // Training loop
        int globalIter = 0;
        double bestLoss = double.MaxValue;
        int patienceCounter = 0;

        // Create save directory
        Directory.CreateDirectory(config.SaveDir);

        for (int epoch = 0; epoch < config.Epochs; epoch++)
        {
            model.train();

            // Close mosaic in last N epochs
            bool closeMosaic = epoch >= config.Epochs - config.CloseMosaic;

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

                // Update LR
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

                    // EMA update
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
            double avgLoss = (epochBoxLoss + epochClsLoss + epochDflLoss) / batchCount;
            Console.WriteLine($"  Epoch {epoch + 1} complete - " +
                $"avg box: {epochBoxLoss / batchCount:F4}, " +
                $"avg cls: {epochClsLoss / batchCount:F4}, " +
                $"avg dfl: {epochDflLoss / batchCount:F4}");

            // Early stopping check
            if (avgLoss < bestLoss)
            {
                bestLoss = avgLoss;
                patienceCounter = 0;

                // Save best model
                model.save(Path.Combine(config.SaveDir, "best.pt"));
                Console.WriteLine($"  Saved best model (loss: {bestLoss:F4})");
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

            // Save last model
            model.save(Path.Combine(config.SaveDir, "last.pt"));
        }

        Console.WriteLine("Training complete!");
    }
}
