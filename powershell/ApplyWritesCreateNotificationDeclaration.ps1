param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null
)

. .\_Defaults.ps1

& $dnprotoPath /command ApplyWritesCreateNotificationDeclaration /actor $actor /dataDir $dataDir /logLevel $logLevel