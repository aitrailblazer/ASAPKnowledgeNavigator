using ASAPKnowledgeNavigator.Web;
using ASAPKnowledgeNavigator.Web.Components;
using Microsoft.FluentUI.AspNetCore.Components;
using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureCosmosDBNoSQL;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.Prompty;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.ChatCompletion;
var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire client integrations.
builder.AddServiceDefaults();
// Add Razor components, Fluent UI, Razor Pages, and Blazor server-side with detailed error options.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor().AddCircuitOptions(options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.DetailedErrors = true;
    }
});
// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddFluentUIComponents();

builder.Services.AddOutputCache();

// Register SemanticKernelService
RegisterSemanticKernelService(builder.Services);

string azureCosmosDbEndpointUri = "https://aitrailblazer-asap.documents.azure.com:443/";
string AzureCosmosDBNoSQLDatabaseName = "asapdb";
string knowledgeBaseContainerName = "secrag"; // rag

builder.Services.AddSingleton<CosmosDbService>((provider) =>
{
    var logger = provider.GetRequiredService<ILogger<CosmosDbService>>(); // Retrieve the logger

    return new CosmosDbService(
        endpoint: azureCosmosDbEndpointUri ?? string.Empty,
        databaseName: AzureCosmosDBNoSQLDatabaseName ?? string.Empty,
        knowledgeBaseContainerName: knowledgeBaseContainerName ?? string.Empty,
        logger: logger // Pass the logger to the constructor
    );

});
// Create a cancellation token and source to pass to the application service to allow them
// to request a graceful application shutdown.
CancellationTokenSource appShutdownCancellationTokenSource = new();
CancellationToken appShutdownCancellationToken = appShutdownCancellationTokenSource.Token;
builder.Services.AddKeyedSingleton("AppShutdown", appShutdownCancellationTokenSource);

// Register Semantic Kernel and related services
RegisterKernelServices(builder.Services);
builder.Services.AddHttpClient<WeatherApiClient>(client =>
    {
        // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
        // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
        client.BaseAddress = new("https+http://apiservice");
    });

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();

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

static void RegisterKernelServices(IServiceCollection services)
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
            //maxConversationTokens: "4000",
            //cacheSimilarityScore: "0.5",
            logger: logger
        );
    });
}
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