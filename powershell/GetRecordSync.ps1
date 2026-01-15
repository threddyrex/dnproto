param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$collection,
    [string]$rkey
)

. .\_Defaults.ps1


& $dnprotoPath /command GetRecordSync /actor $actor /collection $collection /rkey $rkey /logLevel $logLevel /dataDir $dataDir /logToDataDir $logToDataDir
