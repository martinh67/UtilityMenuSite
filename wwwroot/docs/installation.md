# Installation

## System Requirements

- **Microsoft Excel** 2016 or later (Windows)
- **Windows** 10 or later (64-bit recommended)
- Internet connection for licence validation

## Download

1. Go to the [Downloads](/downloads) section of this site.
2. Click **Download UtilityMenu** to get the latest installer.
3. Run `UtilityMenu-Setup-x.y.z.exe` as a normal user (no administrator rights required).

## Installation Steps

1. **Run the installer** — double-click the downloaded `.exe` file.
2. **Accept the licence agreement** and click **Install**.
3. The installer places the add-in in your `%APPDATA%\UtilityMenu` folder and registers it with Excel.
4. **Open Excel** — a new **UtilityMenu** tab appears in the ribbon.
5. On first launch, you will be prompted to enter your **licence key**.

## Entering Your Licence Key

1. Open the **UtilityMenu** tab in Excel.
2. Click **Activate Licence** in the ribbon.
3. Paste your `UMENU-XXXX-XXXX-XXXX` licence key.
4. Click **Activate**. The add-in calls the UtilityMenu API to validate and activate your machine.

## Troubleshooting Installation

| Problem | Solution |
|---------|----------|
| Ribbon tab not visible | Go to **File → Options → Add-ins**, set manage to **COM Add-ins**, click Go, and enable UtilityMenu |
| "Cannot connect to server" | Check your firewall allows outbound HTTPS to `api.utilitymenu.com` |
| Setup.exe blocked by Windows | Right-click the file → **Properties** → check **Unblock** → Apply |

## Uninstalling

Open **Control Panel → Programs → Uninstall a Program** and select **UtilityMenu**. This removes the add-in files and deregisters the ribbon extension. Your licence key is retained and can be reused on another machine.
