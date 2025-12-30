param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false
)

. .\_Defaults.ps1


& $dnprotoPath /command InitializeDb /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir 
