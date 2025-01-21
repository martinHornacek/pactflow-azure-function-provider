using pactflow_azure_function_provider.Models;
using System.Text.Json;

namespace pactflow_azure_function_provider
{
    public class SampleDataClient
    {
        public async Task<SampleDataModel> GetSampleData(string baseUrl, string id, HttpClient? httpClient = null)
        {
            using var client = httpClient == null ? new HttpClient() : httpClient;

            var response = await client.GetAsync(baseUrl + $"sample/{id}");
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadAsStringAsync();

            if (result == null)
            {
                return new SampleDataModel();
            }

            return JsonSerializer.Deserialize<SampleDataModel>(result);
        }
    }
}
