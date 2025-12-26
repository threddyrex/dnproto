param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$cid
)

. .\_Defaults.ps1


& $dnprotoPath /command GetBlob /actor $actor /cid $cid /logLevel $logLevel /logToDataDir $logToDataDir /dataDir $dataDir
