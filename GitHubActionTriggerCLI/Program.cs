using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Collections.Generic;
using k8s;
using k8s.Models;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using System.Text.Json;
using System.Linq; // Add this for LINQ methods

class Program
{
    /// <summary>
    /// Entry point of the application.
    /// Validates the provided access code, lists Kubernetes pods, filters out failing ones, describes them,
    /// generates a GPT-based analysis, and creates separate GitHub issues for each failing pod.
    /// </summary>
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
            Console.WriteLine("Access code is valid. Listing Kubernetes pods...");
            var failingPods = await GetFailingPodsAsync();

            if (failingPods.Count > 0)
            {
                Console.WriteLine($"Found {failingPods.Count} failing pods. Fetching detailed descriptions...");

                foreach (var pod in failingPods)
                {
                    string podName = pod.Metadata?.Name ?? "Unknown";
                    string namespaceName = pod.Metadata?.NamespaceProperty ?? "Unknown";

                    // Skip pods that do not have the reason "ProviderFailed"
                    if (!string.Equals(pod.Status?.Reason, "ProviderFailed", StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine($"Skipping pod: {podName} as it does not have the reason 'ProviderFailed'.");
                        continue;
                    }

                    Console.WriteLine($"Describing pod: {podName}");
                    var podDescription = await GetPodDescriptionAsync(pod);

                    // Ensure the description is not empty before proceeding
                    if (string.IsNullOrWhiteSpace(podDescription))
                    {
                        Console.WriteLine($"Skipping pod: {podName} due to missing description.");
                        continue;
                    }

                    Console.WriteLine("Pod description fetched successfully.");
                    Console.WriteLine($"Generating GPT-based analysis for pod: {podName}...");
                    Console.WriteLine($"podDescription: {podDescription}");

                    string logData = GenerateLogDataForPod(pod, podDescription);
                    Console.WriteLine($"logData: {logData}");

                    var (issueTitle, logAnalysisContent) = await GenerateGPTAnalysis(logData, podName, namespaceName, podDescription);

                    Console.WriteLine($"GPT-based analysis generated for pod: {podName}.");
                    Console.WriteLine($"Issue Title: {issueTitle}");
                    Console.WriteLine($"Log Analysis Content: {logAnalysisContent}");

                    // Uncomment the following block to create GitHub issues
                    if (!string.IsNullOrEmpty(issueTitle) && !string.IsNullOrEmpty(logAnalysisContent))
                    {
                        Console.WriteLine($"GPT-based analysis generated for pod: {podName}. Creating GitHub issue...");
                        await CreateGitHubIssue(issueTitle, logAnalysisContent);
                    }
                    else
                    {
                        Console.WriteLine($"Failed to generate GPT-based analysis for pod: {podName}. Skipping issue creation.");
                    }
                    
                }
            }
            else
            {
                Console.WriteLine("No failing pods found. Exiting.");
            }
        }
        else
        {
            Console.WriteLine("Invalid access code. Access denied.");
        }
    }
    /// <summary>
    /// Validates the provided access code by comparing its SHA-256 hash with a known secret hash.
    /// </summary>
    private static async Task<bool> ValidateAccessCode(string accessCode, string knownSecretHash)
    {
        await Task.Yield();
        using var sha256 = SHA256.Create();
        var codeBytes = Encoding.UTF8.GetBytes(accessCode);
        var computedHash = BitConverter.ToString(sha256.ComputeHash(codeBytes)).Replace("-", "").ToLower();
        return knownSecretHash.Equals(computedHash, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Lists all Kubernetes pods and filters out the failing ones.
    /// </summary>
    private static async Task<List<V1Pod>> GetFailingPodsAsync()
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        var client = new Kubernetes(config);

        var podList = await client.ListPodForAllNamespacesAsync();
        var failingPods = new List<V1Pod>();

        foreach (var pod in podList.Items)
        {
            if (pod.Status.Phase == "Failed" ||
                (pod.Status.ContainerStatuses != null &&
                pod.Status.ContainerStatuses.Any(c => c.State.Waiting != null || c.State.Terminated != null)))
            {
                failingPods.Add(pod);
            }
        }

        return failingPods;
    }

    /// <summary>
    /// Fetches a detailed description of the given pod, similar to "kubectl describe".
    /// </summary>
    private static async Task<string> GetPodDescriptionAsync(V1Pod pod)
    {
        var config = KubernetesClientConfiguration.BuildConfigFromConfigFile();
        Console.WriteLine($"Kubeconfig context: {config.CurrentContext}");
        var client = new Kubernetes(config);

        var namespaceName = pod.Metadata?.NamespaceProperty ?? "Unknown";
        var podName = pod.Metadata?.Name ?? "Unknown";

        try
        {
            Console.WriteLine($"Fetching details for pod: {podName} in namespace: {namespaceName}");

            // Fetch pod details
            var podDetails = await client.ReadNamespacedPodAsync(podName, namespaceName);
            if (podDetails == null)
            {
                Console.WriteLine("Pod details are null.");
                return "Error: Pod details could not be retrieved.";
            }
            // Check if the pod has Reason: ProviderFailed
            if (!string.Equals(podDetails.Status?.Reason, "ProviderFailed", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Pod {podName} does not have 'ProviderFailed' reason. Skipping.");
                return $"Pod {podName} does not have 'ProviderFailed' reason.";
            }
            Console.WriteLine("Pod details fetched successfully.");

            // Fetch events
            var events = await CoreV1OperationsExtensions.ListNamespacedEventAsync(
                client,
                namespaceName,
                fieldSelector: $"involvedObject.name={podName}"
            );

            if (events == null || events.Items.Count == 0)
            {
                Console.WriteLine("No events found for the pod.");
            }

            var description = new StringBuilder();

            // General Pod Information
            description.AppendLine($"Name: {podDetails.Metadata?.Name}");
            description.AppendLine($"Namespace: {podDetails.Metadata?.NamespaceProperty}");
            description.AppendLine($"Priority: {podDetails.Spec?.Priority?.ToString() ?? "N/A"}");
            description.AppendLine($"Priority Class Name: {podDetails.Spec?.PriorityClassName ?? "N/A"}");
            description.AppendLine($"Service Account: {podDetails.Spec?.ServiceAccountName}");
            description.AppendLine($"Node: {podDetails.Spec?.NodeName ?? "N/A"}");
            description.AppendLine($"Labels: {string.Join(", ", podDetails.Metadata?.Labels?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? new List<string>())}");
            description.AppendLine($"Annotations: {string.Join(", ", podDetails.Metadata?.Annotations ?? new Dictionary<string, string>())}");
            description.AppendLine($"Status: {podDetails.Status?.Phase}");
            description.AppendLine($"Reason: {podDetails.Status?.Reason ?? "N/A"}");
            description.AppendLine($"Message: {podDetails.Status?.Message ?? "N/A"}");
            description.AppendLine($"IP: {podDetails.Status?.PodIP ?? "N/A"}");

            // Controlled By
            if (podDetails.Metadata?.OwnerReferences != null && podDetails.Metadata.OwnerReferences.Count > 0)
            {
                description.AppendLine($"Controlled By: {string.Join(", ", podDetails.Metadata.OwnerReferences.Select(o => $"{o.Kind}/{o.Name}"))}");
            }

            // Containers
            description.AppendLine("Containers:");
            foreach (var container in podDetails.Spec?.Containers ?? new List<V1Container>())
            {
                description.AppendLine($"  {container.Name}:");
                description.AppendLine($"    Image: {container.Image}");
                description.AppendLine($"    Ports: {string.Join(", ", container.Ports?.Select(p => $"{p.ContainerPort}/{p.Protocol}") ?? new List<string>())}");
                description.AppendLine($"    Args: {string.Join(" ", container.Args ?? new List<string>())}");
                description.AppendLine($"    Limits: {string.Join(", ", container.Resources?.Limits?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? new List<string>())}");
                description.AppendLine($"    Requests: {string.Join(", ", container.Resources?.Requests?.Select(kvp => $"{kvp.Key}={kvp.Value}") ?? new List<string>())}");

                // Environment Variables
                description.AppendLine("    Environment:");
                foreach (var envVar in container.Env ?? new List<V1EnvVar>())
                {
                    description.AppendLine($"      {envVar.Name}: {envVar.Value ?? $"ValueFrom: {envVar.ValueFrom?.ToString() ?? "N/A"}"}");
                }

                // Volume Mounts
                description.AppendLine("    Mounts:");
                foreach (var mount in container.VolumeMounts ?? new List<V1VolumeMount>())
                {
                    // Use GetValueOrDefault or null-coalescing operator to handle nullable boolean
                    var isReadOnly = mount.ReadOnlyProperty ?? false; // Default to false if null
                    description.AppendLine($"      {mount.MountPath} from {mount.Name} ({(isReadOnly ? "ro" : "rw")})");
                }

            }

            // Volumes
            description.AppendLine("Volumes:");
            foreach (var volume in podDetails.Spec?.Volumes ?? new List<V1Volume>())
            {
                description.AppendLine($"  {volume.Name}:");
                if (volume.HostPath != null)
                {
                    description.AppendLine($"    Type: HostPath");
                    description.AppendLine($"    Path: {volume.HostPath.Path}");
                }
                else if (volume.EmptyDir != null)
                {
                    description.AppendLine($"    Type: EmptyDir");
                }
                else if (volume.ConfigMap != null)
                {
                    description.AppendLine($"    Type: ConfigMap");
                    description.AppendLine($"    Name: {volume.ConfigMap.Name}");
                }
                else if (volume.Projected != null)
                {
                    description.AppendLine($"    Type: Projected");
                }
            }

            // Tolerations
            description.AppendLine("Tolerations:");
            foreach (var toleration in podDetails.Spec?.Tolerations ?? new List<V1Toleration>())
            {
                description.AppendLine($"  - Key: {toleration.Key ?? "N/A"}, Effect: {toleration.Effect ?? "N/A"}, Value: {toleration.Value ?? "N/A"}, Toleration Seconds: {toleration.TolerationSeconds?.ToString() ?? "N/A"}");
            }

            // Events
            description.AppendLine("Events:");
            if (events?.Items != null && events.Items.Count > 0)
            {
                foreach (var evt in events.Items)
                {
                    description.AppendLine($"  - {evt.LastTimestamp}: {evt.Message}");
                }
            }
            else
            {
                description.AppendLine("  No events found for this pod.");
            }

            return description.ToString();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching description for pod {podName}: {ex.Message}");
            return $"Error fetching description for pod {podName}: {ex.Message}";
        }
    }

    // Helper method to format key-value pairs
    private static string FormatKeyValuePairs(IDictionary<string, string>? keyValuePairs)
    {
        if (keyValuePairs == null || keyValuePairs.Count == 0)
            return "<none>";

        return string.Join(", ", keyValuePairs.Select(kv => $"{kv.Key}={kv.Value}"));
    }

    // Helper method to format resource requirements
    private static string FormatResourceRequirements(IDictionary<string, ResourceQuantity>? resources)
    {
        if (resources == null || resources.Count == 0)
            return "<unset>";

        return string.Join(", ", resources.Select(r => $"{r.Key}: {r.Value}"));
    }

    /// <summary>
    /// Generates log data for a single pod to be used in GPT analysis.
    /// </summary>
    private static string GenerateLogDataForPod(V1Pod pod, string podDescription)
    {
        var logData = new StringBuilder();

        logData.AppendLine($"Pod: {pod.Metadata.Name}");
        logData.AppendLine($"Namespace: {pod.Metadata.NamespaceProperty}");
        logData.AppendLine($"Description:");
        logData.AppendLine(podDescription);

        return logData.ToString();
    }

    private static async Task<(string issueTitle, string logAnalysisContent)> GenerateGPTAnalysis(
        string logData, string podName, string namespaceName, string originalLogData)
    {
        try
        {
            // Retrieve environment variables
            string? endpointString = Environment.GetEnvironmentVariable("ENDPOINT");
            string? apiKey = Environment.GetEnvironmentVariable("API_KEY");
            string? modelName = Environment.GetEnvironmentVariable("MODEL");

            // Validate environment variables
            if (string.IsNullOrEmpty(endpointString) || string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(modelName))
            {
                throw new ArgumentException("Missing required environment variables: ENDPOINT, API_KEY, or MODEL.");
            }

#pragma warning disable SKEXP0010
            // Define execution settings for Azure OpenAI
            var executionSettings = new AzureOpenAIPromptExecutionSettings
            {
                Temperature = 0.7f,
                MaxTokens = 1000,
                ResponseFormat = typeof(AnalysisResult) // For structured response
            };
#pragma warning restore SKEXP0010

            // Create the kernel for OpenAI chat completion
            Kernel kernel = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: modelName,
                    endpoint: endpointString,
                    apiKey: apiKey)
                .Build();

            // Define the analysis prompt
            const string AnalysisPrompt = """
You are a Kubernetes pod analysis assistant.

Analyze the following pod log data and provide the following:
   - **IssueTitle**: A concise title summarizing the pod health and issues.
   - **Warnings and Errors**: A summary of warnings and errors with details and timestamps.
   - **Recommendations**: Actionable recommendations based on the analysis of errors and warnings.

Pod Log Data:
{{ $logData }}

[TASK]
Create:
- IssueTitle: (Concise title summarizing the pod health and issues)
- Warnings and Errors: (Summarize warnings and errors with details and timestamps)
- Recommendations: (Provide actionable recommendations based on the analysis of errors and warnings)
""";

            // Pass logData as arguments
            var arguments = new KernelArguments { ["logData"] = logData };

            // Invoke the analysis prompt
            var analysisOracle = kernel.CreateFunctionFromPrompt(AnalysisPrompt, executionSettings);
            var analysisResponse = await kernel.InvokeAsync(analysisOracle, arguments);
            string analysisRawResponse = analysisResponse.ToString();
            Console.WriteLine($"analysisRawResponse: {analysisRawResponse}");

            // Parse structured output
            string issueTitle = ParseStructuredOutput(analysisRawResponse, "IssueTitle");
            string logAnalysisContent = ParseStructuredOutput(analysisRawResponse, "LogAnalysisContent");

            // Format the issue body with Markdown
            string issueBody = FormatIssueBodyWithMarkdown(podName, namespaceName, logAnalysisContent, logData);

            return (issueTitle, issueBody);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during GPT analysis: {ex.Message}\n{ex.StackTrace}");
            return ("Error in Analysis", $"An error occurred: {ex.Message}. Please check the logs for details.");
        }
    }
    private static (string IssueTitle, string LogAnalysisContent) ExtractAnalysisDetails(string jsonResponse)
    {
        try
        {
            var jsonDocument = JsonDocument.Parse(jsonResponse);

            string issueTitle = jsonDocument.RootElement.GetProperty("IssueTitle").GetString() ?? "Default Issue Title";
            string logAnalysisContent = jsonDocument.RootElement.GetProperty("LogAnalysisContent").GetString() ?? "No Analysis Content Provided";

            return (issueTitle, logAnalysisContent);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting analysis details: {ex.Message}");
            return ("Error extracting IssueTitle", "Error extracting LogAnalysisContent");
        }
    }

    // Helper method to parse structured output
    private static string ParseStructuredOutput(string response, string key)
    {
        try
        {
            var json = JsonDocument.Parse(response);
            if (json.RootElement.TryGetProperty(key, out var section))
            {
                return section.ToString();
            }
            return $"No '{key}' section found in the response.";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing '{key}' section: {ex.Message}");
            return $"Error parsing '{key}' section: {ex.Message}";
        }
    }


    private static string FormatIssueBodyWithMarkdown(string podName, string namespaceName, string logAnalysisContent, string originalLogData)
    {
        var formattedBody = new StringBuilder();

        // Add Analysis Header
        formattedBody.AppendLine("### Analysis");
        formattedBody.AppendLine();

        // Add Pod Overview Section
        formattedBody.AppendLine("#### Pod Overview:");
        formattedBody.AppendLine($"- **Pod Name**: {podName}");
        formattedBody.AppendLine($"- **Namespace**: {namespaceName}");
        formattedBody.AppendLine();

        // Add Log Analysis Content
        formattedBody.AppendLine("#### Log Analysis Content:");
        formattedBody.AppendLine(string.IsNullOrWhiteSpace(logAnalysisContent)
            ? "No analysis content was generated."
            : logAnalysisContent.Trim());
        formattedBody.AppendLine();

        // Add Original Log Data Section
        formattedBody.AppendLine("#### Original Log Data:");
        formattedBody.AppendLine(string.IsNullOrWhiteSpace(originalLogData)
            ? "No original log data was provided."
            : $"```\n{originalLogData.Trim()}\n```");
        formattedBody.AppendLine();

        // Add Summary Section
        formattedBody.AppendLine("### Summary");
        formattedBody.AppendLine($"Further investigation into the pod **`{podName}`** in the namespace **`{namespaceName}`** is recommended based on the above analysis.");
        formattedBody.AppendLine();

        return formattedBody.ToString();
    }

    /// <summary>
    /// Creates a GitHub issue with the given title and content.
    /// </summary>
    private static async Task CreateGitHubIssue(string issueTitle, string logAnalysisContent)
    {
        try
        {
            // Ensure the title is within the maximum length
            issueTitle = TruncateTitle(issueTitle, 256);

            // Check if the content is empty and provide a fallback
            if (string.IsNullOrWhiteSpace(logAnalysisContent))
            {
                logAnalysisContent = "**Analysis Failed:**\n\nThe log analysis could not produce any meaningful output. Please review the pod's logs and configuration manually for further details.";
            }

            // Write the body content to a temporary file
            string tempFilePath = Path.GetTempFileName();
            await File.WriteAllTextAsync(tempFilePath, logAnalysisContent);

            var processStartInfo = new ProcessStartInfo
            {
                FileName = "gh",
                Arguments = $"issue create --title \"{EscapeForCLI(issueTitle)}\" --body-file \"{tempFilePath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = new Process { StartInfo = processStartInfo };
            process.Start();

            string output = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();

            if (process.ExitCode == 0)
            {
                Console.WriteLine($"GitHub issue created successfully:\n{output}");
            }
            else
            {
                Console.WriteLine($"Error creating GitHub issue:\n{error}");
            }

            // Clean up the temporary file
            File.Delete(tempFilePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred while creating the GitHub issue: {ex.Message}");
        }
    }

    private static string EscapeForCLI(string input)
    {
        return input.Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        if (title.Length <= maxLength)
        {
            return title;
        }

        // Find the last space within the maxLength limit for a clean cut
        int truncateIndex = title.LastIndexOf(' ', maxLength - 3);
        if (truncateIndex == -1) // If no space found, truncate strictly
        {
            truncateIndex = maxLength - 3;
        }

        return title.Substring(0, truncateIndex) + "...";
    }

    public class AnalysisResult
    {
        public string IssueTitle { get; set; }
        public string LogAnalysisContent { get; set; }
    }

}
