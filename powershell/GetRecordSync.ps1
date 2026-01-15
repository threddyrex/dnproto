param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$key
)

. .\_Defaults.ps1


& $dnprotoPath /command GetRecordSync /actor $actor /key $key /logLevel $logLevel /dataDir $dataDir /logToDataDir $logToDataDir
