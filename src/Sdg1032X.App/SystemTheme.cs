using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace Sdg1032X.App;

internal static class SystemTheme
{
	[DllImport("dwmapi.dll")]
	private static extern int DwmSetWindowAttribute(
		IntPtr window,
		int attribute,
		ref int value,
		int size);

	public static void UseImmersiveDarkMode(Window window)
	{
		window.SourceInitialized+=(_, _)=>
		{
			int enabled=1;
			IntPtr handle=new WindowInteropHelper(window).Handle;
			DwmSetWindowAttribute(handle,20,ref enabled,sizeof(int));
		};
	}
}
