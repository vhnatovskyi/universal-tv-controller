using System.Runtime.InteropServices;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;

namespace BraviaRemote;

public partial class MainPage
{
    internal void ConfigureWindowsChrome(Microsoft.Maui.Controls.Window window)
    {
        window.Created += (_, _) =>
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native
                || native.AppWindow.Presenter is not OverlappedPresenter presenter)
                return;

            presenter.SetBorderAndTitleBar(hasBorder: true, hasTitleBar: false);
            CaptionButtons.IsVisible = true;
            MinimizeWindowButton.Clicked += (_, _) => presenter.Minimize();
            MaximizeWindowButton.Clicked += (_, _) =>
            {
                if (presenter.State == OverlappedPresenterState.Maximized)
                    presenter.Restore();
                else
                    presenter.Maximize();
            };
            CloseWindowButton.Clicked += (_, _) => native.Close();

            if (TitleDragArea.Handler?.PlatformView is FrameworkElement dragArea)
                dragArea.PointerPressed += BeginWindowDrag;

            void BeginWindowDrag(object sender, PointerRoutedEventArgs args)
            {
                if (sender is not FrameworkElement element
                    || !args.GetCurrentPoint(element).Properties.IsLeftButtonPressed)
                    return;

                args.Handled = true;
                ReleaseCapture();
                SendMessage(WinRT.Interop.WindowNative.GetWindowHandle(native), 0x00A1, 2, 0);
            }
        };
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseCapture();

    [DllImport("user32.dll", EntryPoint = "SendMessageW")]
    private static extern nint SendMessage(nint window, uint message, nint wParam, nint lParam);
}
