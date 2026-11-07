## Universal TV Controller v0.4.6

### English

- Fix TV power commands by using POWER with the current TV state instead of SLEEP/WAKEUP.
- Open the TV settings app and wait for confirmation instead of sending an ignored settings key.
- Add an update button on Windows: download the latest installer, verify SHA-256, and open the installation wizard in the current app folder. TV profiles are kept.
- Add download cancellation, update status, and Ukrainian and English messages. On iPhone, open the latest release page.
- Add protocol and update-download regression checks to CI and releases.
- Physical TV behavior and the complete in-app upgrade still require manual verification.

### Українська

- Виправлено команди живлення ТВ: POWER із перевіркою поточного стану замість SLEEP/WAKEUP.
- Налаштування ТВ відкриваються запуском системного застосунку з очікуванням підтвердження замість кнопки, яку ТВ може ігнорувати.
- Додано кнопку оновлення на Windows: завантаження останнього інсталятора, перевірка SHA-256 і запуск майстра встановлення в поточну папку програми. Профілі телевізорів зберігаються.
- Додано скасування завантаження, статус оновлення та повідомлення українською й англійською. На iPhone відкривається сторінка останнього релізу.
- До CI та публікації релізів додано перевірки протоколу та завантаження оновлень.
- Робота на фізичному телевізорі та повний цикл оновлення із застосунку ще потребують ручної перевірки.
