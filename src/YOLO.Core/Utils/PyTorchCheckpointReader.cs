using System.IO.Compression;
using TorchSharp;
using static TorchSharp.torch;

namespace YOLO.Core.Utils;

/// <summary>
/// Represents a PyTorch tensor storage reference from a checkpoint file.
/// </summary>
public class StorageRef
{
    public string Key { get; set; } = "";
    public string DeviceStr { get; set; } = "cpu";
    public long NumElements { get; set; }
    public ScalarType DType { get; set; } = ScalarType.Float32;
    public byte[]? Data { get; set; }
}

/// <summary>
/// Represents a PyTorch tensor reconstructed from checkpoint data.
/// </summary>
public class TensorInfo
{
    public StorageRef Storage { get; set; } = new();
    public long StorageOffset { get; set; }
    public long[] Shape { get; set; } = Array.Empty<long>();
    public long[] Strides { get; set; } = Array.Empty<long>();
}

/// <summary>
/// Pure C# reader for PyTorch checkpoint (.pt) files.
///
/// PyTorch saves checkpoints as zip archives containing:
///   - archive/data.pkl  (pickle-serialized object graph with tensor references)
///   - archive/data/0    (raw bytes for tensor storage 0)
///   - archive/data/1    (raw bytes for tensor storage 1)
///   - ...
///
/// This reader parses the pickle to extract tensor metadata, reads raw tensor data
/// from the zip entries, and reconstructs a state dict mapping parameter names to tensors.
/// Supports both full ultralytics checkpoints and plain state_dict files.
/// </summary>
public class PyTorchCheckpointReader : IDisposable
{
    private readonly string _path;
    private ZipArchive? _archive;
    private readonly Dictionary<string, StorageRef> _storages = new();
    private string _archivePrefix = "archive/";

    public PyTorchCheckpointReader(string path)
    {
        _path = path;
    }

    /// <summary>
    /// Read all parameter tensors from the checkpoint file.
    /// Returns a dictionary mapping full parameter names (Python convention) to TorchSharp tensors.
    /// </summary>
    public Dictionary<string, Tensor> ReadStateDict()
    {
        _archive = ZipFile.OpenRead(_path);

        // Detect archive prefix (could be "archive/" or empty or other)
        DetectArchivePrefix();

        // Read the pickle data
        var pklEntry = _archive.GetEntry($"{_archivePrefix}data.pkl")
            ?? throw new InvalidDataException("No data.pkl found in checkpoint file");

        byte[] pklData;
        using (var stream = pklEntry.Open())
        using (var ms = new MemoryStream())
        {
            stream.CopyTo(ms);
            pklData = ms.ToArray();
        }

        // Parse pickle with our persistent_load handler
        var reader = new PickleReader(PersistentLoad);
        var root = reader.Load(pklData);

        // Extract state dict from the unpickled object graph
        var paramTensors = new Dictionary<string, TensorInfo>();
        ExtractStateDictFromRoot(root, paramTensors);

        // Load raw tensor data from zip and create TorchSharp tensors
        LoadStorageData();

        var result = new Dictionary<string, Tensor>();
        foreach (var (name, info) in paramTensors)
        {
            var tensor = CreateTensor(info);
            if (tensor is not null && !tensor.IsInvalid)
                result[name] = tensor;
        }

        return result;
    }

    /// <summary>
    /// Detect the prefix used in the zip archive for data entries.
    /// </summary>
    private void DetectArchivePrefix()
    {
        foreach (var entry in _archive!.Entries)
        {
            if (entry.FullName.EndsWith("data.pkl"))
            {
                _archivePrefix = entry.FullName[..^"data.pkl".Length];
                break;
            }
        }
    }

    /// <summary>
    /// PyTorch's persistent_load callback for the pickle reader.
    /// Called for each BINPERSID opcode, which references external tensor storage.
    /// </summary>
    private object? PersistentLoad(object? pid)
    {
        if (pid is not List<object?> tuple || tuple.Count < 5)
            return null;

        var tag = tuple[0]?.ToString();
        if (tag != "storage")
            return null;

