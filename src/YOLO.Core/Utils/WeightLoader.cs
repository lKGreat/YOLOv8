using System.Text.RegularExpressions;
using TorchSharp;
using YOLO.Core.Abstractions;
using YOLO.Core.Models;
using static TorchSharp.torch;

namespace YOLO.Core.Utils;

/// <summary>
/// Loads weights from Python PyTorch checkpoint files into C# TorchSharp YOLOv models.
///
/// Handles the key name mapping between Python ultralytics naming convention
/// (e.g. "model.0.conv.weight") and C# TorchSharp naming convention
/// (e.g. "backbone0.conv.weight").
///
/// Supports:
///   - Full ultralytics checkpoint files (.pt with 'model' key)
///   - Plain state_dict files
///   - Automatic fp16 → fp32 conversion
///   - Strict and non-strict loading modes
///   - Detect, Segment, Pose, and Classify model architectures
/// </summary>
public static class WeightLoader
{
    /// <summary>
    /// Supported model task types for weight mapping.
    /// </summary>
    public enum TaskType { Detect, Segment, Pose, Classify }

    /// <summary>
    /// Layer index to C# field name mapping for detection/segment/pose architecture.
    /// Maps Python's "model.N" prefix to C#'s field name prefix.
    /// </summary>
    private static readonly Dictionary<int, string> DetectLayerMap = new()
    {
        [0] = "b0",     // Conv stem
        [1] = "b1",     // Conv
        [2] = "b2",     // C2f
        [3] = "b3",     // Conv
        [4] = "b4",     // C2f
        [5] = "b5",     // Conv
        [6] = "b6",     // C2f
        [7] = "b7",     // Conv
        [8] = "b8",     // C2f
        [9] = "b9",     // SPPF
        // Layers 10, 11, 13, 14, 17, 20 are Upsample/Concat (no parameters)
        [12] = "n_c2f1",    // C2f (FPN P4)
        [15] = "n_c2f2",    // C2f (FPN P3)
        [16] = "n_down1",   // Conv (PAN downsample)
        [18] = "n_c2f3",    // C2f (PAN P4)
        [19] = "n_down2",   // Conv (PAN downsample)
        [21] = "n_c2f4",    // C2f (PAN P5)
        [22] = "detect",    // DetectHead (also used for seg/pose inner detect)
    };

    /// <summary>
    /// Original layer map used by YOLOv8Model (detect-only, uses backbone0..backbone9 naming).
    /// </summary>
    private static readonly Dictionary<int, string> LegacyDetectLayerMap = new()
    {
        [0] = "backbone0",
        [1] = "backbone1",
        [2] = "backbone2",
        [3] = "backbone3",
        [4] = "backbone4",
        [5] = "backbone5",
        [6] = "backbone6",
        [7] = "backbone7",
        [8] = "backbone8",
        [9] = "backbone9",
        [12] = "neck_c2f1",
        [15] = "neck_c2f2",
        [16] = "neck_down1",
        [18] = "neck_c2f3",
        [19] = "neck_down2",
        [21] = "neck_c2f4",
        [22] = "detect",
    };

    /// <summary>
    /// Load weights from a Python PyTorch checkpoint file into a YOLOv model.
    /// Automatically handles key name remapping between Python and C# conventions.
    /// </summary>
    /// <param name="model">The target YOLOv model to load weights into</param>
    /// <param name="checkpointPath">Path to the .pt checkpoint file</param>
    /// <param name="strict">If true, all checkpoint keys must match model parameters</param>
    /// <returns>
    /// A LoadResult containing counts of loaded, skipped, and missing parameters
    /// </returns>
    public static LoadResult LoadFromCheckpoint(YOLOModel model, string checkpointPath, bool strict = false)
    {
        return LoadFromCheckpointGeneric(model, checkpointPath, strict, TaskType.Detect);
    }

    /// <summary>
    /// Load weights from a Python checkpoint into a Seg/Pose/Cls model using the appropriate key map.
    /// </summary>
    public static LoadResult LoadFromCheckpoint(nn.Module model, string checkpointPath,
        TaskType task, bool strict = false)
    {
        return LoadFromCheckpointGeneric(model, checkpointPath, strict, task);
    }

