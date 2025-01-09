// Copyright (c) AITrailblazer. All rights reserved.

using System.Net;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Diagnostics;
using BlingFire;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Cosmos.Copilot.Models;

using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;


/// <summary>
/// Class that loads text from a PDF file into a vector store.
/// </summary>
/// <typeparam name="TKey">The type of the data model key.</typeparam>
public class DataLoader<TKey>(
    IVectorStoreRecordCollection<TKey, TextSnippet<TKey>> vectorStoreRecordCollection,
    ITextEmbeddingGenerationService textEmbeddingGenerationService,
    CosmosDbService cosmosDbService,
    IChatCompletionService chatCompletionService,
    ILogger<DataLoader<TKey>> logger) : IDataLoader where TKey : notnull
{
    private readonly IVectorStoreRecordCollection<TKey, TextSnippet<TKey>> _vectorStoreRecordCollection = vectorStoreRecordCollection;
    private readonly ITextEmbeddingGenerationService _textEmbeddingGenerationService = textEmbeddingGenerationService;
    private readonly CosmosDbService _cosmosDbService = cosmosDbService;
    private readonly ILogger<DataLoader<TKey>> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc/>
    public async Task LoadSinglePdf(
       string tenantId,
       string userId,
       string fileName,
       string directory,
       string blobName,
       string memoryKey,
       Stream fileStream,
       int batchSize,
       int betweenBatchDelayInMs,
       CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting PDF loading process for file: {FileName}", fileName);

        // Ensure the vector store collection exists.
        await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        // Extract sections from the PDF stream.
        var sections = LoadTextAndImagesFromStream(fileStream, cancellationToken);
        var batches = sections.Chunk(batchSize);
        int counterBatch = 1;
        int counterBatchContent = 1;

        foreach (var batch in batches)
        {
            _logger.LogInformation("Batch {BatchNumber}", counterBatch);

            foreach (var content in batch)
            {
                try
                {
                    _logger.LogInformation("Batch content {BatchNumber}, Page {PageNumber}, content.Text: {ContentText}", counterBatchContent, content.PageNumber, content.Text);

                    if (content.Text == null && content.Image != null)
                    {
                        // Convert image to text.
                        content.Text = await ConvertImageToTextWithRetryAsync(
                            chatCompletionService,
                            content.Image.Value,
                            cancellationToken
                        ).ConfigureAwait(false);
                    }

                    if (string.IsNullOrWhiteSpace(content.Text))
                    {
                        _logger.LogWarning("Skipped empty content for page {PageNumber}.", content.PageNumber);
                        continue;
                    }
                    if (true)
                    {
                        var allsentences = BlingFireUtils.GetSentences(content.Text);

                        var i = 1;
                        foreach (var sentence in allsentences)
                        {
                            //Console.WriteLine($"-->  sentence {++i} saved: {sentence}");

                            // Generate a unique key for the KnowledgeBaseItem.
                            string uniqueKey = $"{memoryKey}-page{content.PageNumber}-{counterBatchContent}-{i}-D5";
                            string destination = $"{tenantId}/{userId}/{directory}/{blobName}/{fileName}";
                            string categoryId = "Document";
                            var vectors = (await GenerateEmbeddingsWithRetryAsync(
                                textEmbeddingGenerationService,
                                sentence,//content.Text!,
                                cancellationToken: cancellationToken).ConfigureAwait(false)).ToArray();

                            // Create the KnowledgeBaseItem.
                            var knowledgeBaseItem = new KnowledgeBaseItem(
                                uniqueKey, // Assigning uniqueKey as the Id
                                tenantId: tenantId,
                                userId: userId,
                                categoryId: categoryId, // Use file name as the category
                                title: $"Page {content.PageNumber}",
                                content: sentence,//content.Text!,
                                referenceDescription: $"{fileName}#page={content.PageNumber}",
                                referenceLink: $"{destination}#page={content.PageNumber}",
                                vectors: vectors // Skipping embeddings generation for now
                            );

                            _logger.LogInformation("Upserting knowledge base item with Id {UniqueKey}.", uniqueKey);

                            // Upsert the KnowledgeBaseItem into Cosmos DB.
                            await _cosmosDbService.UpsertKnowledgeBaseItemAsync(
                                tenantId,
                                userId,
                                categoryId,
                                knowledgeBaseItem);
                            _logger.LogInformation("Successfully upserted item with Id {uniqueKey}.", uniqueKey);
                            i++;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing page {PageNumber} .", content.PageNumber);
                    // Continue to the next content in the batch.
                }

                counterBatchContent++;
            }
            counterBatch++;

            // Delay between batch processing to avoid overloading the system.
            _logger.LogInformation("Completed batch processing. Waiting for {Delay} ms before next batch.", betweenBatchDelayInMs);
            await Task.Delay(betweenBatchDelayInMs, cancellationToken).ConfigureAwait(false);
        }

        stopwatch.Stop();
        _logger.LogInformation("Completed PDF loading process for file: {FileName}. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", fileName, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
    }
    public async Task LoadPdf(
        string tenantId,
        string userId,
        string fileName,
        string directory,
        string blobName,
        string memoryKey,
        Stream fileStream,
        int batchSize,
        int betweenBatchDelayInMs,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation("Starting PDF loading process for file: {FileName}", fileName);

        // Ensure the vector store collection exists.
        await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        // Extract sections from the PDF stream.
        var sections = LoadTextAndImagesFromStream(fileStream, cancellationToken);
        var knowledgeBaseItems = new List<KnowledgeBaseItem>();

        foreach (var content in sections)
        {
            try
            {
                if (content.Text == null && content.Image != null)
                {
                    // Convert image to text.
                    content.Text = await ConvertImageToTextWithRetryAsync(
                        chatCompletionService,
                        content.Image.Value,
                        cancellationToken
                    ).ConfigureAwait(false);
                }

                if (string.IsNullOrWhiteSpace(content.Text))
                {
                    _logger.LogWarning("Skipped empty content for page {PageNumber}.", content.PageNumber);
                    continue;
                }

                // Split content into sentences
                var sentences = BlingFireUtils.GetSentences(content.Text);
                int sentenceCounter = 1;

                foreach (var sentence in sentences)
                {
                    string uniqueKey = $"{memoryKey}-page{content.PageNumber}-{sentenceCounter}-D5";
                    string destination = $"{tenantId}/{userId}/{directory}/{blobName}/{fileName}";
                    string categoryId = "Document";

                    var vectors = (await GenerateEmbeddingsWithRetryAsync(
                        textEmbeddingGenerationService,
                        sentence,
                        cancellationToken).ConfigureAwait(false)).ToArray();

                    var knowledgeBaseItem = new KnowledgeBaseItem(
                        uniqueKey,
                        tenantId: tenantId,
                        userId: userId,
                        categoryId: categoryId,
                        title: $"Page {content.PageNumber}",
                        content: sentence,
                        referenceDescription: $"{fileName}#page={content.PageNumber}",
                        referenceLink: $"{destination}#page={content.PageNumber}",
                        vectors: vectors
                    );

                    knowledgeBaseItems.Add(knowledgeBaseItem);
                    sentenceCounter++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content for page {PageNumber}.", content.PageNumber);
            }
        }

        // Perform bulk upserts
        _logger.LogInformation("Starting bulk upsert of KnowledgeBaseItems.");
        await _cosmosDbService.BulkUpsertKnowledgeBaseItemsAsync(
            tenantId,
            userId,
            "Document", // Category ID
            knowledgeBaseItems,
            batchSize,
            betweenBatchDelayInMs,
            cancellationToken
        );

        stopwatch.Stop();
        _logger.LogInformation("Completed PDF loading process for file: {FileName}. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", fileName, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
    }

    /// <summary>
    /// Reads the text and images from each page in the provided PDF stream.
    /// </summary>
    private static IEnumerable<RawContent> LoadTextAndImagesFromStream(Stream pdfStream, CancellationToken cancellationToken)
    {
        using (var document = PdfDocument.Open(pdfStream))
        {
            foreach (var page in document.GetPages())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    yield break;
                }

                foreach (var image in page.GetImages())
                {
                    if (image.TryGetPng(out var png))
                    {
                        yield return new RawContent { Image = png, PageNumber = page.Number };
                    }
                }
                // A text block typically represents a paragraph or section of text. In this case, a page.
                var blocks = DefaultPageSegmenter.Instance.GetBlocks(page.GetWords());
                foreach (var block in blocks)
                {
                    //Console.WriteLine($"==>  block {block.Text}");

                    if (cancellationToken.IsCancellationRequested)
                    {
                        yield break;
                    }
                    //var allsentences = BlingFireUtils.GetSentences(block.Text);

                    //var i = 0;
                    //foreach (var s in allsentences)
                    //{
                    //    Console.WriteLine($"-->  sentence {++i} saved: {s}");
                    //}

                    yield return new RawContent { Text = block.Text, PageNumber = page.Number };
                }
            }
        }
    }
    public async Task DeletePdf(
          string tenantId,
          string userId,
          string fileNamePrefix,
          string categoryId,
          int batchSize = 100,
          int betweenBatchDelayInMs = 100)
    {
        _logger.LogInformation("Starting deletion of PDF with prefix: {FileNamePrefix}", fileNamePrefix);

        try
        {
            await _cosmosDbService.DeleteItemsByIdPrefixAsync(
                idPrefix: fileNamePrefix,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId,
                batchSize: batchSize,
                betweenBatchDelayInMs: betweenBatchDelayInMs
            );

            _logger.LogInformation("Successfully completed deletion of PDF with prefix: {FileNamePrefix}", fileNamePrefix);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while deleting PDF with prefix: {FileNamePrefix}", fileNamePrefix);
            throw;
        }
    }
    /// <summary>
    /// Retries converting image to text with the chat completion service.
    /// </summary>
    private static async Task<string> ConvertImageToTextWithRetryAsync(
         IChatCompletionService chatCompletionService,
         ReadOnlyMemory<byte> imageBytes,
         CancellationToken cancellationToken)
    {
        var tries = 0;

        while (true)
        {
            try
            {
                var chatHistory = new ChatHistory();
                chatHistory.AddUserMessage([
                    new TextContent("What’s in this image?"),
                    new ImageContent(imageBytes, "image/png"),
                ]);
                var result = await chatCompletionService.GetChatMessageContentsAsync(chatHistory, cancellationToken: cancellationToken).ConfigureAwait(false);
                return string.Join("\n", result.Select(x => x.Content));
            }
            catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                tries++;

                if (tries < 3)
                {
                    Console.WriteLine($"Failed to generate text from image. Error: {ex}");
                    Console.WriteLine("Retrying text to image conversion...");
                    await Task.Delay(10_000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
        }
    }

    /// <summary>
    /// Add a simple retry mechanism to embedding generation.
    /// </summary>
    /// <param name="textEmbeddingGenerationService">The embedding generation service.</param>
    /// <param name="text">The text to generate the embedding for.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>The generated embedding.</returns>
    private static async Task<ReadOnlyMemory<float>> GenerateEmbeddingsWithRetryAsync(
        ITextEmbeddingGenerationService textEmbeddingGenerationService,
        string text,
        CancellationToken cancellationToken)
    {
        var tries = 0;

        while (true)
        {
            try
            {
                return await textEmbeddingGenerationService.GenerateEmbeddingAsync(text, cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (HttpOperationException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
            {
                tries++;

                if (tries < 3)
                {
                    Console.WriteLine($"Failed to generate embedding. Error: {ex}");
                    Console.WriteLine("Retrying embedding generation...");
                    await Task.Delay(10_000, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    throw;
                }
            }
        }
    }


    /// <summary>
    /// Represents the raw content (text or image) of a PDF page.
    /// </summary>
    private sealed class RawContent
    {
        public string? Text { get; set; }
        public ReadOnlyMemory<byte>? Image { get; set; }
        public int PageNumber { get; set; }
    }
}
