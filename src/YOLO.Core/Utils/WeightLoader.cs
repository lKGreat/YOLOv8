using System.Text.RegularExpressions;
using TorchSharp;
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
/// </summary>
public static class WeightLoader
{
    /// <summary>
    /// Layer index to C# field name mapping for YOLOv architecture.
    /// Maps Python's "model.N" prefix to C#'s field name prefix.
    /// </summary>
    private static readonly Dictionary<int, string> LayerMap = new()
    {
        [0] = "backbone0",     // Conv stem
        [1] = "backbone1",     // Conv
        [2] = "backbone2",     // C2f
        [3] = "backbone3",     // Conv
        [4] = "backbone4",     // C2f
        [5] = "backbone5",     // Conv
        [6] = "backbone6",     // C2f
        [7] = "backbone7",     // Conv
        [8] = "backbone8",     // C2f
        [9] = "backbone9",     // SPPF
        // Layers 10, 11, 13, 14, 17, 20 are Upsample/Concat (no parameters)
        [12] = "neck_c2f1",    // C2f (FPN P4)
        [15] = "neck_c2f2",    // C2f (FPN P3)
        [16] = "neck_down1",   // Conv (PAN downsample)
        [18] = "neck_c2f3",    // C2f (PAN P4)
        [19] = "neck_down2",   // Conv (PAN downsample)
        [21] = "neck_c2f4",    // C2f (PAN P5)
        [22] = "detect",       // DetectHead
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
    public static LoadResult LoadFromCheckpoint(YOLOvModel model, string checkpointPath, bool strict = false)
    {
        Console.WriteLine($"  Loading weights from: {checkpointPath}");

        // Read the checkpoint file
        using var reader = new PyTorchCheckpointReader(checkpointPath);
        var pyStateDict = reader.ReadStateDict();

        Console.WriteLine($"  Found {pyStateDict.Count} tensors in checkpoint");

        // Remap keys from Python naming to C# naming
        var remappedDict = new Dictionary<string, Tensor>();
        var unmappedKeys = new List<string>();

        foreach (var (pyKey, tensor) in pyStateDict)
        {
            var csKey = RemapKey(pyKey);
            if (csKey != null)
            {
                remappedDict[csKey] = tensor;
            }
            else
            {
                unmappedKeys.Add(pyKey);
            }
        }

        Console.WriteLine($"  Remapped {remappedDict.Count} keys, {unmappedKeys.Count} unmapped");

        // Get model's parameter names and shapes
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

        // Load remapped weights into model parameters
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

            // Check for missing model parameters
            foreach (var name in modelParams.Keys)
            {
                if (!remappedDict.ContainsKey(name))
                {
                    missing++;
                    missingKeys.Add(name);
                }
            }
        }

        // Dispose the checkpoint tensors
        foreach (var tensor in pyStateDict.Values)
            tensor.Dispose();

        var result = new LoadResult(loaded, skipped, missing, unmappedKeys, missingKeys);

        Console.WriteLine($"  Loaded: {loaded}, Skipped: {skipped}, Missing: {missing}");
        if (missingKeys.Count > 0 && missingKeys.Count <= 10)
        {
            Console.WriteLine($"  Missing keys: {string.Join(", ", missingKeys)}");
        }

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
    ///
    /// Examples:
    ///   "model.0.conv.weight"           → "backbone0.conv.weight"
    ///   "model.2.m.0.cv1.conv.weight"   → "backbone2.m.0.cv1.conv.weight"
    ///   "model.22.cv2.0.0.conv.weight"  → "detect.cv2.0.cv2_0_0.conv.weight"
    ///   "model.22.cv3.1.2.weight"       → "detect.cv3.1.cv3_1_2.weight"
    ///   "model.22.dfl.conv.weight"       → "detect.dfl.conv.weight"
    /// </summary>
    public static string? RemapKey(string pythonKey)
    {
        // Handle "model.N.rest" format (ultralytics model structure)
        var match = Regex.Match(pythonKey, @"^model\.(\d+)\.(.+)$");
        if (!match.Success)
        {
            // Could be a key without "model." prefix (plain state dict)
            // Try to match directly
            return RemapKeyDirect(pythonKey);
        }

        int layerIndex = int.Parse(match.Groups[1].Value);
        string rest = match.Groups[2].Value;

        if (!LayerMap.TryGetValue(layerIndex, out var csPrefix))
            return null; // Layer without parameters (Upsample, Concat, etc.)

        // Special handling for detect head (layer 22)
        if (layerIndex == 22)
        {
            rest = RemapDetectHeadKey(rest);
        }

        return $"{csPrefix}.{rest}";
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
    public static void SaveTorchSharp(YOLOvModel model, string path)
    {
        model.save(path);
        Console.WriteLine($"  Model saved to: {path} (TorchSharp format)");
    }

    /// <summary>
    /// Load a YOLOv model from TorchSharp native format.
    /// </summary>
    public static void LoadTorchSharp(YOLOvModel model, string path)
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
    public static LoadResult SmartLoad(YOLOvModel model, string path, bool strict = false)
    {
        var format = DetectFormat(path);
        Console.WriteLine($"  Detected format: {format}");

        switch (format)
        {
            case CheckpointFormat.PyTorch:
                return LoadFromCheckpoint(model, path, strict);

            case CheckpointFormat.TorchSharp:
                model.load(path);
                long paramCount = model.parameters().Sum(p => p.numel());
                Console.WriteLine($"  Loaded TorchSharp model ({paramCount:N0} parameters)");
                return new LoadResult((int)model.parameters().Count(), 0, 0,
                    new List<string>(), new List<string>());

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
