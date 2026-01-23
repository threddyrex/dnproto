param (
    [string]$dnprotoPath = $null,
    [string]$dataDir = $null,
    [Parameter(Position = 0)]
    [bool]$enabled = $true
)

. .\_Defaults.ps1


& $dnprotoPath /command EnableOauth /dataDir $dataDir /enabled $enabled