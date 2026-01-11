param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command ActivateAccount /actor $actor /logLevel $logLevel /dataDir $dataDir
