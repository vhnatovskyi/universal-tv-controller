## Universal TV Controller v0.4.3

### English

- Smaller framework-dependent Windows distribution with shared .NET 10 x64 and Windows App Runtime 1.7 x64.
- Setup checks prerequisites and downloads missing Microsoft runtimes, verifying their digital signatures before execution. First installation may require internet and administrator permission.
- Windows LZX compression reduces app disk usage on supported NTFS volumes. Shared runtimes use additional disk space.
- Remove obsolete bundled runtime files during upgrades.
- Fix startup failure caused by duplicate visual states on the Power button.

### Українська

- Компактна версія Windows зі спільними .NET 10 x64 та Windows App Runtime 1.7 x64.
- Інсталятор перевіряє залежності та завантажує відсутні runtime Microsoft із перевіркою цифрового підпису. Перше встановлення може потребувати інтернету й дозволу адміністратора.
- Стиснення Windows LZX зменшує місце для файлів застосунку на підтримуваних дисках NTFS. Спільні runtime займають додаткове місце.
- Під час оновлення видаляються застарілі вбудовані файли runtime.
- Виправлено збій запуску через дублювання візуальних станів кнопки живлення.
