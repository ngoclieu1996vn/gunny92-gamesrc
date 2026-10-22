param(
    [string]$GameRoot = 'C:\Gunny92',
    [Parameter(Mandatory = $true)][string]$CecilDll,
    [string]$ServerAssembly
)
$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$output = Join-Path $GameRoot 'bin/compatibility'
New-Item -ItemType Directory -Path $output -Force | Out-Null
if (-not $ServerAssembly) { $ServerAssembly = Join-Path $GameRoot 'Server/road/Game.Server.dll' }
$baseDll = Join-Path $GameRoot 'Server/road/game.base.dll'
$reader = Join-Path $GameRoot 'gameSrc/Game.Server/Game.Server.Packets/Client/LoginPacketReader.cs'
$sourceRoot = Join-Path $GameRoot 'gameSrc'
function Compile([string[]]$CompilerArgs) {
    & $compiler /define:TRACE @CompilerArgs
    if ($LASTEXITCODE -ne 0) { throw 'Compatibility compilation failed.' }
}
Compile @('/nologo', '/target:library', "/out:$output/LoginPacketReader.dll", "/reference:$baseDll", $reader,
    "/reference:$ServerAssembly", "/reference:$GameRoot/Server/road/Game.Logic.dll",
    "/reference:$GameRoot/Request/bin/SqlDataProvider.dll",
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/LoginResponseV92.cs'),
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/RoomResponseV92.cs'),
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/HallSceneV92.cs'))
Compile @('/nologo', '/target:exe', "/out:$output/LoginPacketReaderTests.exe", "/reference:$baseDll",
    $reader, (Join-Path $sourceRoot 'tests/LoginPacketReaderTests.cs'))
Copy-Item -LiteralPath $baseDll -Destination (Join-Path $output 'Game.Base.dll') -Force
Copy-Item -LiteralPath (Join-Path $GameRoot 'Server/road/log4net.dll') -Destination $output -Force
& (Join-Path $output 'LoginPacketReaderTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Login protocol regression tests failed.' }
Compile @('/nologo', '/target:exe', "/out:$output/LoginResponseTests.exe", "/reference:$baseDll",
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/LoginResponseV92.cs'),
    (Join-Path $sourceRoot 'tests/LoginResponseTests.cs'))
& (Join-Path $output 'LoginResponseTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Login response regression tests failed.' }
Compile @('/nologo', '/target:exe', "/out:$output/RoomResponseTests.exe", "/reference:$baseDll",
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/RoomResponseV92.cs'),
    (Join-Path $sourceRoot 'tests/RoomResponseTests.cs'))
& (Join-Path $output 'RoomResponseTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Room response regression tests failed.' }
Compile @('/nologo', '/target:exe', "/out:$output/HallSceneTests.exe", "/reference:$baseDll",
    "/reference:$ServerAssembly", "/reference:$GameRoot/Server/road/Game.Logic.dll",
    "/reference:$GameRoot/Request/bin/SqlDataProvider.dll",
    (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/HallSceneV92.cs'),
    (Join-Path $sourceRoot 'tests/HallSceneTests.cs'))
Copy-Item -LiteralPath $ServerAssembly -Destination (Join-Path $output 'Game.Server.dll') -Force
Copy-Item -LiteralPath (Join-Path $GameRoot 'Server/road/Game.Logic.dll'), (Join-Path $GameRoot 'Request/bin/SqlDataProvider.dll') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $GameRoot 'Server/road/Bussiness.dll') -Destination $output -Force
& (Join-Path $output 'HallSceneTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'Hall scene regression tests failed.' }
Compile @('/nologo', '/target:exe', "/out:$output/PatchLoginProtocol.exe", "/reference:$CecilDll",
    (Join-Path $sourceRoot 'tools/PatchLoginProtocol.cs'))
Copy-Item -LiteralPath $CecilDll -Destination (Join-Path $output 'Mono.Cecil.dll') -Force
& (Join-Path $output 'PatchLoginProtocol.exe') $ServerAssembly (Join-Path $output 'LoginPacketReader.dll') (Join-Path $output 'Game.Server.dll')
if ($LASTEXITCODE -ne 0) { throw 'Server assembly patch failed.' }
$requestBin = Join-Path $GameRoot 'Request/bin'
Compile @('/nologo', '/target:library', "/out:$output/Tank.Request.V92.dll",
    '/reference:System.Web.dll', '/reference:System.Xml.Linq.dll',
    "/reference:$requestBin/Bussiness.dll", "/reference:$requestBin/Road.Flash.dll",
    "/reference:$requestBin/SqlDataProvider.dll", "/reference:$requestBin/Tank.Request.dll",
    (Join-Path $sourceRoot 'Tank.Request/Tank.Request/QuestListV92.cs'),
    (Join-Path $sourceRoot 'Tank.Request/Tank.Request/NoviceNodeDataV92.cs'))
Write-Output "Compatibility artifacts built in $output. No running service was changed."
