# BRAVIA Remote for iPhone

Проєкт iOS-застосунку для керування Sony BRAVIA з iPhone. За основу набору кнопок беремо пульт Sony RMF-TX820U.

## Перевірка сумісності

1. Знайти точну модель телевізора в розділі «Про пристрій» або на наклейці. Sony комплектує пультом RMF-TX820U кілька серій BRAVIA.
2. Під’єднати iPhone і телевізор до однієї локальної мережі. На телевізорі перевірити наявність налаштувань IP Control та Pre-Shared Key.
3. На ввімкненому телевізорі перевірити `getRemoteControllerInfo` та одну команду IRCC-IP, наприклад VolumeUp. Документація Sony описує IRCC-IP для професійних дисплеїв, тому підтримку на конкретній побутовій моделі потрібно перевірити окремо.
4. Окремо перевірити ввімкнення зі standby. Воно залежить від мережевих налаштувань і режиму очікування телевізора.

Якщо IRCC-IP недоступний, для моделі з Google TV наступна перевірка — чи працює мережевий пульт Google Home. Підтримку цього способу у власному застосунку потрібно дослідити окремо.

## Перша версія

- SwiftUI та URLSession, введення IP-адреси телевізора вручну.
- Дозвіл iOS на доступ до локальної мережі. Ключ телевізора, якщо він потрібен, зберігати в Keychain.
- Стрілки, OK, Home, Back, гучність, Mute та вибір входу. Кнопку живлення додати після перевірки standby.
- Для збірки та тестування нативного застосунку потрібні Mac із Xcode та iPhone. Apple Account із Personal Team дозволяє тестувати без платної підписки. Профіль підпису діє сім днів.

## Документація

- [Моделі BRAVIA та комплектні пульти Sony](https://www.sony.com/electronics/support/articles/00343536)
- [Sony IRCC-IP](https://pro-bravia.sony.net/remote-display-control/ircc-ip/)
- [Доступ до локальної мережі на iOS](https://developer.apple.com/documentation/technotes/tn3179-understanding-local-network-privacy)
- [Тестування з Personal Team](https://developer.apple.com/help/account/basics/about-your-developer-account)
