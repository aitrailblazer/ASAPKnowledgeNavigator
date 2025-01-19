using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static System.Environment;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.Prompty;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.Options;
using Cosmos.Copilot.Models;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Microsoft.ML.Tokenizers;
using System;
using Azure.AI.Inference;
using Azure.Core;
using System.Text.Json;
using Newtonsoft.Json;
using BlingFire;
using System.Linq;
using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using PuppeteerSharp;
using System.IO;
using Microsoft.Playwright;

class Program
{

    // The Azure Cosmos DB client instance
    private CosmosClient cosmosClient = null!;
    // The Azure Cosmos DB database instance
    private Database database = null!;
    // The Azure Cosmos DB container instance
    private Container container = null!;

    // 
    /// <summary>
    /// The main entry point for the application.
    /// </summary> 
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
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
        Console.WriteLine("  SEC-RAG-Navigator create-container-edgar <containerName>");
        Console.WriteLine("  SEC-RAG-Navigator list-containers");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-cohere");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-cohere-edgar");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-delete");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-delete-edgar");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-search-edgar \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-rerank-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator phi-4");
        Console.WriteLine("  SEC-RAG-Navigator phi-4-streaming");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat-streaming");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat-streaming-http");
        Console.WriteLine("  SEC-RAG-Navigator cohere-embed-dbupsert");
        Console.WriteLine("  SEC-RAG-Navigator vector-store");



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
                    string azureCosmosDbEndpointUri = GetEnvironmentVariable("COSMOS_DB_ENDPOINT");
                    string primaryKey = GetEnvironmentVariable("COSMOS_DB_PRIMARY_KEY");

                    return new CosmosClientBuilder(azureCosmosDbEndpointUri, primaryKey)
                        .WithApplicationName("SEC-RAG-Navigator")
                        .Build();
                });

                // Register SEC_RAG_NavigatorService
                services.AddScoped<SEC_RAG_NavigatorService>();
                services.AddScoped<CosmosDbServiceWorking>(provider =>
                {
                    // Resolve dependencies
                    var cosmosClient = provider.GetRequiredService<CosmosClient>();
                    string databaseId = GetEnvironmentVariable("COSMOS_DB_DATABASE_ID");
                    var logger = provider.GetRequiredService<ILogger<CosmosDbServiceWorking>>();
                    var ragChatService = provider.GetRequiredService<RAGChatService<string>>(); // Resolve RAGChatService
                    var chatService = provider.GetRequiredService<ChatService>(); // Resolve ChatService
                    var cosmosDbService = provider.GetRequiredService<CosmosDbService>(); // Resolve ChatService

                    // Return an instance of CosmosDbServiceWorking with all dependencies
                    return new CosmosDbServiceWorking(cosmosClient, databaseId, logger, ragChatService, chatService, cosmosDbService);
                });


                // Register SemanticKernelService
                RegisterSemanticKernelService(services);

                // Register CosmosDbService
                services.AddSingleton<CosmosDbService>((provider) =>
                {
                    var cosmosDbOptions = provider.GetRequiredService<IOptions<CosmosDb>>();
                    if (cosmosDbOptions is null)
                    {
                        throw new ArgumentException($"{nameof(IOptions<CosmosDb>)} was not resolved through dependency injection.");
                    }
                    else
                    {
                        var logger = provider.GetRequiredService<ILogger<CosmosDbService>>(); // Retrieve the logger
                        string azureCosmosDbEndpointUri = GetEnvironmentVariable("COSMOS_DB_ENDPOINT");
                        string AzureCosmosDBNoSQLDatabaseName = "asapdb";
                        string knowledgeBaseContainerName = "secrag"; // rag
                        string knowledgeBaseContainerName2 = "edgar"; // rag

                        return new CosmosDbService(
                            endpoint: azureCosmosDbEndpointUri ?? string.Empty,
                            databaseName: AzureCosmosDBNoSQLDatabaseName ?? string.Empty,
                            knowledgeBaseContainerName: knowledgeBaseContainerName ?? string.Empty,
                            knowledgeBaseContainerName2: knowledgeBaseContainerName2 ?? string.Empty,
                            logger: logger // Pass the logger to the constructor
                        );
                    }
                });

                // Create a cancellation token and source to pass to the application service to allow them
                // to request a graceful application shutdown.
                CancellationTokenSource appShutdownCancellationTokenSource = new();
                CancellationToken appShutdownCancellationToken = appShutdownCancellationTokenSource.Token;
                services.AddKeyedSingleton("AppShutdown", appShutdownCancellationTokenSource);

                // Register Semantic Kernel and related services
                RegisterKernelServices(context, services);
            });

    // 
    /// <summary>
    /// Registers the SemanticKernelService with the provided <see cref="IServiceCollection"/>.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add the service to.</param>
    /// <remarks>
    /// This method fetches the necessary environment variables for configuring the SemanticKernelService,
    /// including the endpoint, API key, and deployment names for completion and embedding models.
    /// If the deployment names are not provided, default values are used.
    /// The service is registered with a scoped lifetime.
    /// </remarks>
    static void RegisterSemanticKernelService(IServiceCollection services)
    {
        // Fetch environment variables for Semantic Kernel
        string endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string endpointEmbedding = GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_ENDPOINT");
        string apiKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");
        string apiKeyEmbedding = GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_KEY");
        string completionDeploymentName = GetEnvironmentVariable("AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME") ?? "gpt-4o";
        string embeddingDeploymentName = GetEnvironmentVariable("AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME") ?? "text-embedding-3-large";
        int dimensions = 1024;

        // Register SemanticKernelService
        services.AddScoped<SemanticKernelService>(provider =>
        {
            var logger = provider.GetRequiredService<ILogger<SemanticKernelService>>();

            return new SemanticKernelService(
                endpoint: endpoint,
                endpointEmbedding: endpointEmbedding,
                completionDeploymentName: completionDeploymentName,
                embeddingDeploymentName: embeddingDeploymentName,
                apiKey: apiKey,
                apiKeyEmbedding: apiKeyEmbedding,
                dimensions: dimensions,
                logger: logger
            );
        });
    }

    /// <summary>
    /// Registers kernel services and configurations for the application.
    /// </summary>
    /// <param name="context">The host builder context.</param>
    /// <param name="services">The service collection to which services are added.</param>
    static void RegisterKernelServices(HostBuilderContext context, IServiceCollection services)
    {
        string azureOpenAIChatDeploymentName = "gpt-4o";
        string azureEmbeddingDeploymentName = "text-embedding-3-large";
        string azureOpenAIEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string azureOpenAIKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");
        string azureCosmosDBNoSQLConnectionString = GetEnvironmentVariable("COSMOS_DB_CONNECTION_STRING");
        string azureCosmosDBNoSQLDatabaseName = GetEnvironmentVariable("COSMOS_DB_DATABASE_ID");
        string ragCollectionName = "ragcontent";
        int azureEmbeddingDimensions = 1024;

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


    /// <summary>
    /// Registers services and configurations for the application.
    /// </summary>
    /// <typeparam name="TKey">The type of the key used in the text snippets.</typeparam>
    /// <param name="services">The service collection to which services are added.</param>
    /// <param name="kernelBuilder">The kernel builder used to configure the kernel.</param>
    static void RegisterServices<TKey>(IServiceCollection services, IKernelBuilder kernelBuilder)
        where TKey : notnull
    {
        // Add vector store text search with custom mappers
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

        // Register the data loader as a singleton service
        services.AddSingleton<IDataLoader, DataLoader<TKey>>();

        // Register the RAG chat service as a scoped service
        services.AddScoped<RAGChatService<TKey>>();
    }
    static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName)
            ?? throw new ArgumentNullException(variableName, $"{variableName} is not set in environment variables.");
    }
}

public class SEC_RAG_NavigatorService
{
    private readonly CosmosDbServiceWorking _cosmosDbServiceWorking;
    private readonly ChatService _chatService;
    private readonly CosmosDbService _cosmosDbService;

