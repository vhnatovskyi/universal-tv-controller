namespace UniversalTvRemote;

public partial class MainPage
{
    internal void ConfigureWindowsTitleBar(Window window)
    {
        var menu = new Button
        {
            Text = "☰",
            TextColor = Colors.White,
            FontFamily = "Segoe UI Symbol",
            FontSize = 16,
            BackgroundColor = Color.FromArgb("#141E2B"),
            BorderWidth = 0,
            WidthRequest = 46,
            HeightRequest = 32,
            MinimumHeightRequest = 0,
            MinimumWidthRequest = 0,
            Padding = 0,
            CornerRadius = 0
        };
        menu.SetDynamicResource(SemanticProperties.DescriptionProperty, "Loc.Menu");
        menu.Clicked += OnToggleMenu;
        desktopMenu = menu;
        MenuButton.IsVisible = false;
        Grid.SetRow(MenuOverlay, 0);
        Grid.SetRowSpan(MenuOverlay, 3);

        window.TitleBar = new TitleBar
        {
            Title = window.Title ?? "Universal TV Controller",
            Icon = "remote_mark.png",
            BackgroundColor = Color.FromArgb("#101820"),
            ForegroundColor = Colors.White,
            LeadingContent = menu
        };
        window.Created += (_, _) =>
        {
            if (window.Handler?.PlatformView is not Microsoft.UI.Xaml.Window native)
                return;

            native.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "remote.ico"));
            if (native.AppWindow.Presenter is Microsoft.UI.Windowing.OverlappedPresenter presenter)
            {
                presenter.IsResizable = false;
                presenter.IsMaximizable = false;
            }
        };
    }
}
