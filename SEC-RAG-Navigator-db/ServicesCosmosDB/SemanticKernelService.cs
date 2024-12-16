using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Cosmos.Copilot.Models;
using Microsoft.SemanticKernel.Embeddings;
using Azure.AI.OpenAI;
using Azure.Core;
//using Azure.Identity;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

//using AITrailblazer.net.Services;

namespace Cosmos.Copilot.Services;

/// <summary>
/// Semantic Kernel implementation for Azure OpenAI.
/// </summary>
public class SemanticKernelService
{
    //Semantic Kernel
    readonly Kernel kernel;

    /// <summary>
    /// System prompt to guide the model as a knowledge base assistant with specific context and formatting.
    /// </summary>
    private readonly string _systemPromptKnowledgeBase = @"
You are an intelligent assistant designed to extract relevant and concise information from a knowledge base context.
Use the provided knowledge base context below to answer accurately and concisely. Follow these instructions:

- Extract key details such as title, description, and reference link.
- Do not include unrelated information or make assumptions beyond the context provided.
- If no relevant answer exists, respond with: ""I could not find an answer in the knowledge base.""

Format the response clearly as follows:
- **Title**: {Title}
- **Content Summary**: {A summary of the content, including key points or highlights}

Knowledge base context is provided below:
";


    /// <summary>    
    /// System prompt to send with user prompts to instruct the model for summarization
    /// </summary>
    private readonly string _summarizePrompt = @"
        Summarize this text. One to three words maximum length. 
        Plain text only. No punctuation, markup or tags.";

    /// <summary>
    /// Creates a new instance of the Semantic Kernel.
    /// </summary>
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="completionDeploymentName">Name of the deployed Azure OpenAI completion model.</param>
    /// <param name="embeddingDeploymentName">Name of the deployed Azure OpenAI embedding model.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>
    /// <remarks>
    /// This constructor will validate credentials and create a Semantic Kernel instance.
    /// </remarks>
    private readonly ILogger<SemanticKernelService> _logger;

    public SemanticKernelService(
        string endpoint,
        string completionDeploymentName,
        string embeddingDeploymentName,
        string apiKey,
        int dimensions,
        ILogger<SemanticKernelService> logger) // Add logger parameter
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Initialize logger

        kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(deploymentName: completionDeploymentName, endpoint: endpoint, apiKey: apiKey)
            .AddAzureOpenAITextEmbeddingGeneration(
                deploymentName: embeddingDeploymentName, 
                endpoint: endpoint, 
                apiKey: apiKey,
                dimensions: dimensions)
            .Build();
    }



    public async Task<(string generatedCompletion, int tokens)> GetRagKnowledgeBaseCompletionAsync<T>(
        string categoryId,
        List<KnowledgeBaseItem> contextWindow,
        KnowledgeBaseItem contextData,
        bool useChatHistory)
    {
        // Initialize the chat history with structured context data
        var skChatHistory = new ChatHistory();

        // Create a structured context for the model (exclude ReferenceDescription and ReferenceLink)
        string structuredContext = $"""
            "Title": "{contextData.Title}",
            "Content": "{contextData.Content}"
        """;

        _logger.LogInformation($"GetRagKnowledgeBaseCompletionAsync structuredContext: {structuredContext}");

        skChatHistory.AddSystemMessage($"{_systemPromptKnowledgeBase}{structuredContext}");

        // Define execution settings
        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.3 },
                { "TopP", 0.8 },
                { "MaxTokens", 1000 }
            }
        };

        try
        {
            // Generate the response using the configured kernel service
            var response = await kernel.GetRequiredService<IChatCompletionService>()
                                    .GetChatMessageContentAsync(skChatHistory, settings);

            string completion = response.Items[0].ToString()!;

            // Extract usage metrics if available
            var usage = response.Metadata?["Usage"];
            int completionTokens = 0; // usage != null ? Convert.ToInt32(usage["CompletionTokens"]) : 0;

            // Extract the page number if available
            string pageNumber = "";
            if (contextData.ReferenceLink.Contains("#page="))
            {
                var match = Regex.Match(contextData.ReferenceLink, @"#page=(\d+)");
                if (match.Success)
                {
                    pageNumber = $" (Page {match.Groups[1].Value})";
                }
            }

            // Append the reference link and page number to the completion
            string formattedReference = $"\n\nReference: [{contextData.ReferenceDescription}]({contextData.ReferenceLink}){pageNumber}";
            //"referenceLink": "2e58c7ce-9814-4e3d-9e88-467669ba3f5c/8f22704e-0396-4263-84a7-63310d3f39e7/Documents/Default/semantickernel.pdf#page=13",
            _logger.LogInformation("GetRagKnowledgeBaseCompletionAsync formattedReference ", formattedReference);

            completion += formattedReference;

            _logger.LogInformation("Generated response successfully with {Tokens} tokens.", completionTokens);

            return (completion, completionTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating knowledge base completion.");
            throw;
        }
    }

    /// <summary>
    /// Generates embeddings from the deployed OpenAI embeddings model using Semantic Kernel.
    /// </summary>
    /// <param name="input">Text to send to OpenAI.</param>
    /// <returns>Array of vectors from the OpenAI embedding model deployment.</returns>
    public async Task<float[]> GetEmbeddingsAsync(string text)
    {
        var embeddings = await kernel.GetRequiredService<ITextEmbeddingGenerationService>().GenerateEmbeddingAsync(text);

        float[] embeddingsArray = embeddings.ToArray();

        return embeddingsArray;
    }

    /// <summary>
    /// Sends the existing conversation to the Semantic Kernel and returns a two word summary.
    /// </summary>
    /// <param name="sessionId">Chat session identifier for the current conversation.</param>
    /// <param name="conversationText">conversation history to send to Semantic Kernel.</param>
    /// <returns>Summarization response from the OpenAI completion model deployment.</returns>
    public async Task<string> SummarizeConversationAsync(string conversation)
    {
        //return await summarizePlugin.SummarizeConversationAsync(conversation, kernel);

        var skChatHistory = new ChatHistory();
        skChatHistory.AddSystemMessage(_summarizePrompt);
        skChatHistory.AddUserMessage(conversation);

        PromptExecutionSettings settings = new()
        {
            ExtensionData = new Dictionary<string, object>()
            {
                { "Temperature", 0.0 },
                { "TopP", 1.0 },
                { "MaxTokens", 100 }
            }
        };


        var result = await kernel.GetRequiredService<IChatCompletionService>().GetChatMessageContentAsync(skChatHistory, settings);

        string completion = result.Items[0].ToString()!;

        return completion;
    }
}
