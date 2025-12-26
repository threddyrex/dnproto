param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true)]
    [string]$uri
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command DeletePost /uri $uri /logLevel $logLevel /dataDir $dataDir
