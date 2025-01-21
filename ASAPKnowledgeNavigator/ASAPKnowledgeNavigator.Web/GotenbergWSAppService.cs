using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;

public class GotenbergWSAppService
{
    private readonly HttpClient _httpClient;

    public GotenbergWSAppService(HttpClient httpClient)
    {
        // Set a custom timeout for long-running requests
        httpClient.Timeout = TimeSpan.FromMinutes(5);
        _httpClient = httpClient;
    }

    /// <summary>
    /// Fetches a "Hello" response from the root endpoint.
    /// </summary>
    public async Task<string> GetHelloAsync()
    {
        var response = await _httpClient.GetAsync("/");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// Converts an HTML string to a PDF using the Go web service.
    /// </summary>
    /// <param name="htmlContent">The HTML content to convert.</param>
    /// <returns>Byte array containing the PDF file.</returns>
    public async Task<byte[]> ConvertHtmlToPdfAsync(string htmlContent)
    {
        // Validate input
        if (string.IsNullOrWhiteSpace(htmlContent))
            throw new ArgumentException("HTML content cannot be null or empty.", nameof(htmlContent));

        try
        {

            // HTML file into PDF
            // convertHtmlRoute returns an [api.Route] which can convert an HTML file to
            // PDF.
            string endpoint = "/forms/chromium/convert/html";

            // Create the request payload
            var payload = new { html = htmlContent };

            // Send the POST request to the Go service
            var response = await _httpClient.PostAsJsonAsync(endpoint, payload);

            // Ensure the request was successful
            response.EnsureSuccessStatusCode();

            // Read the PDF content as a byte array
            return await response.Content.ReadAsByteArrayAsync();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"HTTP error while converting HTML to PDF: {ex.Message}");
            throw new InvalidOperationException("Failed to convert HTML to PDF using the Go web service.", ex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Unexpected error while converting HTML to PDF: {ex.Message}");
            throw new InvalidOperationException("An unexpected error occurred while converting HTML to PDF.", ex);
        }
    }
}
