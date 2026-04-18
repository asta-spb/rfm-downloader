# 3.3.2. Тестовые перечни

**[← К оглавлению](index.md)** · [← Тестовая авторизация](03_03_test_auth.md) · [Тестовые ФЭС →](03_03_test_fes.md)

Все методы: HTTP POST, требуют заголовок `Authorization: bearer <token>`.

---

## Метод 2: test-contur/suspect-catalogs/current-te2-catalog

**Описание:** Получение информации об актуальном Перечне ТЭ (тестовый режим).  
**Запрос:** параметры отсутствуют.

**Ответ:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| idTerroristCatalog | guid | О | Id Перечня |
| terroristCatalogNumber | string | О | Номер Перечня |
| terroristCatalogDate | int | О | Дата создания Перечня |
| isActive | string | О | Статус Перечня |
| idDbf | string | О | Идентификатор dbf-файла с Перечнем |
| idDoc | string | О | Идентификатор doc-файла с Перечнем |
| idXml | string | О | Идентификатор xml-файла с Перечнем |

**Пример:** [3.5.2](03_05_examples_test.md#3-5-2)

---

## Метод 3: test-contur/suspect-catalogs/current-te2-file

**Описание:** Получение zip-файла Перечня ТЭ (тестовый режим).  
**Content-Type запроса:** `application/x-www-form-urlencoded`  
**Запрос:** `id` — идентификатор из метода `current-te2-catalog`  
**Ответ:** `Content-Type: application/octet-stream` — бинарные данные zip-файла Перечня ТЭ.

**Пример:** [3.5.3](03_05_examples_test.md#3-5-3)

---

## Метод 4: test-contur/suspect-catalogs/current-mvk-catalog

**Описание:** Получение информации об актуальном Перечне МВК (тестовый режим).  
**Запрос:** параметры отсутствуют.

**Ответ:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| date | guid | О | Дата создания Списка МВК |
| isActive | string | О | Статус Списка МВК |
| idXml | int | О | Идентификатор xml-файла со Списком МВК |

**Пример:** [3.5.4](03_05_examples_test.md#3-5-4)

---

## Метод 5: test-contur/suspect-catalogs/current-mvk-file-zip

**Описание:** Получение zip-файла Перечня МВК (тестовый режим).  
**Content-Type запроса:** `application/x-www-form-urlencoded`  
**Запрос:** `id` — идентификатор из метода `current-mvk-catalog`  
**Ответ:** `Content-Type: application/octet-stream` — бинарные данные zip-файла Перечня МВК.

**Пример:** [3.5.5](03_05_examples_test.md#3-5-5)
