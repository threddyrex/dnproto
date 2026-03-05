param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$text = "Hello from TestApplyWritesAndLogFirehose",
    [bool]$logToDataDir = $true
)

. .\_Defaults.ps1

& $dnprotoPath /command TestApplyWritesAndLogFirehose /dataDir $dataDir /logLevel $logLevel /text $text /logtodatadir $logToDataDir
