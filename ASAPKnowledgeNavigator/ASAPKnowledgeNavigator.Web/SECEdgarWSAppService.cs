using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using PuppeteerSharp;
using PuppeteerSharp.Media;
public class SECEdgarWSAppService
{
    private readonly HttpClient _httpClient;

    public SECEdgarWSAppService(HttpClient httpClient)
    {
        // Set a custom timeout for long-running requests
        httpClient.Timeout = TimeSpan.FromMinutes(5); // Increase timeout to 5 minutes
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetches a "Hello, World!" response from the root endpoint.
    /// </summary>
    public async Task<string> GetHelloWorldAsync()
    {
        var response = await _httpClient.GetAsync("/");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Fetches the CIK (Central Index Key) for a given stock ticker.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    public async Task<string> GetCIKAsync(string ticker)
    {
        // Construct the endpoint URL for fetching the CIK
        string endpoint = $"/cik/{ticker}";

        // Send a GET request to the endpoint
        var response = await _httpClient.GetAsync(endpoint);

        // Ensure the request was successful
        response.EnsureSuccessStatusCode();

        // Parse the JSON response to extract the CIK
        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(jsonResponse);

        if (document.RootElement.TryGetProperty("cik", out var cikElement))
        {
            // Handle both string and number types for CIK
            return cikElement.ValueKind switch
            {
                JsonValueKind.String => cikElement.GetString(),
                JsonValueKind.Number => cikElement.GetInt64().ToString(), // Convert number to string
                _ => throw new Exception("Unexpected CIK type in response.")
            };
        }

        throw new Exception("CIK not found in response.");
    }

    /// <summary>
    /// Fetches the filing history for a given stock ticker.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    public async Task<string> GetFilingsAsync(string ticker)
    {
        // Construct the endpoint URL for fetching filings
        string endpoint = $"/filings/{ticker}";

        // Send a GET request to the endpoint
        var response = await _httpClient.GetAsync(endpoint);

        // Ensure the request was successful
        response.EnsureSuccessStatusCode();

        // Return the JSON response as a string
        return await response.Content.ReadAsStringAsync();
    }
    /// <summary>
    /// Fetches the available forms for a given stock ticker.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    public async Task<string[]> GetAvailableFormsAsync(string ticker)
    {
        string endpoint = $"/forms/{ticker}";
        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();

        var jsonResponse = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(jsonResponse);

        if (document.RootElement.TryGetProperty("forms", out var formsElement) && formsElement.ValueKind == JsonValueKind.Array)
        {
            return formsElement.Deserialize<string[]>();
        }

        throw new Exception("Failed to fetch available forms.");
    }
    /// <summary>
    /// Downloads the latest filing of a specified form type as raw HTML.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    /// <param name="formType">The form type (e.g., 10-K, 10-Q).</param>
    /// <returns>HTML content as a string.</returns>
    public async Task<string> DownloadLatestFilingHtmlAsync(string ticker, string formType)
    {
         // Replace "/" with "_" in the form type to ensure it's URL-safe
        formType = formType.Replace("/", "_");

        string endpoint = $"/filing/html/{ticker}/{formType}";
        var response = await _httpClient.GetAsync(endpoint);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }
    /// <summary>
    /// Downloads the latest filing of a specified form type as a PDF.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    /// <param name="formType">The form type (e.g., 10-K, 10-Q).</param>
    /// <returns>Byte array containing the PDF file.</returns>
    public async Task<byte[]> DownloadLatestFilingPdfAsync(string ticker, string formType)
    {
        string endpoint = $"/filing/pdf/{ticker}/{formType}";
        var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        using var memoryStream = new MemoryStream();
        await response.Content.CopyToAsync(memoryStream);
        return memoryStream.ToArray();
    }
    /// <summary>
    /// Downloads the latest 10-K filing as a PDF for the given ticker.
    /// Implements retry logic, streaming, and enhanced error handling.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    /// <returns>Byte array containing the PDF file.</returns>
    public async Task<byte[]> DownloadLatest10KAsync(string ticker)
    {
        string endpoint = $"/10k/pdf/{ticker}";
        int maxRetries = 3; // Maximum retry attempts
        int delay = 1000; // Initial delay in milliseconds

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}: Requesting {endpoint}");
                using var response = await _httpClient.GetAsync(endpoint, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var memoryStream = new MemoryStream();
                await response.Content.CopyToAsync(memoryStream);
                Console.WriteLine($"Successfully downloaded PDF for {ticker}");
                return memoryStream.ToArray();
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"Attempt {attempt} failed due to timeout. Retrying...");
                await Task.Delay(delay); // Exponential backoff
                delay *= 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading PDF for {ticker}: {ex.Message}");
                throw new Exception($"Failed to download the PDF for {ticker}: {ex.Message}", ex);
            }
        }

        throw new Exception($"Failed to download the PDF for {ticker} after {maxRetries} attempts.");
    }
    /// <summary>
    /// Downloads the latest 10-K filing as raw HTML for the given ticker.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    /// <returns>HTML content as a string.</returns>
    public async Task<string> DownloadLatest10KHtmlAsync(string ticker)
    {
        string endpoint = $"/10k/html/{ticker}";
        int maxRetries = 3;
        int delay = 1000;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"Attempt {attempt}: Requesting {endpoint}");
                var response = await _httpClient.GetAsync(endpoint);
                response.EnsureSuccessStatusCode();

                Console.WriteLine($"Successfully downloaded HTML for {ticker}");
                return await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries)
            {
                Console.WriteLine($"Attempt {attempt} failed due to timeout. Retrying...");
                await Task.Delay(delay);
                delay *= 2;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error downloading HTML for {ticker}: {ex.Message}");
                throw new Exception($"Failed to download the HTML for {ticker}: {ex.Message}", ex);
            }
        }

        throw new Exception($"Failed to download the HTML for {ticker} after {maxRetries} attempts.");
    }

    public async Task<string> GetXBRLPlotAsync(string ticker, string concept = "AssetsCurrent", string unit = "USD")
    {
        // Construct the endpoint URL with query parameters
        string endpoint = $"/xbrl/plot/{ticker}?concept={concept}&unit={unit}";

        // Send a GET request to the endpoint
        var response = await _httpClient.GetAsync(endpoint);

        // Ensure the request was successful
        response.EnsureSuccessStatusCode();

        // Return the HTML content as a string
        return await response.Content.ReadAsStringAsync();
    }

}
