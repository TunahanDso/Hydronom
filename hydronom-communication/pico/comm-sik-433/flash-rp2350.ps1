param(
    [Parameter(Mandatory = $true)]
    [string]$ElfPath
)

$ErrorActionPreference = "Stop"

Write-Host "[Hydronom Flash] ELF: $ElfPath" -ForegroundColor Cyan

if (-not (Test-Path $ElfPath)) {
    throw "ELF dosyasi bulunamadi: $ElfPath"
}

$releaseDir = Split-Path $ElfPath -Parent
$uf2Path = Join-Path $releaseDir "hydronom-communication-pico-sik-433-rp2350.uf2"

Write-Host "[Hydronom Flash] RP2350 ARM-S UF2 uretiliyor..." -ForegroundColor Yellow

elf2flash convert -f 0xe48bff59 $ElfPath $uf2Path

if (-not (Test-Path $uf2Path)) {
    throw "UF2 uretilemedi: $uf2Path"
}

Write-Host "[Hydronom Flash] RP2350 BOOTSEL diski araniyor..." -ForegroundColor Yellow

$volume = Get-Volume |
    Where-Object { $_.FileSystemLabel -eq "RP2350" } |
    Select-Object -First 1

if ($null -eq $volume -or [string]::IsNullOrWhiteSpace($volume.DriveLetter)) {
    Write-Host ""
    Write-Host "Pico 2 W BOOTSEL modunda gorunmuyor." -ForegroundColor Red
    Write-Host "1) Pico USB'yi cikar"
    Write-Host "2) BOOTSEL'e basili tut"
    Write-Host "3) USB'yi tak"
    Write-Host "4) BOOTSEL'i birak"
    Write-Host "5) Tekrar cargo run --release calistir"
    throw "RP2350 BOOTSEL diski bulunamadi."
}

$drive = "$($volume.DriveLetter):\"

Write-Host "[Hydronom Flash] UF2 kopyalaniyor: $drive" -ForegroundColor Green
Copy-Item $uf2Path $drive -Force

Write-Host "[Hydronom Flash] Kopyalama tamam. Pico reset atmali." -ForegroundColor Green
Start-Sleep -Seconds 4

Write-Host "[Hydronom Flash] Volume durumu:" -ForegroundColor Cyan
Get-Volume |
    Select-Object DriveLetter,FileSystemLabel,FileSystem,HealthStatus |
    Format-Table -AutoSize

Write-Host "[Hydronom Flash] Mevcut seri portlar:" -ForegroundColor Cyan
Get-CimInstance Win32_SerialPort |
    Select-Object DeviceID,Name,Description |
    Format-Table -AutoSize