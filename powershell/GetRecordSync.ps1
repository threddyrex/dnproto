param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = "trace",
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$url
)

. .\_Defaults.ps1


& $dnprotoPath /command GetRecordSync /url $url /logLevel $logLevel /dataDir $dataDir /logToDataDir $logToDataDir
