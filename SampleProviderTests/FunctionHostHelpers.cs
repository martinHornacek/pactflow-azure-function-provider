using System.Collections.Concurrent;
using System.Diagnostics;

namespace SampleProviderTests
{
    public static class FunctionHostHelpers
    {
        public const string FuncExeFilePath = @"C:\Program Files\Microsoft\Azure Functions Core Tools\in-proc8\func.exe";
        public const int DefaultPort = 7071;
        public const int DefaultStartupTimeoutSeconds = 120;

        public static async Task<Process> StartFunctionHostAsync(
            string azureFunctionProjectFolder,
            int port = DefaultPort,
            int timeoutSeconds = DefaultStartupTimeoutSeconds)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(azureFunctionProjectFolder))
                throw new ArgumentException("Azure Function project folder path cannot be empty.", nameof(azureFunctionProjectFolder));

            // Use CancellationTokenSource for more precise timeout handling
            using var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            Process process = null;
            string originalDirectory = Environment.CurrentDirectory;

            try
            {
                // Use Path.GetFullPath to validate and normalize the folder path
                azureFunctionProjectFolder = Path.GetFullPath(azureFunctionProjectFolder);

                // Change current directory
                Environment.CurrentDirectory = azureFunctionProjectFolder;

                var startInfo = new ProcessStartInfo
                {
                    FileName = FuncExeFilePath,
                    Arguments = $"host start --port {port}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                process = new Process
                {
                    StartInfo = startInfo
                };

                // Use a thread-safe collection for logging
                var outputLines = new ConcurrentQueue<string>();
                var errorLines = new ConcurrentQueue<string>();


                process.OutputDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        outputLines.Enqueue(e.Data);
                        Console.WriteLine(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                    {
                        errorLines.Enqueue(e.Data);
                        Console.Error.WriteLine(e.Data);
                    }
                };

                // Start the process
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for the function host to be ready
                await WaitForFunctionHostReadyAsync(process, cancellationTokenSource.Token);

                return process;
            }
            catch (OperationCanceledException)
            {
                process?.Kill();
                throw new TimeoutException($"Azure Function host did not start within {timeoutSeconds} seconds.");
            }
            catch (Exception)
            {
                process?.Kill();
                throw;
            }
            finally
            {
                // Ensure we always restore the original directory
                Environment.CurrentDirectory = originalDirectory;
            }
        }

        private static async Task WaitForFunctionHostReadyAsync(Process process, CancellationToken cancellationToken)
        {
            var readySignal = new TaskCompletionSource<bool>();

            // Listen for startup completion indicators
            void OutputHandler(object sender, DataReceivedEventArgs e)
            {
                if (e.Data?.Contains("Worker process started and initialized", StringComparison.OrdinalIgnoreCase) == true ||
                    e.Data?.Contains("Host started successfully", StringComparison.OrdinalIgnoreCase) == true)
                {
                    readySignal.TrySetResult(true);
                }
            }

            process.OutputDataReceived += OutputHandler;

            try
            {
                // Wait for either the ready signal or cancellation
                await Task.WhenAny(
                    readySignal.Task,
                    Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                );

                // Throw if cancelled
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                process.OutputDataReceived -= OutputHandler;
            }
        }

        // Optional: Method to gracefully stop the function host
        public static void StopFunctionHost(Process process)
        {
            if (process == null || process.HasExited)
                return;

            try
            {
                process.Kill();
                process.WaitForExit(5000); // Wait up to 5 seconds for process to exit
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error stopping Function Host: {ex.Message}");
            }
        }
    }
}
