namespace Falak.Core;

public sealed record DragEnded(int X, int Y, bool Cancelled, long Timestamp) : GestureEvent(Timestamp);
