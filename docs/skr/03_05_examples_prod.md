# 3.5. Примеры запросов и ответов — штатные методы

**[← К оглавлению](index.md)** · [← Тестовые примеры](03_05_examples_test.md)

---

## 3.5.10. authenticate {#3-5-10}

**Запрос:**
```json
{
  "userName": "rfm",
  "password": "XXX"
}
```

**Ответ:**
```json
{
  "value": {
    "currentUser": {
      "id": "00000000-0000-0000-0000-000000000000",
      "userName": "rfm",
      "kbShortName": null,
      "kbLoginType": 3,
      "isAuthenticated": false
    },
    "accessToken": "token",
    "refreshToken": null
  },
  "success": false,
  "error": null,
  "errors": [],
  "hasErrors": false
}
```

---

## 3.5.11. suspect-catalogs/current-te2-catalog {#3-5-11}

**Ответ:**
```json
{
  "idTerroristCatalog": 1,
  "terroristCatalogNumber": "",
  "terroristCatalogDate": "2019-06-06T10:49:42.81507Z",
  "isActive": true,
  "idDbf": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "idDoc": "cf529a3f-f267-41a1-8a06-6199ab9c40d8",
  "idXml": "6873176f-900f-42b4-8ff0-d1cf06905157"
}
```

---

## 3.5.12. suspect-catalogs/current-te2-file {#3-5-12}

**Запрос:**
```json
{ "id": "efb2f08c-d31d-4091-a209-2b6776d405a0" }
```
**Ответ:** `{... Binary data file content...}`

---

## 3.5.13. suspect-catalogs/current-te21-catalog {#3-5-13}

**Ответ:**
```json
{
  "date": "2019-06-06T10:49:42.81507Z",
  "isActive": true,
  "idXml": "6873176f-900f-42b4-8ff0-d1cf06905157"
}
```

---

## 3.5.14. suspect-catalogs/current-te21-file {#3-5-14}

**Запрос:**
```json
{ "id": "6873176f-900f-42b4-8ff0-d1cf06905157" }
```
**Ответ:** `{... Binary data file content...}`

---

## 3.5.15. suspect-catalogs/current-mvk-catalog {#3-5-15}

**Ответ:**
```json
{
  "date": "2019-06-06T10:49:42.83069Z",
  "isActive": true,
  "idXml": "e37648ca-1bd9-462b-b3d1-f6ebc8848e7e"
}
```

---

## 3.5.16. suspect-catalogs/current-mvk-file-zip {#3-5-16}

**Запрос:**
```json
{ "id": "e37648ca-1bd9-462b-b3d1-f6ebc8848e7e" }
```
**Ответ:** `{... Binary data file content...}`

---

## 3.5.17. formalized-message/send {#3-5-17}

**Запрос:**
```json
{ "file": <binary data>, "sign": <binary data> }
```

**Ответ:**
```json
{
  "IdFormalizedMessage": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "IdExternal": "externalId",
  "IdFormalizedMessageStatus": 1,
  "FormalizedMessageStatusName": "Отправлено",
  "Note": "Примечание"
}
```

---

## 3.5.18. formalized-message/check-status {#3-5-18}

**Запрос:**
```json
{
  "IdFormalizedMessage": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "IdExternal": "externalId"
}
```

**Ответ:**
```json
{
  "IdFormalizedMessage": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "IdExternal": "externalId",
  "IdFormalizedMessageStatus": 3,
  "FormalizedMessageStatusName": "Проверка ФЛК",
  "Note": "Проверка ФЛК"
}
```

---

## 3.5.19. formalized-message/get-ticket {#3-5-19}

**Запрос:**
```json
{
  "IdFormalizedMessage": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "IdExternal": "externalId"
}
```
**Ответ:** `<binary data>`

---

## 3.5.20. suspect-catalogs/current-un-catalog {#3-5-20}

**Запрос:** `{}`

**Ответ:**
```json
{
  "date": "2022-07-08T00:00:00",
  "isActive": true,
  "idRecStatus": null,
  "idXml": "31670029-8982-4eea-9004-11af76ea9c07"
}
```

---

## 3.5.21. suspect-catalogs/current-un-catalog-rus {#3-5-21}

**Запрос:** `{}`

**Ответ:**
```json
{
  "date": "2022-07-08T00:00:00",
  "isActive": true,
  "idRecStatus": null,
  "idXml": "31670029-8982-4eea-9004-11af76ea9c07"
}
```

---

## 3.5.22. suspect-catalogs/current-un-file {#3-5-22}

**Запрос:**
```json
{ "id": "31670029-8982-4eea-9004-11af76ea9c07" }
```
**Ответ:** `{... Binary data file content...}`
