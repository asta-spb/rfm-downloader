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
| `-t`     | `--thumbprint <отп.>`  | Отпечаток сертификата Windows                                                  | —            |
| `-o`     | `--output <папка>`     | Базовая папка для сохранения файлов                                            | rfm_data     |
| `-T`     | `--timeout <сек>`      | Таймаут HTTP-запросов                                                          | 60           |
| `-f`     | `--fes <файл>`         | XML-файл ФЭС для отправки                                                      | —            |
| `-M`     | `--mchd <файл>`        | МЧД-файл (с `--fes`)                                                           | —            |
| `-l`     | `--log <файл>`         | Дублировать вывод в лог-файл                                                   | —            |
| `-s`     | `--save-requests`      | Сохранять служебные JSON (запросы, `auth_response`, `*_catalog`)               | —            |
| `-d`     | `--debug`              | Выводить URL каждого HTTP-запроса в лог                                        | —            |
| `-n`     | `--no-subdir`          | Не создавать подпапку `<mode>_<timestamp>`, использовать `--output` как путь   | —            |
| `-L`     | `--list-certs`         | Показать сертификаты и выйти                                                   | —            |
| `-v`     | `--version`            | Показать версию и время сборки и выйти                                         | —            |
| `-h`     | `--help`               | Справка                                                                        | —            |

**Приоритет:** ключи CLI > config.ini > встроенные умолчания

---

## Примеры

```bat
RfmDownloader.exe --list-certs

RfmDownloader.exe                              # тестовый, из config.ini
RfmDownloader.exe --mode prod                  # продуктовый, из config.ini
RfmDownloader.exe --config prod.ini            # отдельный ini для прода
RfmDownloader.exe --mode prod --output D:\rfm  # прод + своя папка
RfmDownloader.exe --mode test --thumbprint A1B2C3...  # тест с явным сертификатом

# Отправка ФЭС
RfmDownloader.exe --fes message.xml            # отправить ФЭС
RfmDownloader.exe --fes msg.xml --mchd m.xml           # ФЭС с МЧД

# Логирование в файл
RfmDownloader.exe --log rfm.log                # вывод дублируется в rfm.log
RfmDownloader.exe --log rfm.log --mode prod    # прод с логированием

# Пакетный режим: файлы прямо в указанную папку, без <mode>_<timestamp>
RfmDownloader.exe --mode prod --output D:\batch\job_42 --no-subdir

# Версия и время сборки
RfmDownloader.exe --version
# RfmDownloader 1.0.0 (build 2026-05-06 11:50:00 UTC)
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

То же значение появляется в баннере при каждом запуске:

```
==============================================================
  Росфинмониторинг [ПРОДУКТОВЫЙ] — ЗАГРУЗКА ПЕРЕЧНЕЙ
  RfmDownloader 1.0.0 (build 2026-05-06 11:50:00 UTC)
  Запуск: 2026-05-06 14:50:00
