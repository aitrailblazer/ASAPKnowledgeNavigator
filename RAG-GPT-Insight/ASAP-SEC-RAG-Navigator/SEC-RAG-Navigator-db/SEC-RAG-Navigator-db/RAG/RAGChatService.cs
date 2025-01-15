// Copyright (c) AITrailblazer. All rights reserved.

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Data;
using Microsoft.SemanticKernel.PromptTemplates.Handlebars;
using VectorStoreRAG.Options;


/// <summary>
/// Main service class for the application.
/// </summary>
/// <typeparam name="TKey">The type of the data model key.</typeparam>
/// <param name="dataLoader">Used to load data into the vector store.</param>
/// <param name="vectorStoreTextSearch">Used to search the vector store.</param>
/// <param name="kernel">Used to make requests to the LLM.</param>
/// <param name="ragConfigOptions">The configuration options for the application.</param>
/// <param name="appShutdownCancellationTokenSource">Used to gracefully shut down the entire application when cancelled.</param>
public class RAGChatService<TKey>(
    IDataLoader dataLoader,
    VectorStoreTextSearch<TextSnippet<TKey>> vectorStoreTextSearch,
    Kernel kernel,
    IOptions<RagConfig> ragConfigOptions,
    [FromKeyedServices("AppShutdown")] CancellationTokenSource appShutdownCancellationTokenSource) : IHostedService
{
    private Task? _dataLoaded;
    private Task? _chatLoop;

    /// <summary>
    /// Start the service.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the service is started.</returns>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        // Explicitly provide the file path as an argument
        var filePaths = new[] { "tsla-20231231.htm.html.pdf" };
        var tenantID = "1234";
        var userID = "5678";

        // Start to load the specified PDFs into the vector store
        if (ragConfigOptions.Value.BuildCollection)
        {
            this._dataLoaded = this.LoadDataAsync(
                tenantID,
                userID,
                filePaths,
                cancellationToken);

        }
        else
        {
            this._dataLoaded = Task.CompletedTask;
        }

        // Start the chat loop
        this._chatLoop = this.ChatLoopAsync(cancellationToken);

        return Task.CompletedTask;
    }
    /// <summary>
    /// Stop the service.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the service is stopped.</returns>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Contains the main chat loop for the application.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the chat loop is shut down.</returns>
    private async Task ChatLoopAsync(CancellationToken cancellationToken)
    {
        var pdfFiles = string.Join(", ", ragConfigOptions.Value.PdfFilePaths ?? []);

        // Wait for the data to be loaded before starting the chat loop.
        while (this._dataLoaded != null && !this._dataLoaded.IsCompleted && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1_000, cancellationToken).ConfigureAwait(false);
        }

        // If data loading failed, don't start the chat loop.
        if (this._dataLoaded != null && this._dataLoaded.IsFaulted)
        {
            Console.WriteLine("Failed to load data");
            return;
        }

        Console.WriteLine("PDF loading complete\n");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine("Assistant > Press enter with no prompt to exit.");

        // Add a search plugin to the kernel which we will use in the template below
        // to do a vector search for related information to the user query.
        kernel.Plugins.Add(vectorStoreTextSearch.CreateWithGetTextSearchResults("SearchPlugin"));

        // Start the chat loop.
        while (!cancellationToken.IsCancellationRequested)
        {
            // Prompt the user for a question.
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"Assistant > What would you like to know from the loaded PDFs: ({pdfFiles})?");

            // Read the user question.
            Console.ForegroundColor = ConsoleColor.White;
            Console.Write("User > ");
            var question = Console.ReadLine();

            // Exit the application if the user didn't type anything.
            if (string.IsNullOrWhiteSpace(question))
            {
                appShutdownCancellationTokenSource.Cancel();
                break;
            }

            // Invoke the LLM with a template that uses the search plugin to
            // 1. get related information to the user query from the vector store
            // 2. add the information to the LLM prompt.
            var response = kernel.InvokePromptStreamingAsync(
                promptTemplate: """
                    Please use this information to answer the question:
                    {{#with (SearchPlugin-GetTextSearchResults question)}}  
                      {{#each this}}  
                        Name: {{Name}}
                        Value: {{Value}}
                        Link: {{Link}}
                        -----------------
                      {{/each}}
                    {{/with}}

                    Include citations to the relevant information where it is referenced in the response.
                    
                    Question: {{question}}
                    """,
                arguments: new KernelArguments()
                {
                    { "question", question },
                },
                templateFormat: "handlebars",
                promptTemplateFactory: new HandlebarsPromptTemplateFactory(),
                cancellationToken: cancellationToken);

            // Stream the LLM response to the console with error handling.
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("\nAssistant > ");

            try
            {
                await foreach (var message in response.ConfigureAwait(false))
                {
                    Console.Write(message);
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Call to LLM failed with error: {ex}");
            }
        }
    }

    /// <summary>
    /// Load all specified PDFs into the vector store.
    /// </summary>
    /// <param name="pdfFilePaths">A collection of paths to the PDF files to load.</param>
    /// <param name="tenantID">The tenant ID to associate with the records.</param>
    /// <param name="userID">The user ID to associate with the records.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the loading is complete.</returns>
    private async Task LoadDataAsync(
        string tenantID,
        string userID,
        IEnumerable<string> filePaths,
        CancellationToken cancellationToken)
    {
        string memoryKey = "1234";
        try
        {
            foreach (var filePath in filePaths ?? Array.Empty<string>())
            {
                string fileName = Path.GetFileName(filePath);
                await ProcessPdfAsync(
                    tenantID,
                    userID,
                    fileName,
                    filePath,
                    memoryKey,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to load PDFs: {ex}");
            throw;
        }
    }



    /// <summary>
    /// Process a single PDF file and load its content into the vector store.
    /// </summary>
    /// <param name="pdfFilePath">The path to the PDF file to process.</param>
    /// <param name="tenantID">The tenant ID to associate with the records.</param>
    /// <param name="userID">The user ID to associate with the records.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the processing is complete.</returns>
    public async Task ProcessPdfAsync(
        string tenantID,
        string userID,
        string fileName,
        string filePath,
        string memoryKey,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Loading PDF into vector store: {filePath}");

        // Ensure the file exists before attempting to load
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"PDF file does not exist: {filePath}");
            return;
        }

        try
        {
            // Open the file as a stream and pass it to the data loader
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 524288  // 512 KB buffer
            );
            string directory = "";
            string blobName = "";
            int DataLoadingBatchSize = 10;
            int DataLoadingBetweenBatchDelayInMilliseconds = 1000;
            await dataLoader.LoadPdf(
                tenantID,
                userID,
                fileName,
                directory,
                blobName,
                memoryKey,
                fileStream,
                DataLoadingBatchSize,
                DataLoadingBetweenBatchDelayInMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fileException)
        {
            Console.WriteLine($"Error processing file {filePath}: {fileException}");
        }
    }

       public async Task ProcessPdfCohereAsync(
        string tenantID,
        string userID,
        string fileName,
        string filePath,
        string memoryKey,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Loading PDF into vector store: {filePath}");

        // Ensure the file exists before attempting to load
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"PDF file does not exist: {filePath}");
            return;
        }

        try
        {
            // Open the file as a stream and pass it to the data loader
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 524288  // 512 KB buffer
            );
            string directory = "";
            string blobName = "";
            int DataLoadingBatchSize = 10;
            int DataLoadingBetweenBatchDelayInMilliseconds = 1000;
            await dataLoader.LoadPdfCohere(
                tenantID,
                userID,
                fileName,
                directory,
                blobName,
                memoryKey,
                fileStream,
                DataLoadingBatchSize,
                DataLoadingBetweenBatchDelayInMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fileException)
        {
            Console.WriteLine($"Error processing file {filePath}: {fileException}");
        }
    }
      public async Task ProcessPdfCohereEDGARAsync(
        string form,
        string ticker,
        string fileName,
        string filePath,
        string memoryKey,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Loading PDF into vector store: {filePath}");

        // Ensure the file exists before attempting to load
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"PDF file does not exist: {filePath}");
            return;
        }

        try
        {
            // Open the file as a stream and pass it to the data loader
            using var fileStream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 524288  // 512 KB buffer
            );
            string directory = "";
            string blobName = "";
            int DataLoadingBatchSize = 10;
            int DataLoadingBetweenBatchDelayInMilliseconds = 1000;
            await dataLoader.EDGARLoadPdfCohere(
                form,
                ticker,
                fileName,
                directory,
                blobName,
                memoryKey,
                fileStream,
                DataLoadingBatchSize,
                DataLoadingBetweenBatchDelayInMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fileException)
        {
            Console.WriteLine($"Error processing file {filePath}: {fileException}");
        }
    }

    public async Task DeletePdfAsync(
        string tenantID,
        string userID,
        string fileNamePrefix,
        string categoryId)
    {
        Console.WriteLine($"Starting deletion process for PDF items with prefix: {fileNamePrefix}");

        try
        {
            // Call the DataLoader service to delete items based on the fileNamePrefix
            int batchSize = 10; // Adjust batch size for deletion as needed
            int betweenBatchDelayInMilliseconds = 1000; // Delay between batches to avoid throttling

            await dataLoader.DeletePdf(
                tenantID,
                userID,
                fileNamePrefix,
                categoryId,
                batchSize,
                betweenBatchDelayInMilliseconds
            ).ConfigureAwait(false);

            Console.WriteLine($"Successfully completed deletion for items with prefix: {fileNamePrefix}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during deletion for items with prefix {fileNamePrefix}: {ex.Message}");
            throw;
        }
    }
   public async Task DeleteEDGARPdfAsync(
        string form,
        string ticker,
        string fileNamePrefix,
        string categoryId)
    {
        Console.WriteLine($"Starting deletion process for PDF items with prefix: {fileNamePrefix}");

        try
        {
            // Call the DataLoader service to delete items based on the fileNamePrefix
            int batchSize = 10; // Adjust batch size for deletion as needed
            int betweenBatchDelayInMilliseconds = 1000; // Delay between batches to avoid throttling

            await dataLoader.DeletePdfEDGAR(
                form,
                ticker,
                fileNamePrefix,
                categoryId,
                batchSize,
                betweenBatchDelayInMilliseconds
            ).ConfigureAwait(false);

            Console.WriteLine($"Successfully completed deletion for items with prefix: {fileNamePrefix}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during deletion for items with prefix {fileNamePrefix}: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Process a single PDF file and load its content into the vector store.
    /// </summary>
    /// <param name="pdfFilePath">The path to the PDF file to process.</param>
    /// <param name="tenantID">The tenant ID to associate with the records.</param>
    /// <param name="userID">The user ID to associate with the records.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to monitor for cancellation requests.</param>
    /// <returns>An async task that completes when the processing is complete.</returns>
    public async Task ProcessPdfStreamAsync(
        string tenantID,
        string userID,
        string fileName,
        string directory,
        string blobName,
        string memoryKey,
        Stream fileStream,
        CancellationToken cancellationToken)
    {
        Console.WriteLine($"Loading PDF into vector store: {fileName}");
        try
        {
            int DataLoadingBatchSize = 10;
            int DataLoadingBetweenBatchDelayInMilliseconds = 1000;
            await dataLoader.LoadPdf(
                tenantID,
                userID,
                fileName,
                directory,
                blobName,
                memoryKey,
                fileStream,
                DataLoadingBatchSize,
                DataLoadingBetweenBatchDelayInMilliseconds,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception fileException)
        {
            Console.WriteLine($"Error processing file {fileName}: {fileException}");
        }
    }

}
