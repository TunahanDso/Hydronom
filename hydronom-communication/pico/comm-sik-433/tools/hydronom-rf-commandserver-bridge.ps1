param(
    [string]$SerialPortName = "COM17",
    [int]$SerialBaud = 57600,
    [string]$RuntimeHost = "127.0.0.1",
    [int]$RuntimePort = 5060
)

$ErrorActionPreference = "Stop"

Write-Host "[Hydronom RF Bridge] Serial=$SerialPortName Baud=$SerialBaud -> TCP=$RuntimeHost:$RuntimePort" -ForegroundColor Cyan

$serial = New-Object System.IO.Ports.SerialPort $SerialPortName,$SerialBaud,None,8,one
$serial.NewLine = "`n"
$serial.ReadTimeout = 200
$serial.WriteTimeout = 500
$serial.Open()

$tcp = [System.Net.Sockets.TcpClient]::new()
$tcp.Connect($RuntimeHost, $RuntimePort)

$stream = $tcp.GetStream()
$writer = [System.IO.StreamWriter]::new($stream)
$writer.AutoFlush = $true

Write-Host "[Hydronom RF Bridge] Aktif. RF'den gelen satırlar Runtime CommandServer'a aktarılacak." -ForegroundColor Green

try {
    while ($true) {
        try {
            $line = $serial.ReadLine()
            if ([string]::IsNullOrWhiteSpace($line)) {
                continue
            }

            $line = $line.Trim()
            Write-Host "RF >> TCP : $line" -ForegroundColor Yellow
            $writer.WriteLine($line)
        }
        catch [System.TimeoutException] {
        }
    }
}
finally {
    if ($writer) { $writer.Dispose() }
    if ($stream) { $stream.Dispose() }
    if ($tcp) { $tcp.Dispose() }
    if ($serial -and $serial.IsOpen) { $serial.Close() }
}