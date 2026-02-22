# UtilityMenu — Overview

UtilityMenu is a Microsoft Excel add-in that adds a custom ribbon tab with powerful utility commands for data analysts, developers, and power users.

## What is UtilityMenu?

UtilityMenu extends Excel with a set of productivity tools that automate common, repetitive tasks. Instead of writing macros or navigating deep menu hierarchies, you get one-click access to operations like finding the last used row, unhiding all rows, bulk data operations, and SQL query building.

## Core vs Pro

| Feature              | Core (Free) | Pro        |
|----------------------|:-----------:|:----------:|
| GetLastRow           | ✅          | ✅         |
| GetLastColumn        | ✅          | ✅         |
| UnhideRows           | ✅          | ✅         |
| AdvancedData         | ❌          | ✅         |
| BulkOperations       | ❌          | ✅         |
| DataExport           | ❌          | ✅         |
| SqlBuilder           | ❌          | ✅         |
| Licence validation   | Online      | Online     |
| Seat management      | 1 machine   | Up to plan |

## How Licensing Works

UtilityMenu uses an online licence validation model. When Excel opens, the add-in calls the UtilityMenu API to validate your licence key and retrieve your module entitlements. The response is HMAC-signed to prevent tampering.

Licences are validated against a **staleness window** (default 7 days) — if the add-in cannot reach the server within that window, it enters a **grace period** (default 7 days) before disabling Pro features.

## Architecture

```
Excel Add-in (VBA/Office JS)
    │
    ├── GET /api/licence/validate?key=UMENU-...
    ├── GET /api/licence/entitlements?key=UMENU-...
    ├── POST /api/licence/activate   (Bearer: <api-token>)
    └── POST /api/licence/deactivate (Bearer: <api-token>)
```

Your API token is found in the **Dashboard** after logging in.
