# RfmDownloader — Загрузчик данных Росфинмониторинга

Консольное приложение на C# (.NET 10) для работы с API
«Сервисный концентратор Росфинмониторинга» (НФО группа 3484-У / НКО группа 110).

Возможности:
- Загрузка перечней ТЭ, МВК, ООН
- Отправка ФЭС (формализованных электронных сообщений), в том числе с МЧД
- Проверка статуса и загрузка квитанций ФЭС
- Логирование в файл

Поддерживает **тестовый** и **продуктовый** контуры.

---

## Требования

- Windows 10/11 (x64)
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- КриптоПро CSP 4.0 или 5.0
- Сертификат КЭП установлен в хранилище Windows (Личное / My)

---

## Сборка

```bat
cd RfmDownloader
dotnet build -c Release
```

Собрать в один автономный `.exe` (не требует .NET на целевой машине):

```bat
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

Готовый файл: `bin\Release\net10.0\win-x64\publish\RfmDownloader.exe`

---

## Быстрый старт

### 1. Найти отпечаток сертификата

```bat
RfmDownloader.exe --list-certs
```

### 2. Заполнить config.ini

```ini
[mode]
mode       = test        ; test или prod

[credentials]
user       = ВАШ_ЛОГИН
password   = ВАШ_ПАРОЛЬ

[certificate]
thumbprint = A1B2C3D4...  ; без пробелов

[output]
folder     = rfm_data
timeout    = 60

[logging]
; log_file = rfm.log     ; раскомментировать для записи в файл

[debug]
save_requests = false    ; true — сохранять служебные JSON (запросы, auth_response, *_catalog)
```

> При `save_requests = false` (по умолчанию) в выходной папке остаются **только файлы перечней**:
> `te_file.zip`, `mvk_file.zip`, `un_file.xml`, `un_file_ru.xml`. Все JSON-метаданные (`auth_response.json`, `*_catalog.json`) и тела запросов (`*_request.json`) сохраняются только при `save_requests = true`.

### 3. Запустить

```bat
RfmDownloader.exe
```

---

## Параметры командной строки

У каждого ключа есть короткая форма — удобно для пакетных скриптов. Если буква уже занята более частой опцией, для редких используется заглавная (`-T`/`-M`/`-L`).

| Короткий | Длинный                | Описание                                                                       | По умолчанию |
|----------|------------------------|--------------------------------------------------------------------------------|--------------|
| `-c`     | `--config <файл>`      | INI-файл конфигурации                                                          | config.ini   |
| `-m`     | `--mode test\|prod`    | Режим работы                                                                   | test         |
| `-u`     | `--user <логин>`       | Логин личного кабинета                                                         | rfm          |
| `-p`     | `--password <пароль>`  | Пароль                                                                         | XXX          |
| `-t`     | `--thumbprint <отп.>`  | Отпечаток сертификата КЭП в Windows-хранилище **(обязательный)**               | —            |
| `-o`     | `--output <папка>`     | Базовая папка для сохранения файлов                                            | rfm_data     |
| `-T`     | `--timeout <сек>`      | Таймаут HTTP-запросов                                                          | 60           |
| `-f`     | `--fes <файл>`         | XML-файл ФЭС для отправки                                                      | —            |
| `-M`     | `--mchd <файл>`        | МЧД-файл (с `--fes`)                                                           | —            |
| `-l`     | `--log <файл>`         | Дублировать вывод в лог-файл                                                   | —            |
| `-s`     | `--save-requests`      | Сохранять служебные JSON (запросы, `auth_response`, `*_catalog`)               | —            |
| `-d`     | `--debug`              | Выводить URL каждого HTTP-запроса в лог                                        | —            |
| `-n`     | `--no-subdir`          | Не создавать подпапку с датой, использовать `--output` как точный путь         | —            |
| `-L`     | `--list-certs`         | Показать сертификаты и выйти                                                   | —            |
| `-v`     | `--version`            | Показать версию и время сборки и выйти                                         | —            |
| `-h`     | `--help`               | Справка                                                                        | —            |

**Приоритет:** ключи CLI > config.ini > встроенные умолчания

---

## Примеры

```bat
# Информационные команды
RfmDownloader.exe --version                                   # версия и время сборки
RfmDownloader.exe --list-certs                                # сертификаты в Windows-хранилище

# Загрузка перечней (--thumbprint обязателен — обычно из config.ini)
RfmDownloader.exe                                             # из config.ini; если mode не задан — test
RfmDownloader.exe --mode prod                                 # продуктовый контур
RfmDownloader.exe --config prod.ini                           # отдельный ini-файл
RfmDownloader.exe --mode prod --output D:\rfm                 # прод + своя папка
RfmDownloader.exe --mode test --thumbprint A1B2C3...          # тест с явным сертификатом

# Отправка ФЭС
RfmDownloader.exe --fes message.xml                           # отправить ФЭС
RfmDownloader.exe --fes msg.xml --mchd m.xml                  # ФЭС с МЧД

# Логирование в файл
RfmDownloader.exe --log rfm.log                               # вывод дублируется в rfm.log
RfmDownloader.exe --log rfm.log --mode prod                   # прод с логированием

# Пакетный режим: файлы прямо в --output, без подпапки с датой
RfmDownloader.exe --mode prod --output D:\batch\job_42 --no-subdir
```

---

## Структура проекта

Код разбит по модулям, по одному классу на файл:

```
Program.cs              точка входа, баннер, создание выходной папки
AppArgs.cs              разбор CLI и INI, цепочка приоритетов
IniConfig.cs            парсер config.ini
Endpoints.cs            RunMode + URL для test/prod контуров
RfmClient.cs            HTTP-клиент с ГОСТ TLS, методы API
CertHelper.cs           поиск сертификата КЭП в Windows-хранилище
Logger.cs               консольный + файловый логгер
BuildInfo.cs            версия и штамп сборки из MSBuild
JsonNodeExtensions.cs   case-insensitive поиск поля JSON
```

---

## Дополнительные настройки `config.ini`

### `[fes]` — опрос квитанции

Параметры применимы только в режиме отправки ФЭС (`--fes`). Через CLI **не**
переопределяются — это «инфраструктурные» значения для конкретной среды:

```ini
[fes]
receipt_max_attempts  = 10   ; сколько раз пробовать забрать квитанцию
receipt_delay_seconds = 10   ; пауза между попытками
```

По умолчанию — 10 попыток с интервалом 10 секунд (≈100 секунд ожидания).
Если боевая квитанция формируется дольше — увеличьте оба значения.

---

## Версия и сборка

Версия задаётся в `RfmDownloader.csproj` (`<Version>`) и бампится вручную при
выпуске релиза. Время сборки подставляется автоматически свойством
`<SourceRevisionId>` (UTC, формат `yyyyMMdd-HHmmss`) и попадает в
`AssemblyInformationalVersion` как суффикс `+...`. На экране и при `--version`
отображается в виде `<версия> (build <дата UTC>)`.

```bat
RfmDownloader.exe --version
RfmDownloader 1.0.0 (build 2026-05-06 11:50:00 UTC)
```

То же значение появляется в