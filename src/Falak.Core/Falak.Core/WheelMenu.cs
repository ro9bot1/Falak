using System.Collections.Generic;

namespace Falak.Core;

public sealed record WheelMenu(WheelAction Hub, IReadOnlyList<WheelAction> Slices)
{
	public bool IsPending => Hub.Id == SliceCatalog.Pending.Id;

	public bool IsEmpty => Hub.Id == SliceCatalog.Nothing.Id;

	public const int MaxSlices = 8;

	public WheelAction? ActionFor(WheelHit hit)
	{
		if (hit.Kind != WheelHitKind.Slice || IsPending || hit.Slice < 0 || hit.Slice >= Slices.Count)
		{
			return null;
		}
		return Slices[hit.Slice];
	}
}
