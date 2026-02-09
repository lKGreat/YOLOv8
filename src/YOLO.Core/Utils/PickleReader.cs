using System.Text;

namespace YOLO.Core.Utils;

/// <summary>
/// Minimal Python pickle protocol reader for PyTorch checkpoint files.
/// Implements the subset of pickle opcodes used by PyTorch's torch.save().
/// Supports protocols 2-5 (as used by modern PyTorch).
/// </summary>
public class PickleReader
{
    private readonly Stack<object?> _stack = new();
    private readonly List<int> _marks = new();
    private readonly Dictionary<int, object?> _memo = new();
    private readonly Func<object?, object?>? _persistentLoad;

    /// <summary>
    /// Creates a PickleReader.
    /// </summary>
    /// <param name="persistentLoad">
    /// Callback for BINPERSID opcode. PyTorch uses this to reference external tensor storage.
    /// </param>
    public PickleReader(Func<object?, object?>? persistentLoad = null)
    {
        _persistentLoad = persistentLoad;
    }

    /// <summary>
    /// Unpickle data from a stream.
    /// </summary>
    public object? Load(Stream stream)
    {
        using var reader = new BinaryReader(stream, Encoding.UTF8, leaveOpen: true);
        return Execute(reader);
    }

    /// <summary>
    /// Unpickle data from a byte array.
    /// </summary>
    public object? Load(byte[] data)
    {
        using var ms = new MemoryStream(data);
        return Load(ms);
    }

