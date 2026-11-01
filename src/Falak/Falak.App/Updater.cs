using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Falak.Core;
using Falak.Engines;

namespace Falak.App;

internal static class Updater
{
	private static readonly string Site;

	private static readonly HttpClient Http;

	private const int AppModelErrorNoPackage = 15700;

	public static Version Current { get; }

	public static string CurrentText => $"{Current.Major}.{Current.Minor}.{Current.Build}";

	public static string Arch
	{
		get
		{
			if (RuntimeInformation.ProcessArchitecture != Architecture.Arm64)
			{
				return "x64";
			}
			return "arm64";
		}
	}

	private static string UpdatesFolder => Path.Combine(AppPaths.Data, "updates");

	private static string OldCopy => Environment.ProcessPath + ".old";

	public static bool CanReplaceInPlace
	{
		get
		{
			if (!IsPackaged())
			{
				string processPath = Environment.ProcessPath;
				if (processPath != null)
				{
					if (!string.IsNullOrEmpty(typeof(Updater).Assembly.Location))
					{
						return false;
					}
					try
					{
						string path = Path.Combine(Path.GetDirectoryName(processPath), $".falak-write-{Guid.NewGuid():N}");
						File.WriteAllBytes(path, default(ReadOnlySpan<byte>));
						File.Delete(path);
						return true;
					}
					catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
					{
						return false;
					}
				}
			}
			return false;
		}
	}

	public static string Spaced(string words, string version)
	{
		return words + "\u2002" + version;
	}

	private static HttpClient CreateClient()
	{
		HttpClient httpClient = new HttpClient();
		httpClient.Timeout = TimeSpan.FromMinutes(10L);
		httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Falak/{CurrentText} ({Arch}; Windows {Environment.OSVersion.Version})");
		return httpClient;
	}

	public static async Task<UpdateManifest?> CheckAsync(CancellationToken ct = default(CancellationToken))
	{
        if (string.IsNullOrWhiteSpace(Site)) return null;
		using CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
		timeout.CancelAfter(TimeSpan.FromSeconds(20L));
		UpdateManifest updateManifest = UpdateManifest.Verify(await Http.GetStringAsync(Site + "latest.json", timeout.Token)) ?? throw new UserFacingException("ملف الإصدار على الخادم غير موث\u0651ق، فلم نعتمده.");
		return (updateManifest.IsNewerThan(Current) && (object)updateManifest.FileFor(Arch) != null) ? updateManifest : null;
	}

	public static JobItem NewJob(string version)
	{
		return new JobItem(new WheelAction("app.update", "تحديث فَلَك", "Update Falak"), new _003C_003Ez__ReadOnlySingleElementList<string>("الإصدار " + version))
		{
			BadgeOverride = version,
			TitleFor = (JobState state) => state switch
			{
				JobState.Running => Spaced("نحم\u0651ل فَلَك", version), 
				JobState.Done => "التحديث جاهز", 
				JobState.Cancelled => "أ\u064fلغي التحديث", 
				JobState.Failed => "تعذ\u0651ر التحديث", 
				_ => "تحديث فَلَك", 
			}
		};
	}

