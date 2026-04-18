# 3.5. Примеры запросов и ответов — тестовые методы

**[← К оглавлению](index.md)** · [Продуктивные примеры →](03_05_examples_prod.md)

---

## 3.5.1. test-contur/authenticate {#3-5-1}

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

## 3.5.2. test-contur/suspect-catalogs/current-te2-catalog {#3-5-2}

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

## 3.5.3. test-contur/suspect-catalogs/current-te2-file {#3-5-3}

**Запрос:**
```json
{ "id": "efb2f08c-d31d-4091-a209-2b6776d405a0" }
```
**Ответ:** `{... Binary data file content...}`

---

## 3.5.4. test-contur/suspect-catalogs/current-mvk-catalog {#3-5-4}

**Ответ:**
```json
{
  "date": "2019-06-06T10:49:42.83069Z",
  "isActive": true,
  "idXml": "e37648ca-1bd9-462b-b3d1-f6ebc8848e7e"
}
```

---

## 3.5.5. test-contur/suspect-catalogs/current-mvk-file-zip {#3-5-5}

**Запрос:**
```json
{ "id": "e37648ca-1bd9-462b-b3d1-f6ebc8848e7e" }
```
**Ответ:** `{... Binary data file content...}`

---

## 3.5.6. test-contur/formalized-message/send {#3-5-6}

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

## 3.5.7. test-contur/formalized-message/send-with-mchd {#3-5-7}

**Запрос:**
```json
{
  "file": <binary data>,
  "sign": <binary data>,
  "mchd": list<binary data>,
  "mchdSign": list<binary data>
}
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

## 3.5.8. test-contur/formalized-message/check-status {#3-5-8}

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

## 3.5.9. test-contur/formalized-message/get-ticket {#3-5-9}

**Запрос:**
```json
{
  "IdFormalizedMessage": "efb2f08c-d31d-4091-a209-2b6776d405a0",
  "IdExternal": "externalId"
}
```
**Ответ:** `<binary data>`
