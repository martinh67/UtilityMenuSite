# Licence Activation

## How Activation Works

When you activate UtilityMenu on a machine, the add-in sends your licence key and a unique machine fingerprint to the API. The API:

1. Validates the licence key is active and not expired.
2. Checks you have not exceeded your seat limit.
3. Records the machine activation.
4. Returns your module entitlements signed with an HMAC-SHA256 signature.

The add-in stores the signed entitlements locally and re-validates on a configurable schedule.

## Activating a Machine

### Via the Ribbon

1. Open Excel and click the **UtilityMenu** tab.
2. Click **Activate Licence**.
3. Enter your `UMENU-XXXX-XXXX-XXXX` key.
4. Click **Activate**.

### Via the API (programmatic)

```http
POST /api/licence/activate
Authorization: Bearer <your-api-token>
Content-Type: application/json

{
  "licenceKey": "UMENU-XXXX-XXXX-XXXX",
  "machineId": "550e8400-e29b-41d4-a716-446655440000",
  "machineName": "DESKTOP-ABC123"
}
```

**Success response:**
```json
{
  "machineId": "550e8400-e29b-41d4-a716-446655440000",
  "activatedAt": "2026-02-22T12:00:00Z",
  "activeCount": 1,
  "maxActivations": 3
}
```

**Error: seat limit exceeded:**
```json
{
  "error": "Seat limit exceeded. Deactivate a machine first.",
  "code": "SEAT_LIMIT_EXCEEDED"
}
```

## Deactivating a Machine

To free up a seat, deactivate a machine from your [Dashboard](/dashboard) or via the API:

```http
POST /api/licence/deactivate
Authorization: Bearer <your-api-token>
Content-Type: application/json

{
  "machineId": "550e8400-e29b-41d4-a716-446655440000"
}
```

## Seat Limits

| Licence Type | Seat Limit |
|--------------|:----------:|
| Core (Free)  | 1          |
| Pro Monthly  | 3          |
| Pro Annual   | 3          |
| Custom       | Negotiated |

## Offline Grace Period

If the add-in cannot reach the validation server, it enters a **grace period** of 7 days. During this time, the last known valid entitlements are used. After the grace period expires, Pro modules are disabled until connectivity is restored.

## Transferring Your Licence

To move your licence to a new machine:

1. Go to [Dashboard → Machines](/dashboard).
2. Click **Deactivate** next to the old machine.
3. Install UtilityMenu on the new machine and activate with your key.
