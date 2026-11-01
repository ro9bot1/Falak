namespace Falak.Core;

public static class DropEffectPolicy
{
	public static DropEffect Choose(DropEffect allowedBySource)
	{
		if ((allowedBySource & DropEffect.Copy) != 0)
		{
			return DropEffect.Copy;
		}
		if ((allowedBySource & DropEffect.Link) != 0)
		{
			return DropEffect.Link;
		}
		return DropEffect.None;
	}

	public static DropEffect ForHit(WheelHit hit, DropEffect allowedBySource)
	{
		if (hit.Kind != WheelHitKind.None)
		{
			return Choose(allowedBySource);
		}
		return DropEffect.None;
	}
}
