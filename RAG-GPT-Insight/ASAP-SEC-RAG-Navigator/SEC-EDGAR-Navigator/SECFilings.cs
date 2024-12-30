using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using WeasyPrint;

public class SECFilings
{
    private static readonly HttpClient client = new HttpClient();
    private readonly string email;

    public SECFilings(string email)
    {
        this.email = email;
        client.DefaultRequestHeaders.Add("User-Agent", email);
    }

    public async Task<JObject> GetCompanyFilings(string cik)
    {
        string url = $"https://data.sec.gov/submissions/CIK{cik}.json";
        HttpResponseMessage response = await client.GetAsync(url);
        if (response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            return JObject.Parse(content);
        }
        else
        {
            throw new Exception($"Failed to fetch data for CIK {cik}: {response.StatusCode}");
        }
    }

    public List<Dictionary<string, string>> FilingsToDataFrame(JObject filings)
    {
        var recentFilings = filings["filings"]["recent"];
        var filingsList = new List<Dictionary<string, string>>();

        foreach (var filing in recentFilings)
        {
            var filingDict = new Dictionary<string, string>
            {
                { "form", filing["form"].ToString() },
                { "accessionNumber", filing["accessionNumber"].ToString() },
                { "primaryDocument", filing["primaryDocument"].ToString() }
            };
            filingsList.Add(filingDict);
        }

        return filingsList;
    }

    public async Task DownloadDocument(string cik, string accessionNumber, string fileName, string savePath)
    {
        string baseUrl = $"https://www.sec.gov/Archives/edgar/data/{cik}/{accessionNumber}/{fileName}";
        string content = await client.GetStringAsync(baseUrl);

        // Save the content as an HTML file
        string htmlPath = savePath + ".html";
        await File.WriteAllTextAsync(htmlPath, content);

        // Convert HTML content to PDF using WeasyPrint
        string pdfPath = savePath + ".pdf";
        HTML(string: content, base_url: "").write_pdf(pdfPath);
    }
}
