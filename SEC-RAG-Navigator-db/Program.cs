using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Data;
using Microsoft.Extensions.Options;



class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        using IHost host = CreateHostBuilder(args).Build();

        var navigatorService = host.Services.GetRequiredService<SEC_RAG_NavigatorService>();
        await navigatorService.ExecuteAsync(args);
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SEC-RAG-Navigator create-container <containerName>");
        Console.WriteLine("  SEC-RAG-Navigator list-containers");
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Add logging
                services.AddLogging(configure => configure.AddConsole());

                // Register CosmosClient
                services.AddSingleton<CosmosClient>(provider =>
                {
                    string endpointUri = GetEnvironmentVariable("COSMOS_DB_ENDPOINT");
                    string primaryKey = GetEnvironmentVariable("COSMOS_DB_PRIMARY_KEY");

                    return new CosmosClientBuilder(endpointUri, primaryKey)
                        .WithApplicationName("SEC-RAG-Navigator")
                        .Build();
                });

                // Register SEC_RAG_NavigatorService
                services.AddScoped<SEC_RAG_NavigatorService>();

                // Register SemanticKernelService
                RegisterSemanticKernelService(services);

                // Register CosmosDbService
                services.AddScoped<CosmosDbService>(provider =>
                {
                    string databaseId = GetEnvironmentVariable("COSMOS_DB_DATABASE_ID");
                    var logger = provider.GetRequiredService<ILogger<CosmosDbService>>();
                    var cosmosClient = provider.GetRequiredService<CosmosClient>();
                    return new CosmosDbService(cosmosClient, databaseId, logger);
                });

                // Register Semantic Kernel and related services
                RegisterKernelServices(context, services);
            });
    static void RegisterSemanticKernelService(IServiceCollection services)
    {
        // Fetch environment variables for Semantic Kernel
        string endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string apiKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");
        string completionDeploymentName = GetEnvironmentVariable("AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME") ?? "gpt-4o";
        string embeddingDeploymentName = GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME") ?? "text-embedding-3-large";
        int dimensions = 3072;

        // Register SemanticKernelService
        services.AddScoped<SemanticKernelService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SemanticKernelService>>();

            return new SemanticKernelService(
                endpoint: endpoint,
                completionDeploymentName: completionDeploymentName,
                embeddingDeploymentName: embeddingDeploymentName,
                apiKey: apiKey,
                dimensions: dimensions,
                logger: logger
            );
        });
    }
    static void RegisterKernelServices(HostBuilderContext context, IServiceCollection services)
    {
        string azureOpenAIChatDeploymentName = "gpt-4o";
        string azureEmbeddingDeploymentName = "text-embedding-3-large";
        string azureOpenAIEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string azureOpenAIKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");
        string azureCosmosDBNoSQLConnectionString = GetEnvironmentVariable("COSMOS_DB_CONNECTION_STRING");
        string azureCosmosDBNoSQLDatabaseName = GetEnvironmentVariable("COSMOS_DB_DATABASE_ID");
        string ragCollectionName = "ragcontent";
        int azureEmbeddingDimensions = 3072;

        var kernelBuilder = services.AddKernel();

        kernelBuilder.AddAzureOpenAIChatCompletion(
            azureOpenAIChatDeploymentName,
            azureOpenAIEndpoint,
            azureOpenAIKey);

        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            azureEmbeddingDeploymentName,
            azureOpenAIEndpoint,
            azureOpenAIKey,
            dimensions: azureEmbeddingDimensions);

        kernelBuilder.AddAzureCosmosDBNoSQLVectorStoreRecordCollection<TextSnippet<string>>(
            ragCollectionName,
            azureCosmosDBNoSQLConnectionString,
            azureCosmosDBNoSQLDatabaseName);

        RegisterServices<string>(services, kernelBuilder);

        services.AddScoped<ChatService>((provider) =>
        {
            var cosmosDbService = provider.GetRequiredService<CosmosDbService>();
            var semanticKernelService = provider.GetRequiredService<SemanticKernelService>();

            var logger = provider.GetRequiredService<ILogger<ChatService>>();

            return new ChatService(
                cosmosDbService: cosmosDbService,
                semanticKernelService: semanticKernelService,
                maxConversationTokens: "4000",
                cacheSimilarityScore: "0.5",
                logger: logger
            );
        });
    }

    static void RegisterServices<TKey>(IServiceCollection services, IKernelBuilder kernelBuilder)
        where TKey : notnull
    {
        kernelBuilder.AddVectorStoreTextSearch<TextSnippet<TKey>>(
            new TextSearchStringMapper((result) =>
            {
                if (result is TextSnippet<TKey> castResult)
                {
                    return castResult.Text ?? string.Empty;
                }
                throw new InvalidCastException("Result is not of type TextSnippet<TKey>.");
            }),
            new TextSearchResultMapper((result) =>
            {
                if (result is TextSnippet<TKey> castResult)
                {
                    return new TextSearchResult(value: castResult.Text ?? string.Empty)
                    {
                        Name = castResult.ReferenceDescription ?? "No Description",
                        Link = castResult.ReferenceLink ?? "No Link"
                    };
                }
                throw new InvalidCastException("Result is not of type TextSnippet<TKey>.");
            }));

        services.AddSingleton<IDataLoader, DataLoader<TKey>>();
    }

    static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName)
            ?? throw new ArgumentNullException(variableName, $"{variableName} is not set in environment variables.");
    }
}

