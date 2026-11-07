## Universal TV Controller v0.4.11

### English

- Place TV settings alongside Back, Home and Inputs as four equally sized buttons. Keep power centered above the direction pad and preserve the layout without scrolling.

### Українська

- Налаштування ТВ перенесено в один ряд із кнопками «Назад», «Дім» та «Джерело»: усі чотири кнопки однакового розміру. Живлення залишається по центру над навігацією, пульт уміщається без прокручування.

## Universal TV Controller v0.4.10

### English

- Restore the TV settings button beside power. Hold HOME for 1.2 seconds to open the Google TV settings side panel on BRAVIA 3, instead of the ignored SETTINGS key.
- Always release HOME, including when the operation is cancelled. Keep the remote fitting without scrolling.
- Rename the code namespace to UniversalTvRemote, including XAML, platform entry points and localization resources.

### Українська

- Повернуто кнопку налаштувань ТВ поруч із живленням. Утримання Home протягом 1,2 секунди відкриває бічну панель налаштувань Google TV на BRAVIA 3 замість команди Settings, яку телевізор ігнорує.
- Home відпускається навіть після скасування операції. Пульт уміщається без прокручування.
- Namespace змінено на UniversalTvRemote включно з XAML, платформними точками входу та ресурсами перекладів.

## Universal TV Controller v0.4.9

### English

- Center the connection refresh icon using a vector asset instead of a font glyph.

### Українська

- Вирівняно іконку оновлення з’єднання по центру кнопки за допомогою векторного зображення.

## Universal TV Controller v0.4.8

### English

- Center remote icons using vector assets and fit the remote controls without scrolling.
- Wake Sony TVs with Wake-on-LAN instead of relying on a remote session that may be asleep. On Windows, learn and save the TV MAC address automatically. Remote start must be enabled on the TV.
- Keep Sony's required system-control flag and negotiate app/input features for control sessions. Power-off and Wake-on-LAN power-on were verified with the production client on BRAVIA 3.
- Hold volume −/+ to repeat until released; stop on loss of focus or app deactivation.
- Add volume presets 3, 5, 10, 30 and 100. Read the current level and confirm every adjustment to reach the exact target. Large changes can take a few seconds.
- Improve the wake failure message with the Remote start setting needed on BRAVIA.
- Verify every preset with a TLS simulator, cancellation and missing confirmations. Levels 3, 5 and 10 were also verified on the physical TV.
- Fix Netflix and Kyivstar TV shortcuts using their native app links, both verified on the physical TV. Keep YouTube's working native link.
- The TV settings button remains removed while Sony settings compatibility is investigated.

### Українська

- Іконки пульта вирівняно по центру за допомогою векторних зображень; усі кнопки вміщаються без прокручування.
- Для пробудження Sony використовується Wake-on-LAN замість з’єднання пульта, яке може спати. На Windows MAC-адреса ТВ визначається та зберігається автоматично. На телевізорі потрібно ввімкнути «Віддалений запуск».
- Збережено обов’язковий прапорець системного керування Sony та узгоджено можливості застосунків і введення для сеансів команд. Вимкнення та ввімкнення через Wake-on-LAN перевірено production-клієнтом на BRAVIA 3.
- Утримання −/+ безперервно змінює гучність; відпускання, втрата фокуса чи деактивація застосунку зупиняє повторення.
- Додано кнопки гучності 3, 5, 10, 30 і 100. Пульт читає поточний рівень та підтверджує кожен крок до точного значення. Великі зміни можуть тривати кілька секунд.
- Повідомлення про невдале пробудження підказує налаштування «Віддалений запуск».
- Усі рівні, скасування та відсутність підтверджень перевіряються TLS-емулятором. Значення 3, 5 і 10 також перевірено на фізичному ТВ.
- Виправлено кнопки Netflix і Kyivstar TV через власні посилання застосунків; обидва запуски перевірено на фізичному ТВ. Робоче посилання YouTube збережено.
- Кнопка налаштувань ТВ залишається прибраною на час дослідження сумісності Sony.
