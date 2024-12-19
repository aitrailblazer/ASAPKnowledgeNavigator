using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.VectorData;
using Microsoft.ML.OnnxRuntimeGenAI;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.InMemory;
using Microsoft.SemanticKernel.Connectors.Onnx;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using Microsoft.ML.Tokenizers;

class Program
{
    private const int MaxTokens = 16000; // Model max length
    private const int PromptOverheadTokens = 500; // Reserved for prompt and metadata

static async Task Main(string[] args)
{
    // Retrieve paths from environment variables
    var modelPath = Environment.GetEnvironmentVariable("PHI3_MODEL_PATH");
    var embeddingModelPath = Environment.GetEnvironmentVariable("BGE_MICRO_V2_MODEL_PATH");
    var embeddingVocabPath = Environment.GetEnvironmentVariable("BGE_MICRO_V2_VOCAB_PATH");

    if (string.IsNullOrEmpty(modelPath) ||
        string.IsNullOrEmpty(embeddingModelPath) ||
        string.IsNullOrEmpty(embeddingVocabPath))
    {
        Console.WriteLine("Error: One or more required environment variables are not set.");
        Console.WriteLine("Please set PHI3_MODEL_PATH, BGE_MICRO_V2_MODEL_PATH, and BGE_MICRO_V2_VOCAB_PATH.");
        return;
    }

    Console.WriteLine("Initializing AI models and loading facts...");
    using var ogaHandle = new OgaHandle();
    var chatModelId = "phi-3";

    var builder = Kernel.CreateBuilder()
        .AddOnnxRuntimeGenAIChatCompletion(chatModelId, modelPath)
        .AddBertOnnxTextEmbeddingGeneration(embeddingModelPath, embeddingVocabPath);

    var kernel = builder.Build();
    using var chatService = kernel.GetRequiredService<IChatCompletionService>() as OnnxRuntimeGenAIChatCompletionService;
    var embeddingService = kernel.GetRequiredService<ITextEmbeddingGenerationService>();

    // Set up vector store and load facts
    var vectorStore = new InMemoryVectorStore();
    var collectionName = "ExampleCollection";
    var collection = vectorStore.GetCollection<string, InformationItem>(collectionName);
    await collection.CreateCollectionIfNotExistsAsync();

    var factsFolder = "Facts";
    if (!Directory.Exists(factsFolder))
    {
        Directory.CreateDirectory(factsFolder);
        Console.WriteLine("Created 'Facts' directory for storing facts.");
    }

    // Fetch the latest GitHub issue
    Console.WriteLine("Fetching the latest GitHub issue...");
    var latestIssue = await FetchLatestGitHubIssueAsync();

    if (!string.IsNullOrEmpty(latestIssue.Title) && !string.IsNullOrEmpty(latestIssue.Body))
    {
        Console.WriteLine("\nLatest GitHub Issue:");
        Console.WriteLine($"Title: {latestIssue.Title}");
        Console.WriteLine($"Body:\n{latestIssue.Body}\n");

        // Save GitHub issue to Facts folder
        Console.WriteLine("Saving GitHub issue to Facts folder...");
        var issueFileName = Path.Combine(factsFolder, $"GitHubIssue_{DateTime.UtcNow:yyyyMMdd_HHmmss}.txt");
        var issueContent = $"Title: {latestIssue.Title}\n\nBody:\n{latestIssue.Body}";
        await File.WriteAllTextAsync(issueFileName, issueContent);
        Console.WriteLine($"GitHub issue saved to {issueFileName}");
    }
    else
    {
        Console.WriteLine("No issues found to analyze.");
    }

    // Vectorize all facts
    Console.WriteLine("Loading and vectorizing all facts...");
    foreach (var factTextFile in Directory.GetFiles(factsFolder, "*.txt"))
    {
        var factContent = await File.ReadAllTextAsync(factTextFile);
        await collection.UpsertAsync(new()
        {
            Id = Path.GetFileNameWithoutExtension(factTextFile), // Use file name as ID
            Text = factContent,
            Embedding = await embeddingService.GenerateEmbeddingAsync(factContent)
        });
        Console.WriteLine($"Fact vectorized and saved: {factTextFile}");
    }
    Console.WriteLine("All facts vectorized successfully.");

    var vectorStoreTextSearch = new VectorStoreTextSearch<InformationItem>(collection, embeddingService);
    kernel.Plugins.Add(vectorStoreTextSearch.CreateWithSearch("SearchPlugin"));

    // Perform analysis using RAG if a GitHub issue was fetched
    //if (!string.IsNullOrEmpty(latestIssue.Body))
    //{
    //    Console.WriteLine("Splitting the GitHub issue body into smaller parts...");
    //    var chunks = TokenizeAndSplit(latestIssue.Body, MaxTokens, PromptOverheadTokens);

    //    Console.WriteLine("Performing analysis using facts...");
    //    var augmentedAnalysis = await AnalyzeIssueWithRAG(kernel, chunks);
    //    Console.WriteLine($"Augmented Analysis:\n{augmentedAnalysis}");
    //}

    // Enter interactive mode
    Console.WriteLine("\nEntering interactive mode. Type your questions (press Ctrl+C to exit).");

    while (true)
    {
        try
        {
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("\nUser > ");

            var userQuestion = Console.ReadLine();

            // Exit condition: handle null or empty input
            if (string.IsNullOrEmpty(userQuestion))
            {
                Console.WriteLine("Exiting interactive mode. Goodbye!");
                break;
            }

            // Trim whitespace
            userQuestion = userQuestion.Trim();

            // Process the question
            Console.WriteLine("Streaming response from PHI-3:");

            var response = await InterpretWithPHI3Streaming(kernel, userQuestion);

            if (string.IsNullOrEmpty(response))
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nAssistant > (No response received. Try again.)");
                Console.ResetColor();
                continue;
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\nAssistant > {response}");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error: {ex.Message}");
            Console.ResetColor();
        }
    }
}

