param([switch]$Install, [switch]$Restore, [switch]$Query)
$ErrorActionPreference = 'Stop'
if (@(@($Install, $Restore, $Query) | Where-Object { $_ }).Count -ne 1) {
    throw 'Choose exactly one of -Install, -Restore, or -Query.'
}

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
[ComImport, Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B"),
 InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IDesktopWallpaperCinema {
    [PreserveSig] int SetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorId,
        [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
    [PreserveSig] int GetWallpaper(
        [MarshalAs(UnmanagedType.LPWStr)] string monitorId,
        out IntPtr wallpaper);
}
public static class CinemaWallpaperBridge {
    static IDesktopWallpaperCinema Create() {
        return (IDesktopWallpaperCinema)Activator.CreateInstance(
            Type.GetTypeFromCLSID(new Guid("C2CF3110-460E-4FC1-B9D0-8A1C0C9CC4BD")));
    }
    public static string Get() {
        var wallpaper = Create();
        try {
            IntPtr pointer;
            Marshal.ThrowExceptionForHR(wallpaper.GetWallpaper(null, out pointer));
            try { return Marshal.PtrToStringUni(pointer); }
            finally { if (pointer != IntPtr.Zero) Marshal.FreeCoTaskMem(pointer); }
        } finally { Marshal.ReleaseComObject(wallpaper); }
    }
    public static string Set(string path) {
        var wallpaper = Create();
        try {
            Marshal.ThrowExceptionForHR(wallpaper.SetWallpaper(null, path));
        } finally { Marshal.ReleaseComObject(wallpaper); }
        return Get();
    }
}
'@

$stateFolder = Join-Path $env:LOCALAPPDATA 'WukongCinema'
$stateFile = Join-Path $stateFolder 'wallpaper-state.json'
$wallpaperFolder = Join-Path ([Environment]::GetFolderPath('MyDocuments')) 'WukongCinema'
$installedImage = Join-Path $wallpaperFolder 'wallpaper-always-wukong.jpg'
$desktopKey = 'HKCU:\Control Panel\Desktop'
$current = [CinemaWallpaperBridge]::Get()

if ($Query) { Write-Host "Active wallpaper: $current"; return }

if ($Restore) {
    if (-not (Test-Path -LiteralPath $stateFile)) {
        Write-Host 'No wallpaper backup was saved by Wukong Cinema.'
        return
    }
    $state = Get-Content -LiteralPath $stateFile -Raw | ConvertFrom-Json
    if ($current -ine $state.InstalledWallpaper) {
        Write-Host 'Wallpaper was changed manually; leaving it as-is.'
        Remove-Item -LiteralPath $stateFile
        return
    }
    if (-not (Test-Path -LiteralPath $state.PreviousWallpaper)) {
        throw 'The previous wallpaper file is missing. No wallpaper was changed.'
    }
    Set-ItemProperty -Path $desktopKey -Name WallpaperStyle -Value $state.WallpaperStyle
    Set-ItemProperty -Path $desktopKey -Name TileWallpaper -Value $state.TileWallpaper
    $actual = [CinemaWallpaperBridge]::Set($state.PreviousWallpaper)
    if ($actual -ine $state.PreviousWallpaper) { throw 'Windows did not restore the previous wallpaper.' }
    Remove-Item -LiteralPath $stateFile
    Write-Host "Restored previous wallpaper: $actual"
    return
}

$exe = Join-Path $PSScriptRoot 'WukongCinema.exe'
foreach ($required in @($exe, (Join-Path $PSScriptRoot 'landscape-clean.png'),
        (Join-Path $PSScriptRoot 'wukong-original-upscaled.png'))) {
    if (-not (Test-Path -LiteralPath $required)) { throw "Required file is missing: $required" }
}
if (-not (Test-Path -LiteralPath $stateFile)) {
    if (-not $current -or -not (Test-Path -LiteralPath $current)) {
        throw 'The current wallpaper cannot be backed up. No wallpaper was changed.'
    }
    # Preserve the original wallpaper from earlier local-only versions.
    $legacyBackup = Join-Path $stateFolder 'previous-wallpaper.txt'
    $previous = $current
    if ($current -like '*\WukongIdleScreen\wallpaper-always-wukong.jpg' -and
        (Test-Path -LiteralPath $legacyBackup)) {
        $previous = (Get-Content -LiteralPath $legacyBackup -Raw -Encoding UTF8).Trim()
    }
    $style = Get-ItemProperty -Path $desktopKey
    $state = [pscustomobject]@{
        PreviousWallpaper = $previous
        WallpaperStyle = [string]$style.WallpaperStyle
        TileWallpaper = [string]$style.TileWallpaper
        InstalledWallpaper = $installedImage
    }
    New-Item -ItemType Directory -Path $stateFolder -Force | Out-Null
    $state | ConvertTo-Json | Set-Content -LiteralPath $stateFile -Encoding UTF8
}

New-Item -ItemType Directory -Path $wallpaperFolder -Force | Out-Null
Add-Type -AssemblyName System.Windows.Forms
$screen = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
$render = Start-Process -FilePath $exe -ArgumentList @('--render-wallpaper',
    ('"' + $installedImage + '"'), $screen.Width, $screen.Height) -Wait -PassThru -WindowStyle Hidden
if ($render.ExitCode -ne 0 -or -not (Test-Path -LiteralPath $installedImage)) {
    throw 'Could not render the desktop wallpaper.'
}
Set-ItemProperty -Path $desktopKey -Name WallpaperStyle -Value '10'
Set-ItemProperty -Path $desktopKey -Name TileWallpaper -Value '0'
$actual = [CinemaWallpaperBridge]::Set($installedImage)
if ($actual -ine $installedImage) { throw 'Windows did not apply the Wukong wallpaper.' }
Write-Host "Applied Wukong desktop wallpaper: $actual"
