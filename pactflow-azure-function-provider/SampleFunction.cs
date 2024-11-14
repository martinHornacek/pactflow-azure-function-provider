using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using pactflow_azure_function_provider.Models;
using StackExchange.Redis;
using System.Net;
using System.Text.Json;

namespace pactflow_azure_function_provider
{
    public class SampleFunction
    {
        private readonly Container _cosmosContainer;
        private readonly IConnectionMultiplexer _redis;

        public SampleFunction(Container cosmosContainer, IConnectionMultiplexer redis)
        {
            _cosmosContainer = cosmosContainer;
            _redis = redis;
        }

        [Function("GetSampleData")]
        public async Task<HttpResponseData> GetSampleData(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "sample/{id}")] HttpRequestData req,
            string id)
        {
            var response = req.CreateResponse(HttpStatusCode.OK);
            response.Headers.Add("Content-Type", "application/json");

            var redisDb = _redis.GetDatabase();
            var cachedData = await redisDb.StringGetAsync(id);

            if (!cachedData.IsNullOrEmpty)
            {
                await response.WriteStringAsync(cachedData);
                return response;
            }

            var cosmosResponse = await _cosmosContainer.ReadItemAsync<SampleDataModel>(id, new PartitionKey(id));
            var resource = cosmosResponse.Resource;

            var item = JsonSerializer.Serialize(resource);
            await redisDb.StringSetAsync(id, item);

            await response.WriteStringAsync(item);
            return response;
        }
    }
}
