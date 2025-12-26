param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$actor = $null,
    [Parameter(Mandatory=$true)]
    [string]$month = "",
    [bool]$deleteLikes = $false,
    [bool]$deleteReposts = $false,
    [bool]$deletePosts = $false
)

. .\_Defaults.ps1

& $dnprotoPath /command DeleteActivityForMonth /actor $actor /logLevel $logLevel /month $month /dataDir $dataDir /deleteLikes $deleteLikes /deleteReposts $deleteReposts /deletePosts $deletePosts
