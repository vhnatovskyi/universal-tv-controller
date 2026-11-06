namespace BraviaRemote;

public partial class App : Application
{
    public App() => InitializeComponent();

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        var window = new Window(page) { Title = "BRAVIA Remote" };
        window.Stopped += (_, _) => page.SetVolumeTracking(false);
        window.Resumed += (_, _) => page.SetVolumeTracking(true);
        window.Destroying += (_, _) => page.SetVolumeTracking(false);
#if WINDOWS
        window.Width = 500;
        window.Height = 800;
#endif
        return window;
    }
}
