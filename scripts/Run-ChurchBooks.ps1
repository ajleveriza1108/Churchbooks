[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
Push-Location $projectRoot
try {
    & dotnet run --project .\src\ChurchBooks.App\ChurchBooks.App.csproj -c Debug
    if ($LASTEXITCODE -ne 0) { throw 'ChurchBooks failed to run.' }
}
finally {
    Pop-Location
}
