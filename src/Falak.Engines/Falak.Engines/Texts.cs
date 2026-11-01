namespace Falak.Engines;

internal static class Texts
{
	public static string Made(int count, string one, string two, string many)
	{
		return count switch
		{
			1 => "أنشأنا " + one, 
			2 => "أنشأنا " + two, 
			_ => $"أنشأنا {count} {many}", 
		};
	}

	public static string Files(int count)
	{
		if (count <= 10)
		{
			return count switch
			{
				1 => "ملفا\u064b واحدا\u064b", 
				2 => "ملفين", 
				_ => $"{count} ملفات", 
			};
		}
		return $"{count} ملفا\u064b";
	}

	public static string Size(long bytes)
	{
		if (bytes < 1048576)
		{
			if (bytes < 1024)
			{
				return $"{bytes} بايت";
			}
			return $"{(double)bytes / 1024.0:0} ك.ب";
		}
		if (bytes < 1073741824)
		{
			return $"{(double)bytes / 1048576.0:0.0} م.ب";
		}
		return $"{(double)bytes / 1073741824.0:0.00} ج.ب";
	}
}
