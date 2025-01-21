# Function to stop all instances of the Azure Function Host
function Stop-AllFunctionHosts {
    param (
        [string]$ProcessName = "func"
    )

    Write-Host "Stopping all instances of process: $ProcessName" -ForegroundColor Yellow

    try {
        # Get all processes with the specified name
        $processes = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue

        if ($null -eq $processes) {
            Write-Host "No running processes found with name: $ProcessName" -ForegroundColor Green
            return
        }

        # Stop each process
        foreach ($process in $processes) {
            Write-Host "Stopping process ID: $($process.Id), Name: $($process.Name)" -ForegroundColor Cyan
            Stop-Process -Id $process.Id -Force -ErrorAction Stop
        }

        Write-Host "All instances of process '$ProcessName' have been stopped." -ForegroundColor Green
    }
    catch {
        Write-Error "Error stopping processes: $_"
        throw
    }
}

# Call the function
Stop-AllFunctionHosts -ProcessName "func"