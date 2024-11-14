using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace pactflow_azure_function_provider
{
    public class Program
    {
        private static void Main(string[] args)
        {
            var host = new HostBuilder()
               .ConfigureFunctionsWebApplication()
               .ConfigureServices((context, services) =>
               {
                   services.AddApplicationInsightsTelemetryWorkerService();
                   services.ConfigureFunctionsApplicationInsights();

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
               })
               .Build();

            host.Run();
        }
    }
}