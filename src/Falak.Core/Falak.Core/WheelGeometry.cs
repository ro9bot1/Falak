using System;

namespace Falak.Core;

public sealed class WheelGeometry
{
	public const double DesignSize = 300.0;

	public const double DesignCenterRadius = 46.0;

	public const double DesignInnerRadius = 56.0;

	public const double DesignOuterRadius = 128.0;

	public const double DesignHitSlack = 40.0;

	public int SliceCount { get; }

	public double Scale { get; }

	public double Size => 300.0 * Scale;

	public int SizePx => (int)Math.Round(Size);

	public double CenterRadius => 46.0 * Scale;

	public double InnerRadius => 56.0 * Scale;

	public double OuterRadius => 128.0 * Scale;

	public double SliceSweep
	{
		get
		{
			if (SliceCount != 0)
			{
				return 360.0 / (double)SliceCount;
			}
			return 0.0;
		}
	}

	public WheelGeometry(int sliceCount, double scale = 1.0)
	{
		ArgumentOutOfRangeException.ThrowIfNegative(sliceCount, "sliceCount");
		ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(scale, 0.0, "scale");
		SliceCount = sliceCount;
		Scale = scale;
	}

	public WheelHit HitTest(double dx, double dy)
	{
		double num = Math.Sqrt(dx * dx + dy * dy);
		if (num <= CenterRadius)
		{
			return WheelHit.Center;
		}
		if (SliceCount == 0)
		{
			return WheelHit.None;
		}
		if (num < InnerRadius - 4.0 * Scale)
		{
			return WheelHit.None;
		}
		if (num > OuterRadius + 40.0 * Scale)
		{
			return WheelHit.None;
		}
		return WheelHit.OfSlice(Math.Min((int)((ClockwiseDegreesFromTop(dx, dy) + SliceSweep / 2.0) % 360.0 / SliceSweep), SliceCount - 1));
	}

	public double SliceStartDegrees(int index)
	{
		return (double)index * SliceSweep - SliceSweep / 2.0;
	}

	public static double ClockwiseDegreesFromTop(double dx, double dy)
	{
		double num = Math.Atan2(dy, dx) * 180.0 / Math.PI + 90.0;
		if (num < 0.0)
		{
			num += 360.0;
		}
		if (num >= 360.0)
		{
			num -= 360.0;
		}
		return num;
	}
}
