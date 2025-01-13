using Cosmos.Copilot.Models;
using Microsoft.Extensions.Logging; // Added for logging
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;

public class ChatService
{
    private readonly CosmosDbService _cosmosDbService;
    private readonly SemanticKernelService _semanticKernelService;
    private readonly int _maxConversationTokens;
    private readonly double _cacheSimilarityScore;
    private readonly int _knowledgeBaseMaxResults;
    private readonly ILogger<ChatService> _logger;

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
        ILogger<ChatService> logger) // Removed unused string parameters
    {
        _cosmosDbService = cosmosDbService ?? throw new ArgumentNullException(nameof(cosmosDbService));
        _semanticKernelService = semanticKernelService ?? throw new ArgumentNullException(nameof(semanticKernelService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Initialize default values
        _maxConversationTokens = 4096;
        _cacheSimilarityScore = 0.90;
        _knowledgeBaseMaxResults = 10;
    }

    /// <summary>
    /// Event to propagate status updates to the UI.
    /// </summary>
    public event Action<string>? StatusUpdated;

    private void NotifyStatusUpdate(string message)
    {
        StatusUpdated?.Invoke(message);
        _logger.LogInformation(message); // Log status updates
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
            if (string.IsNullOrWhiteSpace(promptText))
                throw new ArgumentException("Prompt text cannot be null or empty.", nameof(promptText));

            NotifyStatusUpdate("Generating embeddings for the prompt...");
            float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(promptText);

            NotifyStatusUpdate("Embeddings generated. Searching knowledge base...");
            string[] searchTerms = GenerateKeywords(promptText);
            List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                vectors: promptVectors,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                similarityScore: similarityScore,
                searchTerms: searchTerms
            );

            if (items == null || !items.Any())
            {
                NotifyStatusUpdate("No similar knowledge base items found.");
                return (string.Empty, null);
            }

            NotifyStatusUpdate($"{items.Count} similar knowledge base items found.");

            var completions = new List<string>();
            foreach (var item in items)
            {
                NotifyStatusUpdate($"Processing item: {item.Title}");

                var (generatedCompletion, _) = await _semanticKernelService.GetASAPQuick<KnowledgeBaseItem>(
                    input: promptText,
                    contextData: item
                );

                NotifyStatusUpdate($"Intermediate Completion for {item.Title}: {generatedCompletion}");
                completions.Add(generatedCompletion);
            }

            string combinedCompletion = string.Join("\n\n", completions);
            NotifyStatusUpdate("Completion generated for all knowledge base items.");
            return (combinedCompletion, items.First().Title);
        }
        catch (Exception ex)
        {
            NotifyStatusUpdate("Error generating knowledge base completion.");
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }

    
    public async Task<(string completion, string? title)> GetKnowledgeBaseStreamingCompletionAsync(
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
            if (string.IsNullOrWhiteSpace(promptText))
                throw new ArgumentException("Prompt text cannot be null or empty.", nameof(promptText));

            NotifyStatusUpdate("Generating embeddings for the prompt...");
            float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(promptText);

            NotifyStatusUpdate("Embeddings generated. Searching knowledge base...");
            string[] searchTerms = GenerateKeywords(promptText);
            List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                vectors: promptVectors,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                similarityScore: similarityScore,
                searchTerms: searchTerms
            );

            if (items == null || !items.Any())
            {
                NotifyStatusUpdate("No similar knowledge base items found.");
                return (string.Empty, null);
            }

            NotifyStatusUpdate($"{items.Count} similar knowledge base items found.");

            var completions = new List<string>();
            foreach (var item in items)
            {
                int currentIndex = items.IndexOf(item) + 1;
                NotifyStatusUpdate($"Processing item {currentIndex}/{items.Count}: {item.Title}");

                string generatedCompletion;

                try
                {
                    generatedCompletion = await ProcessStreamingItemAsync(promptText, item);
                    completions.Add(generatedCompletion);
                    NotifyStatusUpdate($"Finalized Completion for {item.Title}: {generatedCompletion}");
                }
                catch (Exception streamEx)
                {
                    NotifyStatusUpdate($"Error processing item {item.Title}: {streamEx.Message}");
                    _logger.LogError(streamEx, "Streaming error for item {Title}", item.Title);
                    completions.Add($"Error processing item {item.Title}. {streamEx.Message}");
                }
            }

            string combinedCompletion = string.Join("\n\n", completions);
            NotifyStatusUpdate("Completion generated for all knowledge base items.");
            return (combinedCompletion, items.First().Title);
        }
        catch (Exception ex)
        {
            NotifyStatusUpdate("Error generating knowledge base completion.");
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }
    private async Task<string> ProcessStreamingItemAsync(string promptText, KnowledgeBaseItem item)
    {
        StringBuilder completionBuilder = new StringBuilder();
        StringBuilder currentLineBuilder = new StringBuilder();
        var finalizedLines = new HashSet<string>(); // Use a HashSet to avoid duplicates

        await foreach (var partialResponse in _semanticKernelService.GetASAPQuickStreaming<KnowledgeBaseItem>(
            input: promptText,
            contextData: item))
        {
            currentLineBuilder.Append(partialResponse);

            if (partialResponse.Contains("Final response for") || partialResponse.EndsWith("."))
            {
                var finalizedLine = currentLineBuilder.ToString().Trim();

                // Add to finalized lines if it does not already exist
                if (finalizedLines.Add(finalizedLine))
                {
                    NotifyStatusUpdate($"Finalized Line: {finalizedLine}");
                }

                currentLineBuilder.Clear();
            }
            else
            {
                // Notify for streaming updates
                NotifyStatusUpdate(currentLineBuilder.ToString());
            }

            completionBuilder.Append(partialResponse);
        }

        // Add any remaining line
        if (currentLineBuilder.Length > 0)
        {
            var finalizedLine = currentLineBuilder.ToString().Trim();
            if (finalizedLines.Add(finalizedLine))
            {
                NotifyStatusUpdate($"Finalized Line: {finalizedLine}");
            }
        }

        return completionBuilder.ToString();
    }

    private async Task<string> ProcessStreamingItemAsync1(string promptText, KnowledgeBaseItem item)
    {
        StringBuilder completionBuilder = new StringBuilder();
        StringBuilder currentLineBuilder = new StringBuilder();
        var finalizedLines = new List<string>();

        await foreach (var partialResponse in _semanticKernelService.GetASAPQuickStreaming<KnowledgeBaseItem>(
            input: promptText,
            contextData: item))
        {
            // Append the partial response to the current line
            currentLineBuilder.Append(partialResponse);

            // Check if the "Final response for" signal is present
            if (partialResponse.Contains("Final response for"))
            {
                // Add the current line to the finalized lines
                finalizedLines.Add(currentLineBuilder.ToString());

                // Clear the current line builder
                currentLineBuilder.Clear();

                // Notify that the line is finalized
                NotifyStatusUpdate($"Finalized: {finalizedLines.Last()}");
            }
            else
            {
                // Update the current line dynamically
                NotifyStatusUpdate(currentLineBuilder.ToString());
            }

            // Add to the completion builder for final processing
            completionBuilder.Append(partialResponse);
        }

        // Add any remaining text in the current line builder as a finalized line
        if (currentLineBuilder.Length > 0)
        {
            finalizedLines.Add(currentLineBuilder.ToString());
            NotifyStatusUpdate($"Finalized: {finalizedLines.Last()}");
        }

        // Return the full completion
        return completionBuilder.ToString();
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

        var keywords = promptText
            .Split(new[] { ' ', ',', '.', ';', ':', '\n', '\t', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(word => word.Trim().ToLowerInvariant()) // Normalize to lowercase
            .Distinct() // Remove duplicates
            .Where(word => word.Length > 2) // Exclude very short words
            .ToArray();

        return keywords;
    }
}
