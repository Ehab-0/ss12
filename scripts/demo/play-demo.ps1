# Starts a private SS12 server on this computer and joins it with the 3D client.
# Used by "Play 3D Demo.bat". Nothing here talks to the internet.

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$serverExe = Join-Path $here 'bin\Content.Server\Content.Server.exe'
$clientExe = Join-Path $here 'bin\Content.Client\Content.Client.exe'
if (-not (Test-Path $serverExe) -or -not (Test-Path $clientExe)) {
    Write-Host ''
    Write-Host ' I cannot find the game files. Please unzip the whole download first (right-click the zip, "Extract All"),'
    Write-Host ' then open "Play 3D Demo.bat" from the unzipped folder.'
    exit 1
}

$data = Join-Path $here 'data'
New-Item -ItemType Directory -Force -Path (Join-Path $data 'server'), (Join-Path $data 'client') | Out-Null
$serverLog = Join-Path $data 'server.log'
if (Test-Path $serverLog) { Remove-Item $serverLog -Force }

Write-Host ''
Write-Host ' Starting a small private game on your computer. This takes about a minute the first time.'
Write-Host ' (A second window for the game server will open; it is minimised. Do not close it.)'
Write-Host ''

$serverArgs = @(
    '--data-dir', (Join-Path $data 'server'),
    # only this computer can reach the demo server (also keeps Windows from asking about the firewall)
    '--cvar', 'net.bindto=127.0.0.1',
    '--cvar', 'status.bind=127.0.0.1:1212',
    '--cvar', 'game.lobbyenabled=false',
    '--cvar', 'game.map=Saltern',
    '--cvar', 'game.defaultpreset=Sandbox'
)
# the server writes its log to the console; send it to a file so we can see when it is ready
$server = Start-Process -FilePath $serverExe -ArgumentList $serverArgs -WorkingDirectory (Split-Path $serverExe) `
    -RedirectStandardOutput $serverLog -WindowStyle Minimized -PassThru

try {
    $ready = $false
    for ($i = 0; $i -lt 180; $i++) {
        if ($server.HasExited) { break }
        if ((Test-Path $serverLog) -and (Select-String -Path $serverLog -Pattern '-> Ready' -SimpleMatch -Quiet)) { $ready = $true; break }
        Start-Sleep -Seconds 1
    }

    if ($ready) {
        # the server answers on port 1212 once it accepts players
        $ready = $false
        for ($i = 0; $i -lt 60 -and -not $server.HasExited; $i++) {
            try {
                $tcp = New-Object System.Net.Sockets.TcpClient
                $tcp.Connect('127.0.0.1', 1212)
                $tcp.Close()
                $ready = $true
                break
            }
            catch { Start-Sleep -Seconds 1 }
        }
    }

    if (-not $ready) {
        Write-Host ' The game server did not start. Details are in:'
        Write-Host "   $serverLog"
        Write-Host ' Please attach that file if you ask for help.'
        exit 1
    }

    Write-Host ' Ready. Opening the game...'
    Write-Host ''
    Write-Host ' Quick controls:  W A S D  walk      mouse  look around      N  first / third person'
    Write-Host '                  F11  graphics settings (turn things off if it is slow)      F12  3D on / off'
    Write-Host ''

    $clientArgs = @(
        '--connect', '--connect-address', '127.0.0.1',
        '--username', 'Visitor'
    )
    $client = Start-Process -FilePath $clientExe -ArgumentList $clientArgs -WorkingDirectory (Split-Path $clientExe) -PassThru
    $client.WaitForExit()
}
finally {
    if ($server -and -not $server.HasExited) {
        Write-Host ' Closing the private game server...'
        Stop-Process -Id $server.Id -Force -ErrorAction SilentlyContinue
    }
}
Write-Host ' Thanks for trying it!'
