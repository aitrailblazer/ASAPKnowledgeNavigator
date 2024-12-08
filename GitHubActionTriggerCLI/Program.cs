using System;
using System.ClientModel;

using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Azure;
using Azure.AI.OpenAI;

using OpenAI.Chat;

using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
class Program
{
    static async Task Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("Usage: dotnet run <accessCode>");
            return;
        }

        string accessCode = args[0];
        string? knownSecretHash = Environment.GetEnvironmentVariable("ACCESS_CODE_HASH");

        if (string.IsNullOrEmpty(knownSecretHash))
        {
            Console.WriteLine("ACCESS_CODE_HASH environment variable not found. Please set it before running the application.");
            return;
        }

        if (await ValidateAccessCode(accessCode, knownSecretHash))
        {
            Console.WriteLine("Access code is valid. Generating GPT-based analysis...");
            string logData = GetLogData();
            var (issueTitle, logAnalysisContent) = await GenerateGPTAnalysis(logData);

            if (string.IsNullOrEmpty(issueTitle) || string.IsNullOrEmpty(logAnalysisContent))
            {
                Console.WriteLine("Failed to generate GPT-based analysis. Exiting.");
                return;
            }

            Console.WriteLine("GPT-based analysis generated. Triggering GitHub Action...");
            await TriggerGitHubAction(accessCode, issueTitle, logAnalysisContent);
        }
        else
        {
            Console.WriteLine("Invalid access code. Access denied.");
        }
    }

    private static async Task<bool> ValidateAccessCode(string accessCode, string knownSecretHash)
    {
        await Task.Yield(); // Make the method truly async
        using var sha256 = SHA256.Create();
        var codeBytes = Encoding.UTF8.GetBytes(accessCode);
        var computedHash = BitConverter.ToString(sha256.ComputeHash(codeBytes)).Replace("-", "").ToLower();
        return knownSecretHash.Equals(computedHash, StringComparison.OrdinalIgnoreCase);
    }

    private static string GetLogData()
    {
        return @"
2023-11-19T12:30:00.000Z [info]  Application starting...
2023-11-19T12:30:15.000Z [info]  Database connection established.
2023-11-19T12:32:00.000Z [warn]  Database query took longer than 500ms, potential performance bottleneck.
2023-11-19T12:33:00.000Z [error] Failed to connect to external service: Connection refused.
2023-11-19T12:35:00.000Z [info]  External service now reachable, resuming normal operation.
2023-11-19T12:40:00.000Z [info]  Processing batch job #12345.
2023-11-19T12:45:00.000Z [warn]  Memory usage is at 85%, monitor for potential issues.
2023-11-19T12:50:00.000Z [error] Out of memory error during batch job processing. Job #12345 was terminated.
2023-11-19T12:55:00.000Z [info]  Application restarted after OutOfMemoryError, health checks passed.
2023-11-19T13:00:00.000Z [info]  New request received at /api/v1/data endpoint.
2023-11-19T13:05:00.000Z [warn]  Retrying operation after temporary network issue.
";
    }

    private static async Task<(string issueTitle, string logAnalysisContent)> GenerateGPTAnalysis(string logData)
    {
        try
        {
            string? endpointString = Environment.GetEnvironmentVariable("ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("MODEL");

            if (string.IsNullOrEmpty(endpointString) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Missing required environment variables (ENDPOINT, API_KEY, or MODEL)");
            }

            // Create a kernel with Azure OpenAI chat completion
            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: modelName,
                    endpoint: endpointString,
                    apiKey: apiKey)
                .Build();

            // Define the semantic function for the title
            const string TitleFunction = """
You are a Kubernetes pod analysis assistant.

Analyze the following pod log data and create:
- A concise and descriptive title summarizing the pod health and issues (without prefixes like "Title:").

Pod Log Data:
{{ $logData }}

[TASK]
Create:
- Summary: (Concise title summarizing the pod health and issues)
""";

            // Define the semantic function for the analysis
            const string AnalysisFunction = """
You are a Kubernetes pod analysis assistant.

Analyze the following pod log data and create a structured summary including:
   - Key events related to pod lifecycle and health.
   - Warnings and errors with timestamps.
   - Recommendations for resolving issues if applicable.

Pod Log Data:
{{ $logData }}

[TASK]
Create:
- Analysis:
  - Key Events:
    (List significant events chronologically)
  - Warnings and Errors:
    (Summarize warnings and errors with details and timestamps)
  - Recommendations:
    (Provide actionable recommendations based on the analysis)
""";

            // Configure execution settings
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.7f,
                MaxTokens = 1000,
                ToolCallBehavior = ToolCallBehavior.AutoInvokeKernelFunctions
            };

            // Create functions for title and analysis
            var titleOracle = kernel.CreateFunctionFromPrompt(TitleFunction, executionSettings);
            var analysisOracle = kernel.CreateFunctionFromPrompt(AnalysisFunction, executionSettings);

            // Define arguments for the prompt
            var arguments = new KernelArguments
            {
                ["logData"] = logData
            };

            // Invoke the kernel for the title
            var titleResponse = await kernel.InvokeAsync(titleOracle, arguments);
            string issueTitle = titleResponse.GetValue<string>()?.Trim() ?? "Default Title";

            // Ensure the title has no prefixes
            issueTitle = issueTitle.Replace("**Title:**", "").Trim();
            // Remove unwanted prefix "Pod Health Summary:" if present
            issueTitle = issueTitle.Replace("Pod Health Summary:", "").Trim();

            // Invoke the kernel for the analysis
            var analysisResponse = await kernel.InvokeAsync(analysisOracle, arguments);
            string logAnalysisContent = analysisResponse.GetValue<string>()?.Trim() ?? "No analysis available";

            return (issueTitle, logAnalysisContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred during GPT analysis: {ex.Message}");
            return ("Error in Analysis", ex.Message);
        }
    }
    private static async Task TriggerGitHubAction(string accessCode, string issueTitle, string logAnalysisContent)
    {
        string escapedAccessCode = EscapeForCLI(accessCode);
        string escapedIssueTitle = EscapeForCLI(issueTitle);
        string escapedLogAnalysis = EscapeForCLI(logAnalysisContent);

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "gh",
            Arguments = $"workflow run simple-log-analysis-test.yml -f access_code=\"{escapedAccessCode}\" -f issue_title=\"{escapedIssueTitle}\" -f log_analysis=\"{escapedLogAnalysis}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        try
        {
            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            await Task.WhenAll(outputTask, errorTask);
            string output = await outputTask;
            string error = await errorTask;

            Console.WriteLine($"GitHub Action Output:\n{output}");

            if (process.WaitForExit(30000)) // Wait for 30 seconds
            {
                if (process.ExitCode != 0)
                {
                    Console.WriteLine($"Error when triggering GitHub Action:\n{error}");
                }
                else
                {
                    Console.WriteLine("GitHub Action successfully triggered.");
                }
            }
            else
            {
                Console.WriteLine("GitHub Action did not complete within the expected timeframe.");
                try
                {
                    process.Kill();
                }
                catch (InvalidOperationException)
                {
                    // Process may have already exited
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while triggering the GitHub Action: {ex.Message}");
        }
    }

    private static string EscapeForCLI(string input)
    {
        return input.Replace("\"", "\\\"").Replace(Environment.NewLine, "\\n");
    }
}