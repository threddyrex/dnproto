param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [Parameter(Mandatory=$true, Position=0)]
    [string]$url
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command DeletePost /url $url /logLevel $logLevel /dataDir $dataDir /actor $actor