==============================================================
```

---

## Отправка ФЭС

Для отправки формализованного электронного сообщения рядом с XML-файлом
должен находиться файл подписи (`.sig`):

```
message.xml
message.xml.sig    ← или message.sig
```

```bat
RfmDownloader.exe --fes message.xml
```

С МЧД (машиночитаемая доверенность) — файл подписи МЧД аналогично:

```bat
RfmDownloader.exe --fes message.xml --mchd mchd.xml
```

После отправки программа автоматически проверяет статус и скачивает квитанцию.

---

## Логирование

По умолчанию вывод идёт только в консоль. Для дублирования в файл:

**Через командную строку:**
```bat
RfmDownloader.exe --log rfm.log
```

**Через config.ini:**
```ini
[logging]
log_file = rfm.log
```

Лог дописывается в конец файла (append). Формат записи:
```
[2026-04-13 14:30:00] [INFO] Авторизация успешна, JWT-токен получен
[2026-04-13 14:30:01] [STEP] Загрузка каталога ТЭ (v2, тестовый)...
[2026-04-13 14:30:02] [WARN] Идентификатор файла ТЭ не найден.
```

---

## Отличия тестового и продуктового контуров

| | Тестовый (`--mode test`) | Продуктовый (`--mode prod`) |
|---|---|---|
| Авторизация | `test-contur/authenticate` | `authenticate` |
| Перечень ТЭ | `test-contur/...current-te2-catalog` (v2) | `suspect-catalogs/current-te21-catalog` (v2.1) |
| Файл ТЭ | `test-contur/...current-te2-file` | `suspect-catalogs/current-te21-file` |
| Перечень МВК | `test-contur/...current-mvk-catalog` | `suspect-catalogs/current-mvk-catalog` |
| Файл МВК | `test-contur/...current-mvk-file-zip` | `suspect-catalogs/current-mvk-file-zip` |
| Перечень ООН (EN) | — (только в проде) | `suspect-catalogs/current-un-catalog` + `current-un-file` → `un_file.xml` |
| Перечень ООН (RU) | — (только в проде) | `suspect-catalogs/current-un-catalog-rus` + `current-un-file` → `un_file_ru.xml` |
| Получить доступ | Заявка в тех. поддержку | После успешного тестирования всех методов |

---

## Структура выходной папки

Каждый запуск создаёт подпапку с режимом и временной меткой:

```
rfm_data\
  ├── prod_2026-04-10_09-00-00\    ← штатная загрузка перечней (save_requests = false)
  │     ├── te_file.zip          ← Перечень ТЭ
  │     ├── mvk_file.zip         ← Перечень МВК
  │     ├── un_file.xml          ← Перечень ООН (EN)
  │     └── un_file_ru.xml       ← Перечень ООН (RU)
  ├── prod_2026-04-10_09-30-00\    ← та же загрузка с save_requests = true
  │     ├── auth_response.json
  │     ├── te_catalog.json      ; метаданные: idXml, date, isActive
  │     ├── te_file.zip
  │     ├── mvk_catalog.json
  │     ├── mvk_file.zip
  │     ├── un_catalog_en.json
  │     ├── un_catalog_ru.json
  │     ├── un_file.xml
  │     └── un_file_ru.xml
  └── prod_2026-04-10_10-00-00\    ← отправка ФЭС (save_requests = false)
        └── fes_receipt.xml        ← квитанция Росфинмониторинга
```

> При отправке ФЭС с `save_requests = true` дополнительно сохраняются:
> `auth_response.json`, `fes_send_response.json` (или `fes_send_mchd_response.json`)
> и `fes_status_response.json` — содержат `IdFormalizedMessage`, `IdExternal`,
> `FormalizedMessageStatusName`, `Note`. Полезно для разбора, если квитанция
> «не пришла» или сервер вернул нестандартный статус.

### Пакетный режим (`--no-subdir`)

С флагом `--no-subdir` подпапка `<mode>_<timestamp>` не создаётся —
файлы кладутся прямо в `--output`. Флаг доступен **только в командной
строке**: это per-run выбор для пакетного режима, в `config.ini` его
держать нельзя, иначе следующий обычный запуск с тем же конфигом
тихо потеряет подпапку с датой.

```bat
RfmDownloader.exe --mode prod --output D:\batch\job_42 --no-subdir
```

```
D:\batch\job_42\
  ├── te_file.zip
  ├── mvk_file.zip
  ├── un_file.xml
  └── un_file_ru.xml
```

Это удобно, когда папку для каждого запуска готовит внешний оркестратор
и ему нужно знать заранее, где будут лежать файлы. Если папки не существует
— она создаётся автоматически. Если в ней уже есть файлы с такими же именами
— они будут перезаписаны.

---

## Примечание о ГОСТ TLS

Сервер `portal.fedsfm.ru:8081` использует ГОСТ TLS (ГОСТ Р 34.10/34.11).
`HttpClient` Windows использует **Schannel**, который получает поддержку
ГОСТ-шифрования при наличии **КриптоПро CSP**.

Если соединение не устанавливается — убедитесь:
1. КриптоПро CSP установлен и лицензирован
2. Сертификат с закрытым ключом находится в хранилище `Личное`
3. Тот же сертификат используется для входа в Личный кабинет через браузер

---

## Получение доступа к продуктовому контуру

Согласно документации (раздел 3.2):

1. Успешно вызвать **все тестовые методы** (`--mode test`)
2. Собрать JSON-конверты запросов/ответов в zip-архив
3. Заполнить форму заявки (Приложение 1 документации)
4. Направить в тех. поддержку с темой: «Сервисный концентратор» →
   «Регламентная процедура. Получение доступа к продуктивным методам ЭС»
