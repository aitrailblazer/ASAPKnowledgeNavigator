
using Cosmos.Copilot.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Identity;
using Newtonsoft.Json;
using System.Text.Json;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Microsoft.Azure.Cosmos.Linq;


/// <summary>
/// Service to access Azure Cosmos DB for NoSQL.
/// </summary>
public class CosmosDbService
{
    private readonly Container _knowledgeBaseContainer;
    private readonly ILogger<CosmosDbService> _logger;

    /// <summary>
    /// Creates a new instance of the service.
    /// </summary>
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="databaseName">Name of the database to access.</param>
    /// <param name="chatContainerName">Name of the chat container to access.</param>
    /// <param name="cacheContainerName">Name of the cache container to access.</param>
    /// <param name="productContainerName">Name of the product container to access.</param>
    /// <param name="productDataSourceURI">URI to the product data source.</param>
    /// <param name="logger">Logger instance for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when any required parameter is null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a service client instance.
    /// </remarks>
    public CosmosDbService(
        string endpoint,
        string databaseName,
        string knowledgeBaseContainerName,
        ILogger<CosmosDbService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logger.LogInformation("Initializing CosmosDbService.");


        CosmosSerializationOptions options = new()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        };

        try
        {
            _logger.LogInformation("Creating CosmosClient with endpoint: {Endpoint}", endpoint);
            TokenCredential credential = new DefaultAzureCredential();
            CosmosClient client = new CosmosClientBuilder(endpoint, credential)
                .WithSerializerOptions(options)
                .Build();

            _logger.LogInformation("Retrieving database: {DatabaseName}", databaseName);
            Database database = client.GetDatabase(databaseName) ?? throw new ArgumentException("Database not found.");

            _knowledgeBaseContainer = database.GetContainer(knowledgeBaseContainerName); // Initialize knowledge base container

            _logger.LogInformation("CosmosDbService initialized successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing CosmosDbService.");
            throw;
        }
    }
    /// <summary>
    /// Helper function to generate a hierarchical partition key based on tenantId, userId, and categoryId.
    /// All parameters are required and will be included in the partition key, even if they are empty strings.
    /// </summary>
    /// <param name="tenantId">Id of Tenant.</param>
    /// <param name="userId">Id of User.</param>
    /// <param name="categoryId">Category Id of the item.</param>
    /// <returns>Newly created partition key.</returns>
    public static PartitionKey GetPK(
        string tenantId,
        string userId,
        string threadId = null)
    {
        var partitionKeyBuilder = new PartitionKeyBuilder()
            .Add(tenantId ?? string.Empty)   // Add tenantId, defaulting to empty if null
            .Add(userId ?? string.Empty);    // Add userId, defaulting to empty if null

        // Only add threadId if it is not null or empty
        if (!string.IsNullOrEmpty(threadId))
        {
            partitionKeyBuilder.Add(threadId);
        }

        return partitionKeyBuilder.Build();
    }
    public async Task UpsertKnowledgeBaseItemAsync(
        string tenantId,
        string userId,
        string categoryId,
        KnowledgeBaseItem knowledgeBaseItem)
    {
        _logger.LogInformation("Upserting knowledge base item with ID: {KnowledgeBaseItemId} in category: {Category}", knowledgeBaseItem.Id, categoryId);

        // Generate a partition key using the category as the primary identifier
        //PartitionKey partitionKey = GetPK(tenantId, userId, categoryId);
        PartitionKey partitionKey = new PartitionKeyBuilder()
                        .Add(tenantId)
                        .Add(userId)
                        .Add(categoryId)
                        .Build();
        try
        {
            // Upsert the knowledge base item into the specified container
            ItemResponse<KnowledgeBaseItem> response = await _knowledgeBaseContainer.UpsertItemAsync(
                item: knowledgeBaseItem,
                partitionKey: partitionKey);

            _logger.LogInformation("Upserted KnowledgeBaseItem: {KnowledgeBaseItemId} (Title: {Title}) in category: {Category}",
                response.Resource.Id, response.Resource.Title, categoryId);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB Exception for KnowledgeBaseItem ID {KnowledgeBaseItemId} in category: {Category}", knowledgeBaseItem.Id, categoryId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while upserting knowledge base item with ID: {KnowledgeBaseItemId}", knowledgeBaseItem.Id);
            throw;
        }
    }
  
    public async Task<KnowledgeBaseItem?> SearchKnowledgeBaseAsync(
          float[] vectors,
          string tenantId,
          string userId,
          string categoryId,
          double similarityScore,
          string[] searchTerms)
    {
        _logger.LogInformation("Searching closest knowledge base item with similarity score > {SimilarityScore}, for TenantId={TenantId}, UserId={UserId}, and CategoryId={CategoryId}",
            similarityScore, tenantId, userId, categoryId ?? "None");
        // Join search terms into a comma-separated string of quoted literals
        string searchTermsLiteral = string.Join(", ", searchTerms.Select(term => $"\"{term}\""));
        _logger.LogInformation($"SearchKnowledgeBaseAsync searchTermsLiteral: {searchTermsLiteral}");
        try
        {
            // Construct SQL query with optional categoryId filtering
            string queryText = $"""
            SELECT TOP 1
                c.id,
                c.tenantId,
                c.userId,
                c.categoryId,
                c.title,
                c.content,
                c.referenceDescription,
                c.referenceLink,
                VectorDistance(c.vectors, @vectors) AS similarityScore
            FROM c
            WHERE c.type = 'KnowledgeBaseItem'
            AND c.tenantId = @tenantId
            AND c.userId = @userId
            AND FullTextContains(c.text, "Vegas")
            """;

            if (!string.IsNullOrEmpty(categoryId))
            {
                queryText += " AND c.categoryId = @categoryId";
            }

            // Append ORDER BY RANK clause with RRF function
            queryText += " ORDER BY VectorDistance(c.vectors, @vectors)";

            // Set up a query definition with parameters

            _logger.LogInformation($"SearchKnowledgeBaseAsync queryText: {queryText}");

            var queryDef = new QueryDefinition(queryText)
                .WithParameter("@vectors", vectors)
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@userId", userId);

            if (!string.IsNullOrEmpty(categoryId))
            {
                queryDef = queryDef.WithParameter("@categoryId", categoryId);
            }

            using FeedIterator<KnowledgeBaseItem> resultSet = _knowledgeBaseContainer.GetItemQueryIterator<KnowledgeBaseItem>(queryDef);

            if (resultSet.HasMoreResults)
            {
                FeedResponse<KnowledgeBaseItem> response = await resultSet.ReadNextAsync();
                var closestItem = response.FirstOrDefault();

                if (closestItem != null)
                {
                    _logger.LogInformation(
                        "Found the most relevant knowledge base item with ID: {Id}, Title: {Title}, Similarity Score: {SimilarityScore}, Relevance Score: {RelevanceScore}.",
                        closestItem.Id, closestItem.Title, closestItem.SimilarityScore, closestItem.RelevanceScore);
                    return closestItem;
                }
            }

            _logger.LogInformation("No knowledge base items found within the similarity threshold.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching for the closest knowledge base item.");
            throw;
        }
    }

 }