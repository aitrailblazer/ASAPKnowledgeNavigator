using Cosmos.Copilot.Models;
using Microsoft.ML.Tokenizers;
using Microsoft.Extensions.Logging; // Added for logging
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using System.Text;

public class ChatService
{
    private readonly CosmosDbService _cosmosDbService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly int _maxConversationTokens;
    private readonly double _cacheSimilarityScore;
    private readonly int _knowledgeBaseMaxResults;
    private readonly ILogger<ChatService> _logger; // Logger instance

    /// <summary>
    /// Initializes a new instance of the <see cref="ChatService"/> class.
    /// </summary>
    /// <param name="cosmosDbService">The Cosmos DB service instance.</param>
    /// <param name="semanticKernelService">The semantic kernel service instance.</param>
    /// <param name="maxConversationTokens">The maximum number of conversation tokens.</param>
    /// <param name="cacheSimilarityScore">The cache similarity score threshold.</param>
    /// <param name="logger">The logger instance.</param>
    public ChatService(
        CosmosDbService cosmosDbService,
        SemanticKernelService semanticKernelService,
        string maxConversationTokens,
        string cacheSimilarityScore,
        ILogger<ChatService> logger) // Injected logger
    {
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _semanticKernelService = semanticKernelService ?? throw new ArgumentNullException(nameof(semanticKernelService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize max conversation tokens
        _maxConversationTokens = 4096;

        // Initialize cache similarity score
        _cacheSimilarityScore = 0.90;

        // Initialize knowledge base max results
        _knowledgeBaseMaxResults = 10;
    }

    /// <summary>
    /// Retrieves a knowledge base completion based on the provided parameters.
    /// </summary>
    /// <param name="tenantId">The tenant ID.</param>
    /// <param name="userId">The user ID.</param>
    /// <param name="categoryId">The category ID.</param>
    /// <param name="promptText">The prompt text to generate the completion.</param>
    /// <param name="similarityScore">The similarity score threshold for the completion.</param>
    /// <returns>A Task representing the asynchronous operation, with a tuple containing the completion and the title.</returns>
    public async Task<(string completion, string? title)> GetKnowledgeBaseCompletionAsync(
        string tenantId,
        string userId,
        string categoryId,
        string promptText,
        double similarityScore)
    {
        try
        {
            // Validate inputs
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantId));
            ArgumentNullException.ThrowIfNull(userId, nameof(userId));

            // Generate embeddings for the prompt
            float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(promptText);
            _logger.LogInformation("Embeddings generated for the prompt.");

            // Generate keywords from promptText
            string[] searchTerms = GenerateKeywords(promptText);

            // Search for knowledge base items
            List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                vectors: promptVectors,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                similarityScore: similarityScore,
                searchTerms: searchTerms
            );

            if (items == null || items.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return (string.Empty, null);
            }

            // Generate completions for all items and combine results
            var completions = new List<string>();

            foreach (var item in items)
            {
                _logger.LogInformation("Processing item: {Title}", item.Title);

                var (generatedCompletion, _) = await _semanticKernelService.GetASAPQuick<KnowledgeBaseItem>(
                    input: promptText,
                    contextData: item
                );
                // Log the intermediate generated completion
                _logger.LogInformation("Intermediate Completion for {Title}: {Completion}", item.Title, generatedCompletion);

                completions.Add(generatedCompletion);
            }

            // Combine all completions into a single result
            string combinedCompletion = string.Join("\n\n", completions);

            _logger.LogInformation("Completion generated for all knowledge base items.");

            // Return the combined result and title of the first item
            return (combinedCompletion, items.First().Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }

    /// <summary>
    /// Generates keywords from the provided prompt text.
    /// </summary>
    /// <param name="promptText">The prompt text to generate keywords from.</param>
    /// <returns>An array of keywords.</returns>
    private string[] GenerateKeywords(string promptText)
    {
        if (string.IsNullOrWhiteSpace(promptText))
            throw new ArgumentException("Prompt text cannot be null or empty.", nameof(promptText));

        // Tokenize the text: Split into words by common delimiters
        var keywords = promptText
            .Split(new[] { ' ', ',', '.', ';', ':', '\n', '\t', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim().ToLowerInvariant()) // Normalize to lowercase
            .Distinct() // Remove duplicates
            .Where(word => word.Length > 2) // Exclude very short words
            .ToArray();

        return keywords;
    }
}