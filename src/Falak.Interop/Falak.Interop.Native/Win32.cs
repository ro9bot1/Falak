namespace Falak.Interop.Native;

internal static class Win32
{
	public const int WH_KEYBOARD_LL = 13;

	public const int WH_MOUSE_LL = 14;

	public const int HC_ACTION = 0;

	public const uint WM_QUIT = 18u;

	public const uint WM_MOUSEACTIVATE = 33u;

	public const uint WM_KEYDOWN = 256u;

	public const uint WM_KEYUP = 257u;

	public const uint WM_SYSKEYDOWN = 260u;

	public const uint WM_SYSKEYUP = 261u;

	public const uint WM_MOUSEMOVE = 512u;

	public const uint WM_LBUTTONDOWN = 513u;

	public const uint WM_LBUTTONUP = 514u;

	public const int MA_NOACTIVATE = 3;

	public const uint WS_POPUP = 2147483648u;

	public const uint WS_EX_TOPMOST = 8u;

	public const uint WS_EX_TOOLWINDOW = 128u;

	public const uint WS_EX_LAYERED = 524288u;

	public const uint WS_EX_NOACTIVATE = 134217728u;

	public const int SW_HIDE = 0;

	public const int SW_SHOWNOACTIVATE = 4;

	public static readonly nint HWND_TOPMOST = -1;

	public const uint SWP_NOSIZE = 1u;

	public const uint SWP_NOMOVE = 2u;

	public const uint SWP_NOACTIVATE = 16u;

	public const uint ULW_ALPHA = 2u;

	public const byte AC_SRC_OVER = 0;

	public const byte AC_SRC_ALPHA = 1;

	public const int SM_CXDRAG = 68;

	public const int SM_SWAPBUTTON = 23;

	public const int SM_CXSMICON = 49;

	public const int SM_CYSMICON = 50;

	public const int VK_LBUTTON = 1;

	public const int VK_RBUTTON = 2;

	public const uint IMAGE_ICON = 1u;

	public const uint LR_LOADFROMFILE = 16u;

	public const uint WM_APP = 32768u;

	public const uint WM_CONTEXTMENU = 123u;

	public const int SM_CYDRAG = 69;

	public const uint MONITOR_DEFAULTTONEAREST = 2u;

	public const int MDT_EFFECTIVE_DPI = 0;

	public const uint GMEM_MOVEABLE = 2u;

	public const short CF_HDROP = 15;

	public const uint SIGDN_NORMALDISPLAY = 0u;

	public const uint SIGDN_FILESYSPATH = 2147844096u;

	public const int S_OK = 0;

	public const int S_FALSE = 1;
}
