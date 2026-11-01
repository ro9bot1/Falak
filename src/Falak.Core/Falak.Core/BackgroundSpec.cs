namespace Falak.Core;

public sealed record BackgroundSpec(Rgb From, Rgb? To = null, double Angle = 45.0)
{
	public bool IsGradient => To.HasValue;
}