        // tuple: ("storage", StorageClass, key, device, numel)
        var storageClass = tuple[1]; // PythonGlobal like torch.FloatStorage
        var key = tuple[2]?.ToString() ?? "";
        var device = tuple[3]?.ToString() ?? "cpu";
        var numel = Convert.ToInt64(tuple[4]);

        var dtype = ResolveStorageDType(storageClass);

        var storage = new StorageRef
        {
            Key = key,
            DeviceStr = device,
            NumElements = numel,
            DType = dtype
        };

        _storages[key] = storage;
        return storage;
    }

    /// <summary>
    /// Resolve the ScalarType from a PyTorch storage class reference.
    /// </summary>
    private static ScalarType ResolveStorageDType(object? storageClass)
    {
        var name = storageClass switch
        {
            PythonGlobal g => g.Name,
            PythonObject o => o.Type.Name,
            string s => s,
            _ => "FloatStorage"
        };

        return name switch
        {
            "FloatStorage" => ScalarType.Float32,
            "DoubleStorage" => ScalarType.Float64,
            "HalfStorage" => ScalarType.Float16,
            "BFloat16Storage" => ScalarType.BFloat16,
            "LongStorage" => ScalarType.Int64,
            "IntStorage" => ScalarType.Int32,
            "ShortStorage" => ScalarType.Int16,
            "ByteStorage" => ScalarType.Byte,
            "BoolStorage" => ScalarType.Bool,
            "CharStorage" => ScalarType.Int8,
            _ => ScalarType.Float32
        };
    }

    /// <summary>
    /// Extract the state dict from the unpickled root object.
    /// Handles both:
    ///   1. Plain state_dict (dict of string → tensor)
    ///   2. Ultralytics checkpoint (dict with 'model' key containing nn.Module)
    /// </summary>
    private void ExtractStateDictFromRoot(object? root, Dictionary<string, TensorInfo> result)
    {
        if (root is Dictionary<string, object?> topDict)
        {
            // Check if this is a full checkpoint with a 'model' key
            if (topDict.TryGetValue("model", out var modelObj))
            {
                // Ultralytics checkpoint: extract state dict from the model object
                ExtractModuleParams(modelObj, "", result);
            }
            else
            {
                // Plain state dict: each value should be a tensor
                foreach (var (key, value) in topDict)
                {
                    var tensorInfo = ResolveTensorInfo(value);
                    if (tensorInfo != null)
                        result[key] = tensorInfo;
                }
            }
        }
        else if (root is PythonObject pyObj)
        {
            // Root is a single module
            ExtractModuleParams(root, "", result);
        }
    }

    /// <summary>
    /// Recursively extract parameters from a PyTorch nn.Module object graph.
    ///
    /// nn.Module.__getstate__ returns a dict with keys:
    ///   '_parameters': OrderedDict of name → tensor
    ///   '_buffers': OrderedDict of name → tensor  
    ///   '_modules': OrderedDict of name → submodule
    /// </summary>
    private void ExtractModuleParams(object? obj, string prefix,
        Dictionary<string, TensorInfo> result)
    {
        if (obj == null) return;

        Dictionary<string, object?>? stateDict = null;

        if (obj is PythonObject pyObj)
        {
            stateDict = pyObj.SetState;

            // For objects created via REDUCE + BUILD, the state might be nested
            if (stateDict == null && pyObj.State is Dictionary<string, object?> directState)
                stateDict = directState;

            // Some objects store state in Args (e.g., OrderedDict constructed from items)
            if (stateDict == null && pyObj.Type.Name == "OrderedDict" && pyObj.Args?.Count > 0)
            {
                // OrderedDict can be constructed with a list of (key, value) pairs
                stateDict = ConvertOrderedDictArgs(pyObj);
            }
        }
        else if (obj is Dictionary<string, object?> dict)
        {
            stateDict = dict;
        }

        if (stateDict == null) return;

        // Extract _parameters
        if (stateDict.TryGetValue("_parameters", out var paramsObj))
        {
            var paramsDict = ResolveDict(paramsObj);
            if (paramsDict != null)
            {
                foreach (var (name, value) in paramsDict)
                {
                    var tensorInfo = ResolveTensorInfo(value);
                    if (tensorInfo != null)
                    {
                        string fullKey = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
                        result[fullKey] = tensorInfo;
                    }
                }
            }
        }

        // Extract _buffers
        if (stateDict.TryGetValue("_buffers", out var buffersObj))
        {
            var buffersDict = ResolveDict(buffersObj);
            if (buffersDict != null)
            {
                foreach (var (name, value) in buffersDict)
                {
                    var tensorInfo = ResolveTensorInfo(value);
                    if (tensorInfo != null)
                    {
                        string fullKey = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
                        result[fullKey] = tensorInfo;
                    }
                }
            }
        }

        // Recurse into _modules
        if (stateDict.TryGetValue("_modules", out var modulesObj))
        {
            var modulesDict = ResolveDict(modulesObj);
            if (modulesDict != null)
            {
                foreach (var (name, subModule) in modulesDict)
                {
                    string subPrefix = string.IsNullOrEmpty(prefix) ? name : $"{prefix}.{name}";
                    ExtractModuleParams(subModule, subPrefix, result);
                }
            }
        }
    }

