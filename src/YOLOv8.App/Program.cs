using TorchSharp;
using YOLOv8.Core.Models;
using YOLOv8.Data.Datasets;
using YOLOv8.Inference;
using YOLOv8.Inference.Metrics;
using YOLOv8.Training;
using YOLOv8.Training.Config;
using static TorchSharp.torch;

namespace YOLOv8.App;

/// <summary>
/// YOLOv8 C# TorchSharp CLI Application.
/// 
/// Usage:
///   dotnet run -- train --data coco128.yaml --model yolov8n --epochs 100
///   dotnet run -- predict --model best.pt --source image.jpg [--conf 0.25] [--iou 0.45]
///   dotnet run -- val --data coco128.yaml --model best.pt
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("YOLOv8 C# TorchSharp Implementation");
        Console.WriteLine($"TorchSharp version: {torch.__version__}");
        Console.WriteLine($"CUDA available: {torch.cuda.is_available()}");
        Console.WriteLine();

        if (args.Length == 0)
        {
            PrintUsage();
            return 1;
        }

        string command = args[0].ToLowerInvariant();
        var options = ParseOptions(args.Skip(1).ToArray());

        try
        {
            return command switch
            {
                "train" => RunTrain(options),
                "predict" or "detect" => RunPredict(options),
                "val" or "validate" => RunValidate(options),
                "export" => RunExport(options),
                "generate-data" => RunGenerateData(options),
                "--help" or "-h" or "help" => PrintUsage(),
                _ => PrintUnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            Console.Error.WriteLine(ex.StackTrace);
            return 1;
        }
    }

    /// <summary>
    /// Run training command.
    /// </summary>
    private static int RunTrain(Dictionary<string, string> options)
    {
        // Parse required options
        string dataPath = GetRequired(options, "data", "Dataset YAML path is required (--data)");
        string modelVariant = options.GetValueOrDefault("model", "yolov8n");

        // Extract variant letter from model name (e.g., "yolov8n" -> "n")
        string variant = ExtractVariant(modelVariant);

        // Parse optional options
        int epochs = int.Parse(options.GetValueOrDefault("epochs", "100"));
        int batchSize = int.Parse(options.GetValueOrDefault("batch", "16"));
        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "640"));
        string saveDir = options.GetValueOrDefault("project", "runs/train");
        string name = options.GetValueOrDefault("name", "exp");
        string? hypPath = options.GetValueOrDefault("hyp", null);
        string optimizer = options.GetValueOrDefault("optimizer", "auto");
        double lr0 = double.Parse(options.GetValueOrDefault("lr0", "0.01"));
        bool cosLr = options.ContainsKey("cos_lr");

        // Load dataset config
        Console.WriteLine($"Loading dataset config: {dataPath}");
        var dataConfig = DatasetConfig.Load(dataPath);
        Console.WriteLine($"  Classes: {dataConfig.Nc} ({string.Join(", ", dataConfig.Names.Take(5))}" +
            (dataConfig.Names.Count > 5 ? $"... +{dataConfig.Names.Count - 5} more" : "") + ")");
        Console.WriteLine($"  Train: {dataConfig.Train}");
        Console.WriteLine($"  Val: {dataConfig.Val}");

        // Load hyperparameters (from YAML or defaults)
        TrainConfig trainConfig;
        if (hypPath != null && File.Exists(hypPath))
        {
            Console.WriteLine($"Loading hyperparameters: {hypPath}");
            var hypConfig = HyperparamConfig.Load(hypPath);
            trainConfig = hypConfig.ToTrainConfig(dataConfig.Nc, variant,
                Path.Combine(saveDir, name));
        }
        else
        {
            trainConfig = new TrainConfig
            {
                Epochs = epochs,
                BatchSize = batchSize,
                ImgSize = imgSize,
                NumClasses = dataConfig.Nc,
                ModelVariant = variant,
                Optimizer = optimizer,
                Lr0 = lr0,
                CosLR = cosLr,
                SaveDir = Path.Combine(saveDir, name)
            };
        }

        // Override with explicit CLI args
        if (options.ContainsKey("epochs"))
            trainConfig = trainConfig with { Epochs = epochs };
        if (options.ContainsKey("batch"))
            trainConfig = trainConfig with { BatchSize = batchSize };
        if (options.ContainsKey("imgsz"))
            trainConfig = trainConfig with { ImgSize = imgSize };

        // Determine device
        var device = GetDevice(options);

        // Run training
        var trainer = new Trainer(trainConfig, device);
        trainer.Train(dataConfig.Train, dataConfig.Val);

        return 0;
    }

    /// <summary>
    /// Run prediction/inference command.
    /// </summary>
    private static int RunPredict(Dictionary<string, string> options)
    {
        string modelPath = GetRequired(options, "model", "Model path is required (--model)");
        string source = GetRequired(options, "source", "Image source is required (--source)");

        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "640"));
        double conf = double.Parse(options.GetValueOrDefault("conf", "0.25"));
        double iou = double.Parse(options.GetValueOrDefault("iou", "0.45"));
        int maxDet = int.Parse(options.GetValueOrDefault("max_det", "300"));
        int nc = int.Parse(options.GetValueOrDefault("nc", "80"));
        string variant = ExtractVariant(options.GetValueOrDefault("variant", "n"));

        var device = GetDevice(options);

        Console.WriteLine($"Loading model: {modelPath}");

        // Create model and load weights
        using var model = new YOLOv8Model("yolov8", nc, variant, device);
        if (File.Exists(modelPath))
        {
            model.load(modelPath);
            Console.WriteLine("  Model weights loaded.");
        }

        using var predictor = new Predictor(model, imgSize, conf, iou, maxDet, device);

        // Handle single file or directory
        var imageFiles = new List<string>();
        if (File.Exists(source))
        {
            imageFiles.Add(source);
        }
        else if (Directory.Exists(source))
        {
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".webp" };
            imageFiles.AddRange(
                Directory.GetFiles(source, "*.*", SearchOption.AllDirectories)
                    .Where(f => extensions.Contains(Path.GetExtension(f)))
                    .OrderBy(f => f));
        }
        else
        {
            Console.Error.WriteLine($"Source not found: {source}");
            return 1;
        }

        Console.WriteLine($"Processing {imageFiles.Count} image(s)...");
        Console.WriteLine();

        foreach (var imagePath in imageFiles)
        {
            Console.WriteLine($"Image: {imagePath}");
            var detections = predictor.Predict(imagePath);

            if (detections.Count == 0)
            {
                Console.WriteLine("  No detections.");
            }
            else
            {
                foreach (var det in detections)
                {
                    Console.WriteLine($"  Class {det.ClassId}: " +
                        $"[{det.X1:F1}, {det.Y1:F1}, {det.X2:F1}, {det.Y2:F1}] " +
                        $"conf={det.Confidence:F3}");
                }
            }
            Console.WriteLine();
        }

        Console.WriteLine($"Total: {imageFiles.Count} images processed.");
        return 0;
    }

    /// <summary>
    /// Run validation command.
    /// </summary>
    private static int RunValidate(Dictionary<string, string> options)
    {
        string dataPath = GetRequired(options, "data", "Dataset YAML path is required (--data)");
        string modelPath = GetRequired(options, "model", "Model path is required (--model)");

        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "640"));
        int batchSize = int.Parse(options.GetValueOrDefault("batch", "16"));
        double conf = double.Parse(options.GetValueOrDefault("conf", "0.001"));
        double iou = double.Parse(options.GetValueOrDefault("iou", "0.7"));
        string variant = ExtractVariant(options.GetValueOrDefault("variant", "n"));

        var device = GetDevice(options);

        // Load dataset config
        Console.WriteLine($"Loading dataset config: {dataPath}");
        var dataConfig = DatasetConfig.Load(dataPath);
        Console.WriteLine($"  Classes: {dataConfig.Nc}");
        Console.WriteLine($"  Val: {dataConfig.Val}");

        if (!Directory.Exists(dataConfig.Val))
        {
            Console.Error.WriteLine($"Validation directory not found: {dataConfig.Val}");
            return 1;
        }

        // Create model and load weights
        Console.WriteLine($"Loading model: {modelPath}");
        using var model = new YOLOv8Model("yolov8", dataConfig.Nc, variant, device);
        if (File.Exists(modelPath))
        {
            model.load(modelPath);
            Console.WriteLine("  Model weights loaded.");
        }
        model.eval();

        // Create validation dataset
        var valPipeline = YOLOv8.Data.Augmentation.AugmentationPipeline.CreateValPipeline(imgSize);
        var valDataset = new YOLODataset(dataConfig.Val, imgSize, valPipeline, useMosaic: false);
        valDataset.CacheLabels();
        Console.WriteLine($"  Validation samples: {valDataset.Count}");

        // Run validation
        var metric = new MAPMetric(dataConfig.Nc);

        Console.WriteLine("Running validation...");
        int batchCount = 0;

        using (torch.no_grad())
        {
            foreach (var (images, gtBboxes, gtLabels, maskGT) in
                valDataset.GetBatches(batchSize, shuffle: false))
            {
                using var scope = torch.NewDisposeScope();

                var imgs = images.to(device);
                var (boxes, scores, _) = model.forward(imgs);

                long batch = boxes.shape[0];
                for (long b = 0; b < batch; b++)
                {
                    // Get predictions
                    var boxesT = boxes[b].T;    // (N, 4)
                    var scoresT = scores[b].T;  // (N, nc)

                    var (maxScores, maxClasses) = scoresT.max(dim: -1);
                    var confMask = maxScores > conf;

                    var filteredBoxes = boxesT[confMask];
                    var filteredScores = maxScores[confMask];
                    var filteredClasses = maxClasses[confMask];

                    var predBoxesXyxy = YOLOv8.Core.Utils.BboxUtils.Xywh2Xyxy(filteredBoxes);

                    // Get GT
                    var gtMask = maskGT[b, .., 0].to(ScalarType.Bool);
                    var gtBoxesImg = gtBboxes[b][gtMask] * imgSize;
                    var gtClassesImg = gtLabels[b][gtMask][.., 0].to(ScalarType.Int64);

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
                            predBoxArr[i, 0] = predData[i * 4];
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
                            gtBoxArr[i, 0] = gtData[i * 4];
                            gtBoxArr[i, 1] = gtData[i * 4 + 1];
                            gtBoxArr[i, 2] = gtData[i * 4 + 2];
                            gtBoxArr[i, 3] = gtData[i * 4 + 3];
                            gtClassArr[i] = (int)gtClsData[i];
                        }
                    }

                    metric.Update(predBoxArr, predScoreArr, predClassArr, gtBoxArr, gtClassArr);
                }

                batchCount++;
                if (batchCount % 10 == 0)
                    Console.Write($"\r  Processed {batchCount} batches...");
            }
        }

        Console.WriteLine();

        var (map50, map5095, perClassAP50) = metric.Compute();

        Console.WriteLine();
        Console.WriteLine("=== Validation Results ===");
        Console.WriteLine($"  mAP@0.5:      {map50:F4}");
        Console.WriteLine($"  mAP@0.5:0.95: {map5095:F4}");
        Console.WriteLine();

        // Per-class AP
        if (dataConfig.Names.Count > 0)
        {
            Console.WriteLine("  Per-class AP@0.5:");
            for (int c = 0; c < Math.Min(perClassAP50.Length, dataConfig.Names.Count); c++)
            {
                if (perClassAP50[c] > 0)
                    Console.WriteLine($"    {dataConfig.Names[c],-20} {perClassAP50[c]:F4}");
            }
        }

        return 0;
    }

    /// <summary>
    /// Generate synthetic test data for training validation.
    /// </summary>
    private static int RunGenerateData(Dictionary<string, string> options)
    {
        string outputDir = options.GetValueOrDefault("output", "testdata") ?? "testdata";
        int numTrain = int.Parse(options.GetValueOrDefault("num_train", "32") ?? "32");
        int numVal = int.Parse(options.GetValueOrDefault("num_val", "8") ?? "8");
        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "128") ?? "128");
        int nc = int.Parse(options.GetValueOrDefault("nc", "3") ?? "3");

        TestDataGenerator.Generate(outputDir, numTrain, numVal, imgSize, nc);
        return 0;
    }

    /// <summary>
    /// Export model (placeholder for future ONNX export).
    /// </summary>
    private static int RunExport(Dictionary<string, string> options)
    {
        Console.WriteLine("Export is not yet implemented. Coming soon!");
        return 0;
    }

    /// <summary>
    /// Extract variant letter from model name (e.g., "yolov8n" -> "n", "n" -> "n").
    /// </summary>
    private static string ExtractVariant(string modelName)
    {
        modelName = modelName.ToLowerInvariant().Trim();

        // Direct variant letter
        if (modelName.Length == 1 && "nsmlx".Contains(modelName))
            return modelName;

        // yolov8n, yolov8s, etc.
        if (modelName.StartsWith("yolov8") && modelName.Length > 6)
        {
            string suffix = modelName[6..];
            // Could be "n", "s", "m", "l", "x" or with "-cls", "-seg" etc.
            if (suffix.Length >= 1 && "nsmlx".Contains(suffix[0]))
                return suffix[0].ToString();
        }

        // Default to n
        Console.WriteLine($"Warning: Could not parse model variant from '{modelName}', defaulting to 'n'");
        return "n";
    }

    /// <summary>
    /// Get the device based on CLI options.
    /// </summary>
    private static Device GetDevice(Dictionary<string, string> options)
    {
        string deviceStr = options.GetValueOrDefault("device", "auto") ?? "auto";

        if (deviceStr == "auto")
        {
            return torch.cuda.is_available() ? torch.CUDA : torch.CPU;
        }
        else if (deviceStr == "cpu")
        {
            return torch.CPU;
        }
        else if (deviceStr.StartsWith("cuda") || int.TryParse(deviceStr, out _))
        {
            return torch.CUDA;
        }

        return torch.CPU;
    }

    /// <summary>
    /// Parse command-line options into a dictionary.
    /// Supports --key value and --flag formats.
    /// </summary>
    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];

            if (arg.StartsWith("--"))
            {
                string key = arg[2..].Replace("-", "_");

                // Check if next arg is a value or another flag
                if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    options[key] = args[i + 1];
                    i++;
                }
                else
                {
                    options[key] = "true";
                }
            }
            else if (arg.StartsWith("-") && arg.Length == 2)
            {
                // Short flags
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    options[arg[1..]] = args[i + 1];
                    i++;
                }
            }
        }

        return options;
    }

    /// <summary>
    /// Get a required option or throw with a helpful message.
    /// </summary>
    private static string GetRequired(Dictionary<string, string> options, string key, string errorMessage)
    {
        if (options.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        throw new ArgumentException(errorMessage);
    }

    /// <summary>
    /// Print usage information.
    /// </summary>
    private static int PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run -- <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  train     Train a YOLOv8 model");
        Console.WriteLine("  predict   Run inference on images");
        Console.WriteLine("  val       Validate a model on a dataset");
        Console.WriteLine();
        Console.WriteLine("Train options:");
        Console.WriteLine("  --data <path>        Dataset YAML file (required)");
        Console.WriteLine("  --model <name>       Model variant: yolov8n/s/m/l/x (default: yolov8n)");
        Console.WriteLine("  --epochs <n>         Number of epochs (default: 100)");
        Console.WriteLine("  --batch <n>          Batch size (default: 16)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --hyp <path>         Hyperparameter YAML file");
        Console.WriteLine("  --optimizer <name>   Optimizer: SGD/AdamW/auto (default: auto)");
        Console.WriteLine("  --lr0 <n>            Initial learning rate (default: 0.01)");
        Console.WriteLine("  --cos_lr             Use cosine LR schedule");
        Console.WriteLine("  --device <dev>       Device: auto/cpu/cuda/0 (default: auto)");
        Console.WriteLine("  --project <dir>      Save results to project/name (default: runs/train)");
        Console.WriteLine("  --name <name>        Experiment name (default: exp)");
        Console.WriteLine();
        Console.WriteLine("Predict options:");
        Console.WriteLine("  --model <path>       Model weights path (required)");
        Console.WriteLine("  --source <path>      Image file or directory (required)");
        Console.WriteLine("  --conf <n>           Confidence threshold (default: 0.25)");
        Console.WriteLine("  --iou <n>            IoU threshold for NMS (default: 0.45)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --nc <n>             Number of classes (default: 80)");
        Console.WriteLine("  --variant <v>        Model variant for architecture (default: n)");
        Console.WriteLine("  --device <dev>       Device: auto/cpu/cuda/0 (default: auto)");
        Console.WriteLine();
        Console.WriteLine("Val options:");
        Console.WriteLine("  --data <path>        Dataset YAML file (required)");
        Console.WriteLine("  --model <path>       Model weights path (required)");
        Console.WriteLine("  --batch <n>          Batch size (default: 16)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --conf <n>           Confidence threshold (default: 0.001)");
        Console.WriteLine("  --iou <n>            IoU threshold (default: 0.7)");
        Console.WriteLine("  --variant <v>        Model variant for architecture (default: n)");
        Console.WriteLine("  --device <dev>       Device: auto/cpu/cuda/0 (default: auto)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- train --data coco128.yaml --model yolov8n --epochs 100");
        Console.WriteLine("  dotnet run -- predict --model runs/train/exp/weights/best.pt --source image.jpg");
        Console.WriteLine("  dotnet run -- val --data coco128.yaml --model runs/train/exp/weights/best.pt");

        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Use --help for usage information.");
        return 1;
    }
}