private static List<string> TokenizeAndSplit(string text, int maxTokens = 1024, int promptOverheadTokens = 500)
{
    if (string.IsNullOrEmpty(text))
        return new List<string>();

    // Calculate the maximum allowed tokens per chunk
    int effectiveMaxTokens = maxTokens - promptOverheadTokens;

    var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");
    var tokenIds = tokenizer.EncodeToIds(text);

    var chunks = new List<string>();
    var currentChunkTokens = new List<int>();

    foreach (var tokenId in tokenIds)
    {
        if (currentChunkTokens.Count + 1 > effectiveMaxTokens)
        {
            chunks.Add(tokenizer.Decode(currentChunkTokens));
            currentChunkTokens.Clear();
        }

        currentChunkTokens.Add(tokenId);
    }

    if (currentChunkTokens.Any())
    {
        chunks.Add(tokenizer.Decode(currentChunkTokens));
    }

    Console.WriteLine($"Split text into {chunks.Count} chunks.");
    return chunks;
}


    private static async Task<(string Title, string Body, int Number)> FetchLatestGitHubIssueAsync()
    {
        try
        {
            var processStartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = "issue list --limit 1 --json title,body,number",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var output = await process.StandardOutput.ReadToEndAsync();
            var error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                var jsonDocument = JsonDocument.Parse(output);
                var issue = jsonDocument.RootElement.EnumerateArray().FirstOrDefault();

                if (issue.ValueKind == JsonValueKind.Object)
                {
                    return (issue.GetProperty("title").GetString() ?? "Untitled",
                            issue.GetProperty("body").GetString() ?? "No body content",
                            issue.GetProperty("number").GetInt32());
                }
            }
            else
            {
                Console.WriteLine($"Error fetching GitHub issues: {error}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching GitHub issues: {ex.Message}");
        }

        return ("No Issue Found", "Could not retrieve the latest issue.", 0);
    }

    private static async Task<string> AnalyzeIssueWithRAG(Kernel kernel, List<string> chunks)
    {
        var finalResult = new StringBuilder();
        var tokenizer = TiktokenTokenizer.CreateForModel("gpt-4");

        foreach (var chunk in chunks)
        {
            Console.WriteLine($"Processing chunk (length: {chunk.Length}): {chunk.Substring(0, Math.Min(50, chunk.Length))}...");

            try
            {
                // Validate token count
                var tokenCount = tokenizer.EncodeToIds(chunk).Count;

                if (tokenCount + PromptOverheadTokens > 4096)
                {
                    Console.WriteLine($"Skipping chunk due to token limit (tokens: {tokenCount}).");
                    continue;
                }

                var response = kernel.InvokePromptStreamingAsync(
                    promptTemplate: @"""
    Chunk: {{input}}
    Provide a detailed analysis using memory facts:
    {{#with (SearchPlugin-Search input)}}
    {{#each this}}
        {{this}}
        -----------------
    {{/each}}
    {{/with}}
    """,
                    templateFormat: "handlebars",
                    promptTemplateFactory: new HandlebarsPromptTemplateFactory(),
                    arguments: new KernelArguments
                    {
                        { "input", chunk },
                        { "collection", "ExampleCollection" }
                    });

                var chunkResult = new StringBuilder();
                await foreach (var message in response)
                {
                    Console.Write(message);
                    chunkResult.Append(message);
                }

                finalResult.AppendLine(chunkResult.ToString());
                finalResult.AppendLine("\n-----------------\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing chunk: {ex.Message}");
            }
        }

        return finalResult.ToString();
    }

    private static async Task<string> InterpretWithPHI3Streaming(Kernel kernel, string question)
    {
        var result = new StringBuilder();

        try
        {
            // Invoke the prompt and handle the response as a stream
            var response = kernel.InvokePromptStreamingAsync(
                promptTemplate: @"""
    Question: {{input}}
    Answer using retrieved context and memory:
    {{#with (SearchPlugin-Search input)}}
    {{#each this}}
        {{this}}
        -----------------
    {{/each}}
    {{/with}}
    """,
                templateFormat: "handlebars",
                promptTemplateFactory: new HandlebarsPromptTemplateFactory(),
                arguments: new KernelArguments
                {
                    { "input", question },
                    { "collection", "ExampleCollection" }
                });

            // Process the response stream
            await foreach (var message in response)
            {
                Console.Write(message); // Output each part of the response as it arrives
                result.Append(message); // Collect the response parts
            }

            if (result.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\n(No output generated by the model.)");
                Console.ResetColor();
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Error while streaming response: {ex.Message}");
            Console.ResetColor();
        }

        return result.ToString(); // Return the full response
    }
    
 
}

/// <summary>
/// Represents embedding data stored in the memory
/// </summary>
internal sealed class InformationItem
{
    [VectorStoreRecordKey]
    [TextSearchResultName]
    public string Id { get; set; } = string.Empty;

    [VectorStoreRecordData]
    [TextSearchResultValue]
    public string Text { get; set; } = string.Empty;

    [VectorStoreRecordVector(Dimensions: 384)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
