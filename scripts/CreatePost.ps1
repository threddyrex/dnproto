param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [Parameter(Position = 0, Mandatory = $true)]
    [string]$text = $null
)

. .\_Defaults.ps1

# call dnproto.exe to get handle info
& $dnprotoPath /command CreatePost /actor $actor /dataDir $dataDir /logLevel $logLevel /text $text