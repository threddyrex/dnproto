param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$port = "5001"
)

. .\_Defaults.ps1


& $dnprotoPath /command RunPds /port $port /logLevel $logLevel
