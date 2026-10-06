$ErrorActionPreference = 'Stop'
& (Join-Path $PSScriptRoot 'build.ps1')
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$dotnet = if ($env:LOCALWIKI_DOTNET) { $env:LOCALWIKI_DOTNET } else { (Get-Command dotnet -ErrorAction Stop).Source }
$output = Join-Path $PSScriptRoot 'artifacts\publish\win-x64'
& $dotnet publish (Join-Path $PSScriptRoot 'src\LocalWiki.App\LocalWiki.App.csproj') -c Release -r win-x64 --self-contained true -o $output -p:PublishSingleFile=false
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish failed.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'LICENSE') -Destination (Join-Path $output 'LICENSE') -Force
Write-Host "Published LocalWiki to $output"
