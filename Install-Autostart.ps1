# Registers Wukong Cinema for this Windows user at sign-in and unlock.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'WukongCinema.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw 'WukongCinema.exe is missing. Run Build.ps1 first.'
}

$existing = Get-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -ErrorAction SilentlyContinue
$description = 'Start Wukong Cinema at sign-in and restore it after unlock.'
if ($existing) {
    $existingActions = @($existing.Actions)
    $isOurTask = $existing.Description -eq $description -and
        $existingActions.Count -eq 1 -and
        [System.IO.Path]::GetFileName($existingActions[0].Execute) -ieq 'WukongCinema.exe' -and
        $existingActions[0].Arguments -eq '--background'
    if (-not $isOurTask) {
        throw 'A different task named WukongCinema already exists. No changes were made.'
    }

    if ($existingActions[0].Execute -ine $exe) {
        # The user moved or downloaded the app to a new folder. Release the old instance
        # before updating the task, so the new location takes effect immediately.
        & $exe --stop
        for ($attempt = 0; $attempt -lt 30; $attempt++) {
            $running = @(Get-Process -Name 'WukongCinema' -ErrorAction SilentlyContinue |
                Where-Object { $_.Path -ieq $existingActions[0].Execute -or $_.Path -ieq $exe })
            if ($running.Count -eq 0) { break }
            Start-Sleep -Milliseconds 200
        }
        if ($running.Count -gt 0) {
            throw 'Wukong Cinema is still running. Close it and run this installer again.'
        }
    }
}

$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$folder = $service.GetFolder('\')
$task = $service.NewTask(0)
$task.RegistrationInfo.Description = $description
$task.Principal.UserId = $user
$task.Principal.LogonType = 3 # Interactive token; no password stored.
$task.Principal.RunLevel = 0
$task.Settings.Enabled = $true
$task.Settings.StartWhenAvailable = $true
$task.Settings.DisallowStartIfOnBatteries = $false
$task.Settings.StopIfGoingOnBatteries = $false
$task.Settings.ExecutionTimeLimit = 'PT0S'
$task.Settings.MultipleInstances = 2 # Ignore a duplicate while already running.

$logon = $task.Triggers.Create(9)
$logon.UserId = $user
$unlock = $task.Triggers.Create(11)
$unlock.UserId = $user
$unlock.StateChange = 8

$action = $task.Actions.Create(0)
$action.Path = $exe
$action.Arguments = '--background'
$action.WorkingDirectory = $PSScriptRoot

& (Join-Path $PSScriptRoot 'Set-Wallpaper.ps1') -Install
$null = $folder.RegisterTaskDefinition('WukongCinema', $task, 6, $user, $null, 3, $null)
Start-ScheduledTask -TaskName 'WukongCinema'
Write-Host "Wukong Cinema autostart is installed for this user: $exe"
