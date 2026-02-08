param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [string]$listenScheme = $null,
    [string]$listenHost = $null,
    [int]$listenPort = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command InstallServerConfig /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /listenScheme $listenScheme /listenHost $listenHost /listenPort $listenPort