    private readonly ILogger<SEC_RAG_NavigatorService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SEC_RAG_NavigatorService"/> class.
    /// </summary>
    /// <param name="cosmosDbServiceWorking">The Cosmos DB service instance.</param>
    /// <param name="chatService">The chat service instance.</param>
    /// <param name="logger">The logger instance.</param>
    public SEC_RAG_NavigatorService(
        CosmosDbServiceWorking cosmosDbServiceWorking,
        ChatService chatService,
        CosmosDbService cosmosDbService,

        ILogger<SEC_RAG_NavigatorService> logger)
    {
        _cosmosDbServiceWorking = cosmosDbServiceWorking ?? throw new ArgumentNullException(nameof(cosmosDbServiceWorking));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes the specified command asynchronously based on the provided arguments.
    /// </summary>
    /// <param name="args">The command-line arguments.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task ExecuteAsync(string[] args)
    {
        string command = args[0].ToLower();

        try
        {
            if (command == "create-container" && args.Length >= 2)
            {
                string containerName = args[1];
                List<string> partitionKeyPaths = new List<string> { "/tenantId", "/userId", "/categoryId" };
                await _cosmosDbServiceWorking.CreateDatabaseAsync();
                await _cosmosDbServiceWorking.CreateContainerAsync(
                    containerName,
                    "/vectors",
                    partitionKeyPaths,
                    new List<string> { "/*" },
                    1024
                );
            }
            else if (command == "create-container-edgar" && args.Length >= 2)
            {
                string containerName = args[1];
                List<string> partitionKeyPaths = new List<string> { "/form", "/ticker", "/categoryId" };
                await _cosmosDbServiceWorking.CreateDatabaseAsync();
                await _cosmosDbServiceWorking.CreateContainerAsync(
                    containerName,
                    "/vectors",
                    partitionKeyPaths,
                    new List<string> { "/*" },
                    1024
                );
            }
            else if (command == "list-containers")
            {
                await _cosmosDbServiceWorking.ListDatabasesAndContainersAsync();
            }
            else if (command == "rag-chat-service")
            {
                string tenantID = "1234";
                string userID = "5678";
                await _cosmosDbServiceWorking.HandleInputFileFromPath(tenantID, userID);
            }
            else if (command == "rag-chat-service-cohere")
            {
                string tenantID = "1234";
                string userID = "5678";
                await _cosmosDbServiceWorking.HandleCohereInputFileFromPath(tenantID, userID);
            }
            else if (command == "rag-chat-service-cohere-edgar")
            {
                string form = "10-K";
                string ticker = "TSLA";
                await _cosmosDbServiceWorking.HandleCohereEDGARInputFileFromPath(form, ticker);
            }
            else if (command == "rag-chat-service-delete")
            {
                string tenantID = "1234";
                string userID = "5678";
                await _cosmosDbServiceWorking.DeleteRag(tenantID, userID);
            }
            else if (command == "rag-chat-service-delete-edgar")
            {
                string form = "10-K";
                string ticker = "TSLA";
                await _cosmosDbServiceWorking.DeleteRag(form, ticker);
            }
            else if (command == "knowledge-base-search" && args.Length >= 2)
            {
                // Retrieve the prompt text (e.g., "Risk factors")
                string promptText = args[1];

                // Example tenant and user ID
                string tenantID = "1234";
                string userID = "5678";

                // Call the knowledge base search handler
                await _cosmosDbServiceWorking.HandleKnowledgeBaseCompletionCommandAsync(
                    tenantId: tenantID,
                    userId: userID,
                    categoryId: "Document", // Example category
                    promptText: promptText,
                    similarityScore: 0.7 // Default similarity score
                );
            }
            else if (command == "knowledge-base-rag-search-edgar" && args.Length >= 2)
            {
                // Retrieve the prompt text (e.g., "Risk factors")
                string promptText = args[1];

                string form = "10-K";
                string ticker = "TSLA";
                string company = "Tesla";

                // Call the knowledge base search handler
                await _cosmosDbServiceWorking.HandleKnowledgeBaseRAGCommandEDGARAsync(
                    form: form,
                    ticker: ticker,
                    company: company,
                    categoryId: "Document", // Example category
                    promptText: promptText,
                    similarityScore: 0.7 // Default similarity score
                );
            }
            else if (command == "knowledge-base-rag-search" && args.Length >= 2)
            {
                // Retrieve the prompt text (e.g., "Risk factors")
                string promptText = args[1];

                // Example tenant and user ID
                string tenantID = "1234";
                string userID = "5678";

                // Call the knowledge base search handler
                await _cosmosDbServiceWorking.HandleKnowledgeBaseRAGCommandAsync(
                    tenantId: tenantID,
                    userId: userID,
                    categoryId: "Document", // Example category
                    promptText: promptText,
                    similarityScore: 0.7 // Default similarity score
                );
            }
            else if (command == "knowledge-base-rag-rerank-search" && args.Length >= 2)
            {
                // Retrieve the prompt text (e.g., "Risk factors")
                string promptText = args[1];

                // Example tenant and user ID
                string tenantID = "1234";
                string userID = "5678";

                // Call the knowledge base search handler
                await _cosmosDbServiceWorking.HandleKnowledgeBaseRAGRerankAsync(
                    tenantId: tenantID,
                    userId: userID,
                    categoryId: "Document", // Example category
                    promptText: promptText,
                    similarityScore: 0.7 // Default similarity score
                );
            }
            else if (command == "phi-4")
            {
                string endpoint = GetEnvironmentVariable("PHI_ENDPOINT");
                string apiKey = GetEnvironmentVariable("PHI_KEY");
                string modelId = "Phi-4";
                await _cosmosDbServiceWorking.HandleInferenceAsync(endpoint, apiKey, modelId);
            }
            else if (command == "phi-4-streaming")
            {
                string endpoint = GetEnvironmentVariable("PHI_ENDPOINT");
                string apiKey = GetEnvironmentVariable("PHI_KEY");
                string modelId = "Phi-4";
                await _cosmosDbServiceWorking.HandleInferenceStreamingAsync(endpoint, apiKey, modelId);
            }
            else if (command == "cohere-command-r+chat")
            {
                await _cosmosDbServiceWorking.HandleCohereChatCommandRAsync();
            }
            else if (command == "cohere-command-r+chat-streaming")
            {
                await _cosmosDbServiceWorking.HandleCohereChatCommandRAsyncStreaming();
            }
            else if (command == "cohere-command-r+chat-streaming-http")
            {
                await _cosmosDbServiceWorking.HandleHttpCohereChatCommandRAsyncStreaming();
            }
            else if (command == "cohere-embed-dbupsert")
            {
                await _cosmosDbServiceWorking.HandleCohereEmbedUpsertAsync();
            }
            else if (command == "vector-store")
            {
                await _cosmosDbServiceWorking.HandleVectorStoreAsync();
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

    /// <summary>
    /// Prints the usage instructions for the SEC-RAG-Navigator application.
    /// </summary>
    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  SEC-RAG-Navigator create-container <containerName>");
        Console.WriteLine("  SEC-RAG-Navigator create-container-edgar <containerName>");
        Console.WriteLine("  SEC-RAG-Navigator list-containers");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-cohere");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-cohere-edgar");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-delete");
        Console.WriteLine("  SEC-RAG-Navigator rag-chat-service-delete-edgar");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-search-edgar \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator knowledge-base-rag-rerank-search \"<promptText>\"");
        Console.WriteLine("  SEC-RAG-Navigator phi-4");
        Console.WriteLine("  SEC-RAG-Navigator phi-4-streaming");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat-streaming");
        Console.WriteLine("  SEC-RAG-Navigator cohere-command-r+chat-streaming-http");
        Console.WriteLine("  SEC-RAG-Navigator cohere-embed-dbupsert");
        Console.WriteLine("  SEC-RAG-Navigator vector-store");


    }
}

public class CosmosDbServiceWorking
{
    private readonly CosmosClient _cosmosClient;
    private readonly string _databaseId;
    private readonly ILogger<CosmosDbServiceWorking> _logger;
    private readonly RAGChatService<string> _ragChatService;
    private readonly ChatService _chatService;
    private readonly CosmosDbService _cosmosDbService;


    /// <summary>
    /// Initializes a new instance of the <see cref="CosmosDbServiceWorking"/> class.
    /// </summary>
    /// <param name="cosmosClient">The Cosmos DB client.</param>
    /// <param name="databaseId">The ID of the database.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="ragChatService">The RAG chat service instance.</param>
    /// <param name="chatService">The chat service instance.</param>
    public CosmosDbServiceWorking(
        CosmosClient cosmosClient,
        string databaseId,
        ILogger<CosmosDbServiceWorking> logger,
        RAGChatService<string> ragChatService,
        ChatService chatService,
        CosmosDbService cosmosDbService)
    {
        _cosmosClient = cosmosClient ?? throw new ArgumentNullException(nameof(cosmosClient));
        _databaseId = databaseId ?? throw new ArgumentNullException(nameof(databaseId));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ragChatService = ragChatService ?? throw new ArgumentNullException(nameof(ragChatService));
        _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
    }

    /// <summary>
    /// Creates a database in the Cosmos DB account if it does not already exist.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task CreateDatabaseAsync()
    {
        Database database = await _cosmosClient.CreateDatabaseIfNotExistsAsync(_databaseId);
        _logger.LogInformation($"Database created or exists: {database.Id}");
    }

