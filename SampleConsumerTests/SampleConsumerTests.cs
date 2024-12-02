using pactflow_azure_function_provider;
using pactflow_azure_function_provider.Models;
using PactNet;
using PactNet.Infrastructure.Outputters;
using PactNet.Output.Xunit;
using System.Net;
using Xunit.Abstractions;

namespace SampleConsumerTests
{
    public class SampleConsumerTests
    {
        private readonly IPactBuilderV4 _pact;
        private readonly SampleDataModel _sampleData;

        public SampleConsumerTests(ITestOutputHelper output)
        {
            _sampleData = new SampleDataModel { Id = "27", Name = "burger", Description = "food" };

            PactConfig config = new PactConfig
            {
                PactDir = Path.Join("..", "..", "..", "..", "pacts"),
                Outputters = new List<IOutput> { new XunitOutput(output), new ConsoleOutput() },
                LogLevel = PactLogLevel.Debug
            };

            _pact = Pact.V4("pactflow-azure-function-consumer", "pactflow-azure-function-provider", config).WithHttpInteractions();
        }

        [Fact]
        public async Task RetrieveSampleData()
        {
            // Arrange
            string id = "27";

            _pact.UponReceiving("A request to get sample data")
                        .WithRequest(HttpMethod.Get, $"/sample/{id}")
                    .WillRespond()
                    .WithStatus(HttpStatusCode.OK)
                    .WithHeader("Content-Type", "application/json")
                    .WithJsonBody(_sampleData);

            await _pact.VerifyAsync(async ctx =>
            {
                // Act
                var consumer = new SampleDataClient();
                var result = await consumer.GetSampleData(ctx.MockServerUri.AbsoluteUri, id);

                // Assert
                Assert.NotNull(result);
                Assert.Equal("27", result.Id);
                Assert.Equal("burger", result.Name);
                Assert.Equal("food", result.Description);
            });
        }
    }
}
