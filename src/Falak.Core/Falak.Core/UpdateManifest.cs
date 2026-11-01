using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Falak.Core;

public sealed record UpdateManifest([property: JsonPropertyName("app")] string App, [property: JsonPropertyName("version")] string Version, [property: JsonPropertyName("released")] string? Released, [property: JsonPropertyName("notes")] IReadOnlyList<string>? Notes, [property: JsonPropertyName("files")] IReadOnlyDictionary<string, UpdateFile> Files)
{
	private sealed record Envelope([property: JsonPropertyName("format")] int Format, [property: JsonPropertyName("payload")] string Payload, [property: JsonPropertyName("signature")] string Signature);

	public Version ParsedVersion => System.Version.Parse(Version);

	public const string ReleaseKey = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEG/X//ovhgo2nvjVgBTKQFGgnsqEuIlf9Rul4f0gj1NYqS6fs2cEBBUD9fY8nfkdOGRhTc+tShLAXgEOzYm7GVg==";

	public UpdateFile? FileFor(string arch)
	{
		if (!Files.TryGetValue(arch, out UpdateFile value))
		{
			if (!(arch == "arm64"))
			{
				return null;
			}
			return Files.GetValueOrDefault("x64");
		}
		return value;
	}

	public bool IsNewerThan(Version current)
	{
		return Normalize(ParsedVersion) > Normalize(current);
	}

	private static Version Normalize(Version v)
	{
		return new Version(v.Major, v.Minor, Math.Max(v.Build, 0));
	}

	public static UpdateManifest? Verify(string envelopeJson, string publicKeySpki = "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEG/X//ovhgo2nvjVgBTKQFGgnsqEuIlf9Rul4f0gj1NYqS6fs2cEBBUD9fY8nfkdOGRhTc+tShLAXgEOzYm7GVg==")
	{
		try
		{
			Envelope envelope = JsonSerializer.Deserialize<Envelope>(envelopeJson);
			if ((object)envelope == null || envelope.Format != 1)
			{
				return null;
			}
			byte[] array = Convert.FromBase64String(envelope.Payload);
			byte[] signature = Convert.FromBase64String(envelope.Signature);
			using ECDsa eCDsa = ECDsa.Create();
			eCDsa.ImportSubjectPublicKeyInfo(Convert.FromBase64String(publicKeySpki), out var _);
			if (!eCDsa.VerifyData(array, signature, HashAlgorithmName.SHA256))
			{
				return null;
			}
			UpdateManifest updateManifest = JsonSerializer.Deserialize<UpdateManifest>(array);
			if ((object)updateManifest == null || updateManifest.App != "Falak" || updateManifest.Files == null || updateManifest.Files.Count == 0)
			{
				return null;
			}
			if (!System.Version.TryParse(updateManifest.Version, out Version _))
			{
				return null;
			}
			foreach (var (value, updateFile2) in updateManifest.Files)
			{
				if (updateFile2.Name != $"Falak-{updateManifest.Version}-{value}.exe" || updateFile2.Size <= 0 || updateFile2.Sha256.Length != 64)
				{
					return null;
				}
			}
			return updateManifest;
		}
		catch (Exception ex) when ((ex is JsonException || ex is FormatException || ex is CryptographicException || ex is ArgumentException || ex is NotSupportedException) ? true : false)
		{
			return null;
		}
	}
}
