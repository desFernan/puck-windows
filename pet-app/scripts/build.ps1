$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet build "$root\Puck.sln" -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
