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
    /// <param name="knowledgeBaseContainerName">Name of the knowledge base container to access.</param>
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

    /// <summary>
    /// Upserts a knowledge base item into the Cosmos DB.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="knowledgeBaseItem">The knowledge base item to upsert.</param>
    /// <returns>A Task representing the asynchronous operation.</returns>
    public async Task UpsertKnowledgeBaseItemAsync(
        string tenantId,
        string userId,
        string categoryId,
        KnowledgeBaseItem knowledgeBaseItem)
    {
        _logger.LogInformation("Upserting knowledge base item with ID: {KnowledgeBaseItemId} in category: {Category}", knowledgeBaseItem.Id, categoryId);

        // Generate a partition key using the category as the primary identifier
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
    public async Task BulkUpsertKnowledgeBaseItemsAsync(
        string tenantId,
        string userId,
        string categoryId,
        IEnumerable<KnowledgeBaseItem> knowledgeBaseItems,
        int batchSize = 100, // Default batch size
        int betweenBatchDelayInMs = 100, // Delay between batches to avoid throttling
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Bulk upserting knowledge base items in category: {Category}", categoryId);

        // Generate a partition key using the category as the primary identifier
        PartitionKey partitionKey = new PartitionKeyBuilder()
            .Add(tenantId)
            .Add(userId)
            .Add(categoryId)
            .Build();

        // Chunk the items into batches
        var batches = knowledgeBaseItems.Chunk(batchSize);
        int batchCounter = 1;

        foreach (var batch in batches)
        {
            _logger.LogInformation("Processing batch {BatchNumber} with {ItemCount} items.", batchCounter, batch.Count());

            var tasks = new List<Task>();

            foreach (var item in batch)
            {
                tasks.Add(UpsertItemWithRetryAsync(item, partitionKey, categoryId, cancellationToken));
            }

            try
            {
                // Execute all upsert tasks in parallel
                await Task.WhenAll(tasks).ConfigureAwait(false);
                _logger.LogInformation("Successfully upserted batch {BatchNumber}.", batchCounter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch {BatchNumber} upserts.", batchCounter);
            }

            // Delay between batches to avoid throttling
            _logger.LogInformation("Waiting for {Delay} ms before next batch.", betweenBatchDelayInMs);
            await Task.Delay(betweenBatchDelayInMs, cancellationToken).ConfigureAwait(false);

            batchCounter++;
        }

        _logger.LogInformation("Completed bulk upserting of all items in category: {Category}", categoryId);
    }

    private async Task UpsertItemWithRetryAsync(
        KnowledgeBaseItem item,
        PartitionKey partitionKey,
        string categoryId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Upsert the knowledge base item into the specified container
            ItemResponse<KnowledgeBaseItem> response = await _knowledgeBaseContainer.UpsertItemAsync(
                item: item,
                partitionKey: partitionKey,
                cancellationToken: cancellationToken
            ).ConfigureAwait(false);

            _logger.LogInformation("Upserted KnowledgeBaseItem: {KnowledgeBaseItemId} (Title: {Title}) in category: {Category}.",
                response.Resource.Id, response.Resource.Title, categoryId);
        }
        catch (CosmosException ex)
        {
            _logger.LogError(ex, "Cosmos DB exception while upserting item {KnowledgeBaseItemId} in category: {Category}.", item.Id, categoryId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while upserting item {KnowledgeBaseItemId} in category: {Category}.", item.Id, categoryId);
            throw;
        }
    }
    public async Task DeleteItemsByIdPrefixAsync(
        string idPrefix,
        string tenantId,
        string userId,
        string categoryId,
        int batchSize = 100,
        int betweenBatchDelayInMs = 100)
    {
        _logger.LogInformation("Starting deletion of items with ID prefix: {IdPrefix}", idPrefix);

        // Create a SQL query to find items with the specified prefix
        var query = new QueryDefinition("SELECT c.id FROM c WHERE STARTSWITH(c.id, @idPrefix)")
            .WithParameter("@idPrefix", idPrefix);

        var partitionKey = new PartitionKeyBuilder()
            .Add(tenantId)
            .Add(userId)
            .Add(categoryId)
            .Build();

        var itemsToDelete = new List<string>();
        using var iterator = _knowledgeBaseContainer.GetItemQueryIterator<dynamic>(query, requestOptions: new QueryRequestOptions
        {
            PartitionKey = partitionKey
        });
        CancellationToken cancellationToken = default;
        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync(cancellationToken);
            foreach (var item in response)
            {
                itemsToDelete.Add(item.id.ToString());
            }
        }

        _logger.LogInformation("Found {ItemCount} items to delete.", itemsToDelete.Count);

        // Delete items in batches
        var batches = itemsToDelete.Chunk(batchSize);
        int batchCounter = 1;

        foreach (var batch in batches)
        {
            var tasks = batch.Select(id => DeleteItemByIdAsync(id, partitionKey, cancellationToken)).ToList();

            try
            {
                await Task.WhenAll(tasks);
                _logger.LogInformation("Successfully deleted batch {BatchNumber}.", batchCounter);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during batch {BatchNumber} deletion.", batchCounter);
            }

            // Delay between batches to avoid throttling
            _logger.LogInformation("Waiting for {Delay} ms before next batch.", betweenBatchDelayInMs);
            await Task.Delay(betweenBatchDelayInMs, cancellationToken);

            batchCounter++;
        }

        _logger.LogInformation("Completed deletion of items with ID prefix: {IdPrefix}.", idPrefix);
    }

    private async Task DeleteItemByIdAsync(string id, PartitionKey partitionKey, CancellationToken cancellationToken)
    {
        try
        {
            await _knowledgeBaseContainer.DeleteItemAsync<dynamic>(id, partitionKey, cancellationToken: cancellationToken);
            _logger.LogInformation("Deleted item with ID: {Id}.", id);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Item with ID: {Id} not found during deletion.", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting item with ID: {Id}.", id);
        }
    }

    /// <summary>
    /// Searches the knowledge base for items matching the provided criteria.
    /// </summary>
    /// <param name="vectors">The vector embeddings for the search.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="similarityScore">The similarity score threshold for the search.</param>
    /// <param name="searchTerms">The search terms to use in the query.</param>
    /// <returns>A Task representing the asynchronous operation, with a list of matching knowledge base items.</returns>
    public async Task<List<KnowledgeBaseItem>> SearchKnowledgeBaseAsync(
        float[] vectors,
        string tenantId,
        string userId,
        string? categoryId,
        double similarityScore,
        string[] searchTerms)
    {
        _logger.LogInformation(
            "Searching knowledge base items for TenantId={TenantId}, UserId={UserId}, CategoryId={CategoryId}",
            tenantId, userId, categoryId ?? "None"
        );

        // Construct CONTAINS conditions for search terms
        string containsConditions = string.Join(" OR ", searchTerms.Select((term, index) => $"CONTAINS(c.content, @term{index}, true)"));

        // Construct the SQL query
        string queryText = $@"
        SELECT TOP 10 c.id, c.tenantId, c.userId, c.categoryId, c.title, c.content, c.referenceDescription, c.referenceLink, 
               VectorDistance(c.vectors, @vectors) AS similarityScore
        FROM c
        WHERE c.type = 'KnowledgeBaseItem'
        AND c.tenantId = @tenantId
        AND c.userId = @userId
        AND ({containsConditions})
    ";

        if (!string.IsNullOrEmpty(categoryId))
        {
            queryText += " AND c.categoryId = @categoryId";
        }

        queryText += " ORDER BY VectorDistance(c.vectors, @vectors)";

        _logger.LogInformation("Executing query: {QueryText}", queryText);

        // Define query parameters
        var queryDef = new QueryDefinition(queryText)
            .WithParameter("@vectors", vectors)
            .WithParameter("@tenantId", tenantId)
            .WithParameter("@userId", userId);

        if (!string.IsNullOrEmpty(categoryId))
        {
            queryDef = queryDef.WithParameter("@categoryId", categoryId);
        }

        for (int i = 0; i < searchTerms.Length; i++)
        {
            queryDef = queryDef.WithParameter($"@term{i}", searchTerms[i]);
        }

        // Execute the query and collect results
        var results = new List<KnowledgeBaseItem>();
        using FeedIterator<KnowledgeBaseItem> resultSet = _knowledgeBaseContainer.GetItemQueryIterator<KnowledgeBaseItem>(queryDef);

        while (resultSet.HasMoreResults)
        {
            FeedResponse<KnowledgeBaseItem> response = await resultSet.ReadNextAsync();
            // Log each retrieved item (debugging)
            foreach (var item in response)
            {
                _logger.LogInformation(
                    "Retrieved KnowledgeBaseItem - ID: {Id}, Title: {Title}, Similarity Score: {SimilarityScore}",
                    item.Id, item.Title, item.SimilarityScore
                );
            }
            results.AddRange(response); // Add all items from the current page to the results list
        }

        _logger.LogInformation("SearchKnowledgeBaseAsync Found {Count} knowledge base items.", results.Count);
        return results;
    }

    /// <summary>
    /// Searches the knowledge base for the closest item matching the provided criteria.
    /// </summary>
    /// <param name="vectors">The vector embeddings for the search.</param>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="similarityScore">The similarity score threshold for the search.</param>
    /// <param name="searchTerms">The search terms to use in the query.</param>
    /// <returns>A Task representing the asynchronous operation, with the closest matching knowledge base item.</returns>
    public async Task<KnowledgeBaseItem?> SearchKnowledgeBaseAsync01(
        float[] vectors,
        string tenantId,
        string userId,
        string? categoryId,
        double similarityScore,
        string[] searchTerms)
    {
        _logger.LogInformation(
            "Searching closest knowledge base item with similarity score > {SimilarityScore}, for TenantId={TenantId}, UserId={UserId}, and CategoryId={CategoryId}",
            similarityScore, tenantId, userId, categoryId ?? "None"
        );

        // Join search terms for logging
        string searchTermsLiteral = string.Join(", ", searchTerms.Select(term => $"\"{term}\""));
        _logger.LogInformation($"SearchKnowledgeBaseAsync searchTermsLiteral: {searchTermsLiteral}");

        // Construct CONTAINS conditions for search terms (case-insensitive)
        string containsConditions = string.Join(" OR ", searchTerms.Select((term, index) => $"CONTAINS(c.content, @term{index}, true)"));
        _logger.LogInformation($"SearchKnowledgeBaseAsync containsConditions: {containsConditions}");

        try
        {
            // Construct the SQL query with optional categoryId filtering
            string queryText = $@"
            SELECT TOP 10
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
            AND ({containsConditions})
        ";

            // Add optional category filter dynamically
            if (!string.IsNullOrEmpty(categoryId))
            {
                queryText += " AND c.categoryId = @categoryId";
            }

            // Append ordering by similarity score
            queryText += " ORDER BY VectorDistance(c.vectors, @vectors)";

            _logger.LogInformation($"SearchKnowledgeBaseAsync queryText: {queryText}");

            // Create the query definition and add parameters
            var queryDef = new QueryDefinition(queryText)
                .WithParameter("@vectors", vectors)
                .WithParameter("@tenantId", tenantId)
                .WithParameter("@userId", userId);

            // Add categoryId parameter if applicable
            if (!string.IsNullOrEmpty(categoryId))
            {
                queryDef = queryDef.WithParameter("@categoryId", categoryId);
            }

            // Dynamically bind search terms
            for (int i = 0; i < searchTerms.Length; i++)
            {
                queryDef = queryDef.WithParameter($"@term{i}", searchTerms[i]);
            }
            // Log the query text
            _logger.LogInformation("Final Query Text: {QueryText}", queryText);

            // Manually log the parameters, excluding @vectors
            var parameters = new Dictionary<string, object>
            {
                { "@tenantId", tenantId },
                { "@userId", userId }
            };

            if (!string.IsNullOrEmpty(categoryId))
            {
                parameters.Add("@categoryId", categoryId);
            }

            // Add search term parameters
            for (int i = 0; i < searchTerms.Length; i++)
            {
                parameters.Add($"@term{i}", searchTerms[i]);
            }

            // Log all parameters except @vectors
            foreach (var parameter in parameters)
            {
                _logger.LogInformation("Parameter: {Key} = {Value}", parameter.Key, parameter.Value);
            }

            // Execute the query
            using FeedIterator<KnowledgeBaseItem> resultSet = _knowledgeBaseContainer.GetItemQueryIterator<KnowledgeBaseItem>(queryDef);

            while (resultSet.HasMoreResults)
            {
                FeedResponse<KnowledgeBaseItem> response = await resultSet.ReadNextAsync();

                // Log each retrieved item (debugging)
                foreach (var item in response)
                {
                    _logger.LogInformation(
                        "Retrieved KnowledgeBaseItem - ID: {Id}, Title: {Title}, Similarity Score: {SimilarityScore}",
                        item.Id, item.Title, item.SimilarityScore
                    );
                }

                // Return the closest item
                var closestItem = response.FirstOrDefault();
                if (closestItem != null)
                {
                    _logger.LogInformation(
                        "Found the most relevant knowledge base item with ID: {Id}, Title: {Title}, Similarity Score: {SimilarityScore}.",
                        closestItem.Id, closestItem.Title, closestItem.SimilarityScore
                    );
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