    private static LoadResult LoadFromCheckpointGeneric(nn.Module model, string checkpointPath,
        bool strict, TaskType task)
    {
        Console.WriteLine($"  Loading weights from: {checkpointPath} (task={task})");

        using var reader = new PyTorchCheckpointReader(checkpointPath);
        var pyStateDict = reader.ReadStateDict();

        Console.WriteLine($"  Found {pyStateDict.Count} tensors in checkpoint");

        var remappedDict = new Dictionary<string, Tensor>();
        var unmappedKeys = new List<string>();

        foreach (var (pyKey, tensor) in pyStateDict)
        {
            var csKey = RemapKey(pyKey, task);
            if (csKey != null)
                remappedDict[csKey] = tensor;
            else
                unmappedKeys.Add(pyKey);
        }

        Console.WriteLine($"  Remapped {remappedDict.Count} keys, {unmappedKeys.Count} unmapped");

        var modelParams = new Dictionary<string, Tensor>();
        foreach (var (name, param) in model.named_parameters())
            modelParams[name] = param;

        var modelBuffers = new Dictionary<string, Tensor>();
        foreach (var (name, buffer) in model.named_buffers())
            modelBuffers[name] = buffer;

        int loaded = 0;
        int skipped = 0;
        int missing = 0;
        var missingKeys = new List<string>();

        using (torch.no_grad())
        {
            foreach (var (csKey, tensor) in remappedDict)
            {
                if (modelParams.TryGetValue(csKey, out var param))
                {
                    if (TryLoadTensor(param, tensor, csKey))
                        loaded++;
                    else
                        skipped++;
                }
                else if (modelBuffers.TryGetValue(csKey, out var buffer))
                {
                    if (TryLoadTensor(buffer, tensor, csKey))
                        loaded++;
                    else
                        skipped++;
                }
                else
                {
                    skipped++;
                }
            }

            foreach (var name in modelParams.Keys)
            {
                if (!remappedDict.ContainsKey(name))
                {
                    missing++;
                    missingKeys.Add(name);
                }
            }
        }

        foreach (var tensor in pyStateDict.Values)
            tensor.Dispose();

        var result = new LoadResult(loaded, skipped, missing, unmappedKeys, missingKeys);

        Console.WriteLine($"  Loaded: {loaded}, Skipped: {skipped}, Missing: {missing}");
        if (missingKeys.Count > 0 && missingKeys.Count <= 10)
            Console.WriteLine($"  Missing keys: {string.Join(", ", missingKeys)}");

        if (strict && missing > 0)
        {
            throw new InvalidOperationException(
                $"Strict loading failed: {missing} model parameters not found in checkpoint. " +
                $"Missing: {string.Join(", ", missingKeys.Take(5))}...");
        }

        return result;
    }

    /// <summary>
    /// Try to load a source tensor into a target tensor, handling shape and dtype mismatches.
    /// </summary>
    private static bool TryLoadTensor(Tensor target, Tensor source, string key)
    {
        try
        {
            // Convert dtype if needed
            bool needsConvert = source.dtype != target.dtype;
            var src = needsConvert ? source.to(target.dtype) : source;

            // Check shape compatibility
            if (!target.shape.SequenceEqual(src.shape))
            {
                Console.WriteLine(
                    $"  Warning: Shape mismatch for {key}: " +
                    $"model={FormatShape(target.shape)}, " +
                    $"checkpoint={FormatShape(src.shape)}. Skipping.");

                if (needsConvert) src.Dispose();
                return false;
            }

            target.copy_(src);

            if (needsConvert) src.Dispose();
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Warning: Failed to load {key}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Remap a Python-convention parameter key to C# TorchSharp convention.
    /// Overload for backward compatibility (defaults to Detect task with legacy naming).
    /// </summary>
    public static string? RemapKey(string pythonKey)
    {
        return RemapKey(pythonKey, TaskType.Detect, useLegacyNaming: true);
    }

    /// <summary>
    /// Remap a Python-convention parameter key to C# TorchSharp convention.
    /// Supports Detect, Segment, Pose, and Classify task types.
    ///
    /// Examples (Detect):
    ///   "model.0.conv.weight"           → "b0.conv.weight"
    ///   "model.22.cv2.0.0.conv.weight"  → "detect.cv2.0.cv2_0_0.conv.weight"
    ///   "model.22.dfl.conv.weight"      → "detect.dfl.conv.weight"
    ///
    /// Examples (Segment):
    ///   "model.22.proto.cv1.conv.weight" → "segment.proto.cv1.conv.weight"
    ///   "model.22.cv4.0.0.conv.weight"   → "segment.cv4.0.cv4_0_0.conv.weight"
    ///
    /// Examples (Pose):
    ///   "model.22.cv4.0.0.conv.weight"   → "pose.cv4.0.cv4_0_0.conv.weight"
    ///
    /// Examples (Classify):
    ///   "model.9.cv1.conv.weight"  → "classifyHead.conv.weight" (backbone unchanged)
    /// </summary>
    public static string? RemapKey(string pythonKey, TaskType task, bool useLegacyNaming = false)
    {
        var match = Regex.Match(pythonKey, @"^model\.(\d+)\.(.+)$");
        if (!match.Success)
            return RemapKeyDirect(pythonKey);

        int layerIndex = int.Parse(match.Groups[1].Value);
        string rest = match.Groups[2].Value;

        var layerMap = useLegacyNaming ? LegacyDetectLayerMap : DetectLayerMap;

        // For Classify models, backbone layers are the same but head is at the last layer
        if (task == TaskType.Classify)
        {
            // In classification model, layer 10+ is the Classify head
            if (layerIndex <= 9)
            {
                if (!layerMap.TryGetValue(layerIndex, out var bbPrefix))
                    return null;
                return $"{bbPrefix}.{rest}";
            }
            // Classify head (typically layer 10 in cls YAML)
            return $"classifyHead.{rest}";
        }

        // Segment / Pose: layer 22 needs special sub-routing
        if (layerIndex == 22 && task is TaskType.Segment or TaskType.Pose)
        {
            string headPrefix = task == TaskType.Segment ? "segHead" : "poseHead";
            return RemapTaskHeadKey(rest, headPrefix, task);
        }

        // Standard detect layer map
        if (!layerMap.TryGetValue(layerIndex, out var csPrefix))
            return null;

        if (layerIndex == 22)
            rest = RemapDetectHeadKey(rest);

        return $"{csPrefix}.{rest}";
    }

    /// <summary>
    /// Remap Segment/Pose head sub-keys.
    /// Routes to detect sub-head for cv2/cv3/dfl, and to the task head for cv4/proto.
    ///
    /// Python Segment layer 22 keys:
    ///   "cv2.I.J.rest"        → "{headPrefix}.detect.cv2.I.cv2_I_J.rest"
    ///   "cv3.I.J.rest"        → "{headPrefix}.detect.cv3.I.cv3_I_J.rest"
    ///   "dfl.conv.weight"     → "{headPrefix}.detect.dfl.conv.weight"
    ///   "proto.cv1.conv.weight" → "{headPrefix}.proto.cv1.conv.weight"
    ///   "cv4.I.J.rest"        → "{headPrefix}.cv4.I.cv4_I_J.rest"
    /// </summary>
    private static string? RemapTaskHeadKey(string rest, string headPrefix, TaskType task)
    {
        // Proto (Segment only)
        if (rest.StartsWith("proto."))
            return $"{headPrefix}.{rest}";

        // cv4 branches (both Segment and Pose)
        var cv4Match = Regex.Match(rest, @"^cv4\.(\d+)\.(\d+)\.?(.*)$");
        if (cv4Match.Success)
        {
            var levelIdx = cv4Match.Groups[1].Value;
            var subIdx = cv4Match.Groups[2].Value;
            var suffix = cv4Match.Groups[3].Value;
            var csSubName = $"cv4_{levelIdx}_{subIdx}";
            if (string.IsNullOrEmpty(suffix))
                return $"{headPrefix}.cv4.{levelIdx}.{csSubName}";
            return $"{headPrefix}.cv4.{levelIdx}.{csSubName}.{suffix}";
        }

        // cv2/cv3/dfl → route to inner detect head
        var detectKey = RemapDetectHeadKey(rest);
        return $"{headPrefix}.detect.{detectKey}";
    }

    /// <summary>
    /// Remap detect head sub-keys.
    /// Python: "cv2.I.J.rest" or "cv3.I.J.rest" → C#: "cv2.I.cv2_I_J.rest" or "cv3.I.cv3_I_J.rest"
    /// Python: "dfl.conv.weight" → C#: "dfl.conv.weight" (no change)
    /// </summary>
    private static string RemapDetectHeadKey(string rest)
    {
        // Match cv2.I.J.rest or cv3.I.J.rest pattern
        var cvMatch = Regex.Match(rest, @"^(cv[23])\.(\d+)\.(\d+)\.?(.*)$");
        if (cvMatch.Success)
        {
            var cvName = cvMatch.Groups[1].Value; // "cv2" or "cv3"
            var levelIdx = cvMatch.Groups[2].Value; // level index (0, 1, 2)
            var subIdx = cvMatch.Groups[3].Value; // sub-module index (0, 1, 2)
            var suffix = cvMatch.Groups[4].Value; // remaining path

            // C# names Sequential elements as "cv2_I_J" or "cv3_I_J"
            var csSubName = $"{cvName}_{levelIdx}_{subIdx}";

            if (string.IsNullOrEmpty(suffix))
                return $"{cvName}.{levelIdx}.{csSubName}";
            else
                return $"{cvName}.{levelIdx}.{csSubName}.{suffix}";
        }

        // No special remapping needed (e.g., "dfl.conv.weight")
        return rest;
    }

    /// <summary>
    /// Try to remap a key that doesn't have the "model.N" prefix.
    /// This handles plain state dict files where keys are already flattened.
    /// </summary>
    private static string? RemapKeyDirect(string key)
    {
        // If the key already uses C# naming convention, return as-is
        if (key.StartsWith("backbone") || key.StartsWith("neck_") || key.StartsWith("detect"))
            return key;

        // Otherwise, we can't map this key
        return null;
    }

    private static string FormatShape(long[] shape) =>
        $"[{string.Join(", ", shape)}]";

    /// <summary>
    /// Save a YOLOv model in TorchSharp native format.
    /// The saved file can be loaded directly with model.load().
    /// </summary>
    public static void SaveTorchSharp(YOLOModel model, string path)
    {
        model.save(path);
        Console.WriteLine($"  Model saved to: {path} (TorchSharp format)");
    }

    /// <summary>
    /// Load a YOLOv model from TorchSharp native format.
    /// </summary>
    public static void LoadTorchSharp(YOLOModel model, string path)
    {
        model.load(path);
        Console.WriteLine($"  Model loaded from: {path} (TorchSharp format)");
    }

    /// <summary>
    /// Detect if a file is a Python PyTorch checkpoint or a TorchSharp native file.
    /// </summary>
    public static CheckpointFormat DetectFormat(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var header = new byte[4];
            if (stream.Read(header, 0, 4) < 4)
                return CheckpointFormat.Unknown;

            // ZIP files start with PK\x03\x04
            if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
            {
                // Could be either PyTorch or TorchSharp - check for data.pkl
                using var archive = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Read);
                foreach (var entry in archive.Entries)
                {
                    if (entry.FullName.EndsWith("data.pkl"))
                        return CheckpointFormat.PyTorch;
                }
                return CheckpointFormat.TorchSharp;
            }

            return CheckpointFormat.TorchSharp; // Default assumption
        }
        catch
        {
            return CheckpointFormat.Unknown;
        }
    }

    /// <summary>
    /// Smart load: auto-detects the format and loads weights accordingly.
    /// </summary>
    public static LoadResult SmartLoad(YOLOModel model, string path, bool strict = false)
    {
        var format = DetectFormat(path);
        Console.WriteLine($"  Detected format: {format}");

        switch (format)
        {
            case CheckpointFormat.PyTorch:
                return LoadFromCheckpoint(model, path, strict);

            case CheckpointFormat.TorchSharp:
                try
                {
                    model.load(path);
                    long paramCount = model.parameters().Sum(p => p.numel());
                    Console.WriteLine($"  Loaded TorchSharp model ({paramCount:N0} parameters)");
                    return new LoadResult((int)model.parameters().Count(), 0, 0,
                        new List<string>(), new List<string>());
                }
                catch (Exception ex) when (
                    ex.Message.Contains("Mismatched", StringComparison.OrdinalIgnoreCase) ||
                    ex.Message.Contains("shape", StringComparison.OrdinalIgnoreCase))
                {
                    // Strict load failed due to shape mismatch — retry with strict: false
                    // to load as many compatible parameters as possible (mismatched ones
                    // keep their random initialization and will be logged).
                    Console.WriteLine($"  Strict load failed ({ex.Message}), retrying with strict=false...");

                    var loadedParams = new Dictionary<string, bool>();
                    model.load(path, strict: false, loadedParameters: loadedParams);

                    int loaded = loadedParams.Count(kv => kv.Value);
                    int skipped = loadedParams.Count(kv => !kv.Value);

                    var skippedKeys = loadedParams
                        .Where(kv => !kv.Value)
                        .Select(kv => kv.Key)
                        .ToList();

                    if (skippedKeys.Count > 0)
                    {
                        Console.WriteLine($"  Warning: {skipped} parameters had shape mismatches and were skipped:");
                        foreach (var key in skippedKeys.Take(10))
                            Console.WriteLine($"    - {key}");
                        if (skippedKeys.Count > 10)
                            Console.WriteLine($"    ... and {skippedKeys.Count - 10} more");
                    }

                    long totalParams = model.parameters().Sum(p => p.numel());
                    Console.WriteLine($"  Loaded TorchSharp model with fallback ({totalParams:N0} parameters, {loaded} loaded, {skipped} skipped)");

                    return new LoadResult(loaded, skipped, 0,
                        new List<string>(), skippedKeys);
                }

            default:
                throw new InvalidOperationException(
                    $"Unable to determine checkpoint format for: {path}");
        }
    }
}

/// <summary>
/// Result of a weight loading operation.
/// </summary>
public record LoadResult(
    int LoadedCount,
    int SkippedCount,
    int MissingCount,
    List<string> UnmappedKeys,
    List<string> MissingKeys
)
{
    public bool IsFullyLoaded => MissingCount == 0 && SkippedCount == 0;
}

/// <summary>
/// Detected checkpoint file format.
/// </summary>
public enum CheckpointFormat
{
    Unknown,
    PyTorch,    // Python torch.save format (zip + pickle)
    TorchSharp  // TorchSharp native format
}