	public static async Task<string> DownloadAsync(UpdateManifest manifest, IProgress<double> progress, CancellationToken ct)
	{
		UpdateFile file = manifest.FileFor(Arch);
		Directory.CreateDirectory(UpdatesFolder);
		string target = Path.Combine(UpdatesFolder, file.Name);
		foreach (string item in from f in Directory.EnumerateFiles(UpdatesFolder)
			where !f.Equals(target, StringComparison.OrdinalIgnoreCase)
			select f)
		{
			TryDelete(item);
		}
		bool flag = File.Exists(target);
		if (flag)
		{
			flag = await MatchesAsync(target, file, ct);
		}
		if (flag)
		{
			return target;
		}
		string part = target + ".part";
		try
		{
			using HttpResponseMessage response = await Http.GetAsync(Site + "update?arch=" + (manifest.Files.ContainsKey(Arch) ? Arch : "x64"), HttpCompletionOption.ResponseHeadersRead, ct);
			if (!response.IsSuccessStatusCode)
			{
				throw new UserFacingException($"الخادم لم ي\u064fرسل التحديث (الرمز {(int)response.StatusCode}). حاول لاحقا\u064b.");
			}
			using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
			long received = 0L;
			await using (Stream source = await response.Content.ReadAsStreamAsync(ct))
			{
				await using FileStream output = new FileStream(part, FileMode.Create, FileAccess.Write, FileShare.None, 65536, useAsync: true);
				byte[] buffer = new byte[65536];
				int num;
				while ((num = await source.ReadAsync(buffer, ct)) > 0)
				{
					received += num;
					if (received > file.Size)
					{
						throw new UserFacingException("حجم التحديث أكبر من المعلن، فأوقفنا التحميل.");
					}
					hash.AppendData(buffer, 0, num);
					await output.WriteAsync(buffer.AsMemory(0, num), ct);
					progress.Report((double)received / (double)file.Size);
				}
			}
			string text = Convert.ToHexStringLower(hash.GetHashAndReset());
			if (received != file.Size || !text.Equals(file.Sha256, StringComparison.OrdinalIgnoreCase))
			{
				throw new UserFacingException("النسخة التي وصلت لا تطابق البصمة الموق\u0651عة، فلم نستخدمها. حاول لاحقا\u064b.");
			}
			File.Move(part, target, overwrite: true);
			return target;
		}
		catch (HttpRequestException inner)
		{
			throw new UserFacingException("انقطع الاتصال أثناء تحميل التحديث. تحقق من الإنترنت ثم حاول مجددا\u064b.", inner);
		}
		finally
		{
			TryDelete(part);
		}
	}

	private static async Task<bool> MatchesAsync(string path, UpdateFile file, CancellationToken ct)
	{
		if (new FileInfo(path).Length != file.Size)
		{
			return false;
		}
		bool result;
		await using (FileStream stream = File.OpenRead(path))
		{
			result = Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct)).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
		}
		return result;
	}

	public static void Install(string verifiedExe)
	{
		string processPath = Environment.ProcessPath;
		TryDelete(OldCopy);
		File.Move(processPath, OldCopy);
		try
		{
			File.Copy(verifiedExe, processPath);
		}
		catch
		{
			File.Move(OldCopy, processPath);
			throw;
		}
		Log.Write("Update installed over " + processPath + "; restarting.");
		Process.Start(new ProcessStartInfo(processPath, "--tray --after-update " + CurrentText)
		{
			UseShellExecute = false
		});
	}

	public static void CleanUp()
	{
		Task.Run(async () =>
		{
			for (int i = 0; i < 20; i++)
			{
				if (!File.Exists(OldCopy))
				{
					break;
				}
				TryDelete(OldCopy);
				if (File.Exists(OldCopy))
				{
					await Task.Delay(500);
				}
			}
			if (Directory.Exists(UpdatesFolder))
			{
				foreach (string item in Directory.EnumerateFiles(UpdatesFolder))
				{
					TryDelete(item);
				}
			}
		});
	}

	private static void TryDelete(string path)
	{
		try
		{
			if (File.Exists(path))
			{
				File.Delete(path);
			}
		}
		catch (Exception ex) when ((ex is IOException || ex is UnauthorizedAccessException) ? true : false)
		{
		}
	}

	private static bool IsPackaged()
	{
		int packageFullNameLength = 0;
		return GetCurrentPackageFullName(ref packageFullNameLength, 0) != 15700;
	}

	[SkipLocalsInit]
	private unsafe static int GetCurrentPackageFullName(ref int packageFullNameLength, nint packageFullName)
	{
		int result;
		fixed (int* _packageFullNameLength_native = &packageFullNameLength)
		{
			result = __PInvoke(_packageFullNameLength_native, packageFullName);
		}
		return result;
		[DllImport("kernel32.dll", EntryPoint = "GetCurrentPackageFullName", ExactSpelling = true)]
		unsafe static extern int __PInvoke(int* __packageFullNameLength_native, nint __packageFullName_native);
	}

	static Updater()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("FALAK_UPDATE_SITE");
		Site = ((environmentVariable != null && environmentVariable.Length > 0) ? (environmentVariable.TrimEnd('/') + "/") : string.Empty);
		Current = typeof(Updater).Assembly.GetName().Version ?? new Version(0, 0, 0);
		Http = CreateClient();
	}
}
