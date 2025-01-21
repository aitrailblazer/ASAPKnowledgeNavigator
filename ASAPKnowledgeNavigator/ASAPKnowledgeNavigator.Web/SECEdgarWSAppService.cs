using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
    /// Fetches the CIK (Central Index Key) for a given stock ticker from the FastAPI server.
    /// </summary>
    /// <param name="ticker">The stock ticker symbol (e.g., AAPL).</param>
    /// <returns>The CIK as a string.</returns>
    public async Task<string?> GetCIKAsync(string ticker)
    {
        try
        {
            // Validate ticker format before making the request
            if (string.IsNullOrWhiteSpace(ticker) || !System.Text.RegularExpressions.Regex.IsMatch(ticker, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine("Invalid ticker format. Only alphanumeric characters are allowed.");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Construct the endpoint URL for fetching the CIK
            string endpoint = $"/cik/{ticker}";

            // Send a GET request to the endpoint
            var response = await _httpClient.GetAsync(endpoint);

            // If the response status is 404 (Not Found), return null
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Ticker not found: {ticker}");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Parse the response as plain text
            var cik = await response.Content.ReadAsStringAsync();

            // Validate the CIK
            if (string.IsNullOrWhiteSpace(cik) || !long.TryParse(cik, out _))
            {
                Console.WriteLine("Invalid or empty CIK returned by the server.");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Log the retrieved CIK
            Console.WriteLine($"Fetched CIK for {ticker}: {cik}");

            return cik;
        }
        catch (HttpRequestException httpEx)
        {
            // Log the HTTP request error
            Console.WriteLine($"HTTP request error: {httpEx.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
        catch (Exception ex)
        {
            // Log any unexpected error
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
    }


    public async Task<string> GetNameAsync(string ticker)
    {
        try
        {
            // Validate ticker format before making the request
            if (string.IsNullOrWhiteSpace(ticker) || !System.Text.RegularExpressions.Regex.IsMatch(ticker, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine("Invalid ticker format provided.");
                return string.Empty; // Return an empty string for invalid ticker
            }

            // Construct the endpoint URL for fetching the company name
            string endpoint = $"/name/{ticker}";

            // Send a GET request to the endpoint
            var response = await _httpClient.GetAsync(endpoint);

            // If the response status is 404 (Not Found), return null
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Ticker not found: {ticker}");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Parse the response as plain text
            var name = await response.Content.ReadAsStringAsync();

            // Validate the name
            if (string.IsNullOrEmpty(name))
            {
                Console.WriteLine("Invalid or empty name returned by the server.");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Log the retrieved name
            Console.WriteLine($"Fetched name for {ticker}: {name}");

            return name;
        }
        catch (HttpRequestException httpEx)
        {
            // Log the HTTP request error
            Console.WriteLine($"HTTP request error: {httpEx.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
        catch (Exception ex)
        {
            // Log any unexpected error
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
    }
        public async Task<string> GetExchangeAsync(string ticker)
    {
        try
        {
            // Validate ticker format before making the request
            if (string.IsNullOrWhiteSpace(ticker) || !System.Text.RegularExpressions.Regex.IsMatch(ticker, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine("Invalid ticker format provided.");
                return string.Empty; // Return an empty string for invalid ticker
            }

            // Construct the endpoint URL for fetching the company name
            string endpoint = $"/exchange/{ticker}";

            // Send a GET request to the endpoint
            var response = await _httpClient.GetAsync(endpoint);

            // If the response status is 404 (Not Found), return null
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                Console.WriteLine($"Ticker not found: {ticker}");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Parse the response as plain text
            var exchange = await response.Content.ReadAsStringAsync();

            // Validate the name
            if (string.IsNullOrEmpty(exchange))
            {
                Console.WriteLine("Invalid or empty exchange returned by the server.");
                return string.Empty; // Return empty string for unexpected errors
            }

            // Log the retrieved name
            Console.WriteLine($"Fetched exchange for {ticker}: {exchange}");

            return exchange;
        }
        catch (HttpRequestException httpEx)
        {
            // Log the HTTP request error
            Console.WriteLine($"HTTP request error: {httpEx.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
        catch (Exception ex)
        {
            // Log any unexpected error
            Console.WriteLine($"Unexpected error: {ex.Message}");
            return string.Empty; // Return empty string for unexpected errors
        }
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
        try
        {
            // Validate the ticker format before making the request
            if (string.IsNullOrWhiteSpace(ticker) || !Regex.IsMatch(ticker, @"^[A-Za-z0-9]+$"))
            {
                Console.WriteLine("Invalid ticker format provided.");
                return Array.Empty<string>(); // Return an empty array for invalid ticker
            }

            // Construct the endpoint URL
            string endpoint = $"/forms/{ticker}";

            // Send the GET request
            var response = await _httpClient.GetAsync(endpoint);

            // Handle non-successful responses gracefully
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    Console.WriteLine($"No forms found for ticker {ticker}. Returning an empty array.");
                    return Array.Empty<string>(); // Return an empty array if forms are not found
                }

                Console.WriteLine($"Failed to fetch forms for ticker {ticker}. Status: {response.StatusCode}");
                return Array.Empty<string>(); // Return an empty array for other HTTP errors
            }

            // Parse the JSON response
            var jsonResponse = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(jsonResponse);

            // Extract the "forms" property
            if (document.RootElement.TryGetProperty("forms", out var formsElement) && formsElement.ValueKind == JsonValueKind.Array)
            {
                var forms = formsElement.Deserialize<string[]>();
                if (forms != null && forms.Any())
                {
                    Console.WriteLine($"Fetched forms for ticker {ticker}: {string.Join(", ", forms)}");
                    return forms; // Return the list of forms
                }

                Console.WriteLine($"No forms available in the response for ticker {ticker}.");
                return Array.Empty<string>(); // Return an empty array if no forms are available
            }

            Console.WriteLine($"Forms property not found in the response for ticker {ticker}.");
            return Array.Empty<string>(); // Return an empty array if "forms" property is missing
        }
        catch (JsonException jsonEx)
        {
            Console.WriteLine($"JSON parsing error: {jsonEx.Message}");
            return Array.Empty<string>(); // Return an empty array for JSON parsing errors
        }
        catch (HttpRequestException httpEx)
        {
            Console.WriteLine($"HTTP request error: {httpEx.Message}");
            return Array.Empty<string>(); // Return an empty array for network errors
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while fetching forms for ticker {ticker}: {ex.Message}");
            return Array.Empty<string>(); // Return an empty array for unexpected errors
        }
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
