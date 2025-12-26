param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$filepath = $null
)

. .\_Defaults.ps1


& $dnprotoPath /command GenerateBlobCid /filepath $filepath /logLevel $logLevel /logToDataDir $logToDataDir /dataDir $dataDir
