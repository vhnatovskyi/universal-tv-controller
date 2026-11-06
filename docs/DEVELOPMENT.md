# Development and releases

[Українська](DEVELOPMENT.uk.md)

- `src/App` holds the MAUI interface, local settings, localization, and platform code.
- `src/Shared` contains the Google TV pairing and remote client.
- `installer` contains the Windows setup definition, `scripts` builds and verifies the release, and `.github/workflows` runs CI and publishes tagged releases.

For Windows builds, install the .NET 10 SDK, the `maui-windows` workload, and Inno Setup 6. From the repository root:

```powershell
./scripts/Build-Release.ps1
./scripts/Test-Installer.ps1
```

Run the installer test from a clean Windows environment with Universal TV Controller closed. The test installs into a temporary directory and then uninstalls. For a portable-only local build, use `./scripts/Build-Release.ps1 -SkipInstaller`.

On a Mac with compatible Xcode and .NET 10:

```sh
dotnet workload install maui-ios
dotnet build src/App/BraviaRemote.csproj -f net10.0-ios -t:Run -p:RuntimeIdentifier=iossimulator-arm64
```

Select a development certificate and provisioning profile for the physical iPhone. Windows development can also use [Pair to Mac](https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0).

App shortcuts use the [Android TV Remote protocol](https://github.com/sonalder-darlene/libremote-android-tv/blob/main/PROTOCOL.md). Pairing uses port 6467, and app launches use port 6466. The pairing credential is stored locally in the app’s secure storage.

To release a version:

1. Set the same three-part version in `version.json` and `ApplicationDisplayVersion` in the MAUI project. Increase `ApplicationVersion` for iOS.
2. Update both languages in [RELEASE-NOTES.md](RELEASE-NOTES.md), then run the Windows build and installer check.
3. Push to `main` and push the matching tag `vX.Y.Z`. GitHub Actions builds the Windows ZIP and setup executable, verifies installation, and publishes both with `SHA256SUMS.txt`. CI also builds the iOS simulator target. Publishing a signed iPhone build requires separate signing credentials.
