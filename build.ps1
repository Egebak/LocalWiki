$ErrorActionPreference = 'Stop'
Set-Location -LiteralPath $PSScriptRoot

$dotnet = if ($env:LOCALWIKI_DOTNET) { $env:LOCALWIKI_DOTNET } else { (Get-Command dotnet -ErrorAction Stop).Source }
$node = (Get-Command node -ErrorAction Stop).Source
$npmCommand = (Get-Command npm.cmd -ErrorAction Stop).Source
$npmCli = Join-Path (Split-Path $npmCommand) 'node_modules\npm\bin\npm-cli.js'

if (Test-Path -LiteralPath $npmCli) {
    & $node $npmCli ci
} else {
    & $npmCommand ci
}
if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' }

& $node Frontend\build.mjs
if ($LASTEXITCODE -ne 0) { throw 'Frontend build failed.' }

& $dotnet restore LocalWiki.slnx
if ($LASTEXITCODE -ne 0) { throw 'dotnet restore failed.' }
& $dotnet build LocalWiki.slnx --no-restore
if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed.' }
& $dotnet test LocalWiki.slnx --no-build
if ($LASTEXITCODE -ne 0) { throw 'dotnet test failed.' }
