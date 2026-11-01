using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Media.MediaProperties;
using Windows.Media.Transcoding;
using Windows.Storage;

namespace Falak.Engines;

internal static class MediaEngine
{
	public enum Preset
	{
		Source,
		Messaging,
		Small,
		LightAudio
	}

	public static async Task<JobResult> Convert(IReadOnlyList<string> inputs, string extension, Preset preset, IProgress<double>? progress, CancellationToken ct)
	{
		StepProgress steps = new StepProgress(progress, inputs.Count);
		List<string> outputs = new List<string>();
		long before = 0, after = 0;
		foreach (string input in inputs)
		{
			ct.ThrowIfCancellationRequested();
			string destination = OutputPaths.NextTo(input, extension, preset switch
			{
				Preset.Messaging => "للمراسلة",
				Preset.Small => "صغير",
				Preset.LightAudio => "خفيف",
				_ => null
			});
			string folder = Path.GetDirectoryName(destination)!;
			string tempName = $".falak-{Guid.NewGuid():N}{extension}";
			StorageFile source = await WindowsRuntimeSystemExtensions.AsTask(
				StorageFile.GetFileFromPathAsync(Path.GetFullPath(input)));
			StorageFolder storageFolder = await WindowsRuntimeSystemExtensions.AsTask(
				StorageFolder.GetFolderFromPathAsync(folder));
			StorageFile target = await WindowsRuntimeSystemExtensions.AsTask(
				storageFolder.CreateFileAsync(tempName, CreationCollisionOption.FailIfExists));
			try
			{
				MediaTranscoder transcoder = new MediaTranscoder { HardwareAccelerationEnabled = true };
				PrepareTranscodeResult prepared = await WindowsRuntimeSystemExtensions.AsTask(
					transcoder.PrepareFileTranscodeAsync(source, target, Profile(extension, preset)));
				if (!prepared.CanTranscode)
					throw new UserFacingException(Explain(prepared.FailureReason, input));
				await WindowsRuntimeSystemExtensions.AsTask<double>(prepared.TranscodeAsync(), ct,
					new Progress<double>(percent => steps.Item(percent / 100.0)));
				File.Move(Path.Combine(folder, tempName), destination);
			}
			finally
			{
				string tempPath = Path.Combine(folder, tempName);
				if (File.Exists(tempPath)) File.Delete(tempPath);
			}
			before += new FileInfo(input).Length;
			after += new FileInfo(destination).Length;
			outputs.Add(destination);
			steps.Next();
		}
		bool compressed = preset is Preset.Messaging or Preset.Small or Preset.LightAudio;
		string message = compressed ? $"من {Texts.Size(before)} إلى {Texts.Size(after)}"
			: $"حوّلنا إلى {extension.TrimStart('.').ToUpperInvariant()} · {Texts.Size(after)}";
		return new JobResult(outputs, null, message);
	}

	private static MediaEncodingProfile Profile(string extension, Preset preset)
	{
		switch (extension)
		{
		case ".mp4":
			return preset switch
			{
				Preset.Messaging => MediaEncodingProfile.CreateMp4((VideoEncodingQuality)2), 
				Preset.Small => MediaEncodingProfile.CreateMp4((VideoEncodingQuality)3), 
				_ => MediaEncodingProfile.CreateMp4((VideoEncodingQuality)0), 
			};
		case ".mp3":
			if (preset == Preset.LightAudio)
			{
				return MediaEncodingProfile.CreateMp3((AudioEncodingQuality)2);
			}
			return MediaEncodingProfile.CreateMp3((AudioEncodingQuality)1);
		case ".wmv":
			return MediaEncodingProfile.CreateWmv((VideoEncodingQuality)0);
		case ".m4a":
			return MediaEncodingProfile.CreateM4a((AudioEncodingQuality)1);
		case ".wav":
			return MediaEncodingProfile.CreateWav((AudioEncodingQuality)1);
		case ".wma":
			return MediaEncodingProfile.CreateWma((AudioEncodingQuality)1);
		case ".flac":
			return MediaEncodingProfile.CreateFlac((AudioEncodingQuality)1);
		default:
			throw new UserFacingException("الصيغة " + extension + " غير مدعومة.");
		}
	}

	private static string Explain(TranscodeFailureReason reason, string input)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0002: Invalid comparison between Unknown and I4
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Invalid comparison between Unknown and I4
		if ((int)reason != 2)
		{
			if ((int)reason == 3)
			{
				return "ويندوز لا يملك الترميز اللازم لقراءة «" + Path.GetFileName(input) + "». إن كان الفيديو HEVC فثب\u0651ت «HEVC Video Extensions» من متجر مايكروسوفت.";
			}
			return "تعذ\u0651ر تحويل «" + Path.GetFileName(input) + "». قد يكون الملف تالفا\u064b.";
		}
		return "لا يمكن إنشاء هذه الصيغة من هذا الملف.";
	}
}
