param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [Parameter(Mandatory=$true)]
    [string]$password = "",
    [string]$authFactorToken = ""
)

. .\_Defaults.ps1

if([string]::isnullorempty($authFactorToken))
{
    & $dnprotoPath /command CreateSession /actor $actor /password $password /logLevel $logLevel /dataDir $dataDir
}
else
{
    & $dnprotoPath /command CreateSession /actor $actor /password $password /logLevel $logLevel /dataDir $dataDir /authFactorToken $authFactorToken 
}
