# 2. Операции (методы) электронного сервиса

**[← К оглавлению](index.md)**

Электронный сервис представляет собой набор операций (методов). Все методы — HTTP POST.

---

## Тестовые методы (`test-contur/...`)

| Метод | Назначение |
|-------|-----------|
| `test-contur/authenticate` | Получение JWT-токена для тестового режима |
| `test-contur/suspect-catalogs/current-te2-catalog` | Информация об актуальном Перечне ТЭ (метаданные) |
| `test-contur/suspect-catalogs/current-te2-file` | Скачивание zip-файла Перечня ТЭ |
| `test-contur/suspect-catalogs/current-mvk-catalog` | Информация об актуальном Перечне МВК (метаданные) |
| `test-contur/suspect-catalogs/current-mvk-file-zip` | Скачивание zip-файла Перечня МВК |
| `test-contur/formalized-message/send` | Отправка ФЭС (тест) |
| `test-contur/formalized-message/send-with-mchd` | Отправка ФЭС с МЧД (тест) |
| `test-contur/formalized-message/check-status` | Проверка статуса ФЭС (тест) |
| `test-contur/formalized-message/get-ticket` | Получение квитанции ФЭС (тест) |

## Методы в штатном режиме

| Метод | Назначение |
|-------|-----------|
| `authenticate` | Получение JWT-токена |
| `suspect-catalogs/current-te21-catalog` | Информация об актуальном Перечне ТЭ v2.1 (метаданные) |
| `suspect-catalogs/current-te21-file` | Скачивание zip-файла Перечня ТЭ v2.1 |
| `suspect-catalogs/current-mvk-catalog` | Информация об актуальном Перечне МВК (метаданные) |
| `suspect-catalogs/current-mvk-file-zip` | Скачивание zip-файла Перечня МВК |
| `suspect-catalogs/current-un-catalog` | Информация об актуальном Перечне ООН (EN, метаданные) |
| `suspect-catalogs/current-un-catalog-rus` | Информация об актуальном Перечне ООН (RU, метаданные) |
| `suspect-catalogs/current-un-file` | Скачивание xml-файла Перечня ООН |
| `formalized-message/send` | Отправка ФЭС |
| `formalized-message/send-with-mchd` | Отправка ФЭС с МЧД |
| `formalized-message/check-status` | Проверка статуса ФЭС |
| `formalized-message/get-ticket` | Получение квитанции ФЭС |

**Детальное описание:**  
[3.3 Тестовые методы](03_03_test_auth.md) · [3.4 Штатные методы](03_04_prod_auth.md) · [3.5 Примеры](03_05_examples_test.md)
