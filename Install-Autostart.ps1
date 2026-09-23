# Registers Wukong Cinema for this Windows user at sign-in and unlock.
$ErrorActionPreference = 'Stop'
$exe = Join-Path $PSScriptRoot 'WukongCinema.exe'
if (-not (Test-Path -LiteralPath $exe)) {
    throw 'WukongCinema.exe is missing. Run Build.ps1 first.'
}

$existing = Get-ScheduledTask -TaskName 'WukongCinema' -TaskPath '\' -ErrorAction SilentlyContinue
if ($existing -and $existing.Actions.Execute -ne $exe) {
    throw 'A different task named WukongCinema already exists. No changes were made.'
}

$user = [System.Security.Principal.WindowsIdentity]::GetCurrent().Name
$service = New-Object -ComObject 'Schedule.Service'
$service.Connect()
$folder = $service.GetFolder('\')
$task = $service.NewTask(0)
$task.RegistrationInfo.Description = 'Start Wukong Cinema at sign-in and restore it after unlock.'
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

$null = $folder.RegisterTaskDefinition('WukongCinema', $task, 6, $user, $null, 3, $null)
Start-ScheduledTask -TaskName 'WukongCinema'
Write-Host 'Wukong Cinema autostart is installed for this user.'
