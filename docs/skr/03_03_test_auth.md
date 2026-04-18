# 3.3.1. Тестовая авторизация — test-contur/authenticate

**[← К оглавлению](index.md)** · [Тестовые перечни →](03_03_test_catalogs.md) · [Тестовые ФЭС →](03_03_test_fes.md)

---

**Метод:** `test-contur/authenticate`  
**HTTP:** POST · `Content-Type: application/json`  
**Описание:** Возвращает JWT-токен сессии для вызова остальных тестовых методов.

## Запрос

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| userName | string | О | Логин пользователя личного кабинета |
| password | string | О | Пароль пользователя личного кабинета |

## Ответ

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| value | Value | О | Данные о сеансе |
| success | bool | О | `true` — запрос выполнен успешно |
| error | Error[] | УО | Список ошибок (присутствует если `success = false`) |
| errors | string | О | Ошибки |
| hasErrors | string | О | Наличие ошибок |

**Блок Value:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| currentUser | currentUser | О | Данные пользователя личного кабинета |
| access_token | string | О | Токен доступа |
| refreshToken | string | О | Обновлённый токен |

**Блок currentUser:**

| Поле | Тип | Обяз. | Описание |
|------|-----|-------|----------|
| id | string | О | Идентификатор пользователя личного кабинета |
| userName | string | О | Логин |
| kbShortName | string | О | Имя пользователя личного кабинета |
| kbLoginType | string | О | Тип логина (всегда `"3"`) |
| IsAuthenticated | string | О | Статус авторизации |

> Для доступа к другим методам поместить в запрос заголовок `Authorization: bearer <access_token>`

**Пример:** [3.5.1 test-contur/authenticate](03_05_examples_test.md#3-5-1)
