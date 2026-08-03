param(
    [switch]$IncludeSqlServerIntegration
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet --info
    dotnet tool restore
    dotnet restore LawyerPlatform.sln
    dotnet build LawyerPlatform.sln --configuration Release --no-restore

    if ($IncludeSqlServerIntegration) {
        dotnet test LawyerPlatform.sln --configuration Release --no-build
    }
    else {
        dotnet test LawyerPlatform.sln `
            --configuration Release `
            --no-build `
            --filter "Category!=SqlServerIntegration"
    }

    dotnet new uninstall $root 2>$null | Out-Null
    dotnet new install $root

    $output = Join-Path $env:TEMP "BuildingBlockTemplateSmoke"
    Remove-Item $output -Recurse -Force -ErrorAction SilentlyContinue
    dotnet new buildingblock-sln --name SmokeTest.Product --output $output
    dotnet build (Join-Path $output "SmokeTest.Product.sln") --configuration Release
}
finally {
    Pop-Location
}
