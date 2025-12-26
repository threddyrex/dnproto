param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$keyType = "p256"
)

. .\_Defaults.ps1

& $dnprotoPath /command GenerateKeyPair /logLevel $logLevel /dataDir $dataDir /keytype $keyType
