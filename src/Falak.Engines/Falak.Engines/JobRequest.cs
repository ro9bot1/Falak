using System.Collections.Generic;

namespace Falak.Engines;

public sealed record JobRequest(string ActionId, IReadOnlyList<string> Inputs, string? Options = null);
