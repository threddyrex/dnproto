param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null
)

. .\_Defaults.ps1

& $dnprotoPath /command GenerateJwtSecret /logLevel $logLevel /dataDir $dataDir