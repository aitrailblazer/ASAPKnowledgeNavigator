﻿using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Cosmos.Copilot.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using Microsoft.SemanticKernel.TemplateEngine;
using Microsoft.SemanticKernel.Prompty;
using Kernel = Microsoft.SemanticKernel.Kernel;
using Azure.AI.OpenAI;
using Azure.AI.Inference;
using Microsoft.Extensions.AI;


using Azure.Core;
//using Azure.Identity;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

//using AITrailblazer.net.Services;

/// <summary>
/// Semantic Kernel implementation for Azure OpenAI.
/// </summary>
public class SemanticKernelService
{
    // Class-level fields for the variables
    private readonly string endpoint;
    private readonly string endpointEmbedding;
    private readonly string completionDeploymentName;
    private readonly string embeddingDeploymentName;
    private readonly string apiKey;
    private readonly string apiKeyEmbedding;
    private readonly int dimensions;
    private readonly ILogger<SemanticKernelService> _logger;

    // Semantic Kernel instance
    private readonly Kernel kernel;


    /// <summary>
    /// Creates a new instance of the Semantic Kernel.
    /// </summary>
    /// <param name="endpoint">Endpoint URI.</param>
    /// <param name="completionDeploymentName">Name of the deployed Azure OpenAI completion model.</param>
    /// <param name="embeddingDeploymentName">Name of the deployed Azure OpenAI embedding model.</param>
    /// <param name="apiKey">API key for authentication.</param>
    /// <param name="dimensions">Dimensions for the embedding model.</param>
    /// <param name="logger">Logger instance for logging.</param>
    /// <exception cref="ArgumentNullException">Thrown when endpoint, key, or modelName is either null or empty.</exception>


    public SemanticKernelService(
        string endpoint,
        string endpointEmbedding,
        string completionDeploymentName,
        string embeddingDeploymentName,
        string apiKey,
        string apiKeyEmbedding,
        int dimensions,
        ILogger<SemanticKernelService> logger) // Add logger parameter
    {
        // Initialize class-level fields
        this.endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
        this.endpointEmbedding = endpointEmbedding ?? throw new ArgumentNullException(nameof(endpointEmbedding));
        this.completionDeploymentName = completionDeploymentName ?? throw new ArgumentNullException(nameof(completionDeploymentName));
        this.embeddingDeploymentName = embeddingDeploymentName ?? throw new ArgumentNullException(nameof(embeddingDeploymentName));
        this.apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        this.apiKeyEmbedding = apiKeyEmbedding ?? throw new ArgumentNullException(nameof(apiKeyEmbedding));
        this.dimensions = dimensions;

        _logger = logger ?? throw new ArgumentNullException(nameof(logger)); // Initialize logger

        // Initialize the kernel
        kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: completionDeploymentName,
                endpoint: endpoint,
                apiKey: apiKey)
            .AddAzureOpenAITextEmbeddingGeneration(
                deploymentName: embeddingDeploymentName,
                endpoint: endpointEmbedding,
                apiKey: apiKeyEmbedding,
                dimensions: dimensions)
            .Build();
    }
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
    /// Creates a kernel builder using the pre-initialized settings from the constructor.
    /// </summary>
    /// <returns>An instance of IKernelBuilder.</returns>
    public IKernelBuilder CreateKernelBuilder(string modelId)
    {
        // Create HttpClient with custom headers and timeout
        var httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(300) // Set timeout to 300 seconds
        };

        // Uncomment the following line to add custom headers if needed
        // httpClient.DefaultRequestHeaders.Add("My-Custom-Header", "My Custom Value");

        IKernelBuilder kernelBuilder = Kernel.CreateBuilder();

        // Configure chat completion
        kernelBuilder.AddAzureOpenAIChatCompletion(
            deploymentName: modelId,//completionDeploymentName,
            endpoint: endpoint,
            apiKey: apiKey,
            httpClient: httpClient);

        // Configure text embedding generation
        kernelBuilder.AddAzureOpenAITextEmbeddingGeneration(
            deploymentName: embeddingDeploymentName,
            endpoint: endpointEmbedding,
            apiKey: apiKeyEmbedding,
            dimensions: dimensions,
            httpClient: httpClient);

        return kernelBuilder;
    }
    static string GetEnvironmentVariable(string variableName)
    {
        return Environment.GetEnvironmentVariable(variableName)
            ?? throw new ArgumentNullException(variableName, $"{variableName} is not set in environment variables.");
    }

    /// <summary>
    /// Generates a knowledge base completion using the provided context data.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="contextData">Knowledge base context data.</param>
    /// <returns>A Task representing the asynchronous operation, with a tuple containing the generated completion and the number of tokens used.</returns>

    public async Task<(string generatedCompletion, int tokens)> GetASAPQuick01<T>(
       string input,
       KnowledgeBaseItem contextData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                // _logger.LogInformation("Input cannot be null or empty.");
                return ("Invalid input.", 0);
            }
            IKernelBuilder kernelBuilder = CreateKernelBuilder("gpt-4o");
            //kernelBuilder.Plugins.AddFromType<TimeInformation>();
            Kernel kernel = kernelBuilder.Build();

            var promptyTemplate = @"""
---
name: KnowledgeBaseAssistant
description: AI Assistant for Extracting Key Information from Knowledge Base Context
authors:
  - KnowledgeBaseAssistant
model:
  api: completion
  configuration:
    type: azure_openai
  parameters:
    tools_choice: auto
---
system:
You are an intelligent assistant designed to extract relevant and concise information from a knowledge base context.
Use the provided knowledge base title: {{title}} and context: {{context}} to answer accurately and concisely. Follow these instructions:

- Extract key details such as title, description, and reference link.
- Do not include unrelated information or make assumptions beyond the context provided.
- If no relevant answer exists, respond with: ""I could not find an answer in the knowledge base.""

Format the response clearly as follows:
- **Title**: {{title}} {Generate a title from the context}

- **Content Summary**: {A summary of the content, including key points or highlights}

Knowledge base context is provided below:

user:
- input: {{input}}

assistant:
            """;

            // Validate prompty template
            if (string.IsNullOrWhiteSpace(promptyTemplate))
            {
                // _logger.LogInformation("Prompty template content is empty.");
                return ("Prompty template is invalid.", 0);
            }

            double temperature = 0.1;
            double topP = 0.1;
            int seed = 356;
            int maxTokens = 4028;
            // Enable automatic function calling
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = maxTokens,
                Seed = seed,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            string title = contextData.Title;
            string context = contextData.Content;
            // Update prompty template
            promptyTemplate = UpdatepromptyTemplate(
                input,
                title,
                context,
                promptyTemplate);
            //_logger.LogInformation($"GetASAPQuick promptyTemplate: {promptyTemplate}");

            // Create kernel function from prompty
            KernelFunction kernelFunction;
            try
            {
                kernelFunction = kernel.CreateFunctionFromPrompty(promptyTemplate);
            }
            catch (ArgumentException ex)
            {
                // _logger.LogInformation($"Error creating function from prompty: {ex.Message}");
                return ("Failed to create function from prompty template. Please check the template content.", 0);
            }
            catch (InvalidOperationException ex)
            {
                // _logger.LogInformation($"Invalid operation creating function from prompty: {ex.Message}");
                return ("Failed to create function from prompty template due to an invalid operation.", 0);
            }
            catch (Exception ex)
            {
                // _logger.LogInformation($"Unexpected error creating function from prompty: {ex.Message}");
                return ("An unexpected error occurred while creating the function from the prompty template.", 0);
            }

            // _logger.LogInformation($"GetASAPQuick Kernel function created from prompty file: {kernelFunction.Name}");
            // _logger.LogInformation($"GetASAPQuick Kernel function description: {kernelFunction.Description}");
            // _logger.LogInformation($"GetASAPQuick Kernel function parameters: {kernelFunction.Metadata.Parameters}");

            // _logger.LogInformation($"GetASAPQuick input {input}");

            var arguments = new KernelArguments(executionSettings)
            {
                // Custom arguments can be added here
            };

            // _logger.LogInformation($"GetASAPQuick kernel.InvokeAsync ");

            // Execute the kernel function
            try
            {
                var result = await kernel.InvokeAsync(kernelFunction, arguments);
                var completion = result.GetValue<string>();

                // Extract usage metrics if available
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
            catch (ArgumentException ex)
            {
                // _logger.LogInformation($"GetASAPQuick Argument error: {ex.Message}");
                return ("An error occurred with the arguments. Please try again.", 0);
            }
            catch (InvalidOperationException ex)
            {
                // _logger.LogInformation($"GetASAPQuick Invalid operation: {ex.Message}");
                return ("An invalid operation occurred during function execution. Please try again.", 0);
            }
            catch (Exception ex)
            {
                // _logger.LogError(ex, "GetASAPQuick An unexpected error occurred.");
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception Details");
                }
                return ("A critical error occurred. Please contact support.", 0);
            }
        }
        catch (Exception ex)
        {
            // _logger.LogError(ex, "GetASAPQuick  A critical error occurred.");
            if (ex.InnerException != null)
            {
                // _logger.LogError(ex.InnerException, "Inner Exception Details");
            }
            return ("A critical error occurred. Please contact support.", 0);
        }
    }
    public async Task<(string generatedCompletion, int tokens)> GetASAPQuick<T>(
    string input,
    KnowledgeBaseItem contextData)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return ("Invalid input.", 0);
            }
            string endpoint = GetEnvironmentVariable("PHI_ENDPOINT");
            string apiKey = GetEnvironmentVariable("PHI_KEY");

            string modelId = "phi-3-5-moe-instruct";

            // Kernel builder setup
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureAIInferenceChatCompletion(
                endpoint: new Uri(endpoint),
                apiKey: apiKey,
                modelId: modelId
            );

            Kernel kernel = kernelBuilder.Build();

            // Define the function definition template
            var FunctionDefinition = @"""
You are an intelligent assistant designed to extract relevant and concise information from a knowledge base context.
Use the provided knowledge base title: {{$title}} and context: {{$context}} to answer accurately and concisely. Follow these instructions:

- Extract key details such as title, description, and reference link.
- Do not include unrelated information or make assumptions beyond the context provided.
- If no relevant answer exists, respond with: ""I could not find an answer in the knowledge base.""

Format the response clearly as follows:
- **Title**: {{$title}} {Generate a title from the context}

- **Content Summary**: {A summary of the content, including key points or highlights}

Knowledge base context is provided below:
{{$input}}
        """;

            // Create the function
            var knowledgeBaseFunction = kernel.CreateFunctionFromPrompt(FunctionDefinition);

            // Chat settings
            double temperature = 0.1;
            double topP = 0.1;
            int maxTokens = 4028;
            int seed = 356;

            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = temperature,
                TopP = topP,
                MaxTokens = maxTokens,
                Seed = seed
            };

            // Prepare arguments
            string title = contextData.Title;
            string context = contextData.Content;

            var arguments = new KernelArguments(executionSettings)
            {
                ["title"] = title,
                ["context"] = context,
                ["input"] = input
            };

            try
            {
                // Invoke the function
                var result = await knowledgeBaseFunction.InvokeAsync(kernel, arguments);
                var completion = result.GetValue<string>();

                // Extract completion tokens (if applicable)
                int completionTokens = 0; // Replace with actual token extraction logic if available

                // Extract the page number from the reference link (if applicable)
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
                completion += formattedReference;

                _logger.LogInformation("Generated response successfully with {Tokens} tokens.", completionTokens);

                return (completion, completionTokens);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during function execution.");
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner Exception Details");
                }
                return ("An error occurred during function execution. Please try again.", 0);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "A critical error occurred.");
            if (ex.InnerException != null)
            {
                _logger.LogError(ex.InnerException, "Inner Exception Details");
            }
            return ("A critical error occurred. Please contact support.", 0);
        }
    }

    /// <summary>
    /// Updates the prompty template with the provided input, title, and context.
    /// </summary>
    /// <param name="input">User input.</param>
    /// <param name="title">Knowledge base title.</param>
    /// <param name="context">Knowledge base context.</param>
    /// <param name="promptyTemplate">Prompty template to update.</param>
    /// <returns>Updated prompty template.</returns>

    public string UpdatepromptyTemplate(
    string input,
    string title,
    string context,
    string promptyTemplate)
    {

        // Add replacements for placeholders
        var replacements = new Dictionary<string, string>
            {
                { "{{input}}", string.IsNullOrEmpty(input) ? "" : $"\n# Input:\n<input>{input}</input>" },
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
    /// Generates a knowledge base completion using the provided context data.
    /// </summary>
    /// <param name="categoryId">Category ID.</param>
    /// <param name="contextWindow">Context window containing knowledge base items.</param>
    /// <param name="contextData">Knowledge base context data.</param>
    /// <param name="promptText">Prompt text.</param>
    /// <param name="useChatHistory">Flag indicating whether to use chat history.</param>
    /// <returns>A Task representing the asynchronous operation, with a tuple containing the generated completion and the number of tokens used.</returns>

    public async Task<(string generatedCompletion, int tokens)> GetRagKnowledgeBaseCompletionAsync<T>(
        string categoryId,
        List<KnowledgeBaseItem> contextWindow,
        KnowledgeBaseItem contextData,
        string promptText,
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
