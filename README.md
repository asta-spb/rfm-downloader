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
save_requests = false    ; true — сохранять тела запросов в *_request.json
```

### 3. Запустить

```bat
RfmDownloader.exe
```

---

## Параметры командной строки

| Параметр                     | Описание                                  | По умолчанию |
|------------------------------|-------------------------------------------|--------------|
| `--config <файл>`           | INI-файл конфигурации                     | config.ini   |
| `--mode test\|prod`         | Режим работы                              | test         |
| `--user <логин>`            | Логин личного кабинета                    | rfm          |
| `--password <пароль>`       | Пароль                                    | XXX          |
| `--thumbprint <отпечаток>`  | Отпечаток сертификата Windows             | —            |
| `--output <папка>`          | Базовая папка для сохранения файлов       | rfm_data     |
| `--timeout <сек>`           | Таймаут HTTP-запросов                     | 60           |
| `--fes <файл>`              | XML-файл ФЭС для отправки                | —            |
| `--mchd <файлы>`            | МЧД-файлы через запятую (с `--fes`)      | —            |
| `--log <файл>`              | Дублировать вывод в лог-файл              | —            |
| `--save-requests`           | Сохранять тела запросов в `*_request.json`| —            |
| `--list-certs`              | Показать сертификаты и выйти              | —            |
| `--help`                    | Справка                                   | —            |

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
RfmDownloader.exe --fes msg.xml --mchd m1.xml,m2.xml  # ФЭС с МЧД

# Логирование в файл
RfmDownloader.exe --log rfm.log                # вывод дублируется в rfm.log
RfmDownloader.exe --log rfm.log --mode prod    # прод с логированием
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

С МЧД (машиночитаемая доверенность) — файлы подписей МЧД аналогично:

```bat
RfmDownloader.exe --fes message.xml --mchd mchd1.xml,mchd2.xml
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
| Перечень ООН | `suspect-catalogs/current-un-catalog` | те же |
| Получить доступ | Заявка в тех. поддержку | После успешного тестирования всех методов |

---

## Структура выходной папки

Каждый запуск создаёт подпапку с режимом и временной меткой:

```
rfm_data\
  ├── test_2026-04-10_09-00-00\    ← загрузка перечней
  │     ├── auth_response.json
  │     ├── te_catalog.json
  │     ├── te_file.zip
  │     ├── mvk_catalog.json
  │     ├── mvk_file.zip
  │     ├── un_catalog_en.json
  │     ├── un_catalog_ru.json
  │     └── un_file.xml
  ├── test_2026-04-10_10-00-00\    ← отправка ФЭС
  │     ├── auth_response.json
  │     ├── fes_send_response.json
  │     ├── fes_status_response.json
  │     └── fes_receipt.xml
  └── prod_2026-04-10_11-00-00\    ← продуктовый запуск
        └── ...
```

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
