# 3.3.3. Тестовые методы ФЭС

**[← К оглавлению](index.md)** · [← Тестовые перечни](03_03_test_catalogs.md) · [Штатная авторизация →](03_04_prod_auth.md)

Все методы: HTTP POST, требуют заголовок `Authorization: bearer <token>`.

---

## Метод 9: test-contur/formalized-message/send

**Описание:** Отправка ФЭС на портал ФСФМ (тестовый режим). Возвращает идентификатор регистрации в ЕИС ФСФМ.  
**Content-Type:** `multipart/form-data`  
**Запрос:** `{ file: binary, sign: binary }`

**Ответ** (`Content-Type: application/json`):

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| IdFormalizedMessage | Guid | О | Идентификатор ФЭС в ЕИС ФСФМ |
| IdExternal | string | О | Идентификатор ФЭС в системе клиента |
| IdFormalizedMessageStatus | int | О | Идентификатор статуса ФЭС |
| FormalizedMessageStatusName | string | О | Наименование статуса ФЭС |
| Note | string | УО | Примечание (заполняется при наличии) |

**Пример:** [3.5.6](03_05_examples_test.md#3-5-6)

---

## Метод 10: test-contur/formalized-message/send-with-mchd

**Описание:** Отправка ФЭС с МЧД на портал ФСФМ (тестовый режим).  
**Content-Type:** `multipart/form-data`  
**Запрос:** `{ file: binary, sign: binary, mchd: list<binary>, mchdSign: list<binary> }`  
**Ответ:** аналогичен методу [send](#метод-9-test-conturformalized-messagesend).

**Пример:** [3.5.7](03_05_examples_test.md#3-5-7)

---

## Метод 11: test-contur/formalized-message/check-status

**Описание:** Возвращает текущий статус ФЭС по идентификатору (тестовый режим).  
**Content-Type:** `application/json`

**Запрос:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| IdFormalizedMessage | Guid | О | Идентификатор ФЭС в ЕИС ФСФМ |
| IdExternal | string | О | Идентификатор ФЭС в системе клиента |

**Ответ:** аналогичен методу [send](#метод-9-test-conturformalized-messagesend).

**Пример:** [3.5.8](03_05_examples_test.md#3-5-8)

---

## Метод 12: test-contur/formalized-message/get-ticket

**Описание:** Возвращает файл квитанции ФЭС, если квитанция сформирована (тестовый режим).  
**Content-Type:** `application/json`

**Запрос:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| IdFormalizedMessage | Guid | О | Идентификатор ФЭС в ЕИС ФСФМ |
| IdExternal | string | О | Идентификатор ФЭС в системе клиента |

**Ответ:** `Content-Type: application/octet-stream` — бинарные данные файла квитанции ФЭС.

**Пример:** [3.5.9](03_05_examples_test.md#3-5-9)
