using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public class CompanyInfo
{
    private List<Dictionary<string, string>> records;
    private List<string> fields;

    public CompanyInfo(string jsonPath)
    {
        using (StreamReader file = File.OpenText(jsonPath))
        using (JsonTextReader reader = new JsonTextReader(file))
        {
            JObject data = (JObject)JToken.ReadFrom(reader);
            fields = data["fields"].ToObject<List<string>>();
            records = data["data"].ToObject<List<Dictionary<string, string>>>();
        }
    }

    public List<Dictionary<string, string>> ToDataFrame()
    {
        return records;
    }

    public string GetCikByTicker(string ticker)
    {
        foreach (var record in records)
        {
            if (record["ticker"] == ticker)
            {
                return record["cik"];
            }
        }
        throw new Exception($"No company found with ticker: {ticker}");
    }

    public List<Dictionary<string, string>> SearchByName(string substring)
    {
        List<Dictionary<string, string>> result = new List<Dictionary<string, string>>();
        foreach (var record in records)
        {
            if (record["name"].IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Add(record);
            }
        }
        return result;
    }
}
