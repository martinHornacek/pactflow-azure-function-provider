using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using PactNet.Verifier;
using StackExchange.Redis;
using Xunit.Abstractions;

namespace SampleProviderTests
{
    public class SampleProviderTests 
    {
        public const string ProviderName = "pactflow-example-provider-dotnet";
        public const string PactName = "pactflow-azure-function-consumer-pactflow-azure-function-provider.json";

        private readonly string _providerUri;
        private readonly string _pactFilePathName;
        private readonly ITestOutputHelper _outputHelper;

        public SampleProviderTests(ITestOutputHelper output)
        {
            _outputHelper = output;
            _providerUri = "http://localhost:7071";
            _pactFilePathName = Path.Join("..", "..", "..", "..", "pacts", PactName);
        }

        [Fact]
        public async Task EnsureProviderApiHonoursPactWithConsumer()
        {
            PactVerifierConfig config = new PactVerifierConfig
            {
                Outputters = new List<IOutput>
                {
                    new XunitOutput(_outputHelper),
                    new ConsoleOutput()
                },
                LogLevel = PactLogLevel.Debug
            };

            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureServices((context, services) =>
                {
                    IConnectionMultiplexer redis = Substitute.For<IConnectionMultiplexer>();
                    Container container = Substitute.ForPartsOf<Container>();
                    services.AddSingleton(redis);
                    services.AddSingleton(container);

                    services.AddFunctionsWorkerDefaults();
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();

                    services.Configure<GrpcChannelOptions>(options =>
                    {
                        options.Credentials = ChannelCredentials.Insecure;
                    });
                })
                .ConfigureAppConfiguration((context, config) =>
                {
                    var functionProjectPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "pactflow-azure-function-provider", "bin", "Debug", "net8.0"));

                    config.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["Functions:Worker:HostEndpoint"] = _providerUri,
                        ["Functions:Worker:Runtime"] = "dotnet-isolated",
                        ["Functions:Worker:WorkerId"] = Guid.NewGuid().ToString(),
                        ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
                        ["AzureWebJobsScriptRoot"] = functionProjectPath,
                    });
                })
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole();
                    logging.AddDebug();
                    logging.SetMinimumLevel(LogLevel.Debug);
                })
                .Build();

            using (host)
            {
                await host.StartAsync();
                IPactVerifier pactVerifier = new PactVerifier(ProviderName, config);

                pactVerifier.WithHttpEndpoint(new Uri(_providerUri))
                    .WithFileSource(new FileInfo(_pactFilePathName))
                    .Verify();
            }
        }
    }
}