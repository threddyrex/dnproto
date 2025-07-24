param(
    [Parameter(Mandatory = $true)]
    [string]$ScratchDirectory,
    [int]$IterationCount = 5,
    [int]$ItemCount = 15,
    [int]$SleepSeconds = 10
)


[int]$i = 0


while ($i -lt $IterationCount) {
    Write-Host "Iteration $i of $IterationCount"
    
    # Create a unique file name based on the current iteration
    $fileName = $i.ToString("0000") + "_export_plc.txt"
    $filePath = Join-Path -Path $ScratchDirectory -ChildPath $fileName

    
    # Call the export command with the file path
    if($i -eq 0) 
    {
        # For the first iteration, we don't have a previous file, so we won't pass it
        Write-Host "i: $i    filePath: $filePath    ItemCount: $ItemCount    previousFile:"
        & "..\src\bin\Debug\net9.0\dnproto.exe" /command plcdir_export /count $ItemCount /outfile $filePath

    } else 
    {
        # For subsequent iterations, we pass the previous file as an argument
        $previousFilePath = Join-Path -Path $ScratchDirectory -ChildPath (($i - 1).ToString("0000") + "_export_plc.txt")
        Write-Host "i: $i    filePath: $filePath    ItemCount: $ItemCount    previousFile: $previousFilePath"
        & "..\src\bin\Debug\net9.0\dnproto.exe" /command plcdir_export /count $ItemCount /outfile $filePath /previousfile $previousFilePath
    }

    
    # Sleep for the specified duration before the next iteration
    Start-Sleep -Seconds $SleepSeconds
    
    $i++
}

