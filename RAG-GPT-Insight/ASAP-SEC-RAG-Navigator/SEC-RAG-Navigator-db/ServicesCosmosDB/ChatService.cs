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
using System.Net.Http.Headers;

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
        var stopwatch = Stopwatch.StartNew();

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
                categoryId: categoryId
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
            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

            // Return the combined result and title of the first item
            return (combinedCompletion, items.First().Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }
    public async Task<(string completion, string? title)> GetKnowledgeBaseCompletionInt8Async(
         string tenantId,
         string userId,
         string categoryId,
         string promptText,
         double similarityScore)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate inputs
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantId));
            ArgumentNullException.ThrowIfNull(userId, nameof(userId));
            // Initialize HttpClientHandler with custom certificate validation
            using var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
            };

            using var client = new HttpClient(handler)
            {
                //BaseAddress = new Uri(apiEndpoint),
                Timeout = TimeSpan.FromMinutes(5) // Increase timeout to 5 minutes
            };

            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }


            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through"); // Add this header                client.BaseAddress = new Uri(apiEndpoint);
            client.BaseAddress = new Uri(apiEndpoint);
            var queryRequestBody = new
            {
                input = new[] { promptText },
                model = "embed-english-v3.0",
                embeddingTypes = new[] { "float32" }, // Options: int8, binary
                input_type = "query"
            };

            string requestType = "Query";
            string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
            Console.WriteLine($"{requestType} Request Body:");
            Console.WriteLine(requestBodyJson);
            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);
            string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
            float[] promptVectors = parsedResult.data[0]?.embedding?.ToObject<List<float>>()?.ToArray();

            // Generate embeddings for the prompt
            //float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(promptText);
            _logger.LogInformation("Embeddings generated for the prompt.");

            // Generate keywords from promptText
            //string[] searchTerms = GenerateKeywords(promptText);

            // Search for knowledge base items
            List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                vectors: promptVectors,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId
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
                //_logger.LogInformation("Processing item: {Title}", item.Title);

                var (generatedCompletion, _) = await HandleCohereChatCommandAsync(
                    input: promptText,
                    contextData: item
                );
                // Log the intermediate generated completion
                // _logger.LogInformation("Intermediate Completion for {Title}: {Completion}", item.Title, generatedCompletion);

                completions.Add(generatedCompletion);
            }

            // Combine all completions into a single result
            string combinedCompletion = string.Join("\n\n", completions);

            _logger.LogInformation("Completion generated for all knowledge base items.");

            // Return the combined result and title of the first item
            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
            return (combinedCompletion, items.First().Title);

        }

        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }

    }

    /*

    dotnet run knowledge-base-rag-search "What did the report say about the company's Q4 performance and its range of products and services?" 

    Calling GetKnowledgeBaseCompletionAsync...
    Request Body: {
      "messages": [
        {
          "role": "system",
          "content": "## Task and Context Write a search query that will find helpful information for answering the user's question accurately. If you need more than one search query, write a list of search queries. If you decide that a search is very unlikely to find information that would be useful in constructing a response to the user, you should instead directly answer."
        },
        {
          "role": "user",
          "content": "What did the report say about the company's Q4 performance and its range of products and services?"
        }
      ],
      "max_tokens": 2048,
      "temperature": 0.8,
      "top_p": 0.1,
      "frequency_penalty": 0,
      "presence_penalty": 0,
      "seed": 369,
      "tools": [
        {
          "type": "function",
          "function": {
            "name": "internet_search",
            "description": "Returns a list of relevant document snippets for a textual query retrieved from the internet",
            "parameters": {
              "type": "object",
              "properties": {
                "queries": {
                  "type": "array",
                  "items": {
                    "type": "string"
                  },
                  "description": "a list of queries to search the internet with."
                }
              },
              "required": [
                "queries"
              ]
            }
          }
        }
      ]
    }
    API Response (Pretty JSON):
    {
      "choices": [
        {
          "finish_reason": "tool_calls",
          "index": 0,
          "message": {
            "content": "I will search for 'Q4 performance report' and 'range of products and services'.",
            "role": "assistant",
            "tool_calls": [
              {
                "function": {
                  "arguments": "{\"queries\":[\"Q4 performance report\",\"range of products and services\"]}",
                  "call_id": null,
                  "name": "internet_search"
                },
                "id": "internet_search0",
                "type": "function"
              }
            ]
          }
        }
      ],
      "created": 1736636344,
      "id": "48b9c31e-90ff-4a17-8a8c-01be705382c4",
      "model": "tensorrt_llm",
      "object": "chat.completion",
      "usage": {
        "completion_tokens": 35,
        "prompt_tokens": 120,
        "total_tokens": 155
      }
    }
    Generated Queries:
    Q4 performance report
    range of products and services

    */
    public async Task<string[]> HandleCohereQueryGenerationAsync(string input)
    {
        string system_message = @"## Task and Context
Write a search query that will find helpful information for answering the user's question accurately. If you need more than one search query, write a list of search queries. If you decide that a search is very unlikely to find information that would be useful in constructing a response to the user, you should instead directly answer.";

        // Define the query generation tool
        var query_gen_tool = @"
    [
        {
            ""type"": ""function"",
            ""function"": {
                ""name"": ""internet_search"",
                ""description"": ""Returns a list of relevant document snippets for a textual query retrieved from the internet"",
                ""parameters"": {
                    ""type"": ""object"",
                    ""properties"": {
                        ""queries"": {
                            ""type"": ""array"",
                            ""items"": { ""type"": ""string"" },
                            ""description"": ""a list of queries to search the internet with.""
                        }
                    },
                    ""required"": [""queries""]
                }
            }
        }
    ]";

        // Clean up input strings
        system_message = system_message.Replace("\r\n", " ").Replace("\n", " ");
        input = input.Replace("\r\n", " ").Replace("\n", " ");

        using var handler = new HttpClientHandler
        {
            ClientCertificateOptions = ClientCertificateOption.Manual,
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true
        };

        using var client = new HttpClient(handler);
        try
        {
            // Construct the request body
            var requestBody = new
            {
                messages = new[]
                {
                new { role = "system", content = system_message },
                new { role = "user", content = input }
            },
                max_tokens = 2048,
                temperature = 0.8,
                top_p = 0.1,
                frequency_penalty = 0,
                presence_penalty = 0,
                seed = 369,
                tools = JsonConvert.DeserializeObject<dynamic>(query_gen_tool)
            };

            string requestJson = JsonConvert.SerializeObject(requestBody, Formatting.Indented);
            Console.WriteLine($"Request Body: {requestJson}");

            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");
            if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.BaseAddress = new Uri(apiEndpoint);

            // Send the POST request
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("/chat/completions", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                // Parse the response JSON
                var responseJson = JsonConvert.DeserializeObject<dynamic>(result);

                // Pretty-print the full response for debugging
                Console.WriteLine("API Response (Pretty JSON):");
                string prettyJson = JsonConvert.SerializeObject(responseJson, Formatting.Indented);
                Console.WriteLine(prettyJson);

                // Extract queries from tool_calls
                var searchQueries = new List<string>();
                foreach (var choice in responseJson?.choices ?? new List<dynamic>())
                {
                    var toolCalls = choice?.message?.tool_calls;
                    if (toolCalls != null)
                    {
                        foreach (var toolCall in toolCalls)
                        {
                            if (toolCall?.function?.arguments != null)
                            {
                                var arguments = JsonConvert.DeserializeObject<dynamic>(toolCall.function.arguments.ToString());
                                if (arguments?.queries != null)
                                {
                                    foreach (var query in arguments.queries)
                                    {
                                        searchQueries.Add(query.ToString());
                                    }
                                }
                            }
                        }
                    }
                }

                // Log generated queries
                Console.WriteLine("Generated Queries:");
                foreach (var query in searchQueries)
                {
                    Console.WriteLine(query);
                }

                return searchQueries.ToArray();
            }
            else
            {
                // Handle failed responses
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                Console.WriteLine($"The request failed with status code: {response.StatusCode}");
                Console.WriteLine($"Error Details: {responseContent}");
                return Array.Empty<string>();
            }
        }
        catch (Exception ex)
        {
            // Log any exceptions that occur
            Console.WriteLine($"An error occurred: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    public async Task<CohereResponse> GetKnowledgeBaseCompletionRAGInt8Async(
        string tenantId,
        string userId,
        string categoryId,
        string ticker,
        string company,
        string form,
        string promptText,
        double similarityScore)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate inputs
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantId));
            ArgumentNullException.ThrowIfNull(userId, nameof(userId));

            // Generate queries from the prompt text
            string[] queries = await HandleCohereQueryGenerationAsync(promptText);
            _logger.LogInformation("Generated Queries: {Queries}", string.Join(", ", queries));

            var allItems = new List<KnowledgeBaseItem>();

            foreach (var query in queries)
            {
                // Retrieve API key and endpoint from environment variables
                string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
                {
                    throw new InvalidOperationException("API key or endpoint is not configured.");
                }

                // Initialize HttpClientHandler with custom certificate validation
                using var handler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                using var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(apiEndpoint),
                    Timeout = TimeSpan.FromMinutes(5) // Increase timeout
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

                // Prepare embedding request
                var queryRequestBody = new
                {
                    input = new[] { query },
                    model = "embed-english-v3.0",
                    embeddingTypes = new[] { "float32" },
                    input_type = "query"
                };

                string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

                // Send the embedding request
                HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                    continue; // Skip this query and continue with others
                }

                // Parse the embedding response
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                dynamic parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
                float[] promptVectors = parsedResult?.data?[0]?.embedding?.ToObject<List<float>>()?.ToArray();

                if (promptVectors == null || promptVectors.Length == 0)
                {
                    _logger.LogWarning("No embeddings returned for query: {Query}", query);
                    continue;
                }

                _logger.LogInformation("Searching for knowledge base items with vector length: {VectorLength}", promptVectors.Length);

                // Search the knowledge base using the embeddings
                List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                    vectors: promptVectors,
                    tenantId: tenantId,
                    userId: userId,
                    categoryId: categoryId
                );

                if (items?.Count > 0)
                {
                    allItems.AddRange(items);
                }
                string[] searchTerms = GenerateKeywords(query);
                items = await _cosmosDbService.SearchLexicalKnowledgeBaseByTermsAsync(
                              tenantId: tenantId,
                              userId: userId,
                              categoryId: categoryId,
                              searchTerms: searchTerms
                          );

                if (items?.Count > 0)
                {
                    allItems.AddRange(items);
                }
            }

            if (allItems.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return new CohereResponse
                {
                    GeneratedCompletion = "No similar knowledge base items found.",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }

            _logger.LogInformation("Similar knowledge base items found: {Count}", allItems.Count);

            // Deduplicate and rerank items
            var uniqueItems = allItems.GroupBy(item => item.Id).Select(group => group.First()).ToList();
            var reorderedItems = await SendCohereRAGRerankRequestItemsAsync(uniqueItems, promptText);
            _logger.LogInformation("Reordered Items:\n{ReorderedItems}", JsonConvert.SerializeObject(reorderedItems, Formatting.Indented));

            // Send the request to Cohere for RAG Completion
            CohereResponse cohereResponse = await SendCohereRAGCompletionRequestAsync(
                ticker,
                company,
                form,
                reorderedItems,
                promptText);

            // Log the response
            //_logger.LogInformation("Generated Completion: {Completion}", cohereResponse.GeneratedCompletion);
            //_logger.LogInformation("Citations: {Citations}", cohereResponse.Citations != null
            //    ? string.Join(", ", cohereResponse.Citations.Select(citation => citation.Text))
            //    : "No citations provided.");

            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionRAGInt8Async completed in {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

            // Return the full CohereResponse object
            return cohereResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }
    public async Task<CohereResponse> GetKnowledgeBaseCompletionRAGfloat32EDGARAsync(
        string form,
        string ticker,
        string company,
        string categoryId,
        string promptText,
        double similarityScore)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
    
            // Generate queries from the prompt text
            string[] queries = await HandleCohereQueryGenerationAsync(promptText);
            _logger.LogInformation("Generated Queries: {Queries}", string.Join(", ", queries));

            var allItems = new List<EDGARKnowledgeBaseItem>();

            foreach (var query in queries)
            {
                // Retrieve API key and endpoint from environment variables
                string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
                {
                    throw new InvalidOperationException("API key or endpoint is not configured.");
                }

                // Initialize HttpClientHandler with custom certificate validation
                using var handler = new HttpClientHandler
                {
                    ClientCertificateOptions = ClientCertificateOption.Manual,
                    ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                };

                using var client = new HttpClient(handler)
                {
                    BaseAddress = new Uri(apiEndpoint),
                    Timeout = TimeSpan.FromMinutes(5) // Increase timeout
                };

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

                // Prepare embedding request
                var queryRequestBody = new
                {
                    input = new[] { query },
                    model = "embed-english-v3.0",
                    embeddingTypes = new[] { "float32" },
                    input_type = "query"
                };

                string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

                // Send the embedding request
                HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                    continue; // Skip this query and continue with others
                }

                // Parse the embedding response
                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                dynamic parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
                float[] promptVectors = parsedResult?.data?[0]?.embedding?.ToObject<List<float>>()?.ToArray();

                if (promptVectors == null || promptVectors.Length == 0)
                {
                    _logger.LogWarning("No embeddings returned for query: {Query}", query);
                    continue;
                }

                _logger.LogInformation("Searching for knowledge base items with vector length: {VectorLength}", promptVectors.Length);

                // Search the knowledge base using the embeddings
                List<EDGARKnowledgeBaseItem> items = await _cosmosDbService.EDGARSearchKnowledgeBaseAsync(
                    vectors: promptVectors,
                    form: form,
                    ticker: ticker,
                    categoryId: categoryId
                );

                if (items?.Count > 0)
                {
                    allItems.AddRange(items);
                }
                string[] searchTerms = GenerateKeywords(query);
                items = await _cosmosDbService.EDGARSearchLexicalKnowledgeBaseByTermsAsync(
                              form: form,
                              ticker: ticker,
                              categoryId: categoryId,
                              searchTerms: searchTerms
                          );

                if (items?.Count > 0)
                {
                    allItems.AddRange(items);
                }
            }

            if (allItems.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return new CohereResponse
                {
                    GeneratedCompletion = "No similar knowledge base items found.",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }

            _logger.LogInformation("Similar knowledge base items found: {Count}", allItems.Count);

            // Deduplicate and rerank items
            var uniqueItems = allItems.GroupBy(item => item.Id).Select(group => group.First()).ToList();
            var reorderedItems = await SendCohereRAGRerankEDGARRequestItemsAsync(uniqueItems, promptText);
            _logger.LogInformation("Reordered Items:\n{ReorderedItems}", JsonConvert.SerializeObject(reorderedItems, Formatting.Indented));

            // Send the request to Cohere for RAG Completion
            CohereResponse cohereResponse = await SendCohereRAGCompletionEDGARRequestAsync(
                form,
                ticker,
                company,
                reorderedItems,
                promptText);

            // Log the response
            //_logger.LogInformation("Generated Completion: {Completion}", cohereResponse.GeneratedCompletion);
            //_logger.LogInformation("Citations: {Citations}", cohereResponse.Citations != null
            //    ? string.Join(", ", cohereResponse.Citations.Select(citation => citation.Text))
            //    : "No citations provided.");

            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionRAGInt8Async completed in {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

            // Return the full CohereResponse object
            return cohereResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }


    public async Task<List<KnowledgeBaseItem>> GetKnowledgeBaseRerankRAGInt8Async(
       string tenantId,
       string userId,
       string categoryId,
       string promptText,
       double similarityScore)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate inputs
            ArgumentNullException.ThrowIfNull(tenantId, nameof(tenantId));
            ArgumentNullException.ThrowIfNull(userId, nameof(userId));

            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_EMBED_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_EMBED_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                throw new InvalidOperationException("API key or endpoint is not configured.");
            }

            // Initialize HttpClientHandler with custom certificate validation
            using var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (_, _, _, _) => true
            };

            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5) // Increase timeout to 5 minutes
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through"); // Add this header                client.BaseAddress = new Uri(apiEndpoint);
            client.BaseAddress = new Uri(apiEndpoint);

            // Generate embeddings for the prompt
            var queryRequestBody = new
            {
                input = new[] { promptText },
                model = "embed-english-v3.0",
                embeddingTypes = new[] { "float32" },
                input_type = "query"
            };

            string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                return new List<KnowledgeBaseItem>();
            }

            string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
            float[] promptVectors = parsedResult?.data?[0]?.embedding?.ToObject<List<float>>()?.ToArray();

            if (promptVectors == null || promptVectors.Length == 0)
            {
                _logger.LogError("No embeddings returned for the prompt.");
                return new List<KnowledgeBaseItem>();
            }

            // Search for knowledge base items
            List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseAsync(
                vectors: promptVectors,
                tenantId: tenantId,
                userId: userId,
                categoryId: categoryId
            );

            if (items == null || items.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return new List<KnowledgeBaseItem>();
            }

            // Send the request to Cohere for RAG re-ranking
            var reorderedItems = await SendCohereRAGRerankRequestItemsAsync(items, promptText);

            if (reorderedItems == null || reorderedItems.Count == 0)
            {
                _logger.LogInformation("Re-ranking did not return any results.");
                return new List<KnowledgeBaseItem>();
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "GetKnowledgeBaseRerankRAGInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec",
                stopwatch.Elapsed.Minutes,
                stopwatch.Elapsed.Seconds
            );

            // Return the reordered items
            return reorderedItems;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            return new List<KnowledgeBaseItem>();
        }
    }


    private async Task<CohereResponse> SendCohereRAGCompletionRequestAsync(
    string form,
    string ticker,
    string company,
    List<KnowledgeBaseItem> items,
       string promptText)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                _logger.LogError("API key or endpoint is not configured.");
                return new CohereResponse
                {
                    GeneratedCompletion = "API key or endpoint is not configured.",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }

            // Initialize HttpClientHandler with custom certificate validation
            using var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
            };

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiEndpoint),
                Timeout = TimeSpan.FromMinutes(5) // Extended timeout
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

            // Prepare sanitized documents
            var documents = items
                .Where(item => !string.IsNullOrWhiteSpace(SanitizeString(item?.Title)) &&
                               !string.IsNullOrWhiteSpace(SanitizeString(item?.Content)))
                .Select((item, index) => new
                {
                    data = new
                    {
                        id = (index + 1).ToString(),
                        title = SanitizeString(item?.Title),
                        snippet = SanitizeString(item?.Content)
                    }
                })
                .ToArray();

            string documentsJson = JsonConvert.SerializeObject(documents, Formatting.Indented);
            _logger.LogInformation("Prepared Documents:\n{DocumentsJson}", documentsJson);

            string context = "";
            string systemMessage = @"## Task & Context
Act as the company {{company}} with ticker {{ticker}}. Answer questions about your financial report {{form}}. 
Only answer questions based on the info listed below. If the info below doesn't answer the question, say you don't know.

## Style Guide
Answer in full sentences, using proper grammar and spelling.
Format the response as follows:
- **Title**: Generate a title from the context {{context}}
- **Content Summary**: A summary of the content, including key points or highlights.";

            systemMessage = UpdateCompanyFilingSystemTemplate(context, ticker, company, form, systemMessage);

            // Create the request body
            var chatRequestBody = new
            {
                model = "command-r-plus-08-2024",
                messages = new[]
                {
                new { role = "system", content = systemMessage },
                new { role = "user", content = promptText }
            },
                documents = documents,
                temperature = 0.3,
                citation_options = new { mode = "accurate" }
            };

            string requestJson = JsonConvert.SerializeObject(chatRequestBody, Formatting.Indented);
            _logger.LogInformation("Request Body:\n{RequestJson}", requestJson);

            // Send the request
            HttpResponseMessage response = await client.PostAsync("v2/chat", new StringContent(requestJson, Encoding.UTF8, "application/json")).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var parsedJson = JsonConvert.DeserializeObject(responseContent); // Deserialize the JSON

                string prettyResponseContent = JsonConvert.SerializeObject(parsedJson, Formatting.Indented); // Re-serialize with indentation
                                                                                                             // Log the pretty-printed response
                _logger.LogInformation("Pretty Printed Response:\n{PrettyResponseContent}", prettyResponseContent);


                dynamic parsedResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                var result = new CohereResponse
                {
                    GeneratedCompletion = parsedResponse?.message?.content[0]?.text ?? string.Empty,
                    Citations = JsonConvert.DeserializeObject<List<Cosmos.Copilot.Models.Citation>>(JsonConvert.SerializeObject(parsedResponse?.message?.citations)),
                    FinishReason = parsedResponse?.finish_reason,
                    Usage = new Usage
                    {
                        InputTokens = parsedResponse?.usage?.tokens?.input_tokens ?? 0,
                        OutputTokens = parsedResponse?.usage?.tokens?.output_tokens ?? 0,
                        TotalTokens = parsedResponse?.usage?.tokens?.total_tokens ?? 0
                    }
                };

                stopwatch.Stop();
                _logger.LogInformation("Request completed in {ElapsedMinutes} min {ElapsedSeconds} sec",
                    stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

                return result;
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Request failed with status {StatusCode}:\n{ErrorDetails}", response.StatusCode, errorDetails);

                return new CohereResponse
                {
                    GeneratedCompletion = $"Request failed: {errorDetails}",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("An exception occurred: {ExceptionMessage}", ex.Message);
            return new CohereResponse
            {
                GeneratedCompletion = $"An error occurred: {ex.Message}",
                Citations = new List<Cosmos.Copilot.Models.Citation>()
            };
        }
    }
   private async Task<CohereResponse> SendCohereRAGCompletionEDGARRequestAsync(
    string form,
    string ticker,
    string company,
    List<EDGARKnowledgeBaseItem> items,
       string promptText)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve API key and endpoint from environment variables
            string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                _logger.LogError("API key or endpoint is not configured.");
                return new CohereResponse
                {
                    GeneratedCompletion = "API key or endpoint is not configured.",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }

            // Initialize HttpClientHandler with custom certificate validation
            using var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
            };

            using var client = new HttpClient(handler)
            {
                BaseAddress = new Uri(apiEndpoint),
                Timeout = TimeSpan.FromMinutes(5) // Extended timeout
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");

            // Prepare sanitized documents
            var documents = items
                .Where(item => !string.IsNullOrWhiteSpace(SanitizeString(item?.Title)) &&
                               !string.IsNullOrWhiteSpace(SanitizeString(item?.Content)))
                .Select((item, index) => new
                {
                    data = new
                    {
                        id = (index + 1).ToString(),
                        title = SanitizeString(item?.Title),
                        snippet = SanitizeString(item?.Content)
                    }
                })
                .ToArray();

            string documentsJson = JsonConvert.SerializeObject(documents, Formatting.Indented);
            _logger.LogInformation("Prepared Documents:\n{DocumentsJson}", documentsJson);

            string context = "";
            string systemMessage = @"## Task & Context
Act as the company {{company}} with ticker {{ticker}}. Answer questions about your financial report {{form}}. 
Only answer questions based on the info listed below. If the info below doesn't answer the question, say you don't know.

## Style Guide
Answer in full sentences, using proper grammar and spelling.
Format the response as follows:
- **Title**: Generate a title from the context {{context}}
- **Content Summary**: A summary of the content, including key points or highlights.";

            systemMessage = UpdateCompanyFilingSystemTemplate(context, ticker, company, form, systemMessage);

            // Create the request body
            var chatRequestBody = new
            {
                model = "command-r-plus-08-2024",
                messages = new[]
                {
                new { role = "system", content = systemMessage },
                new { role = "user", content = promptText }
            },
                documents = documents,
                temperature = 0.3,
                citation_options = new { mode = "accurate" }
            };

            string requestJson = JsonConvert.SerializeObject(chatRequestBody, Formatting.Indented);
            _logger.LogInformation("Request Body:\n{RequestJson}", requestJson);

            // Send the request
            HttpResponseMessage response = await client.PostAsync("v2/chat", new StringContent(requestJson, Encoding.UTF8, "application/json")).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                var parsedJson = JsonConvert.DeserializeObject(responseContent); // Deserialize the JSON

                string prettyResponseContent = JsonConvert.SerializeObject(parsedJson, Formatting.Indented); // Re-serialize with indentation
                                                                                                             // Log the pretty-printed response
                _logger.LogInformation("Pretty Printed Response:\n{PrettyResponseContent}", prettyResponseContent);


                dynamic parsedResponse = JsonConvert.DeserializeObject<dynamic>(responseContent);

                var result = new CohereResponse
                {
                    GeneratedCompletion = parsedResponse?.message?.content[0]?.text ?? string.Empty,
                    Citations = JsonConvert.DeserializeObject<List<Cosmos.Copilot.Models.Citation>>(JsonConvert.SerializeObject(parsedResponse?.message?.citations)),
                    FinishReason = parsedResponse?.finish_reason,
                    Usage = new Usage
                    {
                        InputTokens = parsedResponse?.usage?.tokens?.input_tokens ?? 0,
                        OutputTokens = parsedResponse?.usage?.tokens?.output_tokens ?? 0,
                        TotalTokens = parsedResponse?.usage?.tokens?.total_tokens ?? 0
                    }
                };

                stopwatch.Stop();
                _logger.LogInformation("Request completed in {ElapsedMinutes} min {ElapsedSeconds} sec",
                    stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

                return result;
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Request failed with status {StatusCode}:\n{ErrorDetails}", response.StatusCode, errorDetails);

                return new CohereResponse
                {
                    GeneratedCompletion = $"Request failed: {errorDetails}",
                    Citations = new List<Cosmos.Copilot.Models.Citation>()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("An exception occurred: {ExceptionMessage}", ex.Message);
            return new CohereResponse
            {
                GeneratedCompletion = $"An error occurred: {ex.Message}",
                Citations = new List<Cosmos.Copilot.Models.Citation>()
            };
        }
    }

    public string UpdateCompanyFilingSystemTemplate(
 string context,
string ticker,
string company,
string form,
 string promptyTemplate)
    {

        // Add replacements for placeholders
        var replacements = new Dictionary<string, string>
            {
                { "{{context}}", string.IsNullOrEmpty(context) ? "" : $"\n# Context:\n<context>{context}</context>"},
                { "{{ticker}}", string.IsNullOrEmpty(ticker) ? "" : $"\n# Ticker:\n<ticker>{ticker}</ticker>"},
                { "{{company}}", string.IsNullOrEmpty(company) ? "" : $"\n# Company:\n<company>{company}</company>"},
                { "{{form}}", string.IsNullOrEmpty(form) ? "" : $"\n# Form:\n<form>{form}</form>"},

            };
        // Replace placeholders in YAML content with corresponding values
        foreach (var replacement in replacements)
        {
            promptyTemplate = promptyTemplate.Replace(replacement.Key, replacement.Value);
        }

        return promptyTemplate;
    }
    // Utility method to sanitize strings by removing control and invisible characters
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


    private async Task<List<KnowledgeBaseItem>> SendCohereRAGRerankRequestItemsAsync(
            List<KnowledgeBaseItem> items,
            string promptText)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve API key and endpoint
            string apiKey = Environment.GetEnvironmentVariable("COHERE_RERANK_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_RERANK_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                _logger.LogError("API key or endpoint is not configured.");
                return (new List<KnowledgeBaseItem>());
            }

            using var client = new HttpClient
            {
                BaseAddress = new Uri(apiEndpoint)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Prepare the documents list
            var documents = items
                .Where(item => !string.IsNullOrWhiteSpace(item?.Title) && !string.IsNullOrWhiteSpace(item?.Content))
                .Select(item => new
                {
                    Title = item.Title.Trim(),
                    Content = item.Content.Trim()
                })
                .ToList();

            if (documents.Count == 0)
            {
                _logger.LogError("No valid documents found.");
                throw new InvalidOperationException("No valid documents found.");
            }
            //  len(documents) * max_chunks_per_doc <10,000 where max_chunks_per_doc is set to 10 as default.
            // Prepare the request body
            var chatRequestBody = new
            {
                model = "rerank-v3.5",
                documents,
                query = promptText,
                rank_fields = new[] { "Title", "Content" },
                top_n = 50
            };

            string requestJson = JsonConvert.SerializeObject(chatRequestBody, Formatting.Indented);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Send POST request
            HttpResponseMessage response = await client.PostAsync("v2/rerank", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                // Parse rankings
                var rankingsList = ((IEnumerable<dynamic>)responseObject.results)
                    .Select(r => new
                    {
                        Index = (int)r.index,
                        RelevanceScore = (double)r.relevance_score
                    })
                    .ToList();

                // Reorder KnowledgeBaseItems
                var reorderedItems = rankingsList
                    .Select(r => new KnowledgeBaseItem
                    {
                        Title = items[r.Index].Title,
                        Content = items[r.Index].Content,
                        RelevanceScore = r.RelevanceScore
                    })
                    .OrderByDescending(r => r.RelevanceScore)
                    .ToList();

                stopwatch.Stop();

                //_logger.LogInformation("Reordered Items:\n{ReorderedItems}", JsonConvert.SerializeObject(reorderedItems, Formatting.Indented));
                return (reorderedItems);
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Request failed with status {StatusCode}:\n{ErrorDetails}", response.StatusCode, errorDetails);
                return (new List<KnowledgeBaseItem>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("An exception occurred: {ExceptionMessage}", ex.Message);
            return (new List<KnowledgeBaseItem>());
        }
    }
  private async Task<List<EDGARKnowledgeBaseItem>> SendCohereRAGRerankEDGARRequestItemsAsync(
            List<EDGARKnowledgeBaseItem> items,
            string promptText)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Retrieve API key and endpoint
            string apiKey = Environment.GetEnvironmentVariable("COHERE_RERANK_KEY");
            string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_RERANK_ENDPOINT");

            if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
            {
                _logger.LogError("API key or endpoint is not configured.");
                return (new List<EDGARKnowledgeBaseItem>());
            }

            using var client = new HttpClient
            {
                BaseAddress = new Uri(apiEndpoint)
            };
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            // Prepare the documents list
            var documents = items
                .Where(item => !string.IsNullOrWhiteSpace(item?.Title) && !string.IsNullOrWhiteSpace(item?.Content))
                .Select(item => new
                {
                    Title = item.Title.Trim(),
                    Content = item.Content.Trim()
                })
                .ToList();

            if (documents.Count == 0)
            {
                _logger.LogError("No valid documents found.");
                throw new InvalidOperationException("No valid documents found.");
            }
            //  len(documents) * max_chunks_per_doc <10,000 where max_chunks_per_doc is set to 10 as default.
            // Prepare the request body
            var chatRequestBody = new
            {
                model = "rerank-v3.5",
                documents,
                query = promptText,
                rank_fields = new[] { "Title", "Content" },
                top_n = 50
            };

            string requestJson = JsonConvert.SerializeObject(chatRequestBody, Formatting.Indented);
            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            // Send POST request
            HttpResponseMessage response = await client.PostAsync("v2/rerank", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var responseObject = JsonConvert.DeserializeObject<dynamic>(responseContent);

                // Parse rankings
                var rankingsList = ((IEnumerable<dynamic>)responseObject.results)
                    .Select(r => new
                    {
                        Index = (int)r.index,
                        RelevanceScore = (double)r.relevance_score
                    })
                    .ToList();

                // Reorder KnowledgeBaseItems
                var reorderedItems = rankingsList
                    .Select(r => new EDGARKnowledgeBaseItem
                    {
                        Title = items[r.Index].Title,
                        Content = items[r.Index].Content,
                        RelevanceScore = r.RelevanceScore
                    })
                    .OrderByDescending(r => r.RelevanceScore)
                    .ToList();

                stopwatch.Stop();

                //_logger.LogInformation("Reordered Items:\n{ReorderedItems}", JsonConvert.SerializeObject(reorderedItems, Formatting.Indented));
                return (reorderedItems);
            }
            else
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Request failed with status {StatusCode}:\n{ErrorDetails}", response.StatusCode, errorDetails);
                return (new List<EDGARKnowledgeBaseItem>());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError("An exception occurred: {ExceptionMessage}", ex.Message);
            return (new List<EDGARKnowledgeBaseItem>());
        }
    }

    public class Citation
    {
        public int Start { get; set; }
        public int End { get; set; }
        public string Text { get; set; }
        public List<DocumentSource> Sources { get; set; }
    }

    public class DocumentSource
    {
        public string Type { get; set; }
        public string Id { get; set; }
        public Document Document { get; set; }
    }

    public class Document
    {
        public string Id { get; set; }
        public string Snippet { get; set; }
        public string Title { get; set; }
    }


    public async Task<(string generatedCompletion, int tokens)> HandleCohereChatCommandAsync(
       string input,
       KnowledgeBaseItem contextData)
    {
        var stopwatch = Stopwatch.StartNew();

        string title = contextData?.Title ?? string.Empty;
        string context = contextData?.Content ?? string.Empty;

        string systemMessage = @"## Task and Context 
        You are a writing assistant
        ## Style Guide
        You are an intelligent assistant designed to extract relevant and concise information from a knowledge base context.
        Use the provided knowledge base title: {{title}} and context: {{context}} to answer accurately and concisely. Follow these instructions:

        - Extract key details such as title, description, and reference link.
        - Do not include unrelated information or make assumptions beyond the context provided.
        - If no relevant answer exists, respond with: ""I could not find an answer in the knowledge base.""

        Format the response clearly as follows:
        - **Title**: {{title}} Generate a title from the context
        - **Content Summary**: A summary of the content, including key points or highlights

        Knowledge base context is provided below:
    ";

        try
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(systemMessage))
            {
                return ("Invalid input or system message.", 0);
            }

            // Update systemMessage template
            systemMessage = UpdatepromptyTemplate(title, context, systemMessage);

            // Initialize HttpClientHandler with custom certificate validation
            using (var handler = new HttpClientHandler
            {
                ClientCertificateOptions = ClientCertificateOption.Manual,
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, cetChain, policyErrors) => true
            })
            using (var client = new HttpClient(handler))
            {
                // Retrieve API key and endpoint from environment variables
                string apiKey = Environment.GetEnvironmentVariable("COHERE_KEY");
                string apiEndpoint = Environment.GetEnvironmentVariable("COHERE_ENDPOINT");

                if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(apiEndpoint))
                {
                    return ("API key or endpoint is not configured.", 0);
                }

                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.BaseAddress = new Uri(apiEndpoint);

                // Construct the JSON request body
                var requestBody = new
                {
                    messages = new[]
                    {
                    new { role = "system", content = systemMessage },
                    new { role = "user", content = input }
                },
                    max_tokens = 2048,
                    temperature = 0.8,
                    top_p = 0.1,
                    frequency_penalty = 0,
                    presence_penalty = 0,
                    seed = 369
                };

                var requestJson = JsonConvert.SerializeObject(requestBody);
                //_logger.LogInformation("Request Body: {requestJson}", requestJson);

                // Set up the request content
                var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                // Send POST request
                HttpResponseMessage response = await client.PostAsync("chat/completions", content).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    //_logger.LogInformation("Response: {result}", result);

                    // Extract and format the JSON response
                    var responseObject = JsonConvert.DeserializeObject<dynamic>(result);
                    string generatedCompletion = responseObject?.choices?[0]?.message?.content ?? "No completion generated.";
                    int completionTokens = responseObject?.usage?.total_tokens ?? 0;
                    stopwatch.Stop();
                    _logger.LogInformation("HandleCohereChatCommandAsync Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

                    return (generatedCompletion, completionTokens);
                }
                else
                {
                    string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                    return ($"Request failed: {response.StatusCode}", 0);
                }
            }

        }
        catch (Exception ex)
        {
            _logger.LogError("An error occurred: {ExceptionMessage}", ex.Message);
            return ($"An error occurred: {ex.Message}", 0);
        }
    }



    public string UpdatepromptyTemplate(
    string title,
    string context,
    string promptyTemplate)
    {

        // Add replacements for placeholders
        var replacements = new Dictionary<string, string>
            {
                //{ "{{input}}", string.IsNullOrEmpty(input) ? "" : $"\n# Input:\n<input>{input}</input>" },
                { "{{title}}", string.IsNullOrEmpty(title) ? "" : $"\n# Title:\n<title>{title}</title>"},
                { "{{context}}", string.IsNullOrEmpty(context) ? "" : $"\n# Context:\n<context>{context}</context>"},
            };
        // Replace placeholders in YAML content with corresponding values
        foreach (var replacement in replacements)
        {
            promptyTemplate = promptyTemplate.Replace(replacement.Key, replacement.Value);
        }

        return promptyTemplate;
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