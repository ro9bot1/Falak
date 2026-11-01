using System;
using System.Collections.Generic;

namespace Falak.Interop;

public sealed record DroppedFiles(IReadOnlyList<string> Paths, IReadOnlyList<string> VirtualNames, string Format)
{
	public int Count => Paths.Count + VirtualNames.Count;

	public static readonly DroppedFiles Empty = new DroppedFiles(Array.Empty<string>(), Array.Empty<string>(), "none");
}