    private object? Execute(BinaryReader r)
    {
        while (true)
        {
            byte opcode = r.ReadByte();

            switch (opcode)
            {
                // === Protocol & Framing ===
                case 0x80: // PROTO
                    r.ReadByte(); // protocol version, ignore
                    break;

                case 0x95: // FRAME (protocol 4+)
                    r.ReadInt64(); // frame length, ignore
                    break;

                case 0x2E: // STOP
                    return _stack.Pop();

                // === None, Bool ===
                case 0x4E: // NONE
                    _stack.Push(null);
                    break;

                case 0x88: // NEWTRUE
                    _stack.Push(true);
                    break;

                case 0x89: // NEWFALSE
                    _stack.Push(false);
                    break;

                // === Integers ===
                case 0x4B: // BININT1 (1-byte unsigned)
                    _stack.Push((long)r.ReadByte());
                    break;

                case 0x4D: // BININT2 (2-byte unsigned LE)
                    _stack.Push((long)r.ReadUInt16());
                    break;

                case 0x4A: // BININT (4-byte signed LE)
                    _stack.Push((long)r.ReadInt32());
                    break;

                case 0x8A: // LONG1 (1-byte length-prefixed long)
                {
                    int len = r.ReadByte();
                    if (len == 0)
                    {
                        _stack.Push(0L);
                    }
                    else
                    {
                        var bytes = r.ReadBytes(len);
                        // Python longs are little-endian, signed
                        long val = 0;
                        for (int i = len - 1; i >= 0; i--)
                            val = (val << 8) | bytes[i];
                        // Sign extension
                        if ((bytes[len - 1] & 0x80) != 0)
                            val -= (1L << (len * 8));
                        _stack.Push(val);
                    }
                    break;
                }

                case 0x8B: // LONG4 (4-byte length-prefixed long)
                {
                    int len = r.ReadInt32();
                    if (len == 0)
                    {
                        _stack.Push(0L);
                    }
                    else
                    {
                        var bytes = r.ReadBytes(len);
                        long val = 0;
                        for (int i = Math.Min(len, 8) - 1; i >= 0; i--)
                            val = (val << 8) | bytes[i];
                        if (len <= 8 && (bytes[len - 1] & 0x80) != 0)
                            val -= (1L << (len * 8));
                        _stack.Push(val);
                    }
                    break;
                }

                // === Floats ===
                case 0x47: // BINFLOAT (8-byte IEEE 754 BE)
                {
                    var bytes = r.ReadBytes(8);
                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(bytes);
                    _stack.Push(BitConverter.ToDouble(bytes, 0));
                    break;
                }

                // === Strings ===
                case 0x8C: // SHORT_BINUNICODE (1-byte length)
                {
                    int len = r.ReadByte();
                    var bytes = r.ReadBytes(len);
                    _stack.Push(Encoding.UTF8.GetString(bytes));
                    break;
                }

                case 0x58: // BINUNICODE (4-byte length)
                {
                    int len = r.ReadInt32();
                    var bytes = r.ReadBytes(len);
                    _stack.Push(Encoding.UTF8.GetString(bytes));
                    break;
                }

                case 0x8D: // BINUNICODE8 (protocol 4+, 8-byte length)
                {
                    long len = r.ReadInt64();
                    var bytes = r.ReadBytes((int)len);
                    _stack.Push(Encoding.UTF8.GetString(bytes));
                    break;
                }

                case 0x54: // BINSTRING (4-byte length, raw bytes)
                {
                    int len = r.ReadInt32();
                    var bytes = r.ReadBytes(len);
                    _stack.Push(Encoding.ASCII.GetString(bytes));
                    break;
                }

                case 0x55: // SHORT_BINSTRING (1-byte length)
                {
                    int len = r.ReadByte();
                    var bytes = r.ReadBytes(len);
                    _stack.Push(Encoding.ASCII.GetString(bytes));
                    break;
                }

                case 0x8E: // SHORT_BINBYTES (protocol 4, 1-byte length)
                {
                    int len = r.ReadByte();
                    _stack.Push(r.ReadBytes(len));
                    break;
                }

                case 0x42: // BINBYTES (4-byte length)
                {
                    int len = r.ReadInt32();
                    _stack.Push(r.ReadBytes(len));
                    break;
                }

                case 0x8F: // SHORT_BINBYTES (protocol 4)
                {
                    int len = r.ReadByte();
                    _stack.Push(r.ReadBytes(len));
                    break;
                }

                case 0x43: // SHORT_BINBYTES
                {
                    int len = r.ReadByte();
                    _stack.Push(r.ReadBytes(len));
                    break;
                }

                case 0x46: // FLOAT (text float, newline terminated)
                {
                    var sb = new StringBuilder();
                    byte ch;
                    while ((ch = r.ReadByte()) != (byte)'\n')
                        sb.Append((char)ch);
                    _stack.Push(double.Parse(sb.ToString(), System.Globalization.CultureInfo.InvariantCulture));
                    break;
                }

                // === Bytes (protocol 5) ===
                case 0x96: // BYTEARRAY8
                {
                    long len = r.ReadInt64();
                    _stack.Push(r.ReadBytes((int)len));
                    break;
                }

                // === Tuples ===
                case 0x29: // EMPTY_TUPLE
                    _stack.Push(new List<object?>());
                    break;

                case 0x85: // TUPLE1
                {
                    var a = _stack.Pop();
                    _stack.Push(new List<object?> { a });
                    break;
                }

                case 0x86: // TUPLE2
                {
                    var b = _stack.Pop();
                    var a = _stack.Pop();
                    _stack.Push(new List<object?> { a, b });
                    break;
                }

                case 0x87: // TUPLE3
                {
                    var c = _stack.Pop();
                    var b = _stack.Pop();
                    var a = _stack.Pop();
                    _stack.Push(new List<object?> { a, b, c });
                    break;
                }

                case 0x74: // TUPLE (from mark)
                {
                    var items = PopMark();
                    _stack.Push(items);
                    break;
                }

                // === Lists ===
                case 0x5D: // EMPTY_LIST
                    _stack.Push(new List<object?>());
                    break;

                case 0x61: // APPEND
                {
                    var item = _stack.Pop();
                    var list = (List<object?>)_stack.Peek()!;
                    list.Add(item);
                    break;
                }

                case 0x65: // APPENDS (from mark)
                {
                    var items = PopMark();
                    var list = (List<object?>)_stack.Peek()!;
                    list.AddRange(items);
                    break;
                }

                // === Dicts ===
                case 0x7D: // EMPTY_DICT
                    _stack.Push(new Dictionary<string, object?>());
                    break;

                case 0x73: // SETITEM
                {
                    var value = _stack.Pop();
                    var key = _stack.Pop();
                    var target = _stack.Peek();
                    if (target is Dictionary<string, object?> dict)
                    {
                        dict[key?.ToString() ?? ""] = value;
                    }
                    else if (target is PythonObject pyObj)
                    {
                        // nn.Module and similar use dict-like SETITEM during unpickle
                        pyObj.SetState ??= new Dictionary<string, object?>();
                        pyObj.SetState[key?.ToString() ?? ""] = value;
                    }
                    break;
                }

                case 0x75: // SETITEMS (from mark)
                {
                    var items = PopMark();
                    var target = _stack.Peek();
                    if (target is Dictionary<string, object?> dict)
                    {
                        for (int i = 0; i < items.Count - 1; i += 2)
                            dict[items[i]?.ToString() ?? ""] = items[i + 1];
                    }
                    else if (target is PythonObject pyObj)
                    {
                        pyObj.SetState ??= new Dictionary<string, object?>();
                        for (int i = 0; i < items.Count - 1; i += 2)
                            pyObj.SetState[items[i]?.ToString() ?? ""] = items[i + 1];
                    }
                    break;
                }

                // === Sets ===
                case 0x91: // EMPTY_SET
                    _stack.Push(new HashSet<object?>());
                    break;

                case 0x90: // ADDITEMS (from mark, for sets)
                {
                    var items = PopMark();
                    if (_stack.Peek() is HashSet<object?> set)
                    {
                        foreach (var item in items)
                            set.Add(item);
                    }
                    break;
                }

                case 0x94: // FROZENSET (from mark)
                {
                    var items = PopMark();
                    _stack.Push(new HashSet<object?>(items));
                    break;
                }

                // === Mark ===
                case 0x28: // MARK
                    _marks.Add(_stack.Count);
                    break;

                // === Memo ===
                case 0x71: // BINPUT (1-byte memo index)
                {
                    int idx = r.ReadByte();
                    _memo[idx] = _stack.Peek();
                    break;
                }

                case 0x72: // LONG_BINPUT (4-byte memo index)
                {
                    int idx = r.ReadInt32();
                    _memo[idx] = _stack.Peek();
                    break;
                }

                case 0x97: // MEMOIZE (protocol 4, auto-indexed)
                {
                    _memo[_memo.Count] = _stack.Peek();
                    break;
                }

                case 0x68: // BINGET (1-byte memo index)
                {
                    int idx = r.ReadByte();
                    _stack.Push(_memo[idx]);
                    break;
                }

                case 0x6A: // LONG_BINGET (4-byte memo index)
                {
                    int idx = r.ReadInt32();
                    _stack.Push(_memo[idx]);
                    break;
                }

                // === Object Construction ===
                case 0x63: // GLOBAL "module\nname\n"
                {
                    var module = ReadLine(r);
                    var name = ReadLine(r);
                    _stack.Push(new PythonGlobal(module, name));
                    break;
                }

                case 0x93: // STACK_GLOBAL
                {
                    var name = (string)_stack.Pop()!;
                    var module = (string)_stack.Pop()!;
                    _stack.Push(new PythonGlobal(module, name));
                    break;
                }

                case 0x52: // REDUCE - call callable with args
                {
                    var args = _stack.Pop();
                    var callable = _stack.Pop();

                    if (callable is PythonGlobal global)
                    {
                        // Special handling for dict-like types: represent as actual C# dictionaries
                        // so that SETITEMS/SETITEM work correctly on them
                        if (global.Name is "OrderedDict" or "dict" or "defaultdict")
                        {
                            _stack.Push(new Dictionary<string, object?>());
                        }
                        else
                        {
                            var result = new PythonObject(global);
                            if (args is List<object?> argList)
                                result.Args = argList;
                            _stack.Push(result);
                        }
                    }
                    else
                    {
                        _stack.Push(new PythonObject(new PythonGlobal("?", "?"))
                        {
                            Args = args is List<object?> al ? al : new List<object?> { args }
                        });
                    }
                    break;
                }

                case 0x62: // BUILD - set state on object
                {
                    var state = _stack.Pop();
                    var obj = _stack.Peek();

                    if (obj is PythonObject pyObj)
                    {
                        pyObj.State = state;
                        // If state is a dict, also store as SetState for easy access
                        if (state is Dictionary<string, object?> stateDict)
                            pyObj.SetState = stateDict;
                    }
                    else if (obj is Dictionary<string, object?> dictObj && state is Dictionary<string, object?> updateDict)
                    {
                        foreach (var kv in updateDict)
                            dictObj[kv.Key] = kv.Value;
                    }
                    break;
                }

                case 0x81: // NEWOBJ - like REDUCE but for __new__
                {
                    var args = _stack.Pop();
                    var cls = _stack.Pop();

                    if (cls is PythonGlobal global)
                    {
                        var result = new PythonObject(global);
                        if (args is List<object?> argList)
                            result.Args = argList;
                        _stack.Push(result);
                    }
                    else
                    {
                        _stack.Push(new PythonObject(new PythonGlobal("?", "?")));
                    }
                    break;
                }

                case 0x92: // NEWOBJ_EX
                {
                    var kwargs = _stack.Pop();
                    var args = _stack.Pop();
                    var cls = _stack.Pop();

                    if (cls is PythonGlobal global)
                    {
                        var result = new PythonObject(global);
                        if (args is List<object?> argList)
                            result.Args = argList;
                        _stack.Push(result);
                    }
                    else
                    {
                        _stack.Push(new PythonObject(new PythonGlobal("?", "?")));
                    }
                    break;
                }

                // === Persistent IDs (PyTorch tensor storage) ===
                case 0x51: // BINPERSID
                {
                    var pid = _stack.Pop();
                    if (_persistentLoad != null)
                        _stack.Push(_persistentLoad(pid));
                    else
                        _stack.Push(new PythonObject(new PythonGlobal("_persistent", "load"))
                        {
                            Args = new List<object?> { pid }
                        });
                    break;
                }

                // === Text string ops (protocol 0/1 compat) ===
                case 0x56: // UNICODE (text, newline terminated)
                {
                    var s = ReadLine(r);
                    _stack.Push(s);
                    break;
                }

                case 0x49: // INT (text int, newline terminated)
                {
                    var s = ReadLine(r);
                    if (s == "00")
                        _stack.Push(false);
                    else if (s == "01")
                        _stack.Push(true);
                    else
                        _stack.Push(long.Parse(s));
                    break;
                }

                case 0x4C: // LONG (text long, newline terminated)
                {
                    var s = ReadLine(r).TrimEnd('L');
                    _stack.Push(long.Parse(s));
                    break;
                }

                case 0x70: // PUT (text memo)
                {
                    var idx = int.Parse(ReadLine(r));
                    _memo[idx] = _stack.Peek();
                    break;
                }

                case 0x67: // GET (text memo)
                {
                    var idx = int.Parse(ReadLine(r));
                    _stack.Push(_memo[idx]);
                    break;
                }

                case 0x64: // DICT (from mark)
                {
                    var items = PopMark();
                    var dict = new Dictionary<string, object?>();
                    for (int i = 0; i < items.Count - 1; i += 2)
                        dict[items[i]?.ToString() ?? ""] = items[i + 1];
                    _stack.Push(dict);
                    break;
                }

                case 0x6C: // LIST (from mark)
                {
                    var items = PopMark();
                    _stack.Push(items);
                    break;
                }

                case 0x30: // POP
                    if (_stack.Count > 0) _stack.Pop();
                    break;

                case 0x31: // POP_MARK
                    PopMark();
                    break;

                case 0x32: // DUP
                    _stack.Push(_stack.Peek());
                    break;

                default:
                    throw new InvalidDataException(
                        $"Unsupported pickle opcode 0x{opcode:X2} at position {r.BaseStream.Position - 1}");
            }
        }
    }

