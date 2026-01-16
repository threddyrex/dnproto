param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [string]$password = "",
    [string]$authFactorToken = ""
)

. .\_Defaults.ps1


$command = "/command createsession /dataDir $dataDir /logLevel $logLevel /logToDataDir $logToDataDir /actor $actor"

if(-not [string]::IsNullOrWhiteSpace($password))
{
    $command += " /password $password"
}

if(-not [string]::IsNullOrWhiteSpace($authFactorToken))
{
    $command += " /authFactorToken $authFactorToken"
}

& $dnprotoPath $command.Split(' ')
