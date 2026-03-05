param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = "info",
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [string]$format = "dagcbor",
    [Parameter(Mandatory=$true, Position = 0)]
    [string]$collection,
    [Parameter(Mandatory=$true, Position = 1)]
    [string]$rkey
)

. .\_Defaults.ps1


& $dnprotoPath /command SyncGetRecordLocal /collection $collection /rkey $rkey /format $format /logLevel $logLevel /dataDir $dataDir /logToDataDir $logToDataDir
