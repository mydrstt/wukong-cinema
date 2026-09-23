$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'WukongCinema.exe'
$existing = Get-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -ErrorAction SilentlyContinue
if ($existing) {
    $existingActions = @($existing.Actions)
    $isOurTask = $existing.Description -eq 'Start Wukong Cinema at sign-in and restore it after unlock.' -and
        $existingActions.Count -eq 1 -and
        [System.IO.Path]::GetFileName($existingActions[0].Execute) -ieq 'WukongCinema.exe' -and
        $existingActions[0].Arguments -eq '--background'
    if (-not $isOurTask) {
        throw 'A different task named WukongCinema exists. No changes were made.'
    }
    Unregister-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -Confirm:$false
}
if (Test-Path -LiteralPath $exe) { & $exe --stop }
Write-Host 'Wukong Cinema autostart was removed for this user.'
