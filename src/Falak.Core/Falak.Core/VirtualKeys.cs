namespace Falak.Core;

public static class VirtualKeys
{
	public const int Escape = 27;

	public const int Space = 32;

	public const int Shift = 16;

	public const int Control = 17;

	public const int Menu = 18;

	public const int LShift = 160;

	public const int RShift = 161;

	public const int LControl = 162;

	public const int RControl = 163;

	public const int LMenu = 164;

	public const int RMenu = 165;

	public static int Normalize(int vk)
	{
		switch (vk)
		{
		case 160:
		case 161:
			return 16;
		case 162:
		case 163:
			return 17;
		case 164:
		case 165:
			return 18;
		default:
			return vk;
		}
	}
}
