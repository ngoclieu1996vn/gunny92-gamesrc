param(
    [string]$GameRoot = 'C:\Gunny92',
    [string]$ClientReader = (Join-Path $PSScriptRoot '../tests/fixtures/PlayerManager-property-reader.as')
)
$ErrorActionPreference = 'Stop'
$sourceRoot = Split-Path $PSScriptRoot
$testOutput = Join-Path $sourceRoot 'bin/property164-tests'
New-Item -ItemType Directory -Path $testOutput -Force | Out-Null
$helper = Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/PlayerPropertyResponseV92.cs'
if (-not (Test-Path -LiteralPath $helper)) {
    # Before the fix, run the real legacy writer rather than a test-built schema.
    $legacy = Get-Content (Join-Path $sourceRoot 'Game.Server/Game.Base.Packets/AbstractPacketLib.cs') -Raw
    $body = [regex]::Match($legacy, '(?s)public GSPacketIn SendUpdatePlayerProperty\(PlayerInfo info, PlayerProperty PlayerProp\)\s*\{(.*?)\r?\n\s*public void SendOpenHappyRecharge').Groups[1].Value
    if (-not $body) { throw 'Cannot extract legacy writer' }
    $body = $body.Substring(0, $body.LastIndexOf('}')).Replace('this.SendTCP(pkg);', '')
    $helper = Join-Path $testOutput 'LegacyPropertyWriter.cs'
    @"
using Game.Base.Packets; using Game.Server.GameUtils; using SqlDataProvider.Data;
namespace Game.Server.Packets { public static class PlayerPropertyResponseV92 {
public static GSPacketIn Create(PlayerInfo info, PlayerProperty PlayerProp, int zoneId) { $body }
} }
"@ | Set-Content -LiteralPath $helper -Encoding UTF8
}
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe "/out:$testOutput/PlayerPropertyResponseTests.exe" "/reference:$GameRoot/Server/road/Game.Base.dll" "/reference:$GameRoot/Server/road/Game.Server.dll" "/reference:$GameRoot/Server/road/Game.Logic.dll" "/reference:$GameRoot/Request/bin/SqlDataProvider.dll" $helper (Join-Path $sourceRoot 'tests/PlayerPropertyResponseTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Property response test compilation failed' }
foreach ($dll in @('Game.Base.dll','Game.Server.dll','Game.Logic.dll','log4net.dll','Bussiness.dll')) {
    Copy-Item -LiteralPath (Join-Path $GameRoot "Server/road/$dll") -Destination $testOutput -Force
}
Copy-Item -LiteralPath (Join-Path $GameRoot 'Request/bin/SqlDataProvider.dll') -Destination $testOutput -Force
& (Join-Path $testOutput 'PlayerPropertyResponseTests.exe') (Join-Path $testOutput 'distinct.bin') (Join-Path $testOutput 'empty.bin')
if ($LASTEXITCODE -ne 0) { throw 'Property response fixture generation failed' }
& node (Join-Path $sourceRoot 'tests/PlayerPropertyClientReaderTests.js') $ClientReader (Join-Path $testOutput 'distinct.bin') (Join-Path $testOutput 'empty.bin')
if ($LASTEXITCODE -ne 0) { throw 'Property response client regression failed' }
