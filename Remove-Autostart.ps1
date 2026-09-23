$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'WukongCinema.exe'
$existing = Get-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -ErrorAction SilentlyContinue
if ($existing) {
    if ($existing.Actions.Execute -ne $exe) {
        throw 'A different task named WukongCinema exists. No changes were made.'
    }
    Unregister-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -Confirm:$false
}
if (Test-Path -LiteralPath $exe) { & $exe --stop }
Write-Host 'Wukong Cinema autostart was removed for this user.'
