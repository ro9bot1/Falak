using System.CodeDom.Compiler;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Falak.Interop.Native;

internal static class Winmm
{
	public const uint SND_ASYNC = 1u;

	public const uint SND_NODEFAULT = 2u;

	public const uint SND_MEMORY = 4u;
	[GeneratedCode("Microsoft.Interop.LibraryImportGenerator", "10.0.15.504")]
	[SkipLocalsInit]
	[return: MarshalAs(UnmanagedType.Bool)]
	public unsafe static bool PlaySound(void* sound, nint module, uint flags)
	{
		return __PInvoke(sound, module, flags) != 0;
		[DllImport("winmm.dll", EntryPoint = "PlaySoundW", ExactSpelling = true)]
		unsafe static extern int __PInvoke(void* __sound_native, nint __module_native, uint __flags_native);
	}
}
