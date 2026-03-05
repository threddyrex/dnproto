param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$debugAttach = $false,
    [bool]$logToDataDir = $false,
    [string]$format = "tree"
)

. .\_Defaults.ps1

& $dnprotoPath /command PrintDbMst /dataDir $dataDir /logLevel $logLevel /format $format /debugattach $debugAttach /logtodatadir $logToDataDir
