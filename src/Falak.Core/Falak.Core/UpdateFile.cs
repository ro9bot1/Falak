using System.Text.Json.Serialization;

namespace Falak.Core;

public sealed record UpdateFile([property: JsonPropertyName("name")] string Name, [property: JsonPropertyName("size")] long Size, [property: JsonPropertyName("sha256")] string Sha256);
