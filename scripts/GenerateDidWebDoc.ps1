param (
    [string]$dnprotoPath = $null,
    [string]$logLevel = $null,
    [string]$dataDir = $null,
    [Parameter(Mandatory = $true, Position = 0)]
    [string]$did,
    [Parameter(Mandatory = $true, Position = 1)]
    [string]$handle,
    [Parameter(Mandatory = $true, Position = 2)]
    [string]$publicKeyMultibase,
    [Parameter(Mandatory = $true, Position = 3)]
    [string]$pds
)

. .\_Defaults.ps1

& $dnprotoPath /command GenerateDidWebDoc /logLevel $logLevel /dataDir $dataDir /did "$did" /handle "$handle" /publicKeyMultibase "$publicKeyMultibase" /pds "$pds"
