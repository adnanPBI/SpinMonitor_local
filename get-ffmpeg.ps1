# SpinMonitor - FFmpeg Download Script
# Downloads and extracts FFmpeg for Windows x64

param(
    [string]$OutputPath = ".\FFmpeg\bin\x64"
)

Write-Host "SpinMonitor - FFmpeg Downloader" -ForegroundColor Cyan
Write-Host "=================================" -ForegroundColor Cyan
Write-Host ""

# Create output directory
$fullPath = Join-Path $PSScriptRoot $OutputPath
Write-Host "Creating directory: $fullPath" -ForegroundColor Yellow
New-Item -ItemType Directory -Force -Path $fullPath | Out-Null

# FFmpeg download URLs (try multiple sources)
$sources = @(
    @{
        Name = "BtbN (GitHub)"
        Url = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip"
        ExtractPath = "ffmpeg-master-latest-win64-gpl\bin\ffmpeg.exe"
    },
    @{
        Name = "GyanD (GitHub)"
        Url = "https://github.com/GyanD/codexffmpeg/releases/download/6.1/ffmpeg-6.1-essentials_build.zip"
        ExtractPath = "ffmpeg-6.1-essentials_build\bin\ffmpeg.exe"
    }
)

$tempZip = Join-Path $env:TEMP "ffmpeg_download.zip"
$tempExtract = Join-Path $env:TEMP "ffmpeg_extract"

foreach ($source in $sources) {
    Write-Host "Trying source: $($source.Name)" -ForegroundColor Green
    Write-Host "URL: $($source.Url)" -ForegroundColor Gray

    try {
        # Download
        Write-Host "Downloading..." -NoNewline
        $ProgressPreference = 'SilentlyContinue'
        Invoke-WebRequest -Uri $source.Url -OutFile $tempZip -TimeoutSec 60
        $ProgressPreference = 'Continue'
        Write-Host " Done" -ForegroundColor Green

        # Extract
        Write-Host "Extracting..." -NoNewline
        Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue
        Expand-Archive -Path $tempZip -DestinationPath $tempExtract -Force
        Write-Host " Done" -ForegroundColor Green

        # Find ffmpeg.exe
        $ffmpegSource = Join-Path $tempExtract $source.ExtractPath

        if (-not (Test-Path $ffmpegSource)) {
            # Try to find it recursively
            $ffmpegSource = Get-ChildItem -Path $tempExtract -Filter "ffmpeg.exe" -Recurse | Select-Object -First 1 -ExpandProperty FullName
        }

        if ($ffmpegSource -and (Test-Path $ffmpegSource)) {
            # Copy to destination
            $destination = Join-Path $fullPath "ffmpeg.exe"
            Copy-Item -Path $ffmpegSource -Destination $destination -Force

            Write-Host ""
            Write-Host "✓ FFmpeg installed successfully!" -ForegroundColor Green
            Write-Host "Location: $destination" -ForegroundColor Cyan

            # Test FFmpeg
            Write-Host ""
            Write-Host "Testing FFmpeg..." -ForegroundColor Yellow
            & $destination -version | Select-Object -First 1

            # Cleanup
            Remove-Item -Path $tempZip -Force -ErrorAction SilentlyContinue
            Remove-Item -Path $tempExtract -Recurse -Force -ErrorAction SilentlyContinue

            Write-Host ""
            Write-Host "Installation complete! You can now run SpinMonitor." -ForegroundColor Green
            exit 0
        }
        else {
            Write-Host " Failed (ffmpeg.exe not found in archive)" -ForegroundColor Red
        }
    }
    catch {
        Write-Host " Failed" -ForegroundColor Red
        Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
    }

    Write-Host ""
}

Write-Host "❌ All download sources failed!" -ForegroundColor Red
Write-Host ""
Write-Host "Please download FFmpeg manually:" -ForegroundColor Yellow
Write-Host "1. Visit: https://github.com/BtbN/FFmpeg-Builds/releases" -ForegroundColor Yellow
Write-Host "2. Download: ffmpeg-master-latest-win64-gpl.zip" -ForegroundColor Yellow
Write-Host "3. Extract ffmpeg.exe to: $fullPath" -ForegroundColor Yellow
Write-Host ""

exit 1
