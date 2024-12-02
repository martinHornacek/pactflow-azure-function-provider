using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using PactNet.Verifier;
using System.Diagnostics;
using Xunit.Abstractions;

namespace SampleProviderTests
{
    public class SampleProviderTests : IDisposable
    {
        public const string ProviderName = "pactflow-example-provider-dotnet";
        public const string PactName = "pactflow-azure-function-consumer-pactflow-azure-function-provider.json";

        public static readonly string SolutionRootFolder = Path.Join("..", "..", "..", "..", "pactflow-azure-function-provider");

        private readonly Uri _providerUri;
        private readonly string _pactFilePathName;
        private readonly ITestOutputHelper _outputHelper;
        private Process _process;
        private bool disposedValue;

        public SampleProviderTests(ITestOutputHelper output)
        {
            _outputHelper = output;
            _providerUri = new Uri("http://localhost:7071/api");
            _pactFilePathName = Path.Join("..", "..", "..", "..", "pacts", PactName);
        }

        [Fact]
        public async Task EnsureProviderApiHonoursPactWithConsumer()
        {
            _process = await FunctionHostHelpers.StartFunctionHostAsync(SolutionRootFolder);

            PactVerifierConfig config = new PactVerifierConfig
            {
                Outputters = new List<IOutput>
                {
                    new XunitOutput(_outputHelper),
                    new ConsoleOutput()
                },
                LogLevel = PactLogLevel.Debug
            };
            IPactVerifier pactVerifier = new PactVerifier(ProviderName, config);

            pactVerifier.WithHttpEndpoint(_providerUri)
                .WithFileSource(new FileInfo(_pactFilePathName))
                .Verify();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_process != null && !_process.HasExited)
                    {
                        _process.Kill();
                        _process.WaitForExit();
                    }

                    _process?.Dispose();
                }
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}