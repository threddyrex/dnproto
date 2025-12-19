param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$publicKey = $null,
    [string]$privateKey = $null,
    [string]$issuer = $null,
    [string]$audience = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command SignToken /dataDir $dataDir /publickey $publicKey /privatekey $privateKey /issuer $issuer /audience $audience /logLevel $logLevel
