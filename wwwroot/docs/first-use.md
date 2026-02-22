# First Use

## After Installation

When you open Excel for the first time after installing UtilityMenu, the add-in loads automatically and the **UtilityMenu** ribbon tab appears.

## Activating Your Licence

1. Click the **UtilityMenu** tab.
2. Click **Activate Licence**.
3. Enter your `UMENU-XXXX-XXXX-XXXX` key (from your [Dashboard](/dashboard)).
4. Click **Activate** — the add-in contacts the API, validates the key, and activates this machine.

> **Free users:** Core modules (GetLastRow, GetLastColumn, UnhideRows) work without a Pro licence. Simply dismiss the activation prompt to use Core features.

## The UtilityMenu Ribbon

The ribbon is divided into groups:

| Group | Buttons |
|-------|---------|
| **Navigation** | GetLastRow, GetLastColumn |
| **Visibility** | UnhideRows |
| **Data** *(Pro)* | AdvancedData, BulkOperations |
| **Export** *(Pro)* | DataExport |
| **Query** *(Pro)* | SqlBuilder |
| **Account** | Activate Licence, Settings |

## Quick Start: GetLastRow

1. Open any workbook with data.
2. Click **GetLastRow** on the UtilityMenu tab.
3. The last used row number in the active column is copied to the clipboard and shown in a message box.

## Getting Your API Token

The API token is required to call the activation/deactivation endpoints programmatically (e.g., from a custom script or CI pipeline).

1. Log in at [utilitymenu.com](/account/login).
2. Go to your [Dashboard](/dashboard).
3. Under **API Token**, click **Show** to reveal the token, then **Copy**.

Keep your API token secret — it has the same authority as your login credentials.
