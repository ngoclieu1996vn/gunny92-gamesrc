param(
    [string]$GameRoot = 'C:\Gunny92',
    [Parameter(Mandatory = $true)][string]$CecilDll,
    [string]$ServerAssembly,
    [string]$FightAssembly
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
if (-not $FightAssembly) {
    # Costura's embedded original remains available after deploying an external DLL.
    Add-Type -Path $CecilDll
    $service=[Mono.Cecil.AssemblyDefinition]::ReadAssembly((Join-Path $GameRoot 'Server/fight/Fighting.Service.exe'))
    try {
        $resource=$service.MainModule.Resources | Where-Object {$_.Name -eq 'costura.fighting.server.dll.compressed'}
        if(-not $resource){throw 'Original Fighting.Server resource not found; pass -FightAssembly.'}
        $stream=$resource.GetResourceStream()
        $deflate=New-Object System.IO.Compression.DeflateStream($stream,[System.IO.Compression.CompressionMode]::Decompress)
        $FightAssembly=Join-Path $output 'Fighting.Server.original.dll'
        $destination=[IO.File]::Create($FightAssembly)
        try {$deflate.CopyTo($destination)} finally {$destination.Dispose();$deflate.Dispose();$stream.Dispose()}
    } finally {$service.Dispose()}
}
if ($FightAssembly) {
    Compile @('/nologo','/target:library',"/out:$output/Game.Matchmaking.V92.dll","/reference:$ServerAssembly","/reference:$baseDll",
        "/reference:$GameRoot/Server/road/Game.Logic.dll","/reference:$GameRoot/Request/bin/SqlDataProvider.dll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/MatchmakingV92.cs'),
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/InventoryResponseV92.cs'),
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/BattleResponseV92.cs'),
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/PetFormsV92.cs'),
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/CaptchaV92.cs'))
    Compile @('/nologo','/target:exe',"/out:$output/BattleResponseTests.exe","/reference:$baseDll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/BattleResponseV92.cs'),(Join-Path $sourceRoot 'tests/BattleResponseTests.cs'))
    & "$output/BattleResponseTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Battle response regression tests failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/BattleStartTests.exe","/reference:$baseDll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/BattleResponseV92.cs'),(Join-Path $sourceRoot 'tests/BattleStartTests.cs'))
    & "$output/BattleStartTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Battle start regression tests failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/BattleTurnTests.exe","/reference:$baseDll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/BattleResponseV92.cs'),(Join-Path $sourceRoot 'tests/BattleTurnTests.cs'))
    & "$output/BattleTurnTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Battle turn regression tests failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/BattleShootTests.exe","/reference:$baseDll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/BattleResponseV92.cs'),(Join-Path $sourceRoot 'tests/BattleShootTests.cs'))
    & "$output/BattleShootTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Battle shoot regression tests failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/PetFormsTests.exe","/reference:$baseDll","/reference:$ServerAssembly",
        "/reference:$GameRoot/Server/road/Game.Logic.dll","/reference:$GameRoot/Request/bin/SqlDataProvider.dll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/PetFormsV92.cs'),(Join-Path $sourceRoot 'tests/PetFormsTests.cs'))
    & "$output/PetFormsTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Pet forms regression tests failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/InventoryResponseTests.exe","/reference:$baseDll",
        (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/InventoryResponseV92.cs'),(Join-Path $sourceRoot 'tests/InventoryResponseTests.cs'))
    & "$output/InventoryResponseTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Inventory response regression tests failed.'}
    Compile @('/nologo','/target:library',"/out:$output/Fight.Matchmaking.V92.dll","/reference:$FightAssembly","/reference:$baseDll",
        "/reference:$GameRoot/Server/road/Game.Logic.dll",
        (Join-Path $sourceRoot 'Fighting.Server/Fighting.Server.Rooms/MatchCancellationV92.cs'))
    Copy-Item -LiteralPath $FightAssembly -Destination "$output/Fighting.Server.dll" -Force
    foreach($test in @(@('MatchmakingTests','Game.Matchmaking.V92'),@('MatchCancellationTests','Fight.Matchmaking.V92'))) {
        Compile @('/nologo','/target:exe',"/out:$output/$($test[0]).exe","/reference:$ServerAssembly","/reference:$FightAssembly",
            "/reference:$output/$($test[1]).dll","/reference:$baseDll","/reference:$GameRoot/Server/road/Game.Logic.dll",
            "/reference:$GameRoot/Request/bin/SqlDataProvider.dll",(Join-Path $sourceRoot "tests/$($test[0]).cs"))
        & "$output/$($test[0]).exe"
        if($LASTEXITCODE -ne 0){ throw "$($test[0]) failed." }
    }
    Compile @('/nologo','/target:exe',"/out:$output/PatchMatchmaking.exe","/reference:$CecilDll",(Join-Path $sourceRoot 'tools/PatchMatchmaking.cs'))
    & "$output/PatchMatchmaking.exe" "$output/Game.Server.dll" "$output/Game.Matchmaking.V92.dll" $FightAssembly "$output/Fight.Matchmaking.V92.dll" "$output/Game.Server.match.dll" "$output/Fighting.Server.match.dll"
    if($LASTEXITCODE -ne 0){ throw 'Matchmaking patch failed.' }
    Move-Item -LiteralPath "$output/Game.Server.match.dll" -Destination "$output/Game.Server.dll" -Force
    Move-Item -LiteralPath "$output/Fighting.Server.match.dll" -Destination "$output/Fighting.Server.dll" -Force
    Compile @('/nologo','/define:PATCHED','/target:exe',"/out:$output/PetFormsHookTests.exe","/reference:$baseDll","/reference:$output/Game.Server.dll",
        "/reference:$output/Game.Matchmaking.V92.dll","/reference:$GameRoot/Server/road/Game.Logic.dll","/reference:$GameRoot/Request/bin/SqlDataProvider.dll",
        (Join-Path $sourceRoot 'tests/PetFormsTests.cs'))
    & "$output/PetFormsHookTests.exe"
    if($LASTEXITCODE -ne 0){throw 'Pet forms runtime hook test failed.'}
    Compile @('/nologo','/target:exe',"/out:$output/CaptchaTests.exe","/reference:$output/Game.Server.dll","/reference:$baseDll",
        "/reference:$GameRoot/Server/road/Game.Logic.dll","/reference:$GameRoot/Request/bin/SqlDataProvider.dll",(Join-Path $sourceRoot 'tests/CaptchaTests.cs'))
    & "$output/CaptchaTests.exe"
    if($LASTEXITCODE -ne 0){throw 'CAPTCHA removal regression tests failed.'}
    Write-Output "Matchmaking artifacts built and verified. Deploy BOTH runtime DLLs and their matching V92 helper DLLs."
}
Write-Output "Compatibility artifacts built in $output. No running service was changed."
