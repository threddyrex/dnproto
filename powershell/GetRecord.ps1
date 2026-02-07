param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true, Position=0)]
    [string]$uri
)

. .\_Defaults.ps1


& $dnprotoPath /command GetRecord /uri $uri /logLevel $logLevel /dataDir $dataDir /logToDataDir $logToDataDir
