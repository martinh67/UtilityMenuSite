# Frequently Asked Questions

## General

### Is UtilityMenu free?

UtilityMenu Core is completely free. It includes GetLastRow, GetLastColumn, and UnhideRows. Pro modules require a paid subscription.

### What versions of Excel are supported?

UtilityMenu supports Excel 2016, 2019, 2021, and Microsoft 365 on Windows. macOS and Excel Online are not currently supported.

### Does UtilityMenu send my spreadsheet data anywhere?

No. UtilityMenu only communicates with the licence validation API (`api.utilitymenu.com`) to validate your key and retrieve entitlements. Your spreadsheet data never leaves your machine.

---

## Licensing

### Where do I find my licence key?

Your licence key is shown in your [Dashboard](/dashboard) under **Licence Details**.

### Can I use my licence on multiple machines?

Yes. Pro licences support up to 3 simultaneous activations. To move to a new machine, deactivate an existing one from your Dashboard first.

### What happens when my Pro subscription expires?

Pro modules are disabled, but Core modules continue to work. Your data is not affected. You can resubscribe at any time to re-enable Pro features immediately.

### I lost my licence key. How do I retrieve it?

Log in to your account and go to the [Dashboard](/dashboard). Your licence key is always displayed there.

---

## Technical

### The UtilityMenu tab is missing after Excel restarts.

Go to **File → Options → Add-ins**, set the manage dropdown to **COM Add-ins**, click **Go**, and make sure **UtilityMenu** is checked.

### I see "Licence validation failed: offline".

The add-in could not reach the validation server. Check your internet connection and that your firewall allows outbound HTTPS to `api.utilitymenu.com`. Your last valid entitlements will be used during the 7-day grace period.

### Can I use the API to automate activation?

Yes. See the [Activation documentation](/docs/activation) for the full API reference. You'll need your API token from the Dashboard.

### How is the machine fingerprint generated?

The machine ID is a stable UUID derived from hardware identifiers (motherboard serial, CPU ID). It does not include any personally identifiable information.

---

## Billing

### How do I cancel my subscription?

Go to your [Dashboard](/dashboard) and click **Manage Billing**. This opens the Stripe customer portal where you can cancel, upgrade, or downgrade your subscription.

### Do you offer refunds?

We offer a 14-day money-back guarantee on first-time Pro subscriptions. Contact us via the [Contact](/contact) page.

### What payment methods are accepted?

All major credit and debit cards via Stripe. We do not store card details — all payment processing is handled by Stripe.
