# Электронный сервис «Сервисный концентратор Росфинмониторинга» — Документация

Версия: 1.2 | Обновление: 28.08.2025  
Адрес сервиса: `https://portal.fedsfm.ru:8081/Services/fedsfm-service`

---

## Содержание

| Раздел | Файл | Краткое описание |
|--------|------|-----------------|
| [1. Общие сведения](01_obshchie_svedeniya.md) | `01_obshchie_svedeniya.md` | Описание сервиса, история версий, параметры подключения |
| [2. Список методов](02_metody.md) | `02_metody.md` | Полный перечень тестовых и продуктивных методов |
| [3.1–3.2. Обозначения и HTTPS](03_01_oboznacheniya_https.md) | `03_01_oboznacheniya_https.md` | Значения поля «Обязательность», установка HTTPS-соединения, заголовок Authorization |
| [3.3.1. Тестовая авторизация](03_03_test_auth.md) | `03_03_test_auth.md` | `test-contur/authenticate` — параметры запроса/ответа |
| [3.3.2. Тестовые перечни](03_03_test_catalogs.md) | `03_03_test_catalogs.md` | Перечни ТЭ, МВК (тест) |
| [3.3.3. Тестовые ФЭС](03_03_test_fes.md) | `03_03_test_fes.md` | send, send-with-mchd, check-status, get-ticket (тест) |
| [3.4.1. Авторизация](03_04_prod_auth.md) | `03_04_prod_auth.md` | `authenticate` — параметры запроса/ответа |
| [3.4.2. Штатные перечни](03_04_prod_catalogs.md) | `03_04_prod_catalogs.md` | Перечни ТЭ v2.1, МВК, ООН (EN/RU) |
| [3.4.3. Штатные ФЭС](03_04_prod_fes.md) | `03_04_prod_fes.md` | send, send-with-mchd, check-status, get-ticket |
| [3.5. Примеры — тест](03_05_examples_test.md) | `03_05_examples_test.md` | JSON-примеры для всех тестовых методов (3.5.1–3.5.9) |
| [3.5. Примеры — штатные](03_05_examples_prod.md) | `03_05_examples_prod.md` | JSON-примеры для всех штатных методов (3.5.10–3.5.22) |
| [4. Получение доступа](04_dostup.md) | `04_dostup.md` | Порядок подачи заявки на тестовый и продуктивный доступ |
| [Приложение 1. Заявка](appendix_1_zayavka.md) | `appendix_1_zayavka.md` | Форма заявки на продуктивный доступ |

---

## Быстрый поиск по методам

### Авторизация
- Тест: [`test-contur/authenticate`](03_03_test_auth.md) · Пример: [3.5.1](03_05_examples_test.md#3-5-1)
- Продуктив: [`authenticate`](03_04_prod_auth.md) · Пример: [3.5.10](03_05_examples_prod.md#3-5-10)

### Перечни (тестовые)
- [`test-contur/suspect-catalogs/current-te2-catalog`](03_03_test_catalogs.md) · [Пример](03_05_examples_test.md#3-5-2)
- [`test-contur/suspect-catalogs/current-te2-file`](03_03_test_catalogs.md) · [Пример](03_05_examples_test.md#3-5-3)
- [`test-contur/suspect-catalogs/current-mvk-catalog`](03_03_test_catalogs.md) · [Пример](03_05_examples_test.md#3-5-4)
- [`test-contur/suspect-catalogs/current-mvk-file-zip`](03_03_test_catalogs.md) · [Пример](03_05_examples_test.md#3-5-5)

### Перечни (штатные)
- [`suspect-catalogs/current-te21-catalog`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-13)
- [`suspect-catalogs/current-te21-file`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-14)
- [`suspect-catalogs/current-mvk-catalog`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-15)
- [`suspect-catalogs/current-mvk-file-zip`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-16)
- [`suspect-catalogs/current-un-catalog`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-20)
- [`suspect-catalogs/current-un-catalog-rus`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-21)
- [`suspect-catalogs/current-un-file`](03_04_prod_catalogs.md) · [Пример](03_05_examples_prod.md#3-5-22)

### ФЭС (тестовые)
- [`test-contur/formalized-message/send`](03_03_test_fes.md) · [Пример](03_05_examples_test.md#3-5-6)
- [`test-contur/formalized-message/send-with-mchd`](03_03_test_fes.md) · [Пример](03_05_examples_test.md#3-5-7)
- [`test-contur/formalized-message/check-status`](03_03_test_fes.md) · [Пример](03_05_examples_test.md#3-5-8)
- [`test-contur/formalized-message/get-ticket`](03_03_test_fes.md) · [Пример](03_05_examples_test.md#3-5-9)

### ФЭС (штатные)
- [`formalized-message/send`](03_04_prod_fes.md) · [Пример](03_05_examples_prod.md#3-5-17)
- [`formalized-message/send-with-mchd`](03_04_prod_fes.md)
- [`formalized-message/check-status`](03_04_prod_fes.md) · [Пример](03_05_examples_prod.md#3-5-18)
- [`formalized-message/get-ticket`](03_04_prod_fes.md) · [Пример](03_05_examples_prod.md#3-5-19)

---

## Структура ответа ФЭС (send / check-status)

| Поле | Тип | Описание |
|------|-----|----------|
| `IdFormalizedMessage` | Guid | Идентификатор ФЭС в ЕИС ФСФМ |
| `IdExternal` | string | Идентификатор ФЭС в системе клиента |
| `IdFormalizedMessageStatus` | int | Числовой код статуса |
| `FormalizedMessageStatusName` | string | Наименование статуса |
| `Note` | string | Примечание (УО) |

---

## Связанная документация

- [Приказ № 261 — Форматы ФЭС](../rfm261_00_index.md) — XML-структуры ФЭС, типы данных, квитанции
