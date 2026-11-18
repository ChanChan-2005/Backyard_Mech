# Gmail SMTP setup

The app sends email from the fixed address `bakyardmechofficial@gmail.com` through `smtp.gmail.com` on port `587` with STARTTLS. The contact email under General Settings does not change the SMTP sender.

Google requires two-step verification before an App Password can be created. After creating one for this app, store it in the local .NET user secrets store from the project directory:

```powershell
dotnet user-secrets set "GmailSmtp:AppPassword" "YOUR_16_CHARACTER_APP_PASSWORD" --project Backyard_Mech.csproj
```

The App Password is not the normal Google account password. Do not add it to `appsettings.json`, `appsettings.Local.json`, source control, or a support message. For deployment, set the `GmailSmtp__AppPassword` environment variable through the host's secret manager instead of using local user secrets.

Restart the app after setting the secret. The sender address and SMTP server are fixed in the application; System Preferences only exposes the automatic notification switches. The test-email control has been removed after delivery was verified.

Enable notifications under **Settings → System Preferences** or **Settings → General**. The app checks every 15 minutes:

- Appointment reminders go to active customers with a valid email when a scheduled, unchecked-in appointment starts within 24 hours. Appointment dates are stored in garage-local time.
- Low-stock summaries go to the garage sender address once per garage-local day when any active part is at or below its reorder level.
- Invoice reminders go to active customers with a valid email when an issued or partially paid invoice reaches 15 days old and has a remaining balance. Invoices older than 16 days are not mailed on rollout.

Successful sends are recorded in `EmailDelivery`. Failed sends are retried after 15 minutes. A crash after Gmail accepts a message but before the database records success can still cause a duplicate after the three-minute lease expires.

Google's [App Password instructions](https://support.google.com/accounts/answer/185833) explain eligibility. [Google's SMTP settings](https://support.google.com/mail/answer/7104828) specify the server, port, and encryption.
