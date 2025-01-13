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

    /*
        public async Task<(string completion, string? title)> GetKnowledgeBaseCompletionRAGInt8AsyncOLD(
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
                string[] queries = await HandleCohereQueryGenerationAsync(promptText);

                // Generate embeddings for the prompt
                var queryRequestBody = new
                {
                    input = queries,
                    model = "embed-english-v3.0",
                    embeddingTypes = new[] { "int8" }, //
                    input_type = "query" // document query
                };

                string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
                var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                    return ($"Embedding request failed: {response.StatusCode}", null);
                }

                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
                float[] promptVectors = parsedResult.data[0]?.embedding?.ToObject<List<float>>()?.ToArray();

                // Generate keywords from promptText
                string[] searchTerms = GenerateKeywords(promptText);

                // Search for knowledge base items
                List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseInt8Async(
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

                // Prepare documents list
                var documents = items.Select(item => new
                {
                    data = new
                    {
                        title = item?.Title ?? string.Empty,
                        snippet = item?.Content ?? string.Empty
                    }
                }).ToList();

                // Log the documents
                string documentsJson = JsonConvert.SerializeObject(documents, Formatting.Indented);
                _logger.LogInformation("Documents prepared: {Documents}", documentsJson);

                // Prepare the request for chat completions
                var chatRequestBody = new
                {
                    messages = new[]
                    {
                    new { role = "user", content = promptText }
                },
                    documents = documents,
                    max_tokens = 2048,
                    temperature = 0.8,
                    top_p = 0.1,
                    frequency_penalty = 0,
                    presence_penalty = 0,
                    seed = 369
                };

                var requestJson = JsonConvert.SerializeObject(chatRequestBody);
                content = new StringContent(requestJson, Encoding.UTF8, "application/json");
                response = await client.PostAsync("chat/completions", content).ConfigureAwait(false);
                _logger.LogInformation("response: {response}", response);

                //if (!response.IsSuccessStatusCode)
                //{
                //    string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                //    _logger.LogError("Chat completion request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                //    return ($"Chat completion request failed: {response.StatusCode}", null);
                //}

                //string chatResult = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                //var responseObject = JsonConvert.DeserializeObject<dynamic>(chatResult);
                //string generatedCompletion = responseObject?.choices?[0]?.message?.content ?? "No completion generated.";

                // Log the completion
                //
                // Return the combined result and title of the first item
                stopwatch.Stop();
                _logger.LogInformation("GetKnowledgeBaseCompletionRAGInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);
                return ("", "");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating knowledge base completion.");
                throw;
            }
        }
        */
    /*
    public async Task<(string completion, string? title)> GetKnowledgeBaseCompletionRAGInt8AsyncOLD1(
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
                ServerCertificateCustomValidationCallback = (httpRequestMessage, cert, certChain, policyErrors) => true
            };
            using var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMinutes(5) // Increase timeout to 5 minutes
            };

            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");
            client.BaseAddress = new Uri(apiEndpoint);

            string[] queries = await HandleCohereQueryGenerationAsync(promptText);
            _logger.LogInformation($"queries: {string.Join(", ", queries)}");

            // Generate embeddings for the prompt

            var queryRequestBody = new
            {
                input = queries,
                model = "embed-english-v3.0",
                embeddingTypes = new[] { "float32" }, // int8
                input_type = "query" // document query
            };

            string requestBodyJson = JsonConvert.SerializeObject(queryRequestBody, Formatting.Indented);
            var content = new StringContent(requestBodyJson, Encoding.UTF8, "application/json");

            HttpResponseMessage response = await client.PostAsync("/embeddings", content).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Embedding request failed with status {StatusCode}: {ErrorDetails}", response.StatusCode, errorDetails);
                return ($"Embedding request failed: {response.StatusCode}", null);
            }

            string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);

            // Pretty print the JSON
            string prettyJson = JsonConvert.SerializeObject(parsedResult, Formatting.Indented);
            //Console.WriteLine("API Response (Pretty JSON):");
            //Console.WriteLine(prettyJson);

            // Loop through embeddings and collect results
            var allItems = new List<KnowledgeBaseItem>();

            foreach (var data in parsedResult?.data ?? new List<dynamic>())
            {
                float[] promptVectors = data?.embedding?.ToObject<List<float>>()?.ToArray();

                if (promptVectors == null || promptVectors.Length == 0)
                {
                    _logger.LogWarning("No embeddings returned for one of the queries.");
                    continue;
                }
                // Print the vectors to the console
                //Console.WriteLine("promptVectors: " + string.Join(", ", promptVectors));

                _logger.LogInformation("Searching for knowledge base items with vector length: {VectorLength}", promptVectors.Length);

                List<KnowledgeBaseItem> items = await _cosmosDbService.SearchKnowledgeBaseInt8QueriesAsync(
                    vectors: promptVectors,
                    tenantId: tenantId,
                    userId: userId,
                    categoryId: categoryId
                );

                if (items?.Count > 0)
                {
                    allItems.AddRange(items);
                }
            }

            if (allItems == null || allItems.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return ("No similar knowledge base items found.", null);
            }
            _logger.LogInformation($"Similar knowledge base items found: {allItems.Count}");

            // Deduplicate items based on ID (optional)
            var uniqueItems = allItems.GroupBy(item => item.Id).Select(group => group.First()).ToList();

            // Send the request to Cohere for RAG Completion
            (string completion, string citations) = await SendCohereRAGCompletionRequestAsync(uniqueItems, promptText);

            // Log the completion text and citations
            _logger.LogInformation("Completion: {Completion}", completion);

            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionRAGInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

            // Return the completion and the title of the first item
            return (completion, uniqueItems.FirstOrDefault()?.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }
    */

    public async Task<(string completion, string? title)> GetKnowledgeBaseCompletionRAGInt8Async(
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


            string[] queries = await HandleCohereQueryGenerationAsync(promptText);
            _logger.LogInformation($"queries: {string.Join(", ", queries)}");
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
                    return ("Embedding request failed.", null);
                }

                string result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var parsedResult = JsonConvert.DeserializeObject<dynamic>(result);
                float[] promptVectors = parsedResult?.data?[0]?.embedding?.ToObject<List<float>>()?.ToArray();

                //float[] promptVectors = await _semanticKernelService.GetEmbeddingsAsync(query);

                if (promptVectors == null || promptVectors.Length == 0)
                {
                    _logger.LogWarning("No embeddings returned for one of the queries.");
                    continue;
                }
                // Print the vectors to the console
                //Console.WriteLine("promptVectors: " + string.Join(", ", promptVectors));

                _logger.LogInformation("Searching for knowledge base items with vector length: {VectorLength}", promptVectors.Length);
                // Generate keywords from promptText
                //string[] searchTerms = GenerateKeywords(promptText);

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
            }

            if (allItems == null || allItems.Count == 0)
            {
                _logger.LogInformation("No similar knowledge base items found.");
                return ("No similar knowledge base items found.", null);
            }
            _logger.LogInformation($"Similar knowledge base items found: {allItems.Count}");

            // Deduplicate items based on ID (optional)
            var uniqueItems = allItems.GroupBy(item => item.Id).Select(group => group.First()).ToList();
            //var reorderedItems = await SendCohereRAGRerankRequestItemsAsync(uniqueItems, promptText);

            // Send the request to Cohere for RAG Completion
            (string completion, string citations) = await SendCohereRAGCompletionRequestAsync(uniqueItems, promptText);

            // Log the completion text and citations
            _logger.LogInformation("Completion: {Completion}", completion);

            stopwatch.Stop();
            _logger.LogInformation("GetKnowledgeBaseCompletionRAGInt8Async: Time spent: {ElapsedMinutes} min {ElapsedSeconds} sec", stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

            // Return the completion and the title of the first item
            return (completion, uniqueItems.FirstOrDefault()?.Title);
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


    private async Task<(string generatedCompletion, string citations)> SendCohereRAGCompletionRequestAsync(
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
                return ("API key or endpoint is not configured.", "");
            }

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


            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            client.DefaultRequestHeaders.Add("extra-parameters", "pass-through");
            client.BaseAddress = new Uri(apiEndpoint);

            // Process all items into a sanitized array
            var documents = items
                .Where(item =>
                    !string.IsNullOrWhiteSpace(SanitizeString(item?.Title)) &&
                    !string.IsNullOrWhiteSpace(SanitizeString(item?.Content))) // Filter out invalid documents
                .Select((item, index) => new
                {
                    id = (index + 1).ToString(), // Generate a unique id
                    data = $"{SanitizeString(item?.Title)}: {SanitizeString(item?.Content)}" // Sanitize and combine Title and Content
                })
                .ToArray(); // Convert the results to an array
            // Sample documents array
            //var documents = new[]
            //{
            //    new { id = "1", data = "Cohere is the best!" }
            //};

            // Log the documents
            string documentsJson = JsonConvert.SerializeObject(documents, Formatting.Indented);
            _logger.LogInformation("Prepared Documents:\n{DocumentsJson}", documentsJson);

            string systemMessage = @"## Task & Context
You help people answer their questions and other requests interactively. You will be asked a very wide array of requests on all kinds of topics. You will be equipped with a wide range of search engines or similar tools to help you, which you use to research your answer. You should focus on serving the user's needs as best you can, which will be wide-ranging.

## Style Guide
Unless the user asks for a different style of answer, you should answer in full sentences, using proper grammar and spelling.

    ";

            // Prepare the request body
            var chatRequestBody = new
            {
                model = "command-r-plus-08-2024",
                messages = new[]
                {
                new { role = "system", content = systemMessage },
                new { role = "user", content = promptText }
            },
                documents = documents,
                //max_tokens = 2048,
                temperature = 0.3,
                //top_p = 0.1,
                //frequency_penalty = 0,
                //presence_penalty = 0,
                //seed = 369,
                citation_options = new { mode = "accurate" } // Fixed citationOptions syntax

            };

            string requestJson = JsonConvert.SerializeObject(chatRequestBody, Formatting.Indented);
            _logger.LogInformation("Request Body:\n{RequestJson}", requestJson);

            var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            /*
            API Routes

            
            Azure AI model inference: Chat Completion
            https://AITCohere-command-r-plus-08-2024.eastus.models.ai.azure.com/chat/completions

            supports Cohere’s native API schema.
            Cohere: Chat
            https://AITCohere-command-r-plus-08-2024.eastus.models.ai.azure.com/v1/chat

            Cohere: Chat
            https://AITCohere-command-r-plus-08-2024.eastus.models.ai.azure.com/v2/chat

            */
            // Send POST request
            _logger.LogInformation("SendCohereRAGCompletionRequestAsync: Sending POST request to {Url}", client.BaseAddress + "v2/chat");
            HttpResponseMessage response = await client.PostAsync("v2/chat", content).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                string responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                // Parse the JSON and pretty-print it
                var parsedJson = JsonConvert.DeserializeObject(responseContent);
                string prettyJson = JsonConvert.SerializeObject(parsedJson, Formatting.Indented);
                _logger.LogInformation($"Response: {prettyJson}");

                stopwatch.Stop();
                _logger.LogInformation("SendCohereRAGCompletionRequestAsync completed in {ElapsedMinutes} min {ElapsedSeconds} sec",
                    stopwatch.Elapsed.Minutes, stopwatch.Elapsed.Seconds);

                return ("", "");
            }
            else
            {
                // Log error details
                string errorDetails = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                _logger.LogError("Request failed with status {StatusCode}:\n{ErrorDetails}", response.StatusCode, errorDetails);
                return ($"Request failed with status {response.StatusCode}: {errorDetails}", "");
            }
        }
        catch (Exception ex)
        {
            // Log exception details
            _logger.LogError("An exception occurred: {ExceptionMessage}", ex.Message);
            return ($"An error occurred: {ex.Message}", "");
        }
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

                _logger.LogInformation("Reordered Items:\n{ReorderedItems}", JsonConvert.SerializeObject(reorderedItems, Formatting.Indented));
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