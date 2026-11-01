namespace Falak.Core;

public sealed record WheelRequested(WheelMode Mode, int X, int Y, long Timestamp) : GestureEvent(Timestamp);
