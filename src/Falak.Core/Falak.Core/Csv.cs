using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Falak.Core;

public static class Csv
{
	public static List<List<string>> Parse(string text, char delimiter)
	{
		List<List<string>> list = new List<List<string>>();
		List<string> list2 = new List<string>();
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		for (int i = 0; i < text.Length; i++)
		{
			char c = text[i];
			if (flag)
			{
				if (c == '"' && i + 1 < text.Length && text[i + 1] == '"')
				{
					stringBuilder.Append('"');
					i++;
				}
				else if (c == '"')
				{
					flag = false;
				}
				else
				{
					stringBuilder.Append(c);
				}
			}
			else if (c == '"' && stringBuilder.Length == 0)
			{
				flag = true;
			}
			else if (c == delimiter)
			{
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
			}
			else if ((c == '\n' || c == '\r') ? true : false)
			{
				if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
				{
					i++;
				}
				list2.Add(stringBuilder.ToString());
				stringBuilder.Clear();
				list.Add(list2);
				list2 = new List<string>();
			}
			else
			{
				stringBuilder.Append(c);
			}
		}
		if (stringBuilder.Length > 0 || list2.Count > 0)
		{
			list2.Add(stringBuilder.ToString());
			list.Add(list2);
		}
		return list;
	}

	public static string Write(IEnumerable<IReadOnlyList<string>> rows, char delimiter)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (IReadOnlyList<string> row in rows)
		{
			for (int i = 0; i < row.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(delimiter);
				}
				string text = row[i];
				bool flag = text.Contains(delimiter) || text.Contains('"') || text.Contains('\n') || text.Contains('\r');
				stringBuilder.Append(flag ? ("\"" + text.Replace("\"", "\"\"") + "\"") : text);
			}
			stringBuilder.Append("\r\n");
		}
		return stringBuilder.ToString();
	}

	public static char Sniff(string text, string extension)
	{
		if (extension.Equals(".tsv", StringComparison.OrdinalIgnoreCase))
		{
			return '\t';
		}
		string source = text.Split('\n', 2)[0];
		int num = source.Count((char c) => c == ',');
		int num2 = source.Count((char c) => c == ';');
		int num3 = source.Count((char c) => c == '\t');
		if (num3 > num && num3 >= num2)
		{
			return '\t';
		}
		if (num2 > num)
		{
			return ';';
		}
		return ',';
	}
}
