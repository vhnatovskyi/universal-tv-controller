# BRAVIA Remote

[Українська](README.uk.md)

A .NET MAUI remote for Sony BRAVIA on Windows and iPhone. It connects to the TV over the local network using Sony Simple IP Control (TCP 20060). The app was initially tested with a BRAVIA 3.

## Install on Windows

1. Open [GitHub Releases](https://github.com/vhnatovskyi/tv-controller/releases/latest) and download `BraviaRemote-Setup-X.Y.Z.exe` for Windows x64.
2. Run the installer, then open **BRAVIA Remote** from the Start menu. The installer runs for the current user. To remove the app, use **Settings → Apps → Installed apps**.
3. For portable use, extract the entire `BraviaRemote-X.Y.Z-Windows-x64.zip` archive and run `BraviaRemote.exe`.

## Use the remote

1. Connect the TV and your Windows PC or iPhone to the same home network. Turn on **Simple IP Control** on the TV. On iPhone, allow local network access when prompted.
2. Add a TV profile with a name and the TV’s IPv4 address. The first profile becomes the default. To change it, open **☰ → Settings**, select another profile, and choose **Set as default**. The app opens the default profile next time.
3. The volume chip updates about every three seconds while the remote is open. **Check connection** shows the result in a temporary notification.
4. In **Settings → Configure quick buttons**, choose **Pair with Google TV** and enter the six-character code on the TV. Then select YouTube, Netflix, or Kyivstar TV and save the button. You can add up to six buttons per profile. The app must be installed on the TV for the shortcut to open it.
5. To switch languages, open **Settings → App language**. English is the default.

Wake from standby depends on the TV’s Remote Start setting and whether Simple IP Control remains reachable in standby.

## Build and release

See the [development and release guide](docs/DEVELOPMENT.md). The iPhone version requires a Mac with Xcode and signing for a physical device.

The app checks GitHub Releases at startup. Use **☰ → Updates** to check manually. A banner links to the release when a newer stable version is available. Downloading and installing updates is manual.

- [Sony Simple IP Control](https://pro-bravia.sony.net/remote-display-control/simple-ip-control/)
- [Pair to Mac for iOS development](https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0)
- [Apple Personal Team](https://developer.apple.com/help/account/basics/about-your-developer-account)
