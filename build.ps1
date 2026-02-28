# Build and Publish Script
param(
    [string]$Configuration = "Release",
    [string]$OutputDir = "out",
    [switch]$CleanOutput = $true,
    [switch]$Force = $true
)

# Get the script directory (project root)
$ProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$SourceDir = Join-Path $ProjectRoot "src/ShiftyGrid"
$PublishOutputDir = Join-Path $ProjectRoot $OutputDir

Write-Host "=== Build and Publish Script ===" -ForegroundColor Green
Write-Host "Project Root: $ProjectRoot" -ForegroundColor Cyan
Write-Host "Source Directory: $SourceDir" -ForegroundColor Cyan
Write-Host "Output Directory: $PublishOutputDir" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan

# Ensure we're in the right directory
if (-not (Test-Path $SourceDir))
{
    Write-Error "Source directory not found: $SourceDir"
    exit 1
}

# Handle existing output directory
if (Test-Path $PublishOutputDir)
{
    $items = Get-ChildItem $PublishOutputDir
    if ($items.Count -gt 0)
    {
        Write-Host "`nOutput directory '$OutputDir' is not empty." -ForegroundColor Yellow

        # Try to stop any running instance of the app
        $exePath = Join-Path $PublishOutputDir "ShiftyGrid.exe"
        if (Test-Path $exePath)
        {
            Write-Host "Attempting to stop running app instance..." -ForegroundColor Yellow
            try
            {
                & $exePath exit | Out-Null
                Start-Sleep -Milliseconds 500
                Write-Host "Sent exit command to app." -ForegroundColor Gray
            }
            catch
            {
                Write-Host "Could not send exit command (app may not be running)." -ForegroundColor Gray
            }
        }

        if ($Force)
        {
            Write-Host "Force parameter specified. Cleaning output directory..." -ForegroundColor Yellow
            Remove-Item $PublishOutputDir -Recurse -Force
        } elseif ($CleanOutput)
        {
            Write-Host "CleanOutput parameter specified. Cleaning output directory..." -ForegroundColor Yellow
            Remove-Item $PublishOutputDir -Recurse -Force
        } else
        {
            Write-Host "Current contents:" -ForegroundColor Gray
            $items | Format-Table Name, Length, LastWriteTime -AutoSize

            $response = Read-Host "Do you want to clean the output directory? (y/N)"
            if ($response -match '^[Yy]([Ee][Ss])?$')
            {
                Write-Host "Cleaning output directory..." -ForegroundColor Yellow
                Remove-Item $PublishOutputDir -Recurse -Force
            } else
            {
                Write-Host "Keeping existing files. Published files will be added/overwritten." -ForegroundColor Yellow
            }
        }
    }
}

# Create output directory if it doesn't exist
if (-not (Test-Path $PublishOutputDir))
{
    New-Item -ItemType Directory -Path $PublishOutputDir -Force | Out-Null
}

# Clean previous builds
Write-Host "`nCleaning previous builds..." -ForegroundColor Yellow
Push-Location $SourceDir
try
{
    dotnet clean --configuration $Configuration
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Clean failed"
        exit 1
    }
} finally
{
    Pop-Location
}

# Build the project
Write-Host "`nBuilding project..." -ForegroundColor Yellow
Push-Location $SourceDir
try
{
    dotnet build --configuration $Configuration #--no-restore
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Build failed"
        exit 1
    }
} finally
{
    Pop-Location
}

# Publish the project
Write-Host "`nPublishing project..." -ForegroundColor Yellow
$PublishTempDir = Join-Path $SourceDir "ShiftyGrid\bin\$Configuration\net10.0-windows10.0.17763.0\win-x64\publish"

Push-Location $SourceDir
try
{
    dotnet publish --configuration $Configuration --no-build --output $PublishTempDir
    if ($LASTEXITCODE -ne 0)
    {
        Write-Error "Publish failed"
        exit 1
    }
} finally
{
    Pop-Location
}

# Copy published files to output directory
Write-Host "`nCopying published files to output directory..." -ForegroundColor Yellow
Copy-Item -Path "$PublishTempDir\*" -Destination $PublishOutputDir -Recurse -Force

# Display results
Write-Host "`n=== Build and Publish Completed Successfully ===" -ForegroundColor Green
Write-Host "Published files copied to: $PublishOutputDir" -ForegroundColor Green

# List the contents of the output directory
Write-Host "`nOutput directory contents:" -ForegroundColor Cyan
Get-ChildItem $PublishOutputDir | Format-Table Name, Length, LastWriteTime -AutoSize

Write-Host "`nBuild and publish process completed!" -ForegroundColor Green
