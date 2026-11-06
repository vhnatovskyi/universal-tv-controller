# BRAVIA Remote

Один застосунок .NET MAUI для Windows і iPhone. Обидві версії підключаються напряму до Sony BRAVIA через Simple IP Control (TCP 20060). Перша цільова модель — BRAVIA 3. Для роботи потрібна спільна домашня мережа й увімкнене «Просте управління IP» на телевізорі.

## Користування

1. Увімкни телевізор і відкрий BRAVIA Remote.
2. Натисни «Додати профіль» і введи назву та IPv4-адресу телевізора. IP-адреса доступна у формі створення або редагування. Якщо попередня версія зберегла адресу, вона стане профілем «BRAVIA 3».
3. Перший профіль стає профілем за замовчуванням. Щоб змінити його, обери інший профіль і натисни «За замовчуванням». Наступного разу застосунок відкриє цей профіль.
4. Індикатор «Гучність» оновлюється приблизно кожні три секунди, поки відкрито пульт. Якщо телевізор не відповідає, показується тире. На iPhone дозволь доступ до локальної мережі, коли iOS запитає.
5. Використовуй стрілки, OK, «Назад», «Додому», вибір входу, гучність і вимкнення звуку. Увімкнення з режиму очікування залежить від параметра «Віддалений пуск» і від того, чи доступний TCP-порт у цьому режимі.

У «Налаштувати швидкі кнопки» можна зберегти до шести кнопок на профіль. Наприклад, назви кнопку «YouTube» та запиши послідовність `Додому, Вниз, Праворуч, OK`, якщо саме так ти переходиш до плитки на своєму телевізорі. Те саме можна налаштувати для Netflix, Kyivstar TV та інших плиток. Якщо їхній порядок або початковий фокус зміниться, відредагуй послідовність.

## Windows

Розпакуй увесь архів Windows x64 і запусти `BraviaRemote.exe` з розпакованої папки.

Для збірки з коду потрібні .NET 10 SDK та інструменти .NET MAUI для Windows. З кореня репозиторію:

```powershell
dotnet publish src/BraviaRemote/BraviaRemote.csproj -c Release -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0 -r win-x64 --self-contained -o work/publish
```

## iPhone

Потрібні Mac із сумісною версією Xcode, .NET 10 SDK, навантаження `maui-ios` та налаштований підпис для Apple Account. З Windows можна використати Pair to Mac. На Mac збірку для симулятора Apple Silicon можна запустити так:

```sh
dotnet workload install maui-ios
dotnet build src/BraviaRemote/BraviaRemote.csproj -f net10.0-ios -t:Run -p:RuntimeIdentifier=iossimulator-arm64
```

Для фізичного iPhone обери під’єднаний пристрій, сертифікат розробника та профіль для `app.braviaremote.controller` в інструментах MAUI. За потреби задай унікальний ідентифікатор застосунку, зареєстрований у твоїй команді Apple. Тестування з Personal Team безкоштовне, профіль діє сім днів.

- [Sony Simple IP Control](https://pro-bravia.sony.net/remote-display-control/simple-ip-control/)
- [Інструкція Microsoft для iOS на Mac](https://learn.microsoft.com/dotnet/maui/ios/cli?view=net-maui-10.0)
- [Підключення до Mac з Windows](https://learn.microsoft.com/dotnet/maui/ios/pair-to-mac?view=net-maui-10.0)
- [Apple Personal Team](https://developer.apple.com/help/account/basics/about-your-developer-account)
