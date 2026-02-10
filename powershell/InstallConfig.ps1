param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [bool]$logToDataDir = $false,
    [Parameter(Mandatory=$true, Position = 0)]
    [string]$listenScheme,
    [Parameter(Mandatory=$true, Position = 1)]
    [string]$listenHost,
    [Parameter(Mandatory=$true, Position = 2)]
    [int]$listenPort
)

. .\_Defaults.ps1


& $dnprotoPath /command InstallConfig /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /listenScheme $listenScheme /listenHost $listenHost /listenPort $listenPort