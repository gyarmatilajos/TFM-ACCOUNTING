param([switch]$Integration)
$ErrorActionPreference = 'Stop'
Push-Location (Split-Path $PSScriptRoot -Parent)
try {
    New-Item -ItemType Directory -Path 'obj/ModelVerification' -Force | Out-Null
    $project = @'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="../../TFM-ACCOUNTING.Desktop/TFM-ACCOUNTING.Desktop.csproj" />
    <Compile Include="../../scripts/verification/FoundationChecks.cs" Link="Program.cs" />
    <Compile Include="../../scripts/verification/OwnCompanyChecks.cs" Link="OwnCompanyChecks.cs" />
    <Compile Include="../../scripts/verification/PartnerChecks.cs" Link="PartnerChecks.cs" />
    <Compile Include="../../scripts/verification/ObligationChecks.cs" Link="ObligationChecks.cs" />
  </ItemGroup>
</Project>
'@
    Set-Content -LiteralPath 'obj/ModelVerification/ModelVerification.csproj' -Value $project -Encoding utf8
    dotnet restore obj/ModelVerification/ModelVerification.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Restore failed.' }
    dotnet build obj/ModelVerification/ModelVerification.csproj --no-restore -m:1
    if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
    $runArgs = @('run', '--project', 'obj/ModelVerification/ModelVerification.csproj', '--no-restore', '--no-build')
    if ($Integration) { $runArgs += @('--', '--integration') }
    & dotnet @runArgs
    if ($LASTEXITCODE -ne 0) { throw 'Foundation verification failed.' }
} finally { Pop-Location }
