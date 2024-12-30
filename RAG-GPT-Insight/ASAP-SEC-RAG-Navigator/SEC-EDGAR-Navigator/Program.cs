using System;
using System.IO;
using Newtonsoft.Json.Linq;

class Program
{
    static void Main(string[] args)
    {
        // Paths and setup
        string datasetPath = "data/company_tickers_exchange.json";
        string outputDir = "data";
        Directory.CreateDirectory(outputDir);
        string email = "your.email@example.com";

        // Initialize components
        CompanyInfo companyInfo = new CompanyInfo(datasetPath);
        SECFilings secFilings = new SECFilings(email);

        // Fetch company CIK by ticker
        string ticker = "TSLA"; // TSLA, AAPL, NVDA, MSFT, AMZN, GOOGLE, META
        try
        {
            string cik = companyInfo.GetCikByTicker(ticker);
            Console.WriteLine($"CIK for {ticker}: {cik}");
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }

        // Fetch filing history
        try
        {
            var filings = secFilings.GetCompanyFilings(cik);
            var filingsDataFrame = secFilings.FilingsToDataFrame(filings);
            Console.WriteLine(filingsDataFrame);
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return;
        }

        // Download the latest 10-K report
        var latest10K = filingsDataFrame.FirstOrDefault(f => f.Form == "10-K");
        if (latest10K != null)
        {
            string accessionNumber = latest10K.AccessionNumber.Replace("-", "");
            string fileName = latest10K.PrimaryDocument;
            string savePath = Path.Combine(outputDir, $"{fileName}.html");

            try
            {
                secFilings.DownloadDocument(cik, accessionNumber, fileName, savePath);
                Console.WriteLine($"Downloaded 10-K to {savePath}");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to download document: {e.Message}");
            }
        }
    }
}
