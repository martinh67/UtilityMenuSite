# API Reference

Base URL: `https://api.utilitymenu.com` (production) · `https://localhost:5001` (development)

## Authentication

Most licence endpoints use a **Bearer token** in the `Authorization` header:

```
Authorization: Bearer <your-api-token>
```

Your API token is available in your [Dashboard](/dashboard).

---

## Licence Endpoints

### Validate a Licence Key

```
GET /api/licence/validate?key=UMENU-XXXX-XXXX-XXXX
```

Fast validity check. Rate limited to **60 requests/minute per IP**.

**Response — valid:**
```json
{
  "isValid": true,
  "licenceType": "pro",
  "expiresAt": "2027-02-22T00:00:00Z"
}
```

**Response — invalid:**
```json
{
  "isValid": false,
  "reason": "Licence expired"
}
```

---

### Get Module Entitlements

```
GET /api/licence/entitlements?key=UMENU-XXXX-XXXX-XXXX
```

Returns the full entitlement payload, including the HMAC-SHA256 signature for offline verification.

**Response:**
```json
{
  "isValid": true,
  "licenceKey": "UMENU-XXXX-XXXX-XXXX",
  "licenceType": "pro",
  "expiresAt": "2027-02-22T00:00:00Z",
  "modules": ["GetLastRow", "GetLastColumn", "UnhideRows", "AdvancedData", "BulkOperations", "DataExport", "SqlBuilder"],
  "signature": "<hmac-sha256-hex>"
}
```

---

### Activate a Machine

```
POST /api/licence/activate
Authorization: Bearer <api-token>
Content-Type: application/json
```

**Request body:**
```json
{
  "licenceKey": "UMENU-XXXX-XXXX-XXXX",
  "machineId": "550e8400-e29b-41d4-a716-446655440000",
  "machineName": "DESKTOP-ABC123"
}
```

**Response (201):**
```json
{
  "machineId": "550e8400-e29b-41d4-a716-446655440000",
  "activatedAt": "2026-02-22T12:00:00Z",
  "activeCount": 1,
  "maxActivations": 3
}
```

**Error codes:**

| Code | HTTP | Description |
|------|------|-------------|
| `SEAT_LIMIT_EXCEEDED` | 400 | All seats are occupied |
| `LICENCE_INVALID` | 404 | Key not found or inactive |
| `AUTH_REQUIRED` | 401 | Missing Bearer token |
| `AUTH_INVALID` | 401 | Token not recognised |
| `VALIDATION_FAILED` | 400 | Request body invalid |

---

### Deactivate a Machine

```
POST /api/licence/deactivate
Authorization: Bearer <api-token>
Content-Type: application/json
```

**Request body:**
```json
{
  "machineId": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Response:**
```json
{ "success": true }
```

---

## Checkout Endpoints

### Create Checkout Session

```
POST /api/checkout/create
Authorization: Bearer <api-token>
Content-Type: application/json
```

**Request body:**
```json
{
  "priceId": "price_xxx",
  "successUrl": "https://utilitymenu.com/checkout/success?session_id={CHECKOUT_SESSION_ID}",
  "cancelUrl": "https://utilitymenu.com/pricing"
}
```

**Response:**
```json
{ "sessionId": "cs_xxx", "url": "https://checkout.stripe.com/pay/cs_xxx" }
```

---

### Get Session Status

```
GET /api/checkout/status?sessionId=cs_xxx
Authorization: Bearer <api-token>
```

**Response:**
```json
{
  "status": "complete",
  "licenceKey": "UMENU-XXXX-XXXX-XXXX",
  "licenceType": "pro"
}
```

Status values: `open` · `complete` · `expired`

---

### Create Billing Portal Session

```
POST /api/checkout/billing-portal
Authorization: Bearer <api-token>
Content-Type: application/json
```

**Request body:**
```json
{ "returnUrl": "https://utilitymenu.com/dashboard" }
```

**Response:**
```json
{ "url": "https://billing.stripe.com/session/xxx" }
```
