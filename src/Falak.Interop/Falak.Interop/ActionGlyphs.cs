using System;
namespace Falak.Interop;

public static class ActionGlyphs
{
	public static string For(string actionId)
	{
        if (actionId.StartsWith("macro.", StringComparison.Ordinal)) return "\ue768";
		switch (actionId)
		{
		case "convert.jpg":
		case "convert.png":
		case "convert.bmp":
		case "convert.gif":
		case "convert.tiff":
		case "convert.webp":
			return "\ue91b";
		case "convert.pdf":
		case "convert.docx":
		case "convert.html":
		case "convert.epub":
		case "convert.md":
			return "\ue8a5";
		case "convert.txt":
		case "tool.fix-encoding":
			return "\ue8d2";
		case "convert.tsv":
		case "convert.csv":
		case "convert.xlsx":
		case "tool.excel-ready":
			return "\ue80a";
		case "convert.srt":
		case "convert.vtt":
			return "\ue7f0";
		case "convert.mp4":
		case "convert.wmv":
			return "\ue714";
		case "convert.mp3":
		case "convert.m4a":
		case "convert.wav":
		case "convert.wma":
		case "convert.flac":
			return "\ue720";
		case "tool.compress-pdf":
		case "tool.small-video":
		case "tool.audio-small":
		case "tool.compress":
		case "tool.compress-pdf-strong":
			return "\ue73f";
		case "tool.messaging":
			return "\ue724";
		case "tool.half-size":
			return "\ue71f";
		case "tool.strip-metadata":
			return "\ue72e";
		case "tool.merge":
		case "tool.merge-all":
		case "tool.images-to-pdf":
			return "\ue8f1";
		case "tool.split":
			return "\ue8c6";
		case "tool.rotate":
			return "\ue7ad";
		case "tool.background":
			return "\ue790";
		case "tool.zip":
			return "\ue7b8";
		case "action.show":
		case "tool.extract":
			return "\ue838";
		case "tool.digits-latin":
		case "tool.digits-arabic":
			return "\ue8ef";
		case "tool.clean-arabic":
			return "\ue721";
		case "action.macros":
            return "\ue8a5";
		case "action.copy-path":
			return "\ue71b";
		case "action.copy-files":
			return "\ue8c8";
		case "action.copy-to":
			return "\ue8f4";
		case "action.move-to":
			return "\ue8de";
		case "action.terminal":
			return "\ue756";
		default:
			return "\ue7c3";
		}
	}
}
