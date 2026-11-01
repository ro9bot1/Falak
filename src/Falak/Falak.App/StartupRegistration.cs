using System;
using Microsoft.Win32;

namespace Falak.App;

internal static class StartupRegistration
{
	private const string RunKey = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";

	private const string Name = "Falak";

	public static void Set(bool enabled)
	{
		using RegistryKey registryKey = Registry.CurrentUser.OpenSubKey("Software\\Microsoft\\Windows\\CurrentVersion\\Run", writable: true);
		if (registryKey != null)
		{
			if (enabled)
			{
				registryKey.SetValue(Name, "\"" + Environment.ProcessPath + "\" --tray");
			}
			else
			{
				registryKey.DeleteValue(Name, throwOnMissingValue: false);
			}
		}
	}
}
