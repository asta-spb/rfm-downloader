# 3.4.3. Штатные методы — Формализованные электронные сообщения

**[← К оглавлению](index.md)** · [← Штатные перечни](03_04_prod_catalogs.md) · [Примеры →](03_05_examples_prod.md)

Все методы: HTTP POST, требуют заголовок `Authorization: bearer <token>`.

---

## Метод 8: formalized-message/send

**Описание:** Отправка ФЭС на портал ФСФМ. Возвращает идентификатор регистрации в ЕИС ФСФМ.  
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

**Пример:** [3.5.17](03_05_examples_prod.md#3-5-17)

---

## Метод 9: formalized-message/send-with-mchd

**Описание:** Отправка ФЭС с МЧД на портал ФСФМ.  
**Content-Type:** `multipart/form-data`  
**Запрос:** `{ file: binary, sign: binary, mchd: list<binary>, mchdSign: list<binary> }`  
**Ответ:** аналогичен методу [send](#метод-8-formalized-messagesend).

**Пример:** — (аналогичен тестовому [3.5.7](03_05_examples_test.md#3-5-7))

---

## Метод 10: formalized-message/check-status

**Описание:** Возвращает текущий статус ФЭС по идентификатору.  
**Content-Type:** `application/json`

**Запрос:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| IdFormalizedMessage | Guid | О | Идентификатор ФЭС в ЕИС ФСФМ |
| IdExternal | string | О | Идентификатор ФЭС в системе клиента |

**Ответ:** аналогичен методу [send](#метод-8-formalized-messagesend).

**Пример:** [3.5.18](03_05_examples_prod.md#3-5-18)

---

## Метод 11: formalized-message/get-ticket

**Описание:** Возвращает файл квитанции ФЭС, если квитанция сформирована.  
**Content-Type:** `application/json`

**Запрос:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| IdFormalizedMessage | Guid | О | Идентификатор ФЭС в ЕИС ФСФМ |
| IdExternal | string | О | Идентификатор ФЭС в системе клиента |

**Ответ:** `Content-Type: application/octet-stream` — бинарные данные файла квитанции ФЭС.

**Пример:** [3.5.19](03_05_examples_prod.md#3-5-19)