public class SEC_RAG_NavigatorService
{
    private readonly CosmosDbService _cosmosDbService;
    private readonly ILogger<SEC_RAG_NavigatorService> _logger;

    public SEC_RAG_NavigatorService(CosmosDbService cosmosDbService, ILogger<SEC_RAG_NavigatorService> logger)
    {
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ExecuteAsync(string[] args)
    {
        string command = args[0].ToLower();

        try
        {
            if (command == "create-container" && args.Length >= 2)
            {
                string containerName = args[1];
                List<string> partitionKeyPaths = new List<string> { "/tenantId", "/userId", "/categoryId" };
                await _cosmosDbService.CreateDatabaseAsync();
                await _cosmosDbService.CreateContainerAsync(
                    containerName,
                    "/vectors",
                    partitionKeyPaths,
                    new List<string> { "/*" },
                    3072
                );
            }
            else if (command == "list-containers")
            {
                await _cosmosDbService.ListDatabasesAndContainersAsync();
            }
            else
            {
                Console.WriteLine("Invalid command or missing arguments. Use one of the following:");
                PrintUsage();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while executing SEC-RAG-Navigator.");
            Console.WriteLine($"\nError: {ex.Message}");
        }
    }

    private void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SEC-RAG-Navigator create-container <containerName>");
        Console.WriteLine("  SEC-RAG-Navigator list-containers");
    }
}

public class CosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseId;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(CosmosClient cosmosClient, string databaseId, ILogger<CosmosDbService> logger)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _databaseId = databaseId ?? throw new ArgumentNullException(nameof(databaseId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task CreateDatabaseAsync()
    {
        Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
        _logger.LogInformation($"Database created or exists: {database.Id}");
    }

    public async Task CreateContainerAsync(
        string containerName,
        string vectorPath,
        List<string> partitionKeyPaths,
        List<string> includedPaths,
        int dimensions)
    {
        _logger.LogInformation($"Creating container '{containerName}' with partition key paths: {string.Join(", ", partitionKeyPaths)}");

        Database database = _cosmosClient.GetDatabase(_databaseId);

        Collection<Microsoft.Azure.Cosmos.Embedding> embeddings = new Collection<Microsoft.Azure.Cosmos.Embedding>()
            {
                new Microsoft.Azure.Cosmos.Embedding()
                {
                    Path = vectorPath,
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = dimensions
                }
            };

        ContainerProperties containerProperties = new ContainerProperties(
            id: containerName,
            partitionKeyPaths: partitionKeyPaths
        )
        {
            VectorEmbeddingPolicy = new VectorEmbeddingPolicy(embeddings),
            IndexingPolicy = new IndexingPolicy()
            {
                VectorIndexes = new Collection<VectorIndexPath>()
                    {
                        new VectorIndexPath()
                        {
                            Path = vectorPath,
                            Type = VectorIndexType.DiskANN,
                        }
                    }
            }
        };

        foreach (string path in includedPaths)
        {
            containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = path });
        }

        containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"_etag\"/?" });

        Container container = await database.CreateContainerIfNotExistsAsync(containerProperties);

        _logger.LogInformation($"Container created or exists: {container.Id}");
    }

    public async Task ListDatabasesAndContainersAsync()
    {
        _logger.LogInformation("Fetching databases and containers...");

        using FeedIterator<DatabaseProperties> databaseIterator = _cosmosClient.GetDatabaseQueryIterator<DatabaseProperties>();
        while (databaseIterator.HasMoreResults)
        {
            FeedResponse<DatabaseProperties> databases = await databaseIterator.ReadNextAsync();

            foreach (var database in databases)
            {
                Console.WriteLine($"Database ID: {database.Id}");

                Database db = _cosmosClient.GetDatabase(database.Id);

                using FeedIterator<ContainerProperties> containerIterator = db.GetContainerQueryIterator<ContainerProperties>();
                while (containerIterator.HasMoreResults)
                {
                    FeedResponse<ContainerProperties> containers = await containerIterator.ReadNextAsync();
                    foreach (var container in containers)
                    {
                        Console.WriteLine($"\tContainer ID: {container.Id}");
                        Console.WriteLine($"\t  Partition Key Paths: {string.Join(", ", container.PartitionKeyPaths)}");
                        Console.WriteLine($"\t  Default Time to Live (TTL): {container.DefaultTimeToLive}");
                    }
                }
            }
        }

        _logger.LogInformation("Finished fetching database and container information.");
    }
}

