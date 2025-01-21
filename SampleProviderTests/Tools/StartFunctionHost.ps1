param(
    [string]$FuncExePath,
    [string]$AzureFunctionProjectFolder,
    [int]$Port = 7071,
    [int]$TimeoutSeconds = 120
)

function Start-FunctionHost {
    param(
        [string]$FuncExeFilePath,
        [string]$ProjectFolder,
        [int]$Port,
        [int]$TimeoutSeconds
    )

    # Ensure the function exe exists
    if (-not (Test-Path $FuncExeFilePath)) {
        throw "Azure Function executable not found at path: $FuncExeFilePath"
    }

    # Ensure the project folder exists and get its full path
    if (-not (Test-Path $ProjectFolder)) {
        throw "Azure Function project folder path cannot be empty or invalid: $ProjectFolder"
    }

    $FullProjectPath = (Get-Item $ProjectFolder).FullName
    Write-Host "Setting working directory to: $FullProjectPath" -ForegroundColor Yellow

    # Store current location
    $OriginalDirectory = $PWD.Path

    # Kill running func.exe processes
    Stop-AllFuncProcesses

    $process = $null

    try {
        # Change to project directory and verify
        Set-Location -Path $FullProjectPath -ErrorAction Stop
        $currentPath = $PWD.Path
        if ($currentPath -ne $FullProjectPath) {
            throw "Failed to set working directory. Current: $currentPath, Expected: $FullProjectPath"
        }

        Write-Host "Current working directory: $PWD" -ForegroundColor Yellow

        # Build the command line arguments
        $Arguments = "host start --dotnet-isolated --port $Port --no-build --prefix bin/Release/net8.0"

        $env:ENVIRONMENT = "Pactflow"
		$env:FUNCTIONS_WORKER_RUNTIME = "dotnet-isolated"
        $env:COSMOS_DB_DATABASE_NAME = "db"
        $env:COSMOS_DB_CONTAINER_NAME = "Test"

        # Create process start info
        $startInfo = New-Object System.Diagnostics.ProcessStartInfo
        $startInfo.FileName = $FuncExeFilePath
        $startInfo.Arguments = $Arguments
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        $startInfo.CreateNoWindow = $true
        $startInfo.WorkingDirectory = $FullProjectPath

        # Create the process
        $process = New-Object System.Diagnostics.Process
        $process.StartInfo = $startInfo

        # Set up output handling using script blocks
        $outputSb = {
            if (-not [String]::IsNullOrEmpty($EventArgs.Data)) {
                Write-Host $EventArgs.Data
            }
        }

        $errorSb = {
            if (-not [String]::IsNullOrEmpty($EventArgs.Data)) {
                Write-Host $EventArgs.Data -ForegroundColor Red
            }
        }

        # Register the event handlers
        Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action $outputSb | Out-Null
        Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action $errorSb | Out-Null

        # Start the process
        Write-Host "Starting process with working directory: $($startInfo.WorkingDirectory)" -ForegroundColor Yellow
        $started = $process.Start()
        if (-not $started) {
            throw "Failed to start the Function Host process"
        }

        # Begin async reading
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()

        # Wait for host to be ready
        Wait-FunctionHostReady -Process $process -TimeoutSeconds $TimeoutSeconds
    }
    catch {
        if ($null -ne $process) {
            if (-not $process.HasExited) {
                try {
                    if ($null -ne $process.Id) {
                        Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
                    }
                }
                catch {
                    Write-Warning ("Error while cleaning up process: {0}" -f $_.Exception.Message)
                }
            }
            $process.Dispose()
        }

        # Clean up any registered events
        Get-EventSubscriber | Unregister-Event

        # Make sure we return to original directory even if there's an error
        Set-Location -Path $OriginalDirectory
        throw $_.Exception.Message
    }
    finally {
        Set-Location -Path $OriginalDirectory
        Write-Host "Restored working directory to: $OriginalDirectory" -ForegroundColor Yellow
    }

    if ($null -eq $process -or $null -eq $process.Id) {
        throw "Failed to start Function Host process properly"
    }

    return $process
}

function Wait-FunctionHostReady {
    param(
        [System.Diagnostics.Process]$Process,
        [int]$TimeoutSeconds
    )

    if ($null -eq $Process) {
        throw "Process object is null"
    }

    $startTime = Get-Date
    $ready = $false

    while (-not $ready) {
        Start-Sleep -Seconds 1

        $elapsed = (Get-Date) - $startTime
        if ($elapsed.TotalSeconds -ge $TimeoutSeconds) {
            throw "Azure Function host did not start within $TimeoutSeconds seconds."
        }

        if ($Process.HasExited) {
            throw "The function host process exited unexpectedly."
        }

        # Check if the process is responding
        if (-not $Process.HasExited -and $Process.Responding) {
            $ready = $true
            Write-Host "Function host is ready." -ForegroundColor Green
        }
    }
}

function Stop-AllFuncProcesses {
    Write-Host "Stopping all instances of func.exe..." -ForegroundColor Yellow

    try {
        $funcProcesses = Get-Process -Name "func" -ErrorAction SilentlyContinue
        if ($funcProcesses) {
            foreach ($proc in $funcProcesses) {
                if ($null -ne $proc -and $null -ne $proc.Id) {
                    Write-Warning "Stopping func.exe process with PID $($proc.Id)"
                    Stop-Process -Id $proc.Id -Force -ErrorAction Stop
                    Write-Host "Successfully stopped func.exe process with PID $($proc.Id)" -ForegroundColor Green
                }
            }
        }
        else {
            Write-Host "No func.exe processes found running" -ForegroundColor Green
        }
    }
    catch {
        Write-Warning ("Error while stopping func.exe processes: {0}" -f $_.Exception.Message)
    }
}

# Main execution
try {
    $Process = Start-FunctionHost -FuncExeFilePath $FuncExePath -ProjectFolder $AzureFunctionProjectFolder -Port $Port -TimeoutSeconds $TimeoutSeconds
    if ($null -ne $Process -and $null -ne $Process.Id) {
        Write-Host "Function host started. Process ID: $($Process.Id)" -ForegroundColor Green
    }
    else {
        throw "Failed to start Function Host process"
    }
}
catch {
    Write-Error $_.Exception.Message
    exit 1
}