$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
dotnet test "$root\Puck.sln" -c Release --nologo
exit $LASTEXITCODE
