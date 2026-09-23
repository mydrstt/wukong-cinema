$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase, System.Windows.Forms, System.Drawing, System.Xaml
$refs = @('System.dll','System.Core.dll','System.Windows.Forms.dll','System.Drawing.dll',
    [System.Windows.Window].Assembly.Location,[System.Windows.Media.Brush].Assembly.Location,
    [System.Windows.Threading.Dispatcher].Assembly.Location,[System.Xaml.XamlReader].Assembly.Location)
$buildPath = Join-Path $PSScriptRoot ('WukongCinema-' + [guid]::NewGuid().ToString('N') + '.exe')
Add-Type -Path (Join-Path $PSScriptRoot 'Cinema.cs') -ReferencedAssemblies $refs -OutputAssembly $buildPath -OutputType WindowsApplication
Move-Item -LiteralPath $buildPath -Destination (Join-Path $PSScriptRoot 'WukongCinema.exe') -Force
