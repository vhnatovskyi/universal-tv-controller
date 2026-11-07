# Universal TV Controller

[Українська](README.uk.md)

A .NET MAUI remote for Sony BRAVIA on Windows and iPhone, using Google TV pairing over your home network.

## Install on Windows

Download `UniversalTvController-Setup-X.Y.Z.exe` from [GitHub Releases](https://github.com/vhnatovskyi/universal-tv-controller/releases/latest). The installer adds the app to the Start menu and supports removal through Windows Settings. A portable ZIP is also available.

## Connect a TV

1. Connect the TV and PC or iPhone to the same home network.
2. Add a profile, select your TV from the discovered devices, and choose **Pair and save**. The TV name is filled in automatically and can be edited.
3. Enter the six-character code shown on the TV. The profile is saved once pairing succeeds.
4. Use **Settings** to choose the default profile, pair again, or add quick buttons for YouTube, Netflix, and Kyivstar TV.

Version 0.3.4 resets profiles from earlier versions and their pairing credentials. Create a new paired profile after upgrading.

The volume and active-app indicator update from Google TV while the remote is open. Hold − or + to change volume continuously; release to stop. The 3, 5, 10, 30, and 100 buttons automatically adjust to that exact level using the TV's reported volume, regardless of the starting level. Large changes can take a few seconds. An unrecognized app shows a generic icon and its package name as a tooltip. App shortcuts report success when the TV confirms the requested app is in the foreground.

For BRAVIA 3 power-on, enable **Settings → Network & Internet → Remote start → On (Powered on by apps)** on the TV. If already enabled, turn it off and back on, as [Sony recommends](https://www.sony.co.uk/electronics/support/articles/00364921).

The interface follows the system language, with English as a fallback. A manual language override is available in Settings. **Updates** lists releases. On Windows, **Update to the latest version** downloads and verifies the installer, then closes the app and opens the installation wizard in the current app folder. TV profiles are kept. On iPhone, the button opens the latest release page. Release pages remain available for manual installation.

For voice input, open a search or text field on the TV, press the microphone, speak a short phrase, and review the text before sending. Windows uses an installed speech recognizer for the selected language. iPhone asks for microphone and speech recognition access. The TV settings button is temporarily removed while Sony compatibility is investigated.

## Development

See the [development guide](docs/DEVELOPMENT.md). The iPhone version requires a Mac with Xcode and Apple signing for a physical device.

- [Google TV Remote protocol reference](https://github.com/sonalder-darlene/libremote-android-tv/blob/main/PROTOCOL.md)
- [Pair to Mac](https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0)

Windows v0.4.3 uses shared .NET 10 (x64) and Windows App Runtime 1.7 (x64). Setup checks these dependencies and downloads missing components from Microsoft; .NET installation may ask for administrator permission. The ZIP requires the same runtimes (run Install-Dependencies.ps1 with Windows PowerShell). Setup applies Windows LZX compression to app files on supported NTFS disks. Runtime storage is additional and shared with other apps.

## Author

Vladyslav Hnatovskyi — [GitHub](https://github.com/vhnatovskyi) · [LinkedIn](https://www.linkedin.com/in/vladyslav-hnatovskyi/) · [vhnatovskyi@hotmail.com](mailto:vhnatovskyi@hotmail.com)
