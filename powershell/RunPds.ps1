param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $true
)

. .\_Defaults.ps1


& $dnprotoPath /command RunPds /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir
