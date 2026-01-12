param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true, Position = 0)]
    [string]$uri
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command GetPost /uri $uri /logLevel $logLevel /dataDir $dataDir