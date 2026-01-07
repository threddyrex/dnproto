param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [bool]$deleteExistingDb = $false
)

. .\_Defaults.ps1


& $dnprotoPath /command InstallDb /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /deleteExistingDb $deleteExistingDb 
