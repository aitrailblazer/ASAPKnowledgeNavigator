// Copyright (c) AITrailblazer. All rights reserved.

using System.Net;
using Microsoft.Extensions.VectorData;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.Cosmos;
using System.Net.Http.Headers;
using System.Diagnostics;
using BlingFire;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Cosmos.Copilot.Models;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text; // For Encoding
using Newtonsoft.Json; // For JsonConvert and Formatting
using System.Linq;

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
                    // Generate a unique key for the KnowledgeBaseItem.
                    string destination = $"{tenantId}/{userId}/{directory}/{blobName}/{fileName}";
                    string categoryId = "Document";
                    string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                    string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");

                    if (true)
                    {
                        var allsentences = BlingFireUtils.GetSentences(content.Text);

                        var i = 1;
                        foreach (var sentence in allsentences)
                        {
                            //Console.WriteLine($"-->  sentence {++i} saved: {sentence}");
                            string uniqueKey = $"{memoryKey}-page{content.PageNumber}-{counterBatchContent}-{i}-D5";
                            _logger.LogInformation($"uniqueKey: {uniqueKey}");

                            var vectors = (await GenerateEmbeddingsCohereWithRetryAsync(
                                apiKey,
                                apiEndpoint,
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
        var stopwatch2 = Stopwatch.StartNew();

        _logger.LogInformation("Starting PDF loading process for file: {FileName}", fileName);
        // Ensure the vector store collection exists.
        await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        var knowledgeBaseItems = new List<KnowledgeBaseItem>();

        // Extract sections from the PDF stream.
        var sections = LoadTextAndImagesFromStream(fileStream, cancellationToken);

        foreach (var content in sections)
        {
            var stopwatch1 = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing page {PageNumber}.", content.PageNumber);

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

                    string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                    string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");
                    //_logger.LogInformation($"uniqueKey: {uniqueKey}");
                    // 
                    var vectors = (await GenerateEmbeddingsWithRetryAsync(
                        textEmbeddingGenerationService,
                        sentence,//content.Text!,
                        cancellationToken: cancellationToken).ConfigureAwait(false)).ToArray();

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
                stopwatch1.Stop();
                _logger.LogInformation("Completed Processing of the page, {sentenceCounter} sentences: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", sentenceCounter, stopwatch1.Elapsed.Minutes, stopwatch1.Elapsed.Seconds);

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content for page {PageNumber}.", content.PageNumber);
            }
        }
        stopwatch2.Stop();
        _logger.LogInformation("Completed Processing of all pages: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch2.Elapsed.Minutes, stopwatch2.Elapsed.Seconds);

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
    public async Task LoadPdfCohere(
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
        var stopwatch2 = Stopwatch.StartNew();

        _logger.LogInformation("Starting PDF loading process for file: {FileName}", fileName);
        // Ensure the vector store collection exists.
        await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        var knowledgeBaseItems = new List<KnowledgeBaseItem>();

        // Extract sections from the PDF stream.
        var sections = LoadTextAndImagesFromStream(fileStream, cancellationToken);

        foreach (var content in sections)
        {
            var stopwatch1 = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing page {PageNumber}.", content.PageNumber);

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

                content.Text = SanitizeString(content.Text);
                // Split content into sentences
                var sentences = BlingFireUtils.GetSentences(content.Text).ToArray();


                string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");
                _logger.LogInformation("GenerateArrayOfEmbeddingsCohereWithRetryAsync");

                // Generate embeddings for all sentences in one request
                var embeddings = await GenerateArrayOfEmbeddingsCohereWithRetryAsync(
                    apiKey,
                    apiEndpoint,
                    sentences, // Array of sentences
                    cancellationToken
                );

                // Create KnowledgeBaseItem for each sentence and embedding
                for (int i = 0; i < sentences.Length; i++)
                {
                    string sentence = sentences[i];
                    var vectors = embeddings[i].ToArray();

                    string uniqueKey = $"{memoryKey}-page{content.PageNumber}-{i + 1}-D5";
                    string destination = $"{tenantId}/{userId}/{directory}/{blobName}/{fileName}";
                    string categoryId = "Document";

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
                }

                stopwatch1.Stop();
                _logger.LogInformation("Completed processing of page {PageNumber}, {SentenceCount} sentences. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", content.PageNumber, sentences.Length, stopwatch1.Elapsed.Minutes, stopwatch1.Elapsed.Seconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content for page {PageNumber}.", content.PageNumber);
            }
        }
    }
   public async Task EDGARLoadPdfCohere(
        string form,
        string ticker,
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
        var stopwatch2 = Stopwatch.StartNew();

        _logger.LogInformation("Starting PDF loading process for file: {FileName}", fileName);
        // Ensure the vector store collection exists.
        await _vectorStoreRecordCollection.CreateCollectionIfNotExistsAsync(cancellationToken).ConfigureAwait(false);

        var EDGARknowledgeBaseItems = new List<EDGARKnowledgeBaseItem>();

        // Extract sections from the PDF stream.
        var sections = LoadTextAndImagesFromStream(fileStream, cancellationToken);

        foreach (var content in sections)
        {
            var stopwatch1 = Stopwatch.StartNew();

            try
            {
                _logger.LogInformation("Processing page {PageNumber}.", content.PageNumber);

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

                content.Text = SanitizeString(content.Text);
                // Split content into sentences
                var sentences = BlingFireUtils.GetSentences(content.Text).ToArray();


                string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");
                _logger.LogInformation("GenerateArrayOfEmbeddingsCohereWithRetryAsync");

                // Generate embeddings for all sentences in one request
                var embeddings = await GenerateArrayOfEmbeddingsCohereWithRetryAsync(
                    apiKey,
                    apiEndpoint,
                    sentences, // Array of sentences
                    cancellationToken
                );

                // Create KnowledgeBaseItem for each sentence and embedding
                for (int i = 0; i < sentences.Length; i++)
                {
                    string sentence = sentences[i];
                    var vectors = embeddings[i].ToArray();

                    string uniqueKey = $"{memoryKey}-page{content.PageNumber}-{i + 1}-D5";
                    string destination = $"{form}/{ticker}/{directory}/{blobName}/{fileName}";
                    string categoryId = "Document";

                    var EDGARknowledgeBaseItem = new EDGARKnowledgeBaseItem(
                        uniqueKey,
                        form: form,
                        ticker: ticker,
                        categoryId: categoryId,
                        title: $"Page {content.PageNumber}",
                        content: sentence,
                        referenceDescription: $"{fileName}#page={content.PageNumber}",
                        referenceLink: $"{destination}#page={content.PageNumber}",
                        vectors: vectors
                    );

                    EDGARknowledgeBaseItems.Add(EDGARknowledgeBaseItem);
                }

                stopwatch1.Stop();
                _logger.LogInformation("Completed processing of page {PageNumber}, {SentenceCount} sentences. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", content.PageNumber, sentences.Length, stopwatch1.Elapsed.Minutes, stopwatch1.Elapsed.Seconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing content for page {PageNumber}.", content.PageNumber);
            }
        }

        stopwatch2.Stop();
        _logger.LogInformation("Completed processing of all pages. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch2.Elapsed.Minutes, stopwatch2.Elapsed.Seconds);

        // Perform bulk upserts
        _logger.LogInformation("Starting bulk upsert of KnowledgeBaseItems.");
        await _cosmosDbService.EDGARBulkTransactUpsertKnowledgeBaseItemsAsync( // BulkUpsertKnowledgeBaseItemsAsync //BulkTransactUpsertKnowledgeBaseItemsAsync
            form,
            ticker,
            "Document", // Category ID
            EDGARknowledgeBaseItems,
            batchSize,
            betweenBatchDelayInMs,
            cancellationToken
        );

        stopwatch.Stop();
        _logger.LogInformation("Completed PDF loading process for file: {FileName}. Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", fileName, stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
    }
    string SanitizeString(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Remove control characters (Unicode category "Cc") and other invisible characters
        return new string(input
            .Where(c => !char.IsControl(c) && !char.IsWhiteSpace(c) || c == ' ') // Allow spaces but remove others
            .ToArray())
            .Trim(); // Remove leading and trailing whitespace
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
        public async Task DeletePdfEDGAR(
          string form,
          string ticker,
          string fileNamePrefix,
          string categoryId,
          int batchSize = 100,
          int betweenBatchDelayInMs = 100)
    {
        _logger.LogInformation("Starting deletion of PDF with prefix: {FileNamePrefix}", fileNamePrefix);

        try
        {
            await _cosmosDbService.EDGARDeleteItemsByIdPrefixAsync(
                idPrefix: fileNamePrefix,
                form: form,
                ticker: ticker,
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
    private static async Task<ReadOnlyMemory<float>> GenerateEmbeddingsCohereWithRetryAsync(
        string apiKey,
        string apiEndpoint,
        string text,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3; // Maximum number of retries
        const int retryDelayMilliseconds = 10_000; // Delay between retries in milliseconds
        int tries = 0;

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(apiEndpoint)
        };

        // Configure HttpClient headers
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

        while (true)
        {
            try
            {
                // Prepare the embedding request payload
                var requestBody = new
                {
                    input = new[] { text },
                    model = "embed-english-v3.0", // Specify the embedding model
                    embeddingTypes = new[] { "int8" }, // Use float32 for precise embeddings
                    input_type = "document" // Specify input type as 'query' or 'document'
                };

                // Serialize the request body
                string requestBodyJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                //Console.WriteLine("Embedding Request Body:");
                //Console.WriteLine(requestBodyJson);

                // Send the request
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/embeddings", content, cancellationToken).ConfigureAwait(false);

                // Handle response
                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    try
                    {
                        // Parse the response
                        var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);

                        //Console.WriteLine(JsonConvert.SerializeObject(parsedResult, Formatting.Indented));

                        if (parsedResult?.data != null && parsedResult.data.Count > 0)
                        {
                            // Extract the embedding
                            var embeddingArray = parsedResult.data[0]?.embedding?.ToObject<List<float>>();
                            //Console.WriteLine($"Embedding Length: {embeddingArray.Count}");
                            // Return the embedding as ReadOnlyMemory<float>
                            return new ReadOnlyMemory<float>(embeddingArray.ToArray());
                            /*
                            if (embeddingArray != null)
                            {

                                // Get the first 10 values manually
                                var embeddingPreview = new List<float>();
                                for (int i = 0; i < embeddingArray.Count && i < 1; i++)
                                {
                                    embeddingPreview.Add(embeddingArray[i]);
                                }

                                Console.WriteLine($"Embedding (First 1 Values): {string.Join(", ", embeddingPreview)}");
                                Console.WriteLine($"Embedding Length: {embeddingArray.Count}");

                                // Return the embedding as ReadOnlyMemory<float>
                                return new ReadOnlyMemory<float>(embeddingArray.ToArray());
                            }
                            else
                            {
                                throw new InvalidOperationException("Embedding is null or could not be parsed.");
                            }
                            */
                        }
                        else
                        {
                            throw new InvalidOperationException("Response data is null or empty.");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"Error parsing JSON response: {jsonEx.Message}");
                        throw;
                    }
                }
                else
                {
                    Console.WriteLine($"Request failed with status code: {response.StatusCode}");
                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"Error Details: {responseContent}");
                    throw new HttpRequestException($"Embedding request failed with status code {response.StatusCode}.");
                }
            }
            catch (HttpRequestException ex) when (tries < maxRetries && ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                tries++;
                Console.WriteLine($"Rate limit reached. Retrying ({tries}/{maxRetries}) in {retryDelayMilliseconds / 1000} seconds...");
                await Task.Delay(retryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
    }
    private static async Task<List<ReadOnlyMemory<float>>> GenerateArrayOfEmbeddingsCohereWithRetryAsync(
        string apiKey,
        string apiEndpoint,
        string[] textArray,
        CancellationToken cancellationToken)
    {
        const int maxRetries = 3; // Maximum number of retries
        const int retryDelayMilliseconds = 10_000; // Delay between retries in milliseconds
        int tries = 0;

        // Initialize HttpClientHandler with custom certificate validation
        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
        };
        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri(apiEndpoint)
        };

        // Configure HttpClient headers
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");
        /*
        float32 
        "vectors": [
                -0.0008873939514160156,
                -0.020050048828125,
                -0.016845703125,
                -0.058746337890625,
                -0.0182952880859375,

        int8
           "embedding": [
                -0.0016727448,
                -0.016067505,
                -0.0025558472,        
        */
        while (true)
        {
            try
            {
                // Prepare the embedding request payload
                var requestBody = new
                {
                    input = textArray, // Array of strings
                    model = "embed-english-v3.0", // Specify the embedding model
                    embeddingTypes = new[] { "float32" }, // Use float32 int8 for precise embeddings
                    input_type = "document" // Specify input type as 'query' or 'document'
                };

                // Serialize the request body
                string requestBodyJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
                // Console.WriteLine("Embedding Request Body:");
                // Console.WriteLine(requestBodyJson);

                // Send the request
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/embeddings", content, cancellationToken).ConfigureAwait(false);

                // Handle response
                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                    try
                    {
                        // Parse the response
                        var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);

                        if (parsedResult?.data != null && parsedResult.data.Count > 0)
                        {
                            var embeddings = new List<ReadOnlyMemory<float>>();

                            // Extract embeddings for all inputs
                            foreach (var item in parsedResult.data)
                            {
                                var embeddingArray = item?.embedding?.ToObject<List<float>>();
                                if (embeddingArray != null)
                                {
                                    embeddings.Add(new ReadOnlyMemory<float>(embeddingArray.ToArray()));
                                }
                                else
                                {
                                    throw new InvalidOperationException("An embedding is null or could not be parsed.");
                                }
                            }

                            return embeddings;
                        }
                        else
                        {
                            throw new InvalidOperationException("Response data is null or empty.");
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"Error parsing JSON response: {jsonEx.Message}");
                        throw;
                    }
                }
                else
                {
                    Console.WriteLine($"Request failed with status code: {response.StatusCode}");
                    string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine($"Error Details: {responseContent}");
                    throw new HttpRequestException($"Embedding request failed with status code {response.StatusCode}.");
                }
            }
            catch (HttpRequestException ex) when (tries < maxRetries && ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                tries++;
                Console.WriteLine($"Rate limit reached. Retrying ({tries}/{maxRetries}) in {retryDelayMilliseconds / 1000} seconds...");
                await Task.Delay(retryDelayMilliseconds, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                throw;
            }
        }
    }

    public class CohereEmbeddingResponse
    {
        public float[][] Embeddings { get; set; }
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
