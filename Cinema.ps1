param([int]$IdleMinutes = 5, [switch]$RenderCheck)
$ErrorActionPreference = 'Stop'
try {
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing, System.Xaml
    $refs = @('System.dll','System.Core.dll','System.Windows.Forms.dll','System.Drawing.dll',
        [System.Windows.Window].Assembly.Location,
        [System.Windows.Media.Brush].Assembly.Location,
        [System.Windows.Threading.Dispatcher].Assembly.Location,
        [System.Xaml.XamlReader].Assembly.Location)
    Add-Type -Path (Join-Path $PSScriptRoot 'Cinema.cs') -ReferencedAssemblies $refs
    if ($RenderCheck) { [WukongCinema.Cinema]::RenderCheck($PSScriptRoot) }
    else { [WukongCinema.Cinema]::Start($PSScriptRoot, $IdleMinutes) }
} catch {
    $_ | Out-String | Add-Content -LiteralPath (Join-Path $PSScriptRoot 'startup-error.log')
    throw
}
