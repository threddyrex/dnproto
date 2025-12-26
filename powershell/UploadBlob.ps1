param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [bool]$logToDataDir = $false,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [string]$actor = $null,
    [string]$filePath
)

. .\_Defaults.ps1


& $dnprotoPath /command UploadBlob /actor $actor /filepath $filePath /logLevel $logLevel /logToDataDir $logToDataDir /dataDir $dataDir
