namespace Falak.Core;

public sealed record DragArmed(int X, int Y, long Timestamp) : GestureEvent(Timestamp);
