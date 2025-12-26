param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [string]$stateJsonFile = "../atproto-scraping/state.json",
    [bool]$debugAttach = $false
)

. .\_Defaults.ps1

& $dnprotoPath /command GetDidWebRepos /stateJsonFile $stateJsonFile /dataDir $dataDir /logLevel $logLevel /debugAttach $debugAttach
