using System;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Falak.Core;

public static partial class Subtitles
{
	[GeneratedRegex("(\\d{2}:\\d{2}:\\d{2}),(\\d{3})")]
	private static partial Regex SrtTime();

	[GeneratedRegex("(\\d{2}:\\d{2}:\\d{2})\\.(\\d{3})")]
	private static partial Regex VttTime();

	[GeneratedRegex("^(\\d{1,2}:)?\\d{2}:\\d{2}[.,]\\d{3}\\s*-->")]
	private static partial Regex CueTiming();

	[GeneratedRegex("(?<!\\d:)\\b(\\d{2}:\\d{2})\\.(\\d{3})")]
	private static partial Regex ShortVttTime();

	public static string SrtToVtt(string srt)
	{
		string text = SrtTime().Replace(Normalize(srt), "$1.$2");
		return "WEBVTT\n\n" + text.TrimStart();
	}

	public static string VttToSrt(string vtt)
	{
		string[] array = Normalize(vtt).Split('\n');
		StringBuilder stringBuilder = new StringBuilder();
		int num = 0;
		bool flag = false;
		string[] array2 = array;
		for (int i = 0; i < array2.Length; i++)
		{
			string text = array2[i].TrimEnd();
			if (CueTiming().IsMatch(text))
			{
				if (flag)
				{
					stringBuilder.Append('\n');
				}
				stringBuilder.Append(++num).Append('\n');
				string input = ShortVttTime().Replace(text, "00:$1.$2");
				input = VttTime().Replace(input, "$1,$2");
				int num2 = input.IndexOf("-->", StringComparison.Ordinal);
				string[] array3 = input.Substring(num2 + 3).Trim().Split(' ', 2);
				stringBuilder.Append(input.Substring(0, num2).Trim()).Append(" --> ").Append(array3[0])
					.Append('\n');
				flag = true;
			}
			else if (flag && text.Length > 0)
			{
				stringBuilder.Append(text).Append('\n');
			}
			else if (text.Length == 0)
			{
				flag = false;
			}
		}
		return stringBuilder.ToString().Replace("\n\n\n", "\n\n");
	}

	public static string ToPlainText(string subtitles)
	{
		StringBuilder stringBuilder = new StringBuilder();
		string[] array = Normalize(subtitles).Split('\n');
		for (int i = 0; i < array.Length; i++)
		{
			string text = array[i].Trim();
			if (text.Length != 0 && !(text == "WEBVTT") && !CueTiming().IsMatch(text) && !text.All(char.IsDigit))
			{
				stringBuilder.AppendLine(Regex.Replace(text, "<[^>]+>", ""));
			}
		}
		return stringBuilder.ToString();
	}

	private static string Normalize(string text)
	{
		return text.Replace("\r\n", "\n").Replace('\r', '\n');
	}
}
