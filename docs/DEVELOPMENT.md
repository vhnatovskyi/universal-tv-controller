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

Run the local Google TV protocol regression checks (no TV or additional test packages required):

```powershell
dotnet run --project tests/GoogleTvProtocol/GoogleTvProtocol.csproj
```

The checks use a local TLS server on port 6466 to verify Sony feature 16 negotiation, confirmed power commands, idempotent power state changes, controls without volume support, actual app launches despite an initial matching package report, and text input. Sony BRAVIA 3 requires feature 16 for system keys: volume works without it, but POWER is silently ignored. Power-off and power-on were verified on the physical TV. The settings button holds HOME for 1.2 seconds (DOWN then UP), which opens the Google TV settings side panel on BRAVIA 3. Both the raw command and production client were verified on the physical TV. SETTINGS (176), NOTIFICATION (83), and a long SETTINGS press were ignored. HOME is released in a finally block, including after cancellation, with a separate release timeout. Power-on uses Wake-on-LAN as described below.

Run `dotnet run --project tests/ReleaseInstaller/ReleaseInstaller.csproj` to check update downloads, checksum validation, HTTP errors, cancellation, and partial-file cleanup without downloading or executing a real installer.

Volume presets confirm each step on one Google TV connection; holding a volume button repeats until release, deactivation, or a volume limit. Tests cover all five presets from different initial levels, cancellation, and missing confirmations. Values 3, 5 and 10 were verified on BRAVIA 3.

Sony BRAVIA 3 wake-up uses a Wake-on-LAN magic packet followed by a pinned Google TV handshake to confirm the TV is on. Windows resolves the MAC with `SendARP` and stores it as the optional `MacAddress` profile field. No POWER retry is sent after a wake packet. Enable Remote start on the TV, following [Sony's BRAVIA 3 instructions](https://www.sony.co.uk/electronics/support/articles/00364921). Power-off followed by Wake-on-LAN power-on was verified using the production client.

Native app links verified on the physical TV: YouTube `vnd.youtube://`, Netflix `nflx://www.netflix.com/browse`, and Kyivstar TV `com.kyivstar.tv.androidtv://`. A matching initial package report never skips sending the launch command.

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
