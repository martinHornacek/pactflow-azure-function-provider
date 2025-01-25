
# pactflow-azure-function-provider

This repository demonstrates how to write **PactFlow Consumer and Provider Tests** for an Azure Function using the .NET Isolated Worker model. It includes instructions on how to publish and verify pacts using **Azure DevOps** pipelines.

## Solution Structure

- **pactflow-azure-function-provider**: The Azure Function project implemented using the .NET Isolated Worker model.
- **SampleConsumerTests**: A test project containing consumer pact tests.
- **SampleProviderTests**: A test project containing provider pact tests and the Azure DevOps pipeline configuration.
- **Solution Items**:
  - Contains a sample consumer pact (`pactflow-azure-function-consumer-pactflow-azure-function-provider.json`).
- **Tools**:
  - `StartFunctionHost.ps1`: A PowerShell script to start the Azure Function on localhost and set required environment variables without relying on `local.settings.json`.
  - `pipeline.yml`: Azure DevOps pipeline configuration for publishing and verifying pacts using PactFlow tooling.

## Prerequisites

1. Install the following tools:
   - [.NET SDK](https://dotnet.microsoft.com/download)
   - [PactNet](https://github.com/pact-foundation/pact-net) library
   - [Pact CLI](https://github.com/pact-foundation/pact-ruby-standalone/releases)
   - Azure DevOps pipeline agent (if running locally).
2. A PactFlow account for publishing and verifying pacts.

---

## Writing Pact Tests

### 1. **Consumer Pact Tests**

Located in `SampleConsumerTests`, these tests define interactions expected by the consumer application. Example:

```csharp
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
```

### 2. **Provider Pact Tests**

Located in `SampleProviderTests`, these tests verify that the provider (Azure Function) satisfies the pacts. Example:

```csharp
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
```

---

## Running the Azure Function Locally

Use the `StartFunctionHost.ps1` script in the `Tools` folder to start the Azure Function locally and set required environment variables:

```powershell
.\StartFunctionHost.ps1
```

This script:
1. Sets environment variables required for the function (instead of `local.settings.json`).
2. Starts the Azure Function host on localhost.

---

## Azure DevOps Pipeline

The Azure DevOps pipeline (`pipeline.yml`) is located in the `Tools` folder of `SampleProviderTests`. The pipeline:

1. Publishes consumer pacts to PactFlow.
2. Verifies provider pacts using PactFlow tooling for .NET.

### Key Pipeline Steps:

- **Consumer Pact Publishing**:
  - Run consumer pact tests in `SampleConsumerTests`.
  - Publish the resulting pact file to the PactFlow broker.

- **Provider Pact Verification**:
  - Start the Azure Function host using `StartFunctionHost.ps1`.
  - Verify the pacts against the running provider instance.

### Example Pipeline Snippet:

```yaml
    - task: PowerShell@2
      inputs:
        targetType: 'filePath'
        filePath: "$(Agent.BuildDirectory)/s/source/repos/pactflow-azure-function-provider/SampleProviderTests/Tools/StartFunctionHost.ps1"
        arguments: "-FuncExePath 'C:/Program Files/Microsoft/Azure Functions Core Tools/in-proc8/func.exe' -AzureFunctionProjectFolder '$(Agent.BuildDirectory)/s/source/repos/pactflow-azure-function-provider/pactflow-azure-function-provider'"
      displayName: Start Azure function Host process

    - script:      
        .\pact-provider-verifier --provider-base-url=http://localhost:7071 --pact-broker-base-url=<BROKER URL> --provider "pactflow-azure-function-provider" --broker-token=<ACCESS TOKEN> --consumer-version-tag=1.0.0-$(Build.SourceVersion) --enable-pending=true --provider-version-branch=develop --include-wip-pacts-since=2025-01-01 --publish-verification-results=true --fail-if-no-pacts-found=true --provider-app-version=1.0.0-$(Build.SourceVersion) --verbose
      workingDirectory: '../../../../../pact/bin'
      displayName: Verify Consumer pacts and publish the results to Pact Broker
```

---

## Contributing

Feel free to contribute by opening issues or submitting pull requests. Ensure tests pass before submission.

---

## License

This project is licensed under the MIT License. See the [LICENSE](LICENSE) file for details.
