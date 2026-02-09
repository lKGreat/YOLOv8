namespace YOLO.WinForms.Models;

/// <summary>
/// Interface for undoable annotation commands.
/// </summary>
public interface IAnnotationCommand
{
    void Execute();
    void Undo();
    string Description { get; }
}

/// <summary>
/// Manages undo/redo stacks for annotation operations.
/// </summary>
public class AnnotationCommandManager
{
    private readonly Stack<IAnnotationCommand> _undoStack = new();
    private readonly Stack<IAnnotationCommand> _redoStack = new();

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Execute a command and push it onto the undo stack.
    /// </summary>
    public void Execute(IAnnotationCommand command)
    {
        command.Execute();
        _undoStack.Push(command);
        _redoStack.Clear();
    }

    /// <summary>
    /// Undo the last command.
    /// </summary>
    public void Undo()
    {
        if (!CanUndo) return;
        var cmd = _undoStack.Pop();
        cmd.Undo();
        _redoStack.Push(cmd);
    }

    /// <summary>
    /// Redo the last undone command.
    /// </summary>
    public void Redo()
    {
        if (!CanRedo) return;
        var cmd = _redoStack.Pop();
        cmd.Execute();
        _undoStack.Push(cmd);
    }

    /// <summary>
    /// Clear all undo/redo history.
    /// </summary>
    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
    }
}

/// <summary>
/// Command to add an annotation to an image.
/// </summary>
public class AddAnnotationCommand : IAnnotationCommand
{
    private readonly AnnotationImageInfo _image;
    private readonly RectAnnotation _annotation;

    public string Description => "Add annotation";

    public AddAnnotationCommand(AnnotationImageInfo image, RectAnnotation annotation)
    {
        _image = image;
        _annotation = annotation;
    }

    public void Execute() => _image.Annotations.Add(_annotation);
    public void Undo() => _image.Annotations.Remove(_annotation);
}

/// <summary>
/// Command to delete an annotation from an image.
/// </summary>
public class DeleteAnnotationCommand : IAnnotationCommand
{
    private readonly AnnotationImageInfo _image;
    private readonly RectAnnotation _annotation;
    private int _index;

    public string Description => "Delete annotation";

    public DeleteAnnotationCommand(AnnotationImageInfo image, RectAnnotation annotation)
    {
        _image = image;
        _annotation = annotation;
    }

    public void Execute()
    {
        _index = _image.Annotations.IndexOf(_annotation);
        _image.Annotations.Remove(_annotation);
    }

    public void Undo()
    {
        if (_index >= 0 && _index <= _image.Annotations.Count)
            _image.Annotations.Insert(_index, _annotation);
        else
            _image.Annotations.Add(_annotation);
    }
}

/// <summary>
/// Command to move/resize an annotation.
/// </summary>
public class MoveAnnotationCommand : IAnnotationCommand
{
    private readonly RectAnnotation _annotation;
    private readonly double _oldCX, _oldCY, _oldW, _oldH;
    private readonly double _newCX, _newCY, _newW, _newH;

    public string Description => "Move annotation";

    public MoveAnnotationCommand(RectAnnotation annotation,
        double oldCX, double oldCY, double oldW, double oldH,
        double newCX, double newCY, double newW, double newH)
    {
        _annotation = annotation;
        _oldCX = oldCX; _oldCY = oldCY; _oldW = oldW; _oldH = oldH;
        _newCX = newCX; _newCY = newCY; _newW = newW; _newH = newH;
    }

    public void Execute()
    {
        _annotation.CX = _newCX;
        _annotation.CY = _newCY;
        _annotation.W = _newW;
        _annotation.H = _newH;
    }

    public void Undo()
    {
        _annotation.CX = _oldCX;
        _annotation.CY = _oldCY;
        _annotation.W = _oldW;
        _annotation.H = _oldH;
    }
}
