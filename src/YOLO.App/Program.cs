using System.Text.RegularExpressions;
using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Core.Models;
using YOLO.Core.Utils;
using YOLO.Data.Datasets;
using YOLO.Inference;
using YOLO.Inference.Metrics;
using YOLO.Runtime;
using YOLO.Runtime.Results;
using YOLO.Training;
using YOLO.Training.Config;
using YOLO.App.Parity;
using static TorchSharp.torch;

namespace YOLO.App;

/// <summary>
/// YOLO C# TorchSharp CLI Application.
/// Supports multiple model versions (v8, v9, v10, ...) via ModelRegistry.
///
/// Usage:
///   dotnet run -- train --data coco128.yaml --model yolov8n --epochs 100
///   dotnet run -- train --data coco128.yaml --model yolov8n --teacher best.pt --teacher_variant l
///   dotnet run -- bench --data coco128.yaml --models n,s,m --epochs 50
///   dotnet run -- predict --model best.pt --source image.jpg [--conf 0.25] [--iou 0.45]
///   dotnet run -- val --data coco128.yaml --model best.pt
///   dotnet run -- export --model best.pt --format onnx
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        Console.WriteLine("YOLO C# TorchSharp Implementation");
        Console.WriteLine($"TorchSharp version: {torch.__version__}");
        Console.WriteLine($"CUDA available: {torch.cuda.is_available()}");

        // Ensure model registrations are loaded
        InitializeRegistry();

        Console.WriteLine($"Registered model versions: {string.Join(", ", ModelRegistry.GetVersions())}");
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
                "bench" or "benchmark" => RunBenchmark(options),
                "predict" or "detect" => RunPredict(options),
                "val" or "validate" => RunValidate(options),
                "export" => RunExport(options),
                "generate-data" => RunGenerateData(options),
                "parity-freeze" => RunParityFreeze(options),
                "parity-report" => RunParityReport(options),
                "runtime-infer" => RunRuntimeInfer(options),
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
    /// Initialize the model registry with all known model versions.
    /// </summary>
    private static void InitializeRegistry()
    {
        // Trigger static constructors to register model versions
        YOLOv8Model.EnsureRegistered();
        // Future: YOLOv9Model.EnsureRegistered();
        // Future: YOLOv10Model.EnsureRegistered();

        // Register loss factories (done here to avoid circular dependencies)
        Trainer.RegisterLossFactories();
    }

    /// <summary>
    /// Run training command.
    /// </summary>
    private static int RunTrain(Dictionary<string, string> options)
    {
        string dataPath = GetRequired(options, "data", "Dataset YAML path is required (--data)");
        string modelName = options.GetValueOrDefault("model", "yolov8n");
        var (version, variant) = ParseModelName(modelName);

        int epochs = int.Parse(options.GetValueOrDefault("epochs", "100"));
        int batchSize = int.Parse(options.GetValueOrDefault("batch", "16"));
        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "640"));
        string saveDir = options.GetValueOrDefault("project", "runs/train");
        string name = options.GetValueOrDefault("name", "exp");
        string? hypPath = options.TryGetValue("hyp", out var hypVal) ? hypVal : null;
        string optimizer = options.GetValueOrDefault("optimizer", "auto");
        double lr0 = double.Parse(options.GetValueOrDefault("lr0", "0.01"));
        bool cosLr = options.ContainsKey("cos_lr");
        int seed = int.Parse(options.GetValueOrDefault("seed", "0"));

        // Distillation options
        string? teacherPath = options.TryGetValue("teacher", out var tVal) ? tVal : null;
        string teacherVariant = ExtractVariant(options.GetValueOrDefault("teacher_variant", "l"));
        double distillWeight = double.Parse(options.GetValueOrDefault("distill_weight", "1.0"));
        double distillTemp = double.Parse(options.GetValueOrDefault("distill_temp", "20"));
        string distillMode = options.GetValueOrDefault("distill_mode", "logit");

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
            // Apply version from CLI
            trainConfig = trainConfig with { ModelVersion = version };
        }
        else
        {
            trainConfig = new TrainConfig
            {
                ModelVersion = version,
                Epochs = epochs,
                BatchSize = batchSize,
                ImgSize = imgSize,
                NumClasses = dataConfig.Nc,
                ModelVariant = variant,
                Optimizer = optimizer,
                Lr0 = lr0,
                CosLR = cosLr,
                SaveDir = Path.Combine(saveDir, name),
                Seed = seed
            };
        }

        // Override with explicit CLI args
        if (options.ContainsKey("epochs"))
            trainConfig = trainConfig with { Epochs = epochs };
        if (options.ContainsKey("batch"))
            trainConfig = trainConfig with { BatchSize = batchSize };
        if (options.ContainsKey("imgsz"))
            trainConfig = trainConfig with { ImgSize = imgSize };
        if (options.ContainsKey("seed"))
            trainConfig = trainConfig with { Seed = seed };

        // Apply distillation settings
        if (teacherPath != null)
        {
            trainConfig = trainConfig with
            {
                TeacherModelPath = teacherPath,
                TeacherVariant = teacherVariant,
                DistillWeight = distillWeight,
                DistillTemperature = distillTemp,
                DistillMode = distillMode
            };
        }

        var device = GetDevice(options);

        var trainer = new Trainer(trainConfig, device);
        trainer.Train(dataConfig.Train, dataConfig.Val, dataConfig.Names.ToArray());

        return 0;
    }

    /// <summary>
    /// Run multi-variant benchmark command.
    /// </summary>
    private static int RunBenchmark(Dictionary<string, string> options)
    {
        string dataPath = GetRequired(options, "data", "Dataset YAML path is required (--data)");
        string modelsStr = options.GetValueOrDefault("models", "n,s,m,l,x");
        string versionStr = options.GetValueOrDefault("version", "v8");

        int epochs = int.Parse(options.GetValueOrDefault("epochs", "100"));
        int batchSize = int.Parse(options.GetValueOrDefault("batch", "16"));
        int imgSize = int.Parse(options.GetValueOrDefault("imgsz", "640"));
        string saveDir = options.GetValueOrDefault("project", "runs/bench");
        string name = options.GetValueOrDefault("name", "exp");
        string optimizer = options.GetValueOrDefault("optimizer", "auto");
        double lr0 = double.Parse(options.GetValueOrDefault("lr0", "0.01"));
        bool cosLr = options.ContainsKey("cos_lr");
        int seed = int.Parse(options.GetValueOrDefault("seed", "0"));

        // Parse variant list
        var variants = modelsStr.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => ExtractVariant(v))
            .ToArray();

        // Load dataset config
        Console.WriteLine($"Loading dataset config: {dataPath}");
        var dataConfig = DatasetConfig.Load(dataPath);
        Console.WriteLine($"  Classes: {dataConfig.Nc} ({string.Join(", ", dataConfig.Names.Take(5))}" +
            (dataConfig.Names.Count > 5 ? $"... +{dataConfig.Names.Count - 5} more" : "") + ")");
        Console.WriteLine($"  Train: {dataConfig.Train}");
        Console.WriteLine($"  Val: {dataConfig.Val}");
        Console.WriteLine();

        var baseConfig = new TrainConfig
        {
            ModelVersion = versionStr,
            Epochs = epochs,
            BatchSize = batchSize,
            ImgSize = imgSize,
            NumClasses = dataConfig.Nc,
            Optimizer = optimizer,
            Lr0 = lr0,
            CosLR = cosLr,
            SaveDir = Path.Combine(saveDir, name),
            Seed = seed
        };

        var device = GetDevice(options);

        var runner = new BenchmarkRunner(baseConfig, dataConfig.Train, dataConfig.Val,
            dataConfig.Names.ToArray(), device);
        runner.Run(variants);

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

        string versionStr = options.GetValueOrDefault("version", "v8");
        string variant = ExtractVariant(options.GetValueOrDefault("variant", "n"));

        var device = GetDevice(options);

        Console.WriteLine($"Loading model: {modelPath}");

        using var model = ModelRegistry.Create(versionStr, nc, variant, device);
        if (File.Exists(modelPath))
        {
            WeightLoader.SmartLoad(model, modelPath);
        }

        using var predictor = new Predictor(model, imgSize, conf, iou, maxDet, device);

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

        string versionStr = options.GetValueOrDefault("version", "v8");
        string variant = ExtractVariant(options.GetValueOrDefault("variant", "n"));

        var device = GetDevice(options);

        Console.WriteLine($"Loading dataset config: {dataPath}");
        var dataConfig = DatasetConfig.Load(dataPath);
        Console.WriteLine($"  Classes: {dataConfig.Nc}");
        Console.WriteLine($"  Val: {dataConfig.Val}");

        if (!Directory.Exists(dataConfig.Val))
        {
            Console.Error.WriteLine($"Validation directory not found: {dataConfig.Val}");
            return 1;
        }

        Console.WriteLine($"Loading model: {modelPath}");
        using var model = ModelRegistry.Create(versionStr, dataConfig.Nc, variant, device);
        if (File.Exists(modelPath))
        {
            WeightLoader.SmartLoad(model, modelPath);
        }
        model.eval();

        var valPipeline = YOLO.Data.Augmentation.AugmentationPipeline.CreateValPipeline(imgSize);
        var valDataset = new YOLODataset(dataConfig.Val, imgSize, valPipeline, useMosaic: false);
        valDataset.CacheLabels();
        Console.WriteLine($"  Validation samples: {valDataset.Count}");

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
                    var boxesT = boxes[b].T;
                    var scoresT = scores[b].T;

                    var (maxScores, maxClasses) = scoresT.max(dim: -1);
                    var confMask = maxScores > conf;

                    var filteredBoxes = boxesT[confMask];
                    var filteredScores = maxScores[confMask];
                    var filteredClasses = maxClasses[confMask];

                    var predBoxesXyxy = YOLO.Core.Utils.BboxUtils.Xywh2Xyxy(filteredBoxes);

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
        double fitness = Trainer.ComputeFitness(map50, map5095);

        Console.WriteLine();
        Console.WriteLine("=== Validation Results ===");
        Console.WriteLine($"  mAP@0.5:      {map50:F4}");
        Console.WriteLine($"  mAP@0.5:0.95: {map5095:F4}");
        Console.WriteLine($"  Fitness:       {fitness:F4}");
        Console.WriteLine();

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
    /// Freeze Python baseline into parity/baseline.lock.json.
    /// </summary>
    private static int RunParityFreeze(Dictionary<string, string> options)
    {
        string root = options.GetValueOrDefault("root", FindWorkspaceRoot()) ?? FindWorkspaceRoot();
        string outDir = options.GetValueOrDefault("output", Path.Combine(root, "csharp", "parity"))!;
        var lockPath = ParityBaselineService.Freeze(root, outDir);
        Console.WriteLine($"Baseline lock generated: {lockPath}");
        return 0;
    }

    /// <summary>
    /// Verify current workspace against baseline lock.
    /// </summary>
    private static int RunParityReport(Dictionary<string, string> options)
    {
        string root = options.GetValueOrDefault("root", FindWorkspaceRoot()) ?? FindWorkspaceRoot();
        string lockPath = options.GetValueOrDefault(
            "lock",
            Path.Combine(root, "csharp", "parity", "baseline.lock.json"))!;

        var report = ParityBaselineService.Verify(root, lockPath);
        Console.WriteLine($"Baseline: {report.BaselineTag}");
        Console.WriteLine($"Lock file: {report.BaselineLockPath}");
        Console.WriteLine($"All key files matched: {report.AllKeyFilesMatched}");
        Console.WriteLine();
        foreach (var item in report.KeyFileChecks)
        {
            Console.WriteLine($"{(item.IsMatch ? "OK " : "DIFF")} {item.RelativePath}");
        }
        return report.AllKeyFilesMatched ? 0 : 2;
    }

    /// <summary>
    /// Export model (placeholder for ONNX/TorchScript export).
    /// </summary>
    private static int RunExport(Dictionary<string, string> options)
    {
        Console.WriteLine("Export is not yet fully implemented.");
        Console.WriteLine("Use the YOLO.Export project or YOLO.WinForms application for export functionality.");
        Console.WriteLine();
        Console.WriteLine("Planned formats: ONNX, TorchScript");
        return 0;
    }

    /// <summary>
    /// Run inference using the YOLO.Runtime framework (supports .onnx and .pt).
    /// Demonstrates the minimalist API of YoloInfer.
    /// </summary>
    private static int RunRuntimeInfer(Dictionary<string, string> options)
    {
        string modelPath = GetRequired(options, "model", "Model path is required (--model)");
        string source = GetRequired(options, "source", "Source is required (--source)");

        // Build options from CLI
        var yoloOptions = new YoloOptions
        {
            Confidence = float.Parse(options.GetValueOrDefault("conf", "0.25")),
            IoU = float.Parse(options.GetValueOrDefault("iou", "0.45")),
            ImgSize = int.Parse(options.GetValueOrDefault("imgsz", "640")),
            MaxDetections = int.Parse(options.GetValueOrDefault("max_det", "300")),
            ModelVersion = options.TryGetValue("version", out var ver) ? ver : null,
            ModelVariant = options.GetValueOrDefault("variant", "n"),
            NumClasses = int.Parse(options.GetValueOrDefault("nc", "80")),
        };

        // Device
        string deviceStr = options.GetValueOrDefault("device", "auto") ?? "auto";
        yoloOptions.Device = deviceStr switch
        {
            "cpu" => YOLO.Runtime.DeviceType.Cpu,
            "gpu" or "cuda" => YOLO.Runtime.DeviceType.Gpu,
            _ => YOLO.Runtime.DeviceType.Auto
        };

        bool draw = options.ContainsKey("draw");
        string? saveDir = options.TryGetValue("save_dir", out var sd) ? sd : null;

        Console.WriteLine($"[YOLO.Runtime] Loading model: {modelPath}");
        Console.WriteLine($"  Backend: {(Path.GetExtension(modelPath).ToLower() == ".onnx" ? "ONNX Runtime" : "TorchSharp")}");
        Console.WriteLine($"  Device: {yoloOptions.Device}");
        Console.WriteLine($"  Image size: {yoloOptions.ImgSize}");
        Console.WriteLine($"  Confidence: {yoloOptions.Confidence}");
        Console.WriteLine($"  IoU: {yoloOptions.IoU}");
        Console.WriteLine();

        using var yolo = new YoloInfer(modelPath, yoloOptions);

        // Check if source is a PDF
        if (source.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
        {
            Console.WriteLine($"[PDF Mode] Processing: {source}");
            
            string? savePdfPath = options.TryGetValue("save_pdf", out var sp) ? sp : null;
            
            PageResult[] pages;
            if (savePdfPath != null)
            {
                // Detect and save annotated PDF
                pages = yolo.DetectPdfAndSave(source, savePdfPath);
                Console.WriteLine($"Annotated PDF saved to: {savePdfPath}");
            }
            else
            {
                // Just detect without saving
                pages = yolo.DetectPdf(source);
            }

            foreach (var page in pages)
            {
                Console.WriteLine($"  Page {page.PageIndex}: {page.Detections.Length} detections");
                foreach (var det in page.Detections)
                {
                    Console.WriteLine($"    {det}");
                }
            }
            Console.WriteLine($"Total: {pages.Length} pages, {pages.Sum(p => p.Detections.Length)} detections");
            return 0;
        }

        // Collect image files
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

        if (imageFiles.Count > 1)
        {
            // Batch mode
            var batchResults = yolo.DetectBatch(imageFiles.ToArray());
            for (int i = 0; i < imageFiles.Count; i++)
            {
                Console.WriteLine($"Image: {Path.GetFileName(imageFiles[i])} -> {batchResults[i].Length} detections");
                foreach (var det in batchResults[i])
                {
                    Console.WriteLine($"  {det}");
                }
            }
        }
        else
        {
            // Single image
            var imagePath = imageFiles[0];

            if (draw)
            {
                var (detections, annotated) = yolo.DetectAndDrawDetailed(imagePath);
                Console.WriteLine($"Image: {Path.GetFileName(imagePath)} -> {detections.Length} detections");
                foreach (var det in detections)
                {
                    Console.WriteLine($"  {det}");
                }

                // Save annotated image
                var outPath = saveDir is not null
                    ? Path.Combine(saveDir, "annotated_" + Path.GetFileName(imagePath))
                    : "annotated_" + Path.GetFileNameWithoutExtension(imagePath) + ".png";

                if (saveDir is not null)
                    Directory.CreateDirectory(saveDir);

                File.WriteAllBytes(outPath, annotated);
                Console.WriteLine($"Annotated image saved: {outPath}");
            }
            else
            {
                var detections = yolo.Detect(imagePath);
                Console.WriteLine($"Image: {Path.GetFileName(imagePath)} -> {detections.Length} detections");
                foreach (var det in detections)
                {
                    Console.WriteLine($"  {det}");
                }
            }
        }

        return 0;
    }

    /// <summary>
    /// Parse a model name into (version, variant).
    /// Supports: "yolov8n", "yolov9s", "yolov10m", "v8n", "n" (defaults to v8).
    /// </summary>
    internal static (string version, string variant) ParseModelName(string modelName)
    {
        modelName = modelName.ToLowerInvariant().Trim();

        // Single letter variant -> default to v8
        if (modelName.Length == 1 && "nsmlx".Contains(modelName))
            return ("v8", modelName);

        // Match "yolov{VERSION}{VARIANT}" pattern, e.g. "yolov8n", "yolov10m"
        var match = Regex.Match(modelName, @"^yolov?(\d+)([nsmlx])$");
        if (match.Success)
        {
            string version = $"v{match.Groups[1].Value}";
            string variant = match.Groups[2].Value;
            return (version, variant);
        }

        // Match "v{VERSION}{VARIANT}", e.g. "v8n", "v10s"
        match = Regex.Match(modelName, @"^v(\d+)([nsmlx])$");
        if (match.Success)
        {
            string version = $"v{match.Groups[1].Value}";
            string variant = match.Groups[2].Value;
            return (version, variant);
        }

        // Legacy: "yolov8n" style without "v" prefix pattern
        if (modelName.StartsWith("yolo") && modelName.Length > 4)
        {
            string rest = modelName[4..];
            if (rest.StartsWith("v"))
                rest = rest[1..];

            // Try to extract version number and variant
            match = Regex.Match(rest, @"^(\d+)([nsmlx])$");
            if (match.Success)
            {
                return ($"v{match.Groups[1].Value}", match.Groups[2].Value);
            }
        }

        Console.WriteLine($"Warning: Could not parse model name '{modelName}', defaulting to v8n");
        return ("v8", "n");
    }

    /// <summary>
    /// Extract variant letter from model name (e.g., "yolov8n" -> "n", "n" -> "n").
    /// For backward compatibility with simple variant strings.
    /// </summary>
    private static string ExtractVariant(string modelName)
    {
        modelName = modelName.ToLowerInvariant().Trim();

        if (modelName.Length == 1 && "nsmlx".Contains(modelName))
            return modelName;

        var (_, variant) = ParseModelName(modelName);
        return variant;
    }

    /// <summary>
    /// Get the device based on CLI options.
    /// </summary>
    private static Device GetDevice(Dictionary<string, string> options)
    {
        string deviceStr = options.GetValueOrDefault("device", "auto") ?? "auto";

        if (deviceStr == "auto")
            return torch.cuda.is_available() ? torch.CUDA : torch.CPU;
        else if (deviceStr == "cpu")
            return torch.CPU;
        else if (deviceStr.StartsWith("cuda") || int.TryParse(deviceStr, out _))
            return torch.CUDA;

        return torch.CPU;
    }

    /// <summary>
    /// Parse command-line options into a dictionary.
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
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-"))
                {
                    options[arg[1..]] = args[i + 1];
                    i++;
                }
            }
        }

        return options;
    }

    private static string GetRequired(Dictionary<string, string> options, string key, string errorMessage)
    {
        if (options.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value))
            return value;
        throw new ArgumentException(errorMessage);
    }

    private static int PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run -- <command> [options]");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  train          Train a YOLO model");
        Console.WriteLine("  bench          Benchmark multiple model variants (n/s/m/l/x)");
        Console.WriteLine("  predict        Run inference on images (TorchSharp)");
        Console.WriteLine("  runtime-infer  Run inference via YOLO.Runtime (.onnx/.pt, PDF, batch, draw)");
        Console.WriteLine("  parity-freeze  Freeze Python baseline lock file");
        Console.WriteLine("  parity-report  Verify current source against baseline lock");
        Console.WriteLine("  val            Validate a model on a dataset");
        Console.WriteLine("  export         Export model to ONNX/TorchScript");
        Console.WriteLine();
        Console.WriteLine("Train options:");
        Console.WriteLine("  --data <path>        Dataset YAML file (required)");
        Console.WriteLine("  --model <name>       Model: yolov8n/yolov9s/yolov10m/... (default: yolov8n)");
        Console.WriteLine("  --epochs <n>         Number of epochs (default: 100)");
        Console.WriteLine("  --batch <n>          Batch size (default: 16)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --hyp <path>         Hyperparameter YAML file");
        Console.WriteLine("  --optimizer <name>   Optimizer: SGD/AdamW/auto (default: auto)");
        Console.WriteLine("  --lr0 <n>            Initial learning rate (default: 0.01)");
        Console.WriteLine("  --cos_lr             Use cosine LR schedule");
        Console.WriteLine("  --seed <n>           Random seed for reproducibility (default: 0)");
        Console.WriteLine("  --device <dev>       Device: auto/cpu/cuda/0 (default: auto)");
        Console.WriteLine("  --project <dir>      Save results to project/name (default: runs/train)");
        Console.WriteLine("  --name <name>        Experiment name (default: exp)");
        Console.WriteLine();
        Console.WriteLine("Distillation options (use with train):");
        Console.WriteLine("  --teacher <path>           Teacher weights (.pt)");
        Console.WriteLine("  --teacher_variant <v>      Teacher variant: n/s/m/l/x (default: l)");
        Console.WriteLine("  --distill_weight <n>       Distillation loss weight (default: 1.0)");
        Console.WriteLine("  --distill_temp <n>         Temperature for soft targets (default: 20)");
        Console.WriteLine("  --distill_mode <mode>      logit/feature/both (default: logit)");
        Console.WriteLine();
        Console.WriteLine("Benchmark options:");
        Console.WriteLine("  --data <path>        Dataset YAML file (required)");
        Console.WriteLine("  --models <list>      Comma-separated variants: n,s,m,l,x (default: n,s,m,l,x)");
        Console.WriteLine("  --version <v>        Model version: v8/v9/v10 (default: v8)");
        Console.WriteLine("  --epochs <n>         Number of epochs (default: 100)");
        Console.WriteLine("  --batch <n>          Batch size (default: 16)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --seed <n>           Random seed (default: 0)");
        Console.WriteLine("  --project <dir>      Save results directory (default: runs/bench)");
        Console.WriteLine("  --name <name>        Experiment name (default: exp)");
        Console.WriteLine();
        Console.WriteLine("Predict options:");
        Console.WriteLine("  --model <path>       Model weights path (required)");
        Console.WriteLine("  --source <path>      Image file or directory (required)");
        Console.WriteLine("  --version <v>        Model version: v8/v9/v10 (default: v8)");
        Console.WriteLine("  --variant <v>        Model variant: n/s/m/l/x (default: n)");
        Console.WriteLine("  --conf <n>           Confidence threshold (default: 0.25)");
        Console.WriteLine("  --iou <n>            IoU threshold for NMS (default: 0.45)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --nc <n>             Number of classes (default: 80)");
        Console.WriteLine("  --device <dev>       Device (default: auto)");
        Console.WriteLine();
        Console.WriteLine("Val options:");
        Console.WriteLine("  --data <path>        Dataset YAML file (required)");
        Console.WriteLine("  --model <path>       Model weights path (required)");
        Console.WriteLine("  --version <v>        Model version: v8/v9/v10 (default: v8)");
        Console.WriteLine("  --variant <v>        Model variant: n/s/m/l/x (default: n)");
        Console.WriteLine("  --batch <n>          Batch size (default: 16)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --conf <n>           Confidence threshold (default: 0.001)");
        Console.WriteLine("  --iou <n>            IoU threshold (default: 0.7)");
        Console.WriteLine("  --device <dev>       Device (default: auto)");
        Console.WriteLine();
        Console.WriteLine("Runtime-infer options (YOLO.Runtime framework):");
        Console.WriteLine("  --model <path>       .onnx or .pt model file (required)");
        Console.WriteLine("  --source <path>      Image file, directory, or PDF file (required)");
        Console.WriteLine("  --version <v>        Model version: v8/v9/v10 (default: auto-detect)");
        Console.WriteLine("  --variant <v>        Model variant for .pt: n/s/m/l/x (default: n)");
        Console.WriteLine("  --conf <n>           Confidence threshold (default: 0.25)");
        Console.WriteLine("  --iou <n>            IoU threshold (default: 0.45)");
        Console.WriteLine("  --imgsz <n>          Image size (default: 640)");
        Console.WriteLine("  --nc <n>             Number of classes for .pt (default: 80)");
        Console.WriteLine("  --device <dev>       cpu/gpu/auto (default: auto)");
        Console.WriteLine("  --draw               Draw detection boxes on image");
        Console.WriteLine("  --save_dir <dir>     Save annotated images to directory");
        Console.WriteLine("  --save_pdf <path>    Save annotated PDF to file (for PDF source)");
        Console.WriteLine();
        Console.WriteLine("Examples:");
        Console.WriteLine("  dotnet run -- train --data coco128.yaml --model yolov8n --epochs 100");
        Console.WriteLine("  dotnet run -- train --data coco128.yaml --model yolov9s --epochs 100");
        Console.WriteLine("  dotnet run -- train --data coco128.yaml --model yolov8n --teacher yolov8s.pt --teacher_variant s");
        Console.WriteLine("  dotnet run -- bench --data coco128.yaml --models n,s --version v8 --epochs 50 --seed 42");
        Console.WriteLine("  dotnet run -- predict --model runs/train/exp/weights/best.pt --source image.jpg --version v8");
        Console.WriteLine("  dotnet run -- val --data coco128.yaml --model runs/train/exp/weights/best.pt --version v8");
        Console.WriteLine();
        Console.WriteLine("  # YOLO.Runtime examples:");
        Console.WriteLine("  dotnet run -- runtime-infer --model yolov8n.onnx --source image.jpg");
        Console.WriteLine("  dotnet run -- runtime-infer --model yolov8n.onnx --source ./images/ --device gpu");
        Console.WriteLine("  dotnet run -- runtime-infer --model yolov8n.onnx --source image.jpg --draw --save_dir results/");
        Console.WriteLine("  dotnet run -- runtime-infer --model yolov8n.onnx --source document.pdf");
        Console.WriteLine("  dotnet run -- runtime-infer --model yolov8n.onnx --source document.pdf --save_pdf annotated.pdf");
        Console.WriteLine("  dotnet run -- parity-freeze --root D:\\Code\\YOLOv8");
        Console.WriteLine("  dotnet run -- parity-report --lock D:\\Code\\YOLOv8\\csharp\\parity\\baseline.lock.json");

        return 0;
    }

    private static int PrintUnknownCommand(string command)
    {
        Console.Error.WriteLine($"Unknown command: {command}");
        Console.Error.WriteLine("Use --help for usage information.");
        return 1;
    }

    private static string FindWorkspaceRoot()
    {
        // Start from current process directory and walk up until we find ultralytics/ + csharp/.
        var dir = Directory.GetCurrentDirectory();
        for (int i = 0; i < 8; i++)
        {
            var hasU = Directory.Exists(Path.Combine(dir, "ultralytics"));
            var hasC = Directory.Exists(Path.Combine(dir, "csharp"));
            if (hasU && hasC)
                return dir;
            var parent = Directory.GetParent(dir);
            if (parent is null)
                break;
            dir = parent.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
