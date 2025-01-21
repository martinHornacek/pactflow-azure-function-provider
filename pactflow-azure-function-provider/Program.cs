using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using pactflow_azure_function_provider.Models;
using StackExchange.Redis;

namespace pactflow_azure_function_provider
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureAppConfiguration((context, config) =>
                {
                    // Add local.settings.json
                    var localSettingsJsonFilePath = Path.Join("..", "..", "..", "..", "pactflow-azure-function-provider", "local.settings.json");
                    var fullPath = Path.GetFullPath(localSettingsJsonFilePath);
                    config.AddJsonFile(fullPath, optional: true, reloadOnChange: true);

                    // Add environment variables
                    config.AddEnvironmentVariables();
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddApplicationInsightsTelemetryWorkerService();
                    services.ConfigureFunctionsApplicationInsights();

                    // Get the environment (e.g., Development, Production)
                    string environment = Environment.GetEnvironmentVariable("ENVIRONMENT");

                    if (environment != null && environment.Equals("Pactflow", StringComparison.OrdinalIgnoreCase))
                    {
                        ConfigureServicesForPactflow(services);
                    }
                    else
                    {
                        ConfigureRealServices(services);
                    }
                })
                .Build();

            host.Run();
        }

        public static void ConfigureServicesForPactflow(IServiceCollection services)
        {
            string cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE_NAME");
            string cosmosContainerName = Environment.GetEnvironmentVariable("COSMOS_DB_CONTAINER_NAME");

            // Create the mocked CosmosClient
            var cosmosClient = Substitute.For<CosmosClient>();

            // Create mocked Database and Container
            var cosmosDatabase = Substitute.For<Database>();
            var cosmosContainer = Substitute.For<Container>();

            var sampleDataModel = new SampleDataModel { Id = "27", Description = "food", Name = "burger" };
            var mockResponse = Substitute.For<ItemResponse<SampleDataModel>>();
            mockResponse.Resource.Returns(sampleDataModel);

            cosmosContainer.ReadItemAsync<SampleDataModel>(Arg.Any<string>(), Arg.Any<PartitionKey>())
                .Returns(Task.FromResult(mockResponse));

            // Mock CosmosClient.GetDatabase to return the mocked Database
            cosmosClient.GetDatabase(Arg.Any<string>()).Returns(callInfo =>
            {
                string requestedDatabaseName = callInfo.Arg<string>();
                if (requestedDatabaseName == cosmosDatabaseName)
                {
                    return cosmosDatabase;
                }
                throw new ArgumentException("Unexpected database name: " + requestedDatabaseName);
            });

            // Mock Database.GetContainer to return the mocked Container
            cosmosDatabase.GetContainer(Arg.Any<string>()).Returns(callInfo =>
            {
                string requestedContainerName = callInfo.Arg<string>();
                if (requestedContainerName == cosmosContainerName)
                {
                    return cosmosContainer;
                }
                throw new ArgumentException("Unexpected container name: " + requestedContainerName);
            });

            // Register the mocks in the DI container
            services.AddSingleton(cosmosClient);
            services.AddSingleton(cosmosDatabase);
            services.AddSingleton(cosmosContainer);

            // Mock Redis
            var redis = Substitute.For<IConnectionMultiplexer>();
            services.AddSingleton(redis);
        }


        public static void ConfigureRealServices(IServiceCollection services)
        {
            // Read Cosmos DB settings from environment variables
            string cosmosConnectionString = Environment.GetEnvironmentVariable("COSMOS_DB_CONNECTION_STRING");
            string cosmosDatabaseName = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE_NAME");
            string cosmosContainerName = Environment.GetEnvironmentVariable("COSMOS_DB_CONTAINER_NAME");

            if (string.IsNullOrEmpty(cosmosConnectionString) || string.IsNullOrEmpty(cosmosDatabaseName) || string.IsNullOrEmpty(cosmosContainerName))
            {
                throw new InvalidOperationException("Cosmos DB environment variables are not set.");
            }

            // Register Cosmos DB
            var cosmosClient = new CosmosClient(cosmosConnectionString);
            var cosmosDatabase = cosmosClient.GetDatabase(cosmosDatabaseName);
            services.AddSingleton(cosmosDatabase);
            services.AddSingleton(sp => cosmosDatabase.GetContainer(cosmosContainerName));

            // Read Redis connection string from environment variables
            string redisConnectionString = Environment.GetEnvironmentVariable("REDIS_CONNECTION_STRING");
            if (string.IsNullOrEmpty(redisConnectionString))
            {
                throw new InvalidOperationException("Redis connection string is not set.");
            }

            // Register Redis
            services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisConnectionString));
        }
    }
}
