namespace BraviaRemote;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        Localization.Initialize(Resources);
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var page = new MainPage();
        var window = new Window(page) { Title = "Universal TV Controller v" + global::BraviaRemote.MainPage.DisplayVersion };
        window.Stopped += (_, _) => page.SetVolumeTracking(false);
        window.Deactivated += (_, _) => page.CancelVolumeChange();
        window.Resumed += (_, _) => page.SetVolumeTracking(true);
        window.Destroying += (_, _) => page.SetVolumeTracking(false);
        window.Destroying += (_, _) => page.CancelVoiceInput();
#if WINDOWS
        window.Width = 420;
        window.Height = 520;
        page.ConfigureWindowsTitleBar(window);
#endif
        return window;
    }
}
