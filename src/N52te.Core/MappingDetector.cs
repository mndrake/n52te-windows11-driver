namespace N52te;

/// <summary>Something the keypad sent to the PC while an input was pressed.</summary>
public abstract record DetectedEvent;

/// <summary>A key press or release, already translated to a firmware key code.</summary>
public sealed record DetectedKey(byte Code, bool Press) : DetectedEvent;

public enum DetectedMouseKind { LeftDown, LeftUp, RightDown, RightUp, MiddleDown, MiddleUp, WheelUp, WheelDown, MoveLeft, MoveRight, MoveUp, MoveDown }

public sealed record DetectedMouse(DetectedMouseKind Kind) : DetectedEvent;

/// <summary>
/// Works out an input's action from what the keypad sent while it was pressed. Used by the
/// editor's "read mapping by pressing buttons", which listens with Raw Input (no hook, no driver).
/// </summary>
public static class MappingDetector
{
    /// <summary>Returns the action, or null if nothing usable was sent.</summary>
    public static InputAction? Interpret(IReadOnlyList<DetectedEvent> events)
    {
        if (events.Count == 0) return null;

        var mouse = events.OfType<DetectedMouse>().Select(m => m.Kind).ToList();
        var keys = events.OfType<DetectedKey>().ToList();
        if (mouse.Count > 0 && keys.Count == 0) return InterpretMouse(mouse);
        if (keys.Count == 0) return null;

        // Collapse key auto-repeat (several presses of a key that is already down).
        var steps = new List<MacroStep>();
        var down = new HashSet<byte>();
        foreach (var k in keys)
        {
            if (k.Press && !down.Add(k.Code)) continue;
            if (!k.Press && !down.Remove(k.Code)) continue;   // release of a key we never saw pressed
            steps.Add(new MacroStep(k.Code, k.Press));
        }
        if (steps.Count == 0) return null;
        if (steps.Count == 2 && steps[0].Press && !steps[1].Press && steps[0].Code == steps[1].Code)
            return new KeyAction(steps[0].Code);
        if (steps.Count == 1 && steps[0].Press)
            return new KeyAction(steps[0].Code);   // released after the capture window: still a single key
        // Anything else is a sequence the firmware plays: keep it as a macro (timing is not captured).
        foreach (var code in down) steps.Add(new MacroStep(code, false));
        return steps.Count <= Macro.MaxPairs ? new MacroAction(steps) : null;
    }

    private static InputAction? InterpretMouse(List<DetectedMouseKind> kinds)
    {
        // A button press wins over incidental movement; then the wheel; then the main movement direction.
        foreach (var (kind, function) in new[]
        {
            (DetectedMouseKind.LeftDown, MouseFunction.LeftButton),
            (DetectedMouseKind.RightDown, MouseFunction.RightButton),
            (DetectedMouseKind.MiddleDown, MouseFunction.MiddleButton),
            (DetectedMouseKind.WheelUp, MouseFunction.ScrollUp),
            (DetectedMouseKind.WheelDown, MouseFunction.ScrollDown),
        })
        {
            if (kinds.Contains(kind)) return new MouseAction(function);
        }
        var moves = kinds.Where(k => k >= DetectedMouseKind.MoveLeft).GroupBy(k => k).OrderByDescending(g => g.Count()).FirstOrDefault();
        return moves?.Key switch
        {
            DetectedMouseKind.MoveLeft => new MouseAction(MouseFunction.PointerLeft),
            DetectedMouseKind.MoveRight => new MouseAction(MouseFunction.PointerRight),
            DetectedMouseKind.MoveUp => new MouseAction(MouseFunction.PointerUp),
            DetectedMouseKind.MoveDown => new MouseAction(MouseFunction.PointerDown),
            _ => null,
        };
    }
}
