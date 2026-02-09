using TorchSharp;
using static TorchSharp.torch;

namespace YOLOv8.Training;

/// <summary>
/// Trains multiple YOLOv8 variants sequentially and compares their results.
/// </summary>
public class BenchmarkRunner
{
    private readonly TrainConfig baseConfig;
    private readonly Device device;
    private readonly string trainDataDir;
    private readonly string? valDataDir;
    private readonly string[]? classNames;

    public BenchmarkRunner(TrainConfig baseConfig, string trainDataDir, string? valDataDir,
        string[]? classNames = null, Device? device = null)
    {
        this.baseConfig = baseConfig;
        this.trainDataDir = trainDataDir;
        this.valDataDir = valDataDir;
        this.classNames = classNames;
        this.device = device ?? (torch.cuda.is_available() ? torch.CUDA : torch.CPU);
    }

    /// <summary>
    /// Run benchmark across the specified model variants.
    /// </summary>
    /// <param name="variants">Model variant letters, e.g. ["n", "s", "m", "l", "x"]</param>
    public void Run(string[] variants)
    {
        Console.WriteLine("=== YOLOv8 Multi-Variant Benchmark ===");
        Console.WriteLine($"Variants: {string.Join(", ", variants.Select(v => $"YOLOv8{v}"))}");
        Console.WriteLine($"Dataset:  train={trainDataDir}");
        if (valDataDir != null) Console.WriteLine($"          val={valDataDir}");
        Console.WriteLine($"Epochs:   {baseConfig.Epochs}");
        Console.WriteLine($"Batch:    {baseConfig.BatchSize}");
        Console.WriteLine($"ImgSize:  {baseConfig.ImgSize}");
        Console.WriteLine($"Seed:     {baseConfig.Seed}");
        Console.WriteLine();

        var results = new List<TrainResult>();

        for (int i = 0; i < variants.Length; i++)
        {
            string variant = variants[i].ToLowerInvariant().Trim();

            Console.WriteLine(new string('=', 72));
            Console.WriteLine($"  [{i + 1}/{variants.Length}] Training YOLOv8{variant}");
            Console.WriteLine(new string('=', 72));
            Console.WriteLine();

            // Build per-variant config with its own save directory
            var variantConfig = baseConfig with
            {
                ModelVariant = variant,
                SaveDir = Path.Combine(baseConfig.SaveDir, variant)
            };

            var trainer = new Trainer(variantConfig, device);
            var result = trainer.Train(trainDataDir, valDataDir, classNames);
            results.Add(result);

            Console.WriteLine();
        }

        // Print comparison table
        PrintComparisonTable(results);
    }

    /// <summary>
    /// Print a formatted comparison table across all trained variants.
    /// </summary>
    private static void PrintComparisonTable(List<TrainResult> results)
    {
        Console.WriteLine();
        Console.WriteLine(new string('=', 90));
        Console.WriteLine("  MODEL VARIANT COMPARISON");
        Console.WriteLine(new string('=', 90));
        Console.WriteLine();

        Console.WriteLine($"{"Variant",-10} {"Params",>12} {"mAP@0.5",>10} {"mAP50-95",>10} " +
            $"{"Fitness",>10} {"Best Ep",>8} {"Time",>12}");
        Console.WriteLine(new string('-', 76));

        TrainResult? bestResult = null;
        double bestFitness = -1;

        foreach (var r in results)
        {
            string time = FormatDuration(r.TrainingTime);
            string marker = "";

            if (r.BestFitness > bestFitness)
            {
                bestFitness = r.BestFitness;
                bestResult = r;
            }

            Console.WriteLine($"YOLOv8{r.ModelVariant,-4} {r.ParamCount,12:N0} " +
                $"{r.BestMap50,10:F4} {r.BestMap5095,10:F4} " +
                $"{r.BestFitness,10:F4} {r.BestEpoch,8} {time,12}");
        }

        Console.WriteLine(new string('-', 76));

        if (bestResult != null)
        {
            Console.WriteLine();
            Console.WriteLine($"  Best variant: YOLOv8{bestResult.ModelVariant} " +
                $"(fitness={bestResult.BestFitness:F4}, " +
                $"mAP50={bestResult.BestMap50:F4}, " +
                $"mAP50-95={bestResult.BestMap5095:F4})");

            // Print per-class AP for best variant
            if (bestResult.PerClassAP50.Length > 0 && bestResult.ClassNames.Length > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"  Per-Class AP@0.5 for best variant (YOLOv8{bestResult.ModelVariant}):");
                for (int c = 0; c < Math.Min(bestResult.PerClassAP50.Length, bestResult.ClassNames.Length); c++)
                {
                    Console.WriteLine($"    {bestResult.ClassNames[c],-20} {bestResult.PerClassAP50[c]:F4}");
                }
            }
        }

        Console.WriteLine();
    }

    private static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s";
        if (ts.TotalMinutes >= 1)
            return $"{(int)ts.TotalMinutes}m {ts.Seconds:D2}s";
        return $"{ts.Seconds}s";
    }
}