    /// <summary>
    /// Creates a container in the Cosmos DB with specified properties.
    /// </summary>
    /// <param name="containerName">The name of the container to create.</param>
    /// <param name="vectorPath">The path for the vector embedding.</param>
    /// <param name="partitionKeyPaths">The list of partition key paths.</param>
    /// <param name="includedPaths">The list of included paths for indexing.</param>
    /// <param name="dimensions">The dimensions for the vector embedding.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task CreateContainerAsync(
        string containerName,
        string vectorPath,
        List<string> partitionKeyPaths,
        List<string> includedPaths,
        int dimensions)
    {
        _logger.LogInformation($"Creating container '{containerName}' with partition key paths: {string.Join(", ", partitionKeyPaths)}");

        Database database = _cosmosClient.GetDatabase(_databaseId);
        /*
          Supported metrics for distanceFunction are:

          cosine, which has values from -1 (least similar) to +1 (most similar).
          dotproduct, which has values from -∞ (-inf) (least similar) to +∞ (+inf) (most similar).
          euclidean, which has values from 0 (most similar) to +∞ (+inf) (least similar).

          */
        Collection<Microsoft.Azure.Cosmos.Embedding> embeddings = new Collection<Microsoft.Azure.Cosmos.Embedding>()
            {
                new Microsoft.Azure.Cosmos.Embedding()
                {
                    Path = vectorPath,
                    DataType = VectorDataType.Float32, //The data type of the vectors. Float32, Int8, Uint8 values. Default value is float32.
                    DistanceFunction = Microsoft.Azure.Cosmos.DistanceFunction.Cosine, // DotProduct
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
                            Type = VectorIndexType.QuantizedFlat, //QuantizedFlat DiskANN
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

    /// <summary>
    /// Lists all databases and their containers in the Cosmos DB account.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
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

    /// <summary>
    /// Handles the input file from a specified path.
    /// This method processes a PDF file using the RAG chat service.
    /// </summary>
    /// <param name="tenantID">The tenant ID.</param>
    /// <param name="userID">The user ID.</param>
    /// <returns>A Task representing the asynchronous operation, with a string result indicating the success of the operation.</returns>
    public async Task<string> HandleInputFileFromPath(
            string tenantID,
            string userID)
    {

        var filePath = "tsla-20231231.htm.html.pdf";

        // Generate a memoryKey based on tenantID, userID, and fileName
        var fileName = "tsla-20231231.htm.html.pdf";
        var memoryKey = $"{tenantID}-{userID}-{fileName}";
        Console.WriteLine($"HandleInputFileFromPath memoryKey: {memoryKey}");

        try
        {
            // Call ProcessPdfAsync directly
            Console.WriteLine($"Calling ProcessPdfAsync for {filePath}");
            await _ragChatService.ProcessPdfAsync(
                tenantID,
                userID,
                fileName,
                filePath,
                memoryKey,
                CancellationToken.None);

            Console.WriteLine("ProcessPdfAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleInputFileFromPath: {ex}");
            throw;
        }
        finally
        {

            Console.WriteLine($"HandleInputFileFromPath completed");
        }

        return $"Successfully processed file: {fileName}";
    }

    public async Task<string> HandleCohereInputFileFromPath(
            string tenantID,
            string userID)
    {

        var filePath = "tsla-20231231.htm.html.pdf";

        // Generate a memoryKey based on tenantID, userID, and fileName
        var fileName = "tsla-20231231.htm.html.pdf";
        var memoryKey = $"{tenantID}-{userID}-{fileName}";
        Console.WriteLine($"HandleInputFileFromPath memoryKey: {memoryKey}");

        try
        {
            // Call ProcessPdfAsync directly
            Console.WriteLine($"Calling ProcessPdfAsync for {filePath}");
            await _ragChatService.ProcessPdfCohereAsync(
                tenantID,
                userID,
                fileName,
                filePath,
                memoryKey,
                CancellationToken.None);

            Console.WriteLine("ProcessPdfAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleInputFileFromPath: {ex}");
            throw;
        }
        finally
        {

            Console.WriteLine($"HandleInputFileFromPath completed");
        }

        return $"Successfully processed file: {fileName}";
    }
    public async Task<string> HandleCohereEDGARInputFileFromPath(
            string form,
            string ticker)
    {

        var filePath = "tsla-20231231.pdf";

        // Generate a memoryKey based on tenantID, userID, and fileName
        var fileName = "tsla-20231231.pdf";
        var memoryKey = $"{form}-{ticker}-{fileName}";
        Console.WriteLine($"HandleInputFileFromPath memoryKey: {memoryKey}");

        try
        {
            // Call ProcessPdfAsync directly
            Console.WriteLine($"Calling ProcessPdfAsync for {filePath}");
            await _ragChatService.ProcessPdfCohereEDGARAsync(
                form,
                ticker,
                fileName,
                filePath,
                memoryKey,
                CancellationToken.None);

            Console.WriteLine("ProcessPdfAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in HandleInputFileFromPath: {ex}");
            throw;
        }
        finally
        {

            Console.WriteLine($"HandleInputFileFromPath completed");
        }

        return $"Successfully processed file: {fileName}";
    }

    public async Task<string> DeleteRag(
         string tenantID,
         string userID)
    {

        // Generate a memoryKey based on tenantID, userID, and fileName
        var fileName = "tsla-20231231.htm.html.pdf";

        //"id": "1234-5678-tsla-20231231.htm.html.pdf-page133-133-20-D5",

        var memoryKey = $"{tenantID}-{userID}-{fileName}";

        var categoryId = "Document";
        try
        {
            // Call ProcessPdfAsync directly
            Console.WriteLine($"Calling ProcessPdfAsync for {fileName}");
            await _ragChatService.DeletePdfAsync(
                tenantID,
                userID,
                memoryKey,
                categoryId);

            Console.WriteLine("DeletePdfAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteRag: {ex}");
            throw;
        }
        finally
        {

            Console.WriteLine($"DeleteRag completed");
        }

        return $"Successfully DeleteRag file: {fileName}";
    }
    public async Task<string> DeleteRagEDGAR(
          string form,
          string ticker)
    {

        // Generate a memoryKey based on tenantID, userID, and fileName
        var fileName = "tsla-20231231.pdf";

        var memoryKey = $"{form}-{ticker}-{fileName}";

        var categoryId = "Document";
        try
        {
            // Call ProcessPdfAsync directly
            Console.WriteLine($"Calling ProcessPdfAsync for {fileName}");
            await _ragChatService.DeleteEDGARPdfAsync(
                form,
                ticker,
                memoryKey,
                categoryId);

            Console.WriteLine("DeletePdfAsync completed successfully");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in DeleteRag: {ex}");
            throw;
        }
        finally
        {

            Console.WriteLine($"DeleteRag completed");
        }

        return $"Successfully DeleteRag file: {fileName}";
    }


    /// <summary>
    /// Handles the Knowledge Base Completion Command asynchronously.
    /// This method calls the GetKnowledgeBaseCompletionAsync method from the chat service
    /// to retrieve a completion based on the provided parameters.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="promptText">The prompt text to generate the completion.</param>
    /// <param name="similarityScore">The similarity score threshold for the completion.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandleKnowledgeBaseCompletionCommandAsync(
        string tenantId,
        string userId,
        string categoryId,
        string promptText,
        double similarityScore)
    {


        Console.WriteLine("Calling GetKnowledgeBaseCompletionAsync...");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var (completion, title) = await _chatService.GetKnowledgeBaseCompletionInt8Async( // GetKnowledgeBaseCompletionAsync GetKnowledgeBaseCompletionInt8Async
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                promptText: promptText,
                similarityScore: similarityScore);

            Console.WriteLine($"Completion Title: {title ?? "No Title"}");
            Console.WriteLine($"Completion Text:\n{completion}");
            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling GetKnowledgeBaseCompletionAsync.");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public async Task HandleKnowledgeBaseRAGCommandAsync(
        string tenantId,
        string userId,
        string categoryId,
        string promptText,
        double similarityScore)
    {
        Console.WriteLine("Calling HandleKnowledgeBaseRAGCommandAsync...");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Get the CohereResponse object from the service
            CohereResponse cohereResponse = await _chatService.GetKnowledgeBaseCompletionRAGInt8Async(
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                ticker: "TSLA",
                company: "Tesla",
                form: "10-K",
                promptText: promptText,
                similarityScore: similarityScore);
            // Pretty-print the response as JSON
            string prettyJson = JsonConvert.SerializeObject(cohereResponse, Formatting.Indented);

            // Log or display the JSON
            _logger.LogInformation("CohereResponse (Pretty JSON):\n{PrettyJson}", prettyJson);

            // Log everything in the response
            LogCohereResponse(cohereResponse);

            // Extract data from CohereResponse
            string nonCitedResponse = cohereResponse.GeneratedCompletion;

            // Map citations
            var citations = cohereResponse.Citations?.Select(c => new Citation
            {
                Start = c.Start,
                End = c.End,
                Text = c.Text,
                Sources = c.Sources?.Select(s => new Source
                {
                    Type = s.Type,
                    Id = s.Id,
                    Document = s.Document != null ? new Document
                    {
                        Id = s.Document.Id,
                        //Title = s.Document.Title,
                        Snippet = s.Document.Snippet
                    } : null
                }).ToList()
            }).ToList() ?? new List<Cosmos.Copilot.Models.Citation>();

            // Generate markdown, HTML snippet, and full HTML page responses
            // Generate the responses
            (string markdownResponse, string htmlResponse, string fullHtmlPage, byte[] pdfBytes) = await GenerateResponse(
                nonCitedResponse: nonCitedResponse,
                citations: citations,
                promptText: promptText,
                form: "10-K",
                ticker: "TSLA",
                company: "Tesla"
            );

            // Define the file path to save the PDF
            string pdfFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{"TSLA"}_{"10-K"}_Report.pdf");
            File.WriteAllBytes(pdfFilePath, pdfBytes);

            // Log the PDF file path
            _logger.LogInformation("PDF successfully saved at: {PdfFilePath}", pdfFilePath);
            Console.WriteLine($"PDF successfully saved at: {pdfFilePath}");

            // Log the responses
            _logger.LogInformation("Markdown Response:\n{MarkdownResponse}", markdownResponse);
            _logger.LogInformation("HTML Snippet Response:\n{HtmlResponse}", htmlResponse);
            _logger.LogInformation("Full HTML Page:\n{FullHtmlPage}", fullHtmlPage);

            // Optionally, return or display the responses
            Console.WriteLine("Markdown Response:");
            Console.WriteLine(markdownResponse);

            Console.WriteLine("\nHTML Snippet Response:");
            Console.WriteLine(htmlResponse);

            Console.WriteLine("\nFull HTML Page:");
            Console.WriteLine(fullHtmlPage);

            // Save the full HTML page to a file (optional)
            string filePath = "GeneratedReport.html";
            File.WriteAllText(filePath, fullHtmlPage);
            Console.WriteLine($"\nFull HTML page saved to: {filePath}");

            stopwatch.Stop();
            _logger.LogInformation("HandleKnowledgeBaseRAGCommandAsync: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling GetKnowledgeBaseCompletionRAGInt8Async.");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    public async Task HandleKnowledgeBaseRAGCommandEDGARAsync(
        string form,
        string ticker,
        string company,
        string categoryId,
        string promptText,
        double similarityScore)
    {
        Console.WriteLine("Calling HandleKnowledgeBaseRAGCommandEDGARAsync...");

        try
        {
            var stopwatch = Stopwatch.StartNew();
            // Get the CohereResponse object from the service
            CohereResponse cohereResponse = await _chatService.GetKnowledgeBaseCompletionRAGfloat32EDGARAsync(
                form: form,
                ticker: ticker,
                company: company,
                categoryId: categoryId,
                promptText: promptText,
                similarityScore: similarityScore);
            // Pretty-print the response as JSON
            string prettyJson = JsonConvert.SerializeObject(cohereResponse, Formatting.Indented);

            // Log or display the JSON
            _logger.LogInformation("CohereResponse (Pretty JSON):\n{PrettyJson}", prettyJson);

            // Log everything in the response
            LogCohereResponse(cohereResponse);

            // Extract data from CohereResponse
            string nonCitedResponse = cohereResponse.GeneratedCompletion;

            // Map citations
            var citations = cohereResponse.Citations?.Select(c => new Citation
            {
                Start = c.Start,
                End = c.End,
                Text = c.Text,
                Sources = c.Sources?.Select(s => new Source
                {
                    Type = s.Type,
                    Id = s.Id,
                    Document = s.Document != null ? new Document
                    {
                        Id = s.Document.Id,
                        //Title = s.Document.Title,
                        Snippet = s.Document.Snippet
                    } : null
                }).ToList()
            }).ToList() ?? new List<Cosmos.Copilot.Models.Citation>();

            // Generate markdown, HTML snippet, and full HTML page responses
            // Generate the responses
            (string markdownResponse, string htmlResponse, string fullHtmlPage, byte[] pdfBytes) = await GenerateResponse(
                nonCitedResponse: nonCitedResponse,
                citations: citations,
                promptText: promptText,
                ticker: ticker,
                company: company,
                form: form
            );

            // Define the file path to save the PDF
            string pdfFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"{ticker}_{form}_Report.pdf");
            File.WriteAllBytes(pdfFilePath, pdfBytes);

            // Log the PDF file path
            _logger.LogInformation("PDF successfully saved at: {PdfFilePath}", pdfFilePath);
            Console.WriteLine($"PDF successfully saved at: {pdfFilePath}");

            // Log the responses
            _logger.LogInformation("Markdown Response:\n{MarkdownResponse}", markdownResponse);
            _logger.LogInformation("HTML Snippet Response:\n{HtmlResponse}", htmlResponse);
            _logger.LogInformation("Full HTML Page:\n{FullHtmlPage}", fullHtmlPage);

            // Optionally, return or display the responses
            Console.WriteLine("Markdown Response:");
            Console.WriteLine(markdownResponse);

            Console.WriteLine("\nHTML Snippet Response:");
            Console.WriteLine(htmlResponse);

            Console.WriteLine("\nFull HTML Page:");
            Console.WriteLine(fullHtmlPage);

            // Save the full HTML page to a file (optional)
            string filePath = "GeneratedReport.html";
            File.WriteAllText(filePath, fullHtmlPage);
            Console.WriteLine($"\nFull HTML page saved to: {filePath}");

            stopwatch.Stop();
            _logger.LogInformation("HandleKnowledgeBaseRAGCommandAsync: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling GetKnowledgeBaseCompletionRAGInt8Async.");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }


    private async Task<(string markdownResponse, string htmlResponse, string fullHtmlPage, byte[] pdfBytes)> GenerateResponse(
        string nonCitedResponse,
        List<Cosmos.Copilot.Models.Citation> citations,
        string promptText,
        string ticker,
        string company,
        string form,
        int year = 2024,
        string textColor = "#2C3E50",
        string linkColor = "#2980B9",
        string fontFamily = "Arial, sans-serif",
        string lineHeight = "1.8")
    {
        // Remove page patterns like [Page 63, Page 94, ...]

        // Extract the title from the response (assumes the title starts with "## ")
        var titleMatch = Regex.Match(nonCitedResponse, @"##\s*(.+)");
        string title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : "Generated Report";

        // Add company, ticker, form, and year to the title
        string fullTitle = $"{title} | {company} ({ticker}) - {form} {year}";

        // Initialize markdown response
        string markdownResponse = nonCitedResponse;
        int offset = 0;

        // Process citations
        foreach (var citation in citations)
        {
            if (citation.Start < 0 || citation.Start >= nonCitedResponse.Length ||
                citation.End <= citation.Start || citation.End > nonCitedResponse.Length)
            {
                continue; // Skip invalid citation ranges
            }

            int start = citation.Start + offset;
            int end = citation.End + offset;
            string text = citation.Text;

            // Generate links from document sources
            string docLinks = string.Join(", ", citation.Sources?
                .Select(source => source.Document?.Title)
                .Where(title => !string.IsNullOrEmpty(title)) ?? Enumerable.Empty<string>());

            string replacement = $"{text} [{docLinks}]";

            if (start >= 0 && start < markdownResponse.Length && end <= markdownResponse.Length)
            {
                markdownResponse = markdownResponse.Substring(0, start) + replacement + markdownResponse.Substring(end);
                offset += replacement.Length - text.Length; // Adjust offset
            }
        }

        // Format Markdown into HTML (bold, lists, and line breaks)
        string formattedResponse = FormatMarkdownToHtml(markdownResponse);
        formattedResponse = Regex.Replace(formattedResponse, @"\[\]", string.Empty);

        // Create HTML response (snippet)
        string htmlResponse = $@"
    <div style=""color:{textColor}; font-family:{fontFamily}; line-height:{lineHeight};"">
        <h1 style=""color:{linkColor};"">{fullTitle}</h1>
        <p>Powered by <strong>ASAP Knowledge Navigator for EDGAR</strong></p>

        <div class=""prompt-text"">
            <h3>Prompt Text:</h3>
            <p>{promptText}</p>
        </div>
        {formattedResponse}
    </div>";

        // Create full HTML page with footer and prompt text
        string fullHtmlPage = $@"
    <!DOCTYPE html>
    <html lang=""en"">
    <head>
        <meta charset=""UTF-8"">
        <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
        <title>{fullTitle}</title>
        <style>
            body {{ font-family: {fontFamily}; line-height: {lineHeight}; color: {textColor}; margin: 20px; }}
            h1 {{ color: {linkColor}; font-size: 24px; text-align: center; }}
            a {{ color: {linkColor}; text-decoration: none; }}
            a:hover {{ text-decoration: underline; }}
            ul {{ margin-left: 20px; }}
            footer {{ margin-top: 40px; text-align: center; font-size: 14px; color: #7F8C8D; }}
            .prompt-text {{ margin-top: 30px; padding: 15px; background-color: #F8F9FA; border: 1px solid #DADDE1; border-radius: 5px; font-family: {fontFamily}; font-size: 14px; }}
        </style>
    </head>
    <body>
        {htmlResponse}
        <footer>
            <p>© {year} AITrailblazer. All rights reserved.</p>
            <p>Powered by <strong>ASAP Knowledge Navigator for EDGAR</strong></p>
        </footer>
    </body>
    </html>";

        byte[] pdfBytes;
        string pdfFilePath = Path.Combine(Directory.GetCurrentDirectory(), $"{fullTitle.Replace(' ', '_').Replace('|', '-')}.pdf");

        _logger.LogInformation($"Saving PDF to: {pdfFilePath}");

        using (var playwright = await Playwright.CreateAsync())
        {
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
            var page = await browser.NewPageAsync();

            // Set the full HTML page content
            await page.SetContentAsync(fullHtmlPage);

            // Save PDF to file
            await page.PdfAsync(new PagePdfOptions { Path = pdfFilePath, Format = "A4" });

            // Generate PDF bytes
            pdfBytes = await page.PdfAsync(new PagePdfOptions { Format = "A4" });
        }

        return (markdownResponse, htmlResponse, fullHtmlPage, pdfBytes);
    }

    // Helper method to format Markdown to HTML
    private string FormatMarkdownToHtml(string text)
    {
        // Convert Markdown headers (## text) to HTML h2
        text = Regex.Replace(text, @"##\s*(.+)", @"<h2>$1</h2>");

        // Convert Markdown bold (**text**) to HTML bold (<strong>)
        text = Regex.Replace(text, @"\*\*(.+?)\*\*", @"<strong>$1</strong>");

        // Convert Markdown lists (- item) to HTML unordered lists
        // First, wrap each list item in <li>
        text = Regex.Replace(text, @"(?m)^- (.+)", @"<li>$1</li>");

        // Then, wrap all consecutive <li> tags in a <ul>
        text = Regex.Replace(text, @"(<li>.+?</li>\s*)+", match => $"<ul>{match.Value}</ul>");

        // Replace new lines with <br> for proper HTML rendering, but only outside of lists
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (!lines[i].Contains("<li>") && !lines[i].Contains("</ul>"))
            {
                lines[i] = lines[i].Replace("</h2>", "</h2><br>");
            }
        }
        text = string.Join("\n", lines);

        // Remove extra Markdown symbols or artifacts
        // Note: Be careful with this, as it might remove needed asterisks inside HTML tags
        text = Regex.Replace(text, @"(?<!<strong>)\*(?!</strong>)", "");

        return text;
    }

    private void LogCohereResponse(CohereResponse cohereResponse)
    {
        if (cohereResponse == null)
        {
            _logger.LogWarning("CohereResponse is null.");
            return;
        }

        // Log top-level properties
        _logger.LogInformation("Generated Completion:\n{GeneratedCompletion}", cohereResponse.GeneratedCompletion ?? "No completion provided.");
        _logger.LogInformation("Finish Reason: {FinishReason}", cohereResponse.FinishReason ?? "No finish reason provided.");

        if (cohereResponse.Usage != null)
        {
            _logger.LogInformation("Usage Details:");
            _logger.LogInformation("  - Input Tokens: {InputTokens}", cohereResponse.Usage.InputTokens);
            _logger.LogInformation("  - Output Tokens: {OutputTokens}", cohereResponse.Usage.OutputTokens);
            _logger.LogInformation("  - Total Tokens: {TotalTokens}", cohereResponse.Usage.TotalTokens);
        }
        else
        {
            _logger.LogWarning("Usage data is not available.");
        }

        // Log citations
        if (cohereResponse.Citations != null && cohereResponse.Citations.Any())
        {
            _logger.LogInformation("Citations:");
            foreach (var citation in cohereResponse.Citations)
            {
                _logger.LogInformation("  Citation:");
                _logger.LogInformation("    - Text: {Text}", citation.Text ?? "No text provided.");
                _logger.LogInformation("    - Start: {Start}, End: {End}", citation.Start, citation.End);

                if (citation.Sources != null && citation.Sources.Any())
                {
                    _logger.LogInformation("    Sources:");
                    foreach (var source in citation.Sources)
                    {
                        _logger.LogInformation("      - Type: {Type}, ID: {Id}", source.Type ?? "Unknown", source.Id ?? "Unknown");

                        if (source.Document != null)
                        {
                            _logger.LogInformation("        Document:");
                            _logger.LogInformation("          - ID: {Id}", source.Document.Id ?? "Unknown");
                            _logger.LogInformation("          - Title: {Title}", source.Document.Title ?? "No title provided.");
                            _logger.LogInformation("          - Snippet: {Snippet}", source.Document.Snippet ?? "No snippet provided.");
                        }
                        else
                        {
                            _logger.LogWarning("        Document is null.");
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("    No sources available for this citation.");
                }
            }
        }
        else
        {
            _logger.LogWarning("No citations provided.");
        }
    }


    public async Task HandleKnowledgeBaseRAGRerankAsync(
     string tenantId,
     string userId,
     string categoryId,
     string promptText,
     double similarityScore)
    {
        Console.WriteLine("Calling GetKnowledgeBaseRerankRAGInt8Async...");

        try
        {
            var stopwatch = Stopwatch.StartNew();

            // Call GetKnowledgeBaseRerankRAGInt8Async to get reordered items
            List<KnowledgeBaseItem> reorderedItems = await _chatService.GetKnowledgeBaseRerankRAGInt8Async(
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                promptText: promptText,
                similarityScore: similarityScore);

            stopwatch.Stop();

            if (reorderedItems.Any())
            {
                Console.WriteLine("Reordered Items:");
                foreach (var item in reorderedItems)
                {
                    Console.WriteLine($"- Title: {item.Title}, Relevance Score: {item.RelevanceScore}");
                }
            }
            else
            {
                Console.WriteLine("No reordered items returned.");
            }

            _logger.LogInformation(
                "GetKnowledgeBaseRerankRAGInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes,
                stopwatch.Elapsed.Seconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while calling GetKnowledgeBaseRerankRAGInt8Async.");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }



    /// <summary>
    /// Handles the Phi 3.5 MoE Instruct Command asynchronously using HTTP.
    /// This method sets up a conversational assistant using Azure AI services via HTTP requests.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandlePhi35MoEInstructCommandAsync1()
    {

        var handler = new HttpClientHandler()
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback =
                    (httpRequestMessage, cert, cetChain, policyErrors) => { return true; }
        };
        using (var client = new HttpClient(handler))
        {
            // Request data goes here
            // The example below assumes JSON formatting which may be updated
            // depending on the format your endpoint expects.
            // More information can be found here:
            // https://docs.microsoft.com/azure/machine-learning/how-to-deploy-advanced-entry-script
            var requestBody = @"{
                  ""messages"": [
                    {
                      ""role"": ""user"",
                      ""content"": ""I am going to Paris, what should I see?""
                    },
                    {
                      ""role"": ""assistant"",
                      ""content"": ""Paris, the capital of France, is known for its stunning architecture, art museums, historical landmarks, and romantic atmosphere. Here are some of the top attractions to see in Paris:\n\n1. The Eiffel Tower: The iconic Eiffel Tower is one of the most recognizable landmarks in the world and offers breathtaking views of the city.\n2. The Louvre Museum: The Louvre is one of the world's largest and most famous museums, housing an impressive collection of art and artifacts, including the Mona Lisa.\n3. Notre-Dame Cathedral: This beautiful cathedral is one of the most famous landmarks in Paris and is known for its Gothic architecture and stunning stained glass windows.\n\nThese are just a few of the many attractions that Paris has to offer. With so much to see and do, it's no wonder that Paris is one of the most popular tourist destinations in the world.""
                    },
                    {
                      ""role"": ""user"",
                      ""content"": ""What is so great about #1?""
                    }
                  ],
                  ""max_tokens"": 2048,
                  ""temperature"": 0.8,
                  ""top_p"": 0.1,
                  ""presence_penalty"": 0,
                  ""frequency_penalty"": 0
                }";

            // Replace this with the primary/secondary key, AMLToken, or Microsoft Entra ID token for the endpoint
            string apiKey = GetEnvironmentVariable("PHI_KEY");

            if (string.IsNullOrEmpty(apiKey))
            {
                throw new Exception("A key should be provided to invoke the endpoint");
            }
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(GetEnvironmentVariable("PHI_ENDPOINT"));

            var content = new StringContent(requestBody);
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

            // WARNING: The 'await' statement below can result in a deadlock
            // if you are calling this code from the UI thread of an ASP.Net application.
            // One way to address this would be to call ConfigureAwait(false)
            // so that the execution does not attempt to resume on the original context.
            // For instance, replace code such as:
            //      result = await DoSomeTask()
            // with the following:
            //      result = await DoSomeTask().ConfigureAwait(false)
            HttpResponseMessage response = await client.PostAsync("", content);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync();
                Console.WriteLine("Result: {0}", result);
            }
            else
            {
                Console.WriteLine(string.Format("The request failed with status code: {0}", response.StatusCode));

                // Print the headers - they include the requert ID and the timestamp,
                // which are useful for debugging the failure
                Console.WriteLine(response.Headers.ToString());

                string responseContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine(responseContent);
            }
        }

    }

    /// <summary>
    /// Handles the Phi 3.5 MoE Instruct Command asynchronously using HTTP.
    /// This method sets up a conversational assistant using Azure AI services via HTTP requests.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandlePhi35MoEInstructCommandHttpAsync01()
    {
        // Configuration
        string deploymentName = "phi-3-5-moe-instruct"; // Deployment name from Azure
        string endpoint = GetEnvironmentVariable("PHI_ENDPOINT");
        string apiKey = GetEnvironmentVariable("PHI_KEY");

        // Set up HttpClient with Authorization header
        using var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");

        // Prepare the request payload
        var requestBody = new
        {
            messages = new[]
            {
            new { role = "user", content = "Hello" } // Example user message
        }
        };

        // Serialize the request body to JSON
        string jsonPayload = System.Text.Json.JsonSerializer.Serialize(requestBody);

        // Create the HTTP request
        var request = new HttpRequestMessage
        {
            Method = HttpMethod.Post,
            RequestUri = new Uri(endpoint),
            Content = new StringContent(jsonPayload, System.Text.Encoding.UTF8, "application/json")
        };

        try
        {
            // Send the request
            var response = await httpClient.SendAsync(request);

            // Ensure the response is successful
            response.EnsureSuccessStatusCode();

            // Read and process the response
            string responseBody = await response.Content.ReadAsStringAsync();
            Console.WriteLine("Response:");
            Console.WriteLine(responseBody);
        }
        catch (HttpRequestException ex)
        {
            // Handle HTTP errors
            Console.WriteLine($"Request error: {ex.Message}");
        }
    }

    /// <summary>
    /// Handles the Phi 3.5 MoE Instruct Command asynchronously.
    /// This method sets up a conversational assistant using Azure AI services.
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandlePhi35MoEInstructCommandAsync2()
    {
        string endpoint = GetEnvironmentVariable("PHI_ENDPOINT");
        string apiKey = GetEnvironmentVariable("PHI_KEY");
        string modelId = "phi-3-5-moe-instruct";

        // Kernel builder
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureAIInferenceChatCompletion(
            endpoint: new Uri(endpoint),
            apiKey: apiKey,
            modelId: modelId//,
                            //httpClient: httpClient // Provide the custom HttpClient
        );

        var kernel = kernelBuilder.Build();

        // Create chat service
        var chat = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        // Chat loop
        while (true)
        {
            Console.Write("Q: ");
            var userQ = Console.ReadLine();
            if (string.IsNullOrEmpty(userQ))
            {
                break;
            }

            history.AddUserMessage(userQ);

            Console.Write("Phi3: ");
            string response = string.Empty;

            var result = chat.GetStreamingChatMessageContentsAsync(history);

            await foreach (var message in result)
            {
                Console.Write(message.Content);
                response += message.Content;
            }

            history.AddAssistantMessage(response);
            Console.WriteLine();
        }
    }


    /// <summary>
    /// Handles the Phi 3.5 MoE Instruct Command asynchronously.
    /// This method sets up a conversational assistant using Azure AI services.
    /// using semantic kernel
    /// </summary>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task HandleInferenceAsync(string endpoint, string apiKey, string modelId)
    {
        // Initialize the custom tokenizer (e.g., for GPT-4)
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

        // Kernel builder setup
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureAIInferenceChatCompletion(
            endpoint: new Uri(endpoint),
            apiKey: apiKey,
            modelId: modelId
        );

        var kernel = kernelBuilder.Build();

        // Define the prompt template for general chat
        var promptyTemplate = _promptyTemplate;


        // Chat settings
        double temperature = 0.7;
        double topP = 0.9;
        int maxTokens = 1000;

        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            TopP = topP,
            MaxTokens = maxTokens,
        };

        Console.WriteLine("General Chat Assistant. Type 'exit' to quit.");
        while (true)
        {
            Console.Write("Q: ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrEmpty(userInput) || userInput.ToLower() == "exit")
            {
                break;
            }


            // Set arguments for execution
            var arguments = new KernelArguments(executionSettings)
            {
                ["input"] = userInput,
                ["context"] = userInput
            };

            try
            {
                Console.Write($"{modelId}: ");

                // Calculate token count for input using the custom tokenizer
                int inputTokenCount = tokenizer.CountTokens(userInput);
                Console.WriteLine($"CustomTokenizer:InputTokenCount: {inputTokenCount}");
                //var paragraphWritingFunction = kernel.CreateFunctionFromPrompt(FunctionDefinition);
                var paragraphWritingFunction = new Kernel().CreateFunctionFromPrompty(promptyTemplate);

                // Invoke the function
                var result = await paragraphWritingFunction.InvokeAsync(kernel, arguments);

                // Retrieve and print the raw result value
                var resultValue = result.GetValue<string>();
                Console.WriteLine("Raw Response:");
                Console.WriteLine(resultValue);

                // Calculate token count for output using the custom tokenizer
                int outputTokenCount = tokenizer.CountTokens(resultValue);
                Console.WriteLine($"CustomTokenizer:OutputTokenCount: {outputTokenCount}");

                // Calculate the total token count (input + output)
                int totalTokenCount = inputTokenCount + outputTokenCount;
                Console.WriteLine($"CustomTokenizer:TotalTokenCount: {totalTokenCount}");

                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

        }
    }
    public IKernelBuilder CreateKernelBuilder(string modelId)
    {
        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

        string deploymentName = modelId;
        string endpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string apiKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");


        int embeddingsdDimensions = 1024;

        //string modelId = "gpt-4o-mini";

        // Create HttpClient with custom headers and timeout
        var httpClient = new HttpClient();
        //httpClient.DefaultRequestHeaders.Add("My-Custom-Header", "My Custom Value");
        httpClient.Timeout = TimeSpan.FromSeconds(300);  // Set NetworkTimeout to 30 seconds


        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: deploymentName,
            endpoint: endpoint,
            apiKey: apiKey,
            modelId: modelId, // Optional name of the underlying model if the deployment name doesn't match the model name
                              //serviceId: "YOUR_SERVICE_ID", // Optional; for targeting specific services within Semantic Kernel
            httpClient: httpClient // Optional; if not provided, the HttpClient from the kernel will be used
            );

        return kernelBuilder;
    }
    // Define promptyTemplate as a global variable
    private readonly string _promptyTemplate = @"""
---
name: AIWritingAssistant
description: AI Writing Tool designed to cater to a wide range of fields and purposes
authors:
  - AITrailblazer
model:
  api: completion
  configuration:
    type: azure_openai
  parameters:
    tools_choice: auto
---
system:

You are an AI Writing Tool designed to cater to a wide range of fields and purposes, enabling tailored content creation that meets specific goals and resonates with intended audiences. You are an invaluable resource for organizations and professionals seeking high-quality, goal-aligned content. You streamline the entire writing process, from ideation to the final draft, across various domains and formats.



Detailed Instruction and Objective
You are an invaluable resource for organizations and professionals seeking high-quality, goal-aligned content. You streamline the entire writing process, from ideation to the final draft, across various domains and formats.



Execution Instructions
You will be presented with a <context> and an <input>. Use the following settings to enhance your response:

 
# instructions
1. **Comprehensive Review:** Carefully read and understand the passage of information provided to ensure full comprehension.
2. **Analyze `<context>`:** Thoroughly review the `<context>` to fully grasp its background, details, and relevance to the task.
3. **Examine `<input>`:** Carefully consider the `<input>` to understand the specific instructions or directives it contains.
4. **Generate Response:** Use the insights from the `<context>` and `<input>` to generate a response that is accurate, relevant, and aligned with the requirements. Make sure your response integrates both the `<context>` and `<input>` effectively to achieve the desired outcome.

**Use American English:**  
Always use natural, mainstream, contemporary American English. Verify any unfamiliar terms or regional expressions to ensure they are widely recognized and used in American English. Stick to language commonly employed in America.

Always ensure the output text is cohesive, regardless of the complexity of the topic or the context of the conversation. Focus on the structure and unity of the text, using smooth transitions and logical flow to achieve cohesion. The final output should be a well-organized, unified whole without abrupt transitions or disjointed sections.

If the <input> is missing, use the <context> to generate a response.
If the <context> is missing, use the <input> to generate a response.

Thoroughly review the <context>  and to fully grasp its 
background, details, and relevance to the task and 
carefully justify the response in the format:
<justify>
  Justification for the response.  
</justify>

Do <justify> internally do not show it to the user.

Token Flexibility:
While the target output should aim for a maximum of {{maxTokens}}, the system can allow a slight overflow (e.g., up to 520 or 550 tokens) if necessary to maintain the integrity and coherence of the response.


At the end check if the output is full sentence and if it makes sense. If not, generate a new response.

The output should be maximum of {{maxTokens}}. Try to fit it all in. Don't cut

- context: {{context}}

    """;
    public async Task HandleCohereCommandRAsync1()
    {
        // Step 1: Initialize the client with endpoint and API key
        var client = new ChatCompletionsClient(
            new Uri(Environment.GetEnvironmentVariable("COHERE_ENDPOINT")), // Ensure this environment variable is set
            new AzureKeyCredential(Environment.GetEnvironmentVariable("COHERE_KEY")) // Ensure this environment variable is set
        );

        try
        {
            // Step 2: Get model information (optional)
            var modelInfo = client.GetModelInfo();
            Console.WriteLine($"Model name: {modelInfo.Value.ModelName}");
            Console.WriteLine($"Model type: {modelInfo.Value.ModelType}");
            Console.WriteLine($"Model provider name: {modelInfo.Value.ModelProviderName}");
            string userInput = "Hello";
            var promptyTemplate = _promptyTemplate;
            Console.WriteLine($"promptyTemplate ----- : {promptyTemplate}");
            Console.WriteLine($"promptyTemplate ----- :");

            // Step 3: Create chat completion request options
            // Define Messages separately
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(promptyTemplate),
                new ChatRequestUserMessage(userInput)
            };
            Console.WriteLine($"GenerateWithCohereAsync: input: {userInput}");

            try
            {
                // Construct requestOptions using the separate Messages list
                var requestOptions = new ChatCompletionsOptions
                {
                    Messages = messages,
                    MaxTokens = 100, // Limit response length
                    Temperature = 0.7f, // Adjust creativity
                                        //ResponseFormat = new ChatCompletionsResponseFormatJSON()
                };

                string jsonmessages = Newtonsoft.Json.JsonConvert.SerializeObject(messages, Newtonsoft.Json.Formatting.Indented);

                Console.WriteLine($"jsonmessages:\n{jsonmessages}");
                // Step 4: Send chat completion request
                Console.WriteLine("Sending chat completion request...");
                Azure.Response<ChatCompletions> response = await client.CompleteAsync(requestOptions);
                jsonmessages = Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.Indented);
                Console.WriteLine($"jsonmessages response: {jsonmessages}");

                System.Console.WriteLine(response.Value.Content);
                Console.WriteLine($"ID: {response.Value.Id}");
                Console.WriteLine($"Created: {response.Value.Created}");
                Console.WriteLine($"Model: {response.Value.Model}");
                Console.WriteLine($"Content: {response.Value.Content}");
                Console.WriteLine("Usage:");
                Console.WriteLine($"  Completion Tokens: {response.Value.Usage.CompletionTokens}");
                Console.WriteLine($"  Prompt Tokens: {response.Value.Usage.PromptTokens}");
                Console.WriteLine($"  Total Tokens: {response.Value.Usage.TotalTokens}");

                ChatCompletions result = response.Value;
                Console.WriteLine($"FinishReason: {result.FinishReason}");
                Console.WriteLine($"Role: {result.Role}");
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == 422)
                {
                    Console.WriteLine($"Looks like the model doesn't support a parameter: {ex.Message}");
                }
                else
                {
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    public async Task HandlePhi4Async1()
    {
        // Step 1: Initialize the client with endpoint and API key
        var client = new ChatCompletionsClient(
            new Uri(Environment.GetEnvironmentVariable("PHI_ENDPOINT")), // Ensure this environment variable is set
            new AzureKeyCredential(Environment.GetEnvironmentVariable("PHI_KEY")) // Ensure this environment variable is set
        );

        try
        {
            // Step 2: Get model information (optional)
            var modelInfo = client.GetModelInfo();
            Console.WriteLine($"Model name: {modelInfo.Value.ModelName}");
            Console.WriteLine($"Model type: {modelInfo.Value.ModelType}");
            Console.WriteLine($"Model provider name: {modelInfo.Value.ModelProviderName}");
            string userInput = "Hello";
            var promptyTemplate = _promptyTemplate;
            Console.WriteLine($"promptyTemplate ----- : {promptyTemplate}");
            Console.WriteLine($"promptyTemplate ----- :");

            // Step 3: Create chat completion request options
            // Define Messages separately
            var messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(promptyTemplate),
                new ChatRequestUserMessage(userInput)
            };
            Console.WriteLine($"GenerateWithCohereAsync: input: {userInput}");

            try
            {
                // Construct requestOptions using the separate Messages list
                var requestOptions = new ChatCompletionsOptions
                {
                    Messages = messages,
                    MaxTokens = 100, // Limit response length
                    Temperature = 0.7f, // Adjust creativity
                                        //ResponseFormat = new ChatCompletionsResponseFormatJSON()
                };

                string jsonmessages = Newtonsoft.Json.JsonConvert.SerializeObject(messages, Newtonsoft.Json.Formatting.Indented);

                Console.WriteLine($"jsonmessages:\n{jsonmessages}");
                // Step 4: Send chat completion request
                Console.WriteLine("Sending chat completion request...");
                Azure.Response<ChatCompletions> response = await client.CompleteAsync(requestOptions);
                jsonmessages = Newtonsoft.Json.JsonConvert.SerializeObject(response, Newtonsoft.Json.Formatting.Indented);
                Console.WriteLine($"jsonmessages response: {jsonmessages}");

                System.Console.WriteLine(response.Value.Content);
                Console.WriteLine($"ID: {response.Value.Id}");
                Console.WriteLine($"Created: {response.Value.Created}");
                Console.WriteLine($"Model: {response.Value.Model}");
                Console.WriteLine($"Content: {response.Value.Content}");
                Console.WriteLine("Usage:");
                Console.WriteLine($"  Completion Tokens: {response.Value.Usage.CompletionTokens}");
                Console.WriteLine($"  Prompt Tokens: {response.Value.Usage.PromptTokens}");
                Console.WriteLine($"  Total Tokens: {response.Value.Usage.TotalTokens}");

                ChatCompletions result = response.Value;
                Console.WriteLine($"FinishReason: {result.FinishReason}");
                Console.WriteLine($"Role: {result.Role}");
            }
            catch (RequestFailedException ex)
            {
                if (ex.Status == 422)
                {
                    Console.WriteLine($"Looks like the model doesn't support a parameter: {ex.Message}");
                }
                else
                {
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    public async Task<string[]> HandlePhi4Async()
    {
        string input = "Generate 10 queries from the following text: Phi-4 is the best!";
        string system_message = @"## Task and Context 
        You are a query generation assistant.
        ## Style Guide
Please follow these guidelines to ensure high-quality output:
1. Use **US spelling** consistently throughout the text.
2. Maintain a professional and concise tone.
3. Structure queries clearly and ensure they are relevant to the input context.
4. Avoid redundancy and ensure each query is unique.";

        // Clean up input strings
        system_message = system_message.Replace("\r\n", " ").Replace("\n", " ");
        input = input.Replace("\r\n", " ").Replace("\n", " ");

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
        };

        using var client = new HttpClient(handler);
        try
        {
            // Construct request body for query generation
            string requestBody = @"
        {
            'messages': [
                {
                    'role': 'system',
                    'content': '{system_message}'
                },
                {
                    'role': 'user', 
                    'content': '{input}'
                }
            ],
            'max_tokens': 2048,
            'temperature': 0.8,
            'top_p': 0.1,
            'frequency_penalty': 0,
            'presence_penalty': 0,
            'seed': 369
        }"
              .Replace("'", "\"") // Replace single quotes with double quotes for valid JSON
              .Replace("{system_message}", system_message)
              .Replace("{input}", input); // Replace placeholders with actual values

            Console.WriteLine($"Request Body: {requestBody}");

            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("PHI_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("PHI_ENDPOINT");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(apiEndpoint);

            // Set up the request content
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Send POST request to generate queries
            HttpResponseMessage response = await client.PostAsync("/chat/completions", content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Parse the response JSON to extract generated queries
                var responseJson = JsonConvert.DeserializeObject<dynamic>(result);
                var generatedQueries = new List<string>();

                foreach (var choice in responseJson.choices)
                {
                    if (choice.message != null && choice.message.content != null)
                    {
                        generatedQueries.Add(choice.message.content.ToString());
                    }
                }

                Console.WriteLine("Generated Queries:");
                foreach (var query in generatedQueries)
                {
                    Console.WriteLine(query);
                }

                return generatedQueries.ToArray();
            }
            else
            {
                Console.WriteLine($"The request failed with status code: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"Error Details: {responseContent}");
                return Array.Empty<string>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return Array.Empty<string>();
        }
    }


    public async Task HandleCohereChatCommandRAsync()
    {
        // Initial system message
        string systemMessage = @"## Task and Context 
        You are a writing assistant.
        ## Style Guide
Please follow these guidelines to ensure high-quality output:

1. Use **US spelling** consistently throughout the text.
2. Maintain a professional and concise tone.
3. Structure content clearly using appropriate headings, bullet points, and numbering.
4. Ensure grammar, punctuation, and spelling are accurate.
5. Incorporate creativity by writing in **sonnets** where appropriate, while retaining professionalism.
6. Avoid jargon unless explicitly required or beneficial for the audience.";

        string input = "Write a title for a blog post about API design. Only output the title text.";

        // Clean up line breaks for JSON compatibility
        systemMessage = systemMessage.Replace("\r\n", " ").Replace("\n", " ");
        input = input.Replace("\r\n", " ").Replace("\n", " ");

        // Sample documents array
        var documents = new[]
        {
        new { id = "1", data = "Cohere is the best!" }
    };

        // Initialize HttpClientHandler with custom certificate validation
        using (var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        })
        using (var client = new HttpClient(handler))
        {
            try
            {
                // Construct the request body
                var requestBody = new
                {
                    model = "command-r-plus-08-2024",
                    messages = new[]
                    {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = input }
                },
                    documents = documents,
                    max_tokens = 2048,
                    temperature = 0.8,
                    frequency_penalty = 0,
                    presence_penalty = 0,
                    seed = 369
                };

                // Serialize request body to JSON
                string requestBodyJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                Console.WriteLine($"Request Body: {requestBodyJson}");

                // Retrieve API key and endpoint from environment variables
                string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");
                if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
                {
                    throw new InvalidOperationException("API key or endpoint is not configured.");
                }

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.BaseAddress = new Uri(apiEndpoint);

                // Set up the request content
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

                // Send POST request
                HttpResponseMessage response = await client.PostAsync("v2/chat", content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    // Deserialize and pretty print JSON
                    var formattedJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(result), Formatting.Indented);
                    Console.WriteLine("Result (Formatted JSON):");
                    Console.WriteLine(formattedJson);
                }
                else
                {
                    Console.WriteLine($"The request failed with status code: {response.StatusCode}");
                    Console.WriteLine("Response Headers:");
                    Console.WriteLine(response.Headers);

                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var formattedErrorJson = JsonConvert.SerializeObject(JsonConvert.DeserializeObject(responseContent), Formatting.Indented);
                    Console.WriteLine($"Error Details (Formatted JSON):");
                    Console.WriteLine(formattedErrorJson);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    public async Task HandleHttpCohereChatCommandRAsyncStreaming()
    {
        // Define the system message and user input
        string systemMessage = @"## Task and Context 
    You are a writing assistant
    ## Style Guide
Please follow these guidelines to ensure high-quality output:

## Style Guide
1. Use **US spelling** consistently throughout the text.
2. Maintain a professional and concise tone.
3. Structure content clearly using appropriate headings, bullet points, and numbering.
4. Ensure grammar, punctuation, and spelling are accurate.
5. Incorporate creativity by writing in **sonnets** where appropriate, while retaining professionalism.
6. Avoid jargon unless explicitly required or beneficial for the audience.";

        string userInput = "Write a blog post about API design.";

        // Prepare the request payload
        var requestBodyObject = new
        {
            messages = new[]
            {
            new { role = "system", content = systemMessage },
            new { role = "user", content = userInput }
        },
            max_tokens = 2048,
            temperature = 0.8,
            top_p = 0.1,
            frequency_penalty = 0,
            presence_penalty = 0,
            stream = true // Enable streaming responses
        };

        string requestBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestBodyObject, Newtonsoft.Json.Formatting.Indented);

        Console.WriteLine("Request Body (Pretty Printed):");
        Console.WriteLine(requestBody);

        // Retrieve API key and endpoint from environment variables
        string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
        string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");
        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
        {
            throw new InvalidOperationException("API key or endpoint is not configured.");
        }

        using (var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
        })

        using (var client = new HttpClient(handler))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(apiEndpoint);

            try
            {
                // Set up the request content
                var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

                // Send POST request to the chat completions endpoint with streaming enabled
                HttpResponseMessage response = await client.PostAsync("/chat/completions", content).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var reader = new StreamReader(responseStream))
                    {
                        Console.WriteLine("Streaming Response:");

                        while (!reader.EndOfStream)
                        {
                            // Read each line from the response stream
                            var line = await reader.ReadLineAsync().ConfigureAwait(false);

                            // Skip empty lines or lines that don't start with "data:"
                            if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:")) continue;

                            var jsonData = line.Substring(5).Trim(); // Remove "data:" prefix
                            if (jsonData == "[DONE]") break; // End of the stream

                            try
                            {
                                // Parse JSON data and extract content
                                var chunk = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonData);
                                var deltaContent = chunk?.choices?[0]?.delta?.content;

                                if (deltaContent != null)
                                {
                                    Console.Write(deltaContent.ToString()); // Output content immediately
                                    Console.Out.Flush(); // Ensure real-time output
                                }
                            }
                            catch (Newtonsoft.Json.JsonException ex)
                            {
                                Console.WriteLine($"Error parsing JSON: {ex.Message}");
                            }
                        }
                    }
                }
                else
                {
                    Console.WriteLine($"The request failed with status code: {response.StatusCode}");
                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var formattedErrorJson = Newtonsoft.Json.JsonConvert.SerializeObject(Newtonsoft.Json.JsonConvert.DeserializeObject(responseContent), Newtonsoft.Json.Formatting.Indented);
                    Console.WriteLine($"Error Details (Formatted JSON):");
                    Console.WriteLine(formattedErrorJson);
                }
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                Console.WriteLine($"An error occurred while parsing JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }
    }

    public async Task HandleCohereChatCommandRAsyncStreaming()
    {
        // Step 1: Initialize the client with endpoint and API key
        var client = new ChatCompletionsClient(
            new Uri(Environment.GetEnvironmentVariable("COHERE_ENDPOINT")), // Ensure this environment variable is set
            new AzureKeyCredential(Environment.GetEnvironmentVariable("COHERE_KEY")) // Ensure this environment variable is set
        );

        try
        {
            // Step 2: Prepare the prompt template and user input
            string userInput = "How many languages are in the world?";
            var promptyTemplate = _promptyTemplate;
            Console.WriteLine($"promptyTemplate ----- : {promptyTemplate}");
            Console.WriteLine($"promptyTemplate ----- :");
            // Step 3: Create chat completion request options
            var requestOptions = new ChatCompletionsOptions
            {
                Messages = new List<ChatRequestMessage>
            {
                new ChatRequestSystemMessage(promptyTemplate),
                new ChatRequestUserMessage(userInput)
            },
                MaxTokens = 4096, // Limit response length
                Temperature = 0.7f // Adjust creativity
            };

            // Step 4: Send streaming chat completion request
            Console.WriteLine("Sending streaming chat completion request...");
            StreamingResponse<StreamingChatCompletionsUpdate> response = await client.CompleteStreamingAsync(requestOptions);

            // Step 5: Process the streaming response
            StringBuilder contentBuilder = new();
            string id = null;
            string model = null;
            var buffer = new StringBuilder(); // Buffer for partial responses
            var fullOutput = new StringBuilder(); // Buffer for accumulating the full response
            Console.WriteLine("---");

            await foreach (StreamingChatCompletionsUpdate partialResponse in response)
            {

                //Console.WriteLine($"ID: {chatUpdate.Id}");
                //Console.WriteLine($"Created: {chatUpdate.Created}");
                //Console.WriteLine($"Model: {chatUpdate.Model}");
                //Console.WriteLine($"ContentUpdate: {partialResponse.ContentUpdate}");
                buffer.Append(partialResponse.ContentUpdate);
                fullOutput.Append(partialResponse.ContentUpdate); // Accumulate the full response
                                                                  // Check if the buffer contains a complete sentence
                string currentText = buffer.ToString();
                int lastSentenceEnd = currentText.LastIndexOfAny(new[] { '.', '!', '?' });

                if (lastSentenceEnd >= 0)
                {
                    // Extract the complete sentence(s)
                    string completeSentences = currentText.Substring(0, lastSentenceEnd + 1);

                    // Print the complete sentence(s)
                    Console.Write(completeSentences);

                    // Remove the printed sentences from the buffer
                    buffer.Remove(0, lastSentenceEnd + 1);
                }
            }
            Console.WriteLine("---");
            Console.WriteLine($"fullOutput: {fullOutput}");
            Console.WriteLine("---");


        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 422)
            {
                Console.WriteLine($"Looks like the model doesn't support a parameter: {ex.Message}");
            }
            else
            {
                Console.WriteLine($"Request failed: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
    public async Task<string[]> HandleCohereQueryGenerationAsync(string input)
    {
        string system_message = @"## Task and Context 
        You are a query generation assistant.
        ## Style Guide
Please follow these guidelines to ensure high-quality output:
1. Use **US spelling** consistently throughout the text.
2. Maintain a professional and concise tone.
3. Structure queries clearly and ensure they are relevant to the input context.
4. Avoid redundancy and ensure each query is unique.";

        // Clean up input strings
        system_message = system_message.Replace("\r\n", " ").Replace("\n", " ");
        input = input.Replace("\r\n", " ").Replace("\n", " ");

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
        };

        using var client = new HttpClient(handler);
        try
        {
            // Construct request body for query generation
            string requestBody = @"
        {
            'messages': [
                {
                    'role': 'system',
                    'content': '{system_message}'
                },
                {
                    'role': 'user', 
                    'content': '{input}'
                }
            ],
            'max_tokens': 2048,
            'temperature': 0.8,
            'top_p': 0.1,
            'frequency_penalty': 0,
            'presence_penalty': 0,
            'seed': 369
        }"
              .Replace("'", "\"") // Replace single quotes with double quotes for valid JSON
              .Replace("{system_message}", system_message)
              .Replace("{input}", input); // Replace placeholders with actual values

            Console.WriteLine($"Request Body: {requestBody}");

            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(apiEndpoint);

            // Set up the request content
            var content = new StringContent(requestBody, Encoding.UTF8, "application/json");

            // Send POST request to generate queries
            HttpResponseMessage response = await client.PostAsync("/chat/completions", content).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Parse the response JSON to extract generated queries
                var responseJson = JsonConvert.DeserializeObject<dynamic>(result);
                var generatedQueries = new List<string>();

                foreach (var choice in responseJson.choices)
                {
                    if (choice.message != null && choice.message.content != null)
                    {
                        generatedQueries.Add(choice.message.content.ToString());
                    }
                }

                Console.WriteLine("Generated Queries:");
                foreach (var query in generatedQueries)
                {
                    Console.WriteLine(query);
                }

                return generatedQueries.ToArray();
            }
            else
            {
                Console.WriteLine($"The request failed with status code: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"Error Details: {responseContent}");
                return Array.Empty<string>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
            return Array.Empty<string>();
        }
    }


    // "How to connect with my teammates?"
    public async Task HandleCohereEmbedUpsertAsync()
    {
        // Define a list of input documents for embedding
        var documents = new List<string>
    {
        "Joining Slack Channels: You will receive an invite via email. Be sure to join relevant channels to stay informed and engaged.",
        "Finding Coffee Spots: For your caffeine fix, head to the break room's coffee machine or cross the street to the café for artisan coffee.",
        "Team-Building Activities: We foster team spirit with monthly outings and weekly game nights. Feel free to suggest new activity ideas anytime!",
        "Working Hours Flexibility: We prioritize work-life balance. While our core hours are 9 AM to 5 PM, we offer flexibility to adjust as needed."
    };

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
        };

        using var client = new HttpClient(handler);
        try
        {
            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }

            // Generate embeddings for the documents
            var vectors = (await GenerateDocumentsEmbeddingsCohereWithRetryAsync(apiKey, apiEndpoint, documents).ConfigureAwait(false)).ToArray();

            Console.WriteLine("Vectors:");
            for (int i = 0; i < vectors.Length; i++)
            {
                Console.WriteLine($"Vector {i + 1}: {string.Join(", ", vectors[i])}");
            }
            /*
            "id": "memoryKey-page1-counterBatchContent-i-D5",
                "type": "KnowledgeBaseItem",
                "tenantId": "1234",
                "userId": "5678",
                "categoryId": "Document",
                "partitionKey": "1234_5678_SampleCategory",
                "title": "Page 1",
                "content": "Joining Slack Channels: You will receive an invite via email. Be sure to join relevant channels to stay informed and engaged.",
                "referenceDescription": "SampleFile#page=1",
                "referenceLink": "SampleDestination#page=1",
                "createdAt": "2025-01-12T07:50:56.560731Z",
                "updatedAt": "2025-01-12T07:50:56.560748Z",
                "similarityScore": 0,
                "relevanceScore": 0,
                "vectors": [
                    -0.00088739395,
                    -0.020050049,
                    -0.016845703,
            */
            // Process embeddings and create KnowledgeBaseItem objects
            string tenantId = "1234";
            string userId = "5678";
            string categoryId = "Document";
            string fileName = "SampleFile";
            string destination = "SampleDestination";
            int index = 1;

            foreach (var vector in vectors)
            {
                string uniqueKey = $"memoryKey-page{index}-counterBatchContent-i-D5";

                // Create the KnowledgeBaseItem
                var knowledgeBaseItem = new KnowledgeBaseItem(
                    uniqueKey, // Assigning uniqueKey as the Id
                    tenantId: tenantId,
                    userId: userId,
                    categoryId: categoryId, // Use file name as the category
                    title: $"Page {index}",
                    content: documents[index - 1], // Use the corresponding document content
                    referenceDescription: $"{fileName}#page={index}",
                    referenceLink: $"{destination}#page={index}",
                    vectors: vector // Assign the embedding vector
                );

                Console.WriteLine($"Upserting KnowledgeBaseItem with Id: {uniqueKey}");
                // Upsert the KnowledgeBaseItem into Cosmos DB
                await _cosmosDbService.UpsertKnowledgeBaseItemAsync(
                    tenantId,
                    userId,
                    categoryId,
                    knowledgeBaseItem
                );

                Console.WriteLine($"Successfully upserted item with Id: {uniqueKey}");
                index++;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    public async Task HandleVectorStoreAsync()
    {
        string azureOpenAIChatDeploymentName = "gpt-4o";
        string azureEmbeddingDeploymentName = "text-embedding-3-large";
        string azureOpenAIEndpoint = GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT");
        string azureOpenAIKey = GetEnvironmentVariable("AZURE_OPENAI_KEY");
        string azureCosmosDBNoSQLConnectionString = GetEnvironmentVariable("COSMOS_DB_CONNECTION_STRING");
        string azureCosmosDBNoSQLDatabaseName = GetEnvironmentVariable("COSMOS_DB_DATABASE_ID");
        string ragCollectionName = "ragcontent";
        int azureEmbeddingDimensions = 1024;

        var kernelBuilder = Kernel.CreateBuilder();

        kernelBuilder.AddAzureOpenAIChatCompletion(
            azureOpenAIChatDeploymentName,
            azureOpenAIEndpoint,
            azureOpenAIKey);

        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            azureEmbeddingDeploymentName,
            azureOpenAIEndpoint,
            azureOpenAIKey,
            dimensions: azureEmbeddingDimensions);
        var kernel = kernelBuilder.Build();

        var textEmbeddingGeneration = kernel.GetRequiredService<ITextEmbeddingGenerationService>();
        // Construct an InMemory vector store.
        var vectorStore = new InMemoryVectorStore();
        var collectionName = "records";

        // Delegate which will create a record.
        static DataModel CreateRecord(string text, ReadOnlyMemory<float> embedding)
        {
            return new()
            {
                Key = Guid.NewGuid(),
                Text = text,
                Embedding = embedding
            };
        }
        // Create a record collection from a list of strings using the provided delegate.
        string[] lines =
        [
            "Semantic Kernel is a lightweight, open-source development kit that lets you easily build AI agents and integrate the latest AI models into your C#, Python, or Java codebase. It serves as an efficient middleware that enables rapid delivery of enterprise-grade solutions.",
            "Semantic Kernel is a new AI SDK, and a simple and yet powerful programming model that lets you add large language capabilities to your app in just a matter of minutes. It uses natural language prompting to create and execute semantic kernel AI tasks across multiple languages and platforms.",
            "In this guide, you learned how to quickly get started with Semantic Kernel by building a simple AI agent that can interact with an AI service and run your code. To see more examples and learn how to build more complex AI agents, check out our in-depth samples."
        ];
        var vectorizedSearch = await CreateCollectionFromListAsync<Guid, DataModel>(
            vectorStore, collectionName, lines, textEmbeddingGeneration, CreateRecord);

        /*
        Directly constructs VectorStoreTextSearch<DataModel> with the raw vectorizedSearch and textEmbeddingGeneration.
        Simpler but less flexible for adding custom processing layers.
        */
        // Create a text search instance using the InMemory vector store.
        var textSearch = new VectorStoreTextSearch<DataModel>(vectorizedSearch, textEmbeddingGeneration);
        await ExecuteSearchesAsync(textSearch);

        /*
        Adds a VectorizedSearchWrapper<DataModel> between vectorizedSearch and VectorStoreTextSearch<DataModel>.
        Offers additional flexibility and modularity, enabling easier changes or extensions in the vectorized search process.
        */
        // Create a text search instance using a vectorized search wrapper around the InMemory vector store.
        IVectorizableTextSearch<DataModel> vectorizableTextSearch = new VectorizedSearchWrapper<DataModel>(vectorizedSearch, textEmbeddingGeneration);
        textSearch = new VectorStoreTextSearch<DataModel>(vectorizableTextSearch);
        await ExecuteSearchesAsync(textSearch);
    }
    private async Task ExecuteSearchesAsync(VectorStoreTextSearch<DataModel> textSearch)
    {
        var query = "What is the Semantic Kernel?";

        // Search and return results as a string items
        KernelSearchResults<string> stringResults = await textSearch.SearchAsync(query, new() { Top = 2, Skip = 0 });
        Console.WriteLine("--- String Results ---\n");
        await foreach (string result in stringResults.Results)
        {
            Console.WriteLine(result);
            WriteHorizontalRule();
        }

        // Search and return results as TextSearchResult items
        KernelSearchResults<TextSearchResult> textResults = await textSearch.GetTextSearchResultsAsync(query, new() { Top = 2, Skip = 0 });
        Console.WriteLine("\n--- Text Search Results ---\n");
        await foreach (TextSearchResult result in textResults.Results)
        {
            Console.WriteLine($"Name:  {result.Name}");
            Console.WriteLine($"Value: {result.Value}");
            Console.WriteLine($"Link:  {result.Link}");
            WriteHorizontalRule();
        }

        // Search and returns results as DataModel items
        KernelSearchResults<object> fullResults = await textSearch.GetSearchResultsAsync(query, new() { Top = 2, Skip = 0 });
        Console.WriteLine("\n--- DataModel Results ---\n");
        await foreach (DataModel result in fullResults.Results)
        {
            Console.WriteLine($"Key:         {result.Key}");
            Console.WriteLine($"Text:        {result.Text}");
            Console.WriteLine($"Embedding:   {result.Embedding.Length}");
            WriteHorizontalRule();
        }
    }

    /// <summary>
    /// Delegate to create a record.
    /// </summary>
    /// <typeparam name="TKey">Type of the record key.</typeparam>
    /// <typeparam name="TRecord">Type of the record.</typeparam>
    internal delegate TRecord CreateRecord<TKey, TRecord>(string text, ReadOnlyMemory<float> vector) where TKey : notnull;

    /// <summary>
    /// Create a <see cref="IVectorStoreRecordCollection{TKey, TRecord}"/> from a list of strings by:
    /// 1. Creating an instance of <see cref="InMemoryVectorStoreRecordCollection{TKey, TRecord}"/>
    /// 2. Generating embeddings for each string.
    /// 3. Creating a record with a valid key for each string and it's embedding.
    /// 4. Insert the records into the collection.
    /// </summary>
    /// <param name="vectorStore">Instance of <see cref="IVectorStore"/> used to created the collection.</param>
    /// <param name="collectionName">The collection name.</param>
    /// <param name="entries">A list of strings.</param>
    /// <param name="embeddingGenerationService">A text embedding generation service.</param>
    /// <param name="createRecord">A delegate which can create a record with a valid key for each string and it's embedding.</param>
    internal static async Task<IVectorStoreRecordCollection<TKey, TRecord>> CreateCollectionFromListAsync<TKey, TRecord>(
        IVectorStore vectorStore,
        string collectionName,
        string[] entries,
        ITextEmbeddingGenerationService embeddingGenerationService,
        CreateRecord<TKey, TRecord> createRecord)
        where TKey : notnull
    {
        // Get and create collection if it doesn't exist.
        var collection = vectorStore.GetCollection<TKey, TRecord>(collectionName);
        await collection.CreateCollectionIfNotExistsAsync().ConfigureAwait(false);

        // Create records and generate embeddings for them.
        var tasks = entries.Select(entry => Task.Run(async () =>
        {
            var record = createRecord(entry, await embeddingGenerationService.GenerateEmbeddingAsync(entry).ConfigureAwait(false));
            await collection.UpsertAsync(record).ConfigureAwait(false);
        }));
        await Task.WhenAll(tasks).ConfigureAwait(false);

        return collection;
    }

    private const int HorizontalRuleLength = 80;

    /// <summary>
    /// Utility method to write a horizontal rule to the console.
    /// </summary>
    protected void WriteHorizontalRule()
        => Console.WriteLine(new string('-', HorizontalRuleLength));

    /// <summary>
    /// Decorator for a <see cref="IVectorizedSearch{TRecord}"/> that generates embeddings for text search queries.
    /// </summary>
    private sealed class VectorizedSearchWrapper<TRecord>(IVectorizedSearch<TRecord> vectorizedSearch, ITextEmbeddingGenerationService textEmbeddingGeneration) : IVectorizableTextSearch<TRecord>
    {
        /// <inheritdoc/>
        public async Task<VectorSearchResults<TRecord>> VectorizableTextSearchAsync(string searchText, VectorSearchOptions? options = null, CancellationToken cancellationToken = default)
        {
            var vectorizedQuery = await textEmbeddingGeneration!.GenerateEmbeddingAsync(searchText, cancellationToken: cancellationToken).ConfigureAwait(false);

            return await vectorizedSearch.VectorizedSearchAsync(vectorizedQuery, options, cancellationToken);
        }
    }

    /// <summary>
    /// Sample model class that represents a record entry.
    /// </summary>
    /// <remarks>
    /// Note that each property is decorated with an attribute that specifies how the property should be treated by the vector store.
    /// This allows us to create a collection in the vector store and upsert and retrieve instances of this class without any further configuration.
    /// </remarks>
    private sealed class DataModel
    {
        [VectorStoreRecordKey]
        [TextSearchResultName]
        public Guid Key { get; init; }

        [VectorStoreRecordData]
        [TextSearchResultValue]
        public string Text { get; init; }

        [VectorStoreRecordVector(1536)]
        public ReadOnlyMemory<float> Embedding { get; init; }
    }
    private static async Task<float[][]> GenerateDocumentsEmbeddingsCohereWithRetryAsync(
        string apiKey,
        string apiEndpoint,
        List<string> documents,
        CancellationToken cancellationToken = default)
    {
        const int maxRetries = 3; // Maximum number of retries
        const int retryDelayMilliseconds = 10_000; // Delay between retries in milliseconds
        int tries = 0;

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(apiEndpoint)
        };

        // Configure HttpClient headers
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

        while (tries < maxRetries)
        {
            try
            {
                // Prepare the embedding request payload
                var requestBody = new
                {
                    input = documents,
                    model = "embed-english-v3.0", // Specify the embedding model
                    embeddingTypes = new[] { "int8" }, // Use int8 embeddings
                    input_type = "document" // Specify input type as 'document'
                };

                // Serialize the request body
                string requestBodyJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);

                // Send the request
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/embeddings", content, cancellationToken).ConfigureAwait(false);

                // Handle response
                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    // Parse the response
                    var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
                    if (parsedResult?.data != null && parsedResult.data.Count > 0)
                    {
                        // Extract embeddings for all documents
                        var embeddings = new List<float[]>();
                        foreach (var data in parsedResult.data)
                        {
                            var embeddingArray = data?.embedding?.ToObject<List<float>>()?.ToArray();
                            if (embeddingArray != null)
                            {
                                embeddings.Add(embeddingArray);
                            }
                            else
                            {
                                throw new InvalidOperationException("One or more embeddings are null or could not be parsed.");
                            }
                        }

                        Console.WriteLine($"Total Embeddings Generated: {embeddings.Count}");
                        return embeddings.ToArray(); // Return all embeddings as a 2D array
                    }

                    throw new InvalidOperationException("Response data is null or empty.");
                }
                else
                {
                    // Log and handle non-successful responses
                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"Request failed with status code: {response.StatusCode}");
                    Console.WriteLine($"Error Details: {responseContent}");
                    throw new HttpRequestException($"Embedding request failed with status code {response.StatusCode}.");
                }
            }
            catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests && tries < maxRetries)
            {
                tries++;
                Console.WriteLine($"Rate limit reached. Retrying ({tries}/{maxRetries}) in {retryDelayMilliseconds / 1000} seconds...");
                await Task.Delay(retryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (Newtonsoft.Json.JsonException jsonEx)
            {
                Console.WriteLine($"Error parsing JSON response: {jsonEx.Message}");
                throw;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }

        throw new InvalidOperationException("Maximum retry attempts exceeded.");
    }

    private async Task SendEmbeddingRequestAsync(HttpClient client, object requestBody, string requestType)
    {
        try
        {
            string requestBodyJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
            //Console.WriteLine($"{requestType} Request Body:");
            Console.WriteLine(requestBodyJson);

            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);

                Console.WriteLine($"{requestType} Embedding Result:");
                Console.WriteLine(JsonConvert.SerializeObject(parsedResult, Formatting.Indented));

                if (parsedResult?.data != null && parsedResult.data.Count > 0)
                {
                    // Ensure embeddingArray is converted to List<double>
                    var embeddingArray = parsedResult.data[0]?.embedding?.ToObject<List<double>>();

                    if (embeddingArray != null)
                    {
                        // Get the first 10 elements manually
                        var embeddingPreview = new List<double>();
                        for (int i = 0; i < embeddingArray.Count && i < 10; i++)
                        {
                            embeddingPreview.Add(embeddingArray[i]);
                        }

                        Console.WriteLine($"{requestType} Embedding (First 10 Values): {string.Join(", ", embeddingPreview)}");
                        Console.WriteLine($"{requestType} Embedding Length: {embeddingArray.Count}");
                    }
                    else
                    {
                        Console.WriteLine($"{requestType} Embedding is null or could not be parsed.");
                    }
                }
                else
                {
                    Console.WriteLine($"{requestType} Response data is null or empty.");
                }
            }
            else
            {
                Console.WriteLine($"The {requestType} request failed with status code: {response.StatusCode}");
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"{requestType} Error Details: {responseContent}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred during {requestType} request: {ex.Message}");
        }
    }
    public async Task HandleInferenceStreamingAsync(string endpoint, string apiKey, string modelId)
    {
        // Initialize the custom tokenizer (e.g., for GPT-4)
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

        // Kernel builder setup
        var kernelBuilder = Kernel.CreateBuilder();
        kernelBuilder.AddAzureAIInferenceChatCompletion(
            endpoint: new Uri(endpoint),
            apiKey: apiKey,
            modelId: modelId
        );

        var kernel = kernelBuilder.Build();

        // Define the prompt template for general chat
        var promptyTemplate = _promptyTemplate;

        // Chat settings
        double temperature = 0.1;
        double topP = 0.1;
        int maxTokens = 4028;
        int seed = 356;

        var executionSettings = new AzureOpenAIPromptExecutionSettings
        {
            Temperature = temperature,
            PresencePenalty = temperature,
            FrequencyPenalty = temperature,
            TopP = topP,
            MaxTokens = maxTokens,
            Seed = seed
        };

        Console.WriteLine("General Chat Assistant. Type 'exit' to quit.");
        while (true)
        {
            Console.Write("Q: ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrEmpty(userInput) || userInput.ToLower() == "exit")
            {
                break;
            }

            // Calculate token count for the input using the custom tokenizer
            int inputTokenCount = tokenizer.CountTokens(userInput);
            Console.WriteLine($"CustomTokenizer: InputTokenCount: {inputTokenCount}");

            // Set arguments for execution
            var arguments = new KernelArguments(executionSettings)
            {
                ["input"] = userInput,
                ["context"] = userInput
            };

            var buffer = new StringBuilder(); // Buffer for partial responses
            var fullOutput = new StringBuilder(); // Buffer for accumulating the full response

            try
            {
                Console.Write($"{modelId}: ");
                //var paragraphWritingFunction = kernel.CreateFunctionFromPrompt(FunctionDefinition);
                var paragraphWritingFunction = new Kernel().CreateFunctionFromPrompty(promptyTemplate);

                // Streaming response from the AI model
                await foreach (var partialResponse in paragraphWritingFunction.InvokeStreamingAsync(kernel, arguments))
                {
                    buffer.Append(partialResponse);
                    fullOutput.Append(partialResponse); // Accumulate the full response

                    // Check if the buffer contains a complete sentence
                    string currentText = buffer.ToString();
                    int lastSentenceEnd = currentText.LastIndexOfAny(new[] { '.', '!', '?' });

                    if (lastSentenceEnd >= 0)
                    {
                        // Extract the complete sentence(s)
                        string completeSentences = currentText.Substring(0, lastSentenceEnd + 1);

                        // Print the complete sentence(s)
                        Console.Write(completeSentences);

                        // Remove the printed sentences from the buffer
                        buffer.Remove(0, lastSentenceEnd + 1);
                    }
                }

                Console.WriteLine(); // Add a new line after completing the streaming response

                // Calculate the output token count using the full accumulated response
                int outputTokenCount = tokenizer.CountTokens(fullOutput.ToString());

                // Print the final token counts
                Console.WriteLine($"CustomTokenizer: OutputTokenCount: {outputTokenCount}");
                Console.WriteLine($"CustomTokenizer: TotalTokenCount: {inputTokenCount + outputTokenCount}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }


    static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName)
            ?? throw new ArgumentNullException(variableName, $"{variableName} is not set in environment variables.");
    }
}