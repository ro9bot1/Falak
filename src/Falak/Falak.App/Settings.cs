using System;
using System.IO;
using System.Text.Json;
using Falak.Interop;

namespace Falak.App;

public sealed class Settings
{
	private const int CurrentVersion = 2;

	public string Language { get; set; } = "ar";

	public bool Paused { get; set; }

	public bool StartWithWindows { get; set; }

	public bool Onboarded { get; set; }

	public string Theme { get; set; } = "system";

	public string Accent { get; set; } = "emerald";

	public bool Glass { get; set; } = true;

	public bool WheelSound { get; set; } = true;

	public int Version { get; set; } = 2;

	public bool CheckUpdates { get; set; } = false;

	public DateTime? LastUpdateCheck { get; set; }

	public string? WhatsNew { get; set; }

	private static string FilePath => Path.Combine(AppPaths.Data, "settings.json");

	public BrandPalette Palette(bool forPopups = false)
	{
		string theme = Theme;
		ThemePreference theme2 = ((theme == "light") ? ThemePreference.Light : ((theme == "dark") ? ThemePreference.Dark : ThemePreference.System));
		return BrandPalette.From(theme2, AccentChoice.ById(Accent), Glass && !forPopups && Backdrop.Supported);
	}

	public static Settings Load()
	{
		try
		{
			if (!File.Exists(FilePath))
			{
				return new Settings();
			}
			string text = File.ReadAllText(FilePath);
			Settings settings = JsonSerializer.Deserialize<Settings>(text) ?? new Settings();
			if (!text.Contains("\"Version\""))
			{
				settings.Version = 1;
			}
			if (settings.Version < 2)
			{
				settings.Glass = true;
			}
			settings.Version = 2;
			return settings;
		}
		catch (Exception ex) when ((ex is IOException || ex is JsonException) ? true : false)
		{
			return new Settings();
		}
	}

	public void Save()
	{
		try
		{
			Directory.CreateDirectory(AppPaths.Data);
			File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions
			{
				WriteIndented = true
			}));
		}
		catch (IOException)
		{
		}
	}
}
