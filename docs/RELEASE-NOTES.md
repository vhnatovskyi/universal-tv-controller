## Universal TV Controller v0.4.7

### English

- Fix Sony system commands by negotiating feature 16. Power-off and power-on were verified on a physical BRAVIA 3.
- Wait for a changed power state before completing the power action, and avoid relying on stale UI state.
- Temporarily remove the TV settings button while Sony compatibility is investigated.
- Always send app launch commands even when the initial package report already matches. Prefer the modern HTTPS launch link and retain the legacy fallback.
- Show command rejection and unconfirmed power separately from connection errors.
- Add regression coverage for feature negotiation, power confirmation and actual app launch delivery. Kyivstar TV launch still requires further hardware verification.

### Українська

- Виправлено системні команди Sony завдяки узгодженню прапорця 16. Вимкнення та ввімкнення перевірено на фізичному BRAVIA 3.
- Кнопка живлення очікує підтвердження зміни стану ТВ та не покладається на застарілий стан інтерфейсу.
- Кнопку налаштувань ТВ тимчасово прибрано на час дослідження сумісності Sony.
- Команда запуску застосунку надсилається навіть тоді, коли початковий звіт ТВ уже містить той самий пакет. Спершу використовується сучасне HTTPS-посилання, старий формат залишено як резервний.
- Відхилення команди та непідтверджену зміну живлення відокремлено від помилок з’єднання.
- Додано перевірки узгодження прапорців, підтвердження живлення та фактичного надсилання запуску застосунку. Запуск Kyivstar TV ще потребує перевірки на телевізорі.
