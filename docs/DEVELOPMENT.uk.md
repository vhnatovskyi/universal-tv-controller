# Розробка й релізи

[English](DEVELOPMENT.md)

- `src/App` містить інтерфейс MAUI, локальні налаштування, переклади та код платформ.
- `src/Shared` містить клієнт Sony Simple IP Control.
- У `installer` є опис інсталятора Windows, у `scripts` — сценарії збірки та перевірки, а в `.github/workflows` — CI та публікація за тегами.

Для Windows потрібні .NET 10 SDK, навантаження `maui-windows` та Inno Setup 6. Виконай із кореня репозиторію:

```powershell
./scripts/Build-Release.ps1
./scripts/Test-Installer.ps1
```

Запускай перевірку інсталятора в чистому середовищі Windows із закритим BRAVIA Remote. Вона встановлює застосунок у тимчасову папку, а потім видаляє його. Для локальної збірки лише портативного ZIP виконай `./scripts/Build-Release.ps1 -SkipInstaller`.

На Mac із сумісним Xcode та .NET 10:

```sh
dotnet workload install maui-ios
dotnet build src/App/BraviaRemote.csproj -f net10.0-ios -t:Run -p:RuntimeIdentifier=iossimulator-arm64
```

Для фізичного iPhone обери сертифікат розробника та профіль підпису. Під час розробки з Windows можна використати [Pair to Mac](https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0).

Щоб випустити нову версію:

1. Вкажи однакову тричастинну версію у `version.json` і `ApplicationDisplayVersion` проєкту MAUI. Збільш `ApplicationVersion` для iOS.
2. Онови обидві мови в [нотатках до релізу](RELEASE-NOTES.md) і запусти збірку та перевірку інсталятора Windows.
3. Запуш коміт у `main` і відповідний тег `vX.Y.Z`. GitHub Actions збере Windows ZIP та інсталятор, перевірить встановлення й опублікує обидва файли разом із `SHA256SUMS.txt`. CI також збирає версію для симулятора iOS. Для публікації збірки на фізичний iPhone потрібне окреме налаштування підпису.
