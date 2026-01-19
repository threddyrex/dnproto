param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [bool]$all = $true
)

. .\_Defaults.ps1


# call dnproto.exe to get actor info
& $dnprotoPath /command ResolveActorInfo /actor $actor /logLevel $logLevel /logToDataDir $logToDataDir /dataDir $dataDir /all $all
