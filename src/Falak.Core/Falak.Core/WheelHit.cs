namespace Falak.Core;

public readonly record struct WheelHit(WheelHitKind Kind, int Slice)
{
	public static WheelHit None => new WheelHit(WheelHitKind.None, -1);

	public static WheelHit Center => new WheelHit(WheelHitKind.Center, -1);

	public static WheelHit OfSlice(int index)
	{
		return new WheelHit(WheelHitKind.Slice, index);
	}
}
