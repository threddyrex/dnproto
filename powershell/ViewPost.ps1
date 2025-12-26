param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true)]
    [string]$uri
)

. .\_Defaults.ps1


& $dnprotoPath /command ViewPost /uri $uri /logLevel $logLevel