    /// <summary>
    /// Convert an OrderedDict's constructor arguments to a dictionary.
    /// OrderedDict is often pickled as: REDUCE(OrderedDict, [(k1,v1), (k2,v2), ...])
    /// or: REDUCE(OrderedDict, ()) then BUILD with list of items
    /// </summary>
    private Dictionary<string, object?>? ConvertOrderedDictArgs(PythonObject pyObj)
    {
        var result = new Dictionary<string, object?>();

        // Check if args contain a list of (key, value) pairs
        if (pyObj.Args?.Count == 1 && pyObj.Args[0] is List<object?> items)
        {
            foreach (var item in items)
            {
                if (item is List<object?> pair && pair.Count >= 2)
                {
                    var key = pair[0]?.ToString() ?? "";
                    result[key] = pair[1];
                }
            }
        }

        // Also check SetState (BUILD state), which for OrderedDict is the item list
        if (pyObj.State is Dictionary<string, object?> stateDict)
        {
            foreach (var kv in stateDict)
                result[kv.Key] = kv.Value;
        }

        return result.Count > 0 ? result : null;
    }

    /// <summary>
    /// Resolve an object to a dictionary (handles both plain dicts and OrderedDict PythonObjects).
    /// </summary>
    private Dictionary<string, object?>? ResolveDict(object? obj)
    {
        if (obj is Dictionary<string, object?> dict)
            return dict;

        if (obj is PythonObject pyObj)
        {
            if (pyObj.Type.Name == "OrderedDict" || pyObj.Type.Name == "dict")
            {
                var result = pyObj.SetState as Dictionary<string, object?>;
                if (result != null) return result;
                return ConvertOrderedDictArgs(pyObj);
            }

            // Could be a dict wrapped in an object
            if (pyObj.SetState != null)
                return pyObj.SetState;
        }

        return null;
    }

    /// <summary>
    /// Resolve a pickle object to TensorInfo.
    /// PyTorch tensors are reconstructed via torch._utils._rebuild_tensor_v2(storage, offset, size, stride).
    /// </summary>
    private TensorInfo? ResolveTensorInfo(object? obj)
    {
        if (obj is PythonObject pyObj)
        {
            // Direct tensor reconstruction: _rebuild_tensor_v2(storage, offset, size, stride, ...)
            if (pyObj.Type.Name is "_rebuild_tensor_v2" or "_rebuild_tensor")
            {
                return ParseRebuildTensor(pyObj.Args);
            }

            // Parameter wraps a tensor: _rebuild_parameter(tensor, requires_grad, metadata)
            // or _rebuild_parameter_with_state(tensor, requires_grad, metadata, state)
            if (pyObj.Type.Name is "_rebuild_parameter" or "_rebuild_parameter_with_state" or "Parameter")
            {
                if (pyObj.Args?.Count > 0)
                {
                    // args[0] is the inner tensor (usually a _rebuild_tensor_v2 PythonObject)
                    return ResolveTensorInfo(pyObj.Args[0]);
                }
            }

            // Fallback: try to resolve from state
            if (pyObj.State != null)
            {
                return ResolveTensorInfo(pyObj.State);
            }
        }

        return null;
    }

    /// <summary>
    /// Parse the args of _rebuild_tensor_v2(storage, offset, size, stride, ...).
    /// </summary>
    private TensorInfo? ParseRebuildTensor(List<object?>? args)
    {
        if (args == null || args.Count < 4)
            return null;

        // args[0] = storage (StorageRef from persistent_load)
        // args[1] = storage_offset (int)
        // args[2] = size (tuple of ints)
        // args[3] = stride (tuple of ints)

        StorageRef? storage = null;
        if (args[0] is StorageRef sr)
        {
            storage = sr;
        }
        else if (args[0] is PythonObject storageObj)
        {
            // Might be a wrapped storage
            if (storageObj.Type.Name is "_rebuild_tensor_v2" or "_rebuild_tensor")
            {
                var inner = ParseRebuildTensor(storageObj.Args);
                if (inner != null) return inner;
            }
        }

        if (storage == null)
            return null;

        long offset = Convert.ToInt64(args[1] ?? 0L);
        var shape = ConvertToLongArray(args[2]);
        var strides = ConvertToLongArray(args[3]);

        return new TensorInfo
        {
            Storage = storage,
            StorageOffset = offset,
            Shape = shape,
            Strides = strides
        };
    }

    /// <summary>
    /// Convert a pickle tuple/list to a long array.
    /// </summary>
    private static long[] ConvertToLongArray(object? obj)
    {
        if (obj is List<object?> list)
            return list.Select(x => Convert.ToInt64(x ?? 0)).ToArray();
        return Array.Empty<long>();
    }

    /// <summary>
    /// Load raw tensor data from zip archive entries into StorageRef objects.
    /// </summary>
    private void LoadStorageData()
    {
        foreach (var (key, storage) in _storages)
        {
            // Try common path patterns
            var entry = _archive!.GetEntry($"{_archivePrefix}data/{key}")
                ?? _archive.GetEntry($"data/{key}")
                ?? _archive.GetEntry(key);

            if (entry != null)
            {
                using var stream = entry.Open();
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                storage.Data = ms.ToArray();
            }
        }
    }

    /// <summary>
    /// Create a TorchSharp tensor from TensorInfo.
    /// </summary>
    private Tensor? CreateTensor(TensorInfo info)
    {
        if (info.Storage.Data == null || info.Shape.Length == 0)
            return null;

        int elemSize = GetElementSize(info.Storage.DType);
        int byteOffset = (int)(info.StorageOffset * elemSize);

        // Calculate number of elements from shape
        long numElements = 1;
        foreach (var s in info.Shape)
            numElements *= s;

        int byteLength = (int)(numElements * elemSize);

        if (byteOffset + byteLength > info.Storage.Data.Length)
        {
            // Fallback: try reading what we can
            byteLength = Math.Min(byteLength, info.Storage.Data.Length - byteOffset);
            if (byteLength <= 0)
                return null;
        }

        // Create tensor from raw bytes
        var tensor = torch.zeros(info.Shape, dtype: info.Storage.DType);

        // Copy data into tensor
        var srcSpan = new ReadOnlySpan<byte>(info.Storage.Data, byteOffset, byteLength);
        var dstSpan = tensor.bytes;
        srcSpan.CopyTo(dstSpan);

        // Convert fp16 to fp32 (ultralytics saves in half precision)
        if (info.Storage.DType == ScalarType.Float16 || info.Storage.DType == ScalarType.BFloat16)
        {
            var fp32 = tensor.to(ScalarType.Float32);
            tensor.Dispose();
            return fp32;
        }

        return tensor;
    }

    /// <summary>
    /// Get the size in bytes of a scalar type.
    /// </summary>
    private static int GetElementSize(ScalarType dtype) => dtype switch
    {
        ScalarType.Float32 => 4,
        ScalarType.Float64 => 8,
        ScalarType.Float16 => 2,
        ScalarType.BFloat16 => 2,
        ScalarType.Int64 => 8,
        ScalarType.Int32 => 4,
        ScalarType.Int16 => 2,
        ScalarType.Int8 => 1,
        ScalarType.Byte => 1,
        ScalarType.Bool => 1,
        _ => 4
    };

    public void Dispose()
    {
        _archive?.Dispose();
        _archive = null;
    }
}
