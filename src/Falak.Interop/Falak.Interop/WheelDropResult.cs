using Falak.Core;

namespace Falak.Interop;

public sealed record WheelDropResult(WheelMode Mode, FileKind Kind, WheelAction? Action, DroppedFiles Files, DropEffect Answered);
