using System.Collections.Generic;
using System.Linq;

namespace Falak.Engines;

public sealed record JobResult(IReadOnlyList<string> Outputs, string? Folder, string Message)
{
	public string? Primary => Folder ?? Outputs.FirstOrDefault();
}
