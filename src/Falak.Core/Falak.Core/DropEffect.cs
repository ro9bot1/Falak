using System;

namespace Falak.Core;

[Flags]
public enum DropEffect : uint
{
	None = 0u,
	Copy = 1u,
	Move = 2u,
	Link = 4u,
	Scroll = 0x80000000u
}
