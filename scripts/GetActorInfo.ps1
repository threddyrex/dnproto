param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null
)

. .\_Defaults.ps1


# call dnproto.exe to get actor info
& $dnprotoPath /command GetActorInfo /actor $actor /logLevel $logLevel
