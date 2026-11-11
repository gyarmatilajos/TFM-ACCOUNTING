$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    if (-not (Get-Command SqlLocalDB -ErrorAction SilentlyContinue)) {
        throw 'A SQL Server Express LocalDB nincs telepítve. Telepítsd a Visual Studio SQL Server Express LocalDB komponensét.'
    }
    # Deliberately fixed to the local development database. Never use an Azure/environment connection here.
    $developmentConnection = 'Server=(localdb)\MSSQLLocalDB;Database=TFM_ACCOUNTING_Development;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=15'
    SqlLocalDB start MSSQLLocalDB
    if ($LASTEXITCODE -ne 0) { throw 'A helyi SQL Server példány nem indult el.' }
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw 'Az EF Core eszköz visszaállítása nem sikerült.' }
    dotnet restore TFM-ACCOUNTING.slnx
    if ($LASTEXITCODE -ne 0) { throw 'A csomagok visszaállítása nem sikerült.' }
    dotnet build TFM-ACCOUNTING.Data/TFM-ACCOUNTING.Data.csproj --no-restore -c Release -m:1
    if ($LASTEXITCODE -ne 0) { throw 'A Data projekt fordítása nem sikerült.' }
    dotnet ef database update --project TFM-ACCOUNTING.Data --configuration Release --no-build --connection $developmentConnection
    if ($LASTEXITCODE -ne 0) { throw 'A helyi adatbázis előkészítése nem sikerült.' }
    Write-Output 'Kész: (localdb)\MSSQLLocalDB / TFM_ACCOUNTING_Development. Az adatbázis és a felvett adatok megmaradnak.'
} finally { Pop-Location }
