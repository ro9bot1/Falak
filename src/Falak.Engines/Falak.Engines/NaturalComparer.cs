using System.Collections.Generic;

namespace Falak.Engines;

internal sealed class NaturalComparer : IComparer<string>
{
	public static readonly NaturalComparer Instance = new NaturalComparer();

	public int Compare(string? x, string? y)
	{
		if (x == null || y == null)
		{
			return string.CompareOrdinal(x, y);
		}
		int i = 0;
		int j = 0;
		while (i < x.Length && j < y.Length)
		{
			if (char.IsDigit(x[i]) && char.IsDigit(y[j]))
			{
				int num = i;
				int num2 = j;
				for (; i < x.Length && char.IsDigit(x[i]); i++)
				{
				}
				for (; j < y.Length && char.IsDigit(y[j]); j++)
				{
				}
				int num3 = num;
				string text = x.Substring(num3, i - num3).TrimStart('0');
				num3 = num2;
				string text2 = y.Substring(num3, j - num3).TrimStart('0');
				int num4 = text.Length.CompareTo(text2.Length);
				if (num4 != 0)
				{
					return num4;
				}
				int num5 = string.CompareOrdinal(text, text2);
				if (num5 != 0)
				{
					return num5;
				}
			}
			else
			{
				int num6 = char.ToLowerInvariant(x[i]).CompareTo(char.ToLowerInvariant(y[j]));
				if (num6 != 0)
				{
					return num6;
				}
				i++;
				j++;
			}
		}
		return (x.Length - i).CompareTo(y.Length - j);
	}
}
