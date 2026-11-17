$ErrorActionPreference = 'Stop'
Push-Location (Split-Path -Parent $PSScriptRoot)
try {
    # Polling works reliably with files stored in OneDrive.
    $env:DOTNET_USE_POLLING_FILE_WATCHER = '1'
    $env:DOTNET_WATCH_RESTART_ON_RUDE_EDIT = 'true'
    Write-Host 'Live server: http://localhost:5099 (Ctrl+C to stop)'
    # Separate output avoids locking the executable used by the normal server.
    # Rebuild/restart avoids invalid hot-reload metadata after controller or Razor edits.
    dotnet watch --non-interactive --no-hot-reload run --launch-profile live --property:OutputPath=artifacts/live-server/
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}
