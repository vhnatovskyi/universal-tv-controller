namespace BraviaRemote;

public partial class MainPage
{
    internal void ConfigureWindowsTitleBar(Window window)
    {
        var iconFont = OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)
            ? "Segoe Fluent Icons"
            : "Segoe MDL2 Assets";
        var menu = new ImageButton
        {
            Source = new FontImageSource { Glyph = "\uE700", FontFamily = iconFont, Size = 15, Color = Colors.White },
            BackgroundColor = Color.FromArgb("#141E2B"),
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
            Title = window.Title ?? "BRAVIA Remote",
            Icon = "remote_mark.png",
            BackgroundColor = Color.FromArgb("#101820"),
            ForegroundColor = Colors.White,
            LeadingContent = menu
        };
        window.Created += (_, _) =>
        {
            if (window.Handler?.PlatformView is Microsoft.UI.Xaml.Window native)
                native.AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "remote.ico"));
        };
    }
}
