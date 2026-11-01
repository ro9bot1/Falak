using System.Text;

namespace Falak.Core;

public static class ArabicText
{
	public sealed record Decoded(string Text, string EncodingName, bool WasUtf8);

	static ArabicText()
	{
		Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
	}

	public static Decoded Decode(byte[] bytes)
	{
		if (bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
		{
			return new Decoded(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), "UTF-8", WasUtf8: true);
		}
		if (bytes.Length >= 2 && bytes[0] == byte.MaxValue && bytes[1] == 254)
		{
			return new Decoded(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16", WasUtf8: true);
		}
		if (bytes.Length >= 2 && bytes[0] == 254 && bytes[1] == byte.MaxValue)
		{
			return new Decoded(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), "UTF-16", WasUtf8: true);
		}
		try
		{
			return new Decoded(new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes), "UTF-8", WasUtf8: true);
		}
		catch (DecoderFallbackException)
		{
			return new Decoded(Encoding.GetEncoding(1256).GetString(bytes), "Windows-1256", WasUtf8: false);
		}
	}

	public static string ToLatinDigits(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			if (c >= '٠' && c <= '٩')
			{
				stringBuilder.Append((char)(48 + (c - 1632)));
			}
			else if (c >= '۰' && c <= '۹')
			{
				stringBuilder.Append((char)(48 + (c - 1776)));
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		return stringBuilder.ToString();
	}

	public static string ToArabicDigits(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			stringBuilder.Append((c >= '0' && c <= '9') ? ((char)(1632 + (c - 48))) : c);
		}
		return stringBuilder.ToString();
	}

	public static string CleanForSearch(string text)
	{
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		foreach (char c in text)
		{
			bool flag;
			switch (c)
			{
			case 'ـ':
			case '\u064b':
			case '\u064c':
			case '\u064d':
			case '\u064e':
			case '\u064f':
			case '\u0650':
			case '\u0651':
			case '\u0652':
			case '\u0653':
			case '\u0654':
			case '\u0655':
			case '\u0656':
			case '\u0657':
			case '\u0658':
			case '\u0659':
			case '\u065a':
			case '\u065b':
			case '\u065c':
			case '\u065d':
			case '\u065e':
			case '\u065f':
			case '\u0670':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (!flag)
			{
				StringBuilder stringBuilder2 = stringBuilder;
				char value;
				switch (c)
				{
				case 'آ':
				case 'أ':
				case 'إ':
				case 'ٱ':
					value = 'ا';
					break;
				case 'ى':
					value = 'ي';
					break;
				default:
					value = c;
					break;
				}
				stringBuilder2.Append(value);
			}
		}
		return stringBuilder.ToString();
	}

	public static string FoldPresentationForms(string text)
	{
		bool flag = false;
		string text2 = text;
		for (int i = 0; i < text2.Length; i++)
		{
			if (IsPresentationForm(text2[i]))
			{
				flag = true;
				break;
			}
		}
		if (!flag)
		{
			return text;
		}
		StringBuilder stringBuilder = new StringBuilder(text.Length);
		text2 = text;
		for (int i = 0; i < text2.Length; i++)
		{
			char c = text2[i];
			stringBuilder.Append(IsPresentationForm(c) ? c.ToString().Normalize(NormalizationForm.FormKC) : c.ToString());
		}
		return stringBuilder.ToString();
	}

	private static bool IsPresentationForm(char c)
	{
		if (c >= 'ﹰ')
		{
			if (c <= '\ufeff')
			{
				goto IL_0022;
			}
		}
		else if (c >= 'ﭐ' && c <= '﷿')
		{
			goto IL_0022;
		}
		bool flag = false;
		goto IL_0028;
		IL_0022:
		flag = true;
		goto IL_0028;
		IL_0028:
		if (flag)
		{
			return c != '\ufeff';
		}
		return false;
	}

	public static bool ContainsArabic(string text)
	{
		foreach (char c in text)
		{
			if (c >= '\u0600' && c <= 'ۿ')
			{
				return true;
			}
		}
		return false;
	}
}
