param (
    [string]$dnprotoPath = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory=$true, Position = 0)]
    [string]$newLevel
)

. .\_Defaults.ps1


& $dnprotoPath /command SetLogLevel /dataDir $dataDir /newLevel $newLevel