    /// <summary>
    /// Pop items from the stack back to the last mark.
    /// </summary>
    private List<object?> PopMark()
    {
        if (_marks.Count == 0)
            throw new InvalidDataException("No mark on stack");

        int markPos = _marks[^1];
        _marks.RemoveAt(_marks.Count - 1);

        var items = new List<object?>();
        int count = _stack.Count - markPos;
        var temp = new object?[count];
        for (int i = count - 1; i >= 0; i--)
            temp[i] = _stack.Pop();
        items.AddRange(temp);
        return items;
    }

    private static string ReadLine(BinaryReader r)
    {
        var sb = new StringBuilder();
        byte ch;
        while ((ch = r.ReadByte()) != (byte)'\n')
            sb.Append((char)ch);
        var s = sb.ToString();
        if (s.EndsWith('\r'))
            s = s[..^1];
        return s;
    }
}

/// <summary>
/// Represents a Python global reference (module.classname).
/// </summary>
public record PythonGlobal(string Module, string Name)
{
    public string FullName => $"{Module}.{Name}";
    public override string ToString() => FullName;
}

/// <summary>
/// Represents a generic Python object reconstructed from pickle.
/// Used as a placeholder for classes we don't need to fully reconstruct.
/// </summary>
public class PythonObject
{
    public PythonGlobal Type { get; }
    public List<object?> Args { get; set; } = new();
    public object? State { get; set; }
    public Dictionary<string, object?>? SetState { get; set; }

    public PythonObject(PythonGlobal type)
    {
        Type = type;
    }

    /// <summary>
    /// Try to get a value from the object's state dictionary.
    /// </summary>
    public object? GetStateValue(string key)
    {
        if (SetState != null && SetState.TryGetValue(key, out var val))
            return val;
        return null;
    }

    public override string ToString() => $"PythonObject({Type})";
}
