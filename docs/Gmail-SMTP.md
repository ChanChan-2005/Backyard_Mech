# Gmail SMTP setup

Automatic notifications use `smtp.gmail.com` on port `587` with STARTTLS from `bakyardmechofficial@gmail.com`. The business contact email does not change the sender address.

Create a Google App Password after enabling two-step verification, then store it in the local .NET user secrets store from the project directory:

```powershell
dotnet user-secrets set "GmailSmtp:AppPassword" "YOUR_16_CHARACTER_APP_PASSWORD" --project Backyard_Mech.csproj
```

Do not use your regular Google password or add the app password to source control or `appsettings.json`. For deployment, set `GmailSmtp__AppPassword` using the host's secret manager. Restart the app after configuring the secret.

Enable notifications under **Settings → System Preferences**. The app checks every 15 minutes:

- Appointment reminders go to active customers with a valid email when a scheduled appointment is within 24 hours.
- Low-stock summaries go to the fixed garage sender once per local day when parts are at or below reorder level.
- Invoice reminders go to active customers once when an issued or partially paid invoice reaches 15 days old and still has a balance.

Delivery attempts and retry status are stored in `EmailDelivery`. Failed sends retry after 15 minutes.
