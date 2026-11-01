using System;

namespace Falak.Core;

public sealed record WheelAction(string Id, string LabelAr, string LabelEn)
{
	public bool IsFormat => Id.StartsWith("convert.", StringComparison.Ordinal);

	public string? TargetExtension
	{
		get
		{
			if (!IsFormat)
			{
				return null;
			}
			return "." + Id.Substring("convert.".Length);
		}
	}
}
