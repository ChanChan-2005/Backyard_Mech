# Live development server

From the project directory, run:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Start-LiveServer.ps1
```

Open http://localhost:5099. Keep the terminal running; press Ctrl+C to stop.

The server uses `dotnet watch` to rebuild and restart automatically after code
or Razor changes. Hot reload is disabled to avoid invalid metadata errors during
development. An app restart clears in-memory sessions, so you may need to sign
in again. Refresh the browser after a restart if it does not reconnect automatically.

The normal server on port 5000 can stay running. This server uses a separate
build output under `artifacts/live-server`, but the same configured database.
