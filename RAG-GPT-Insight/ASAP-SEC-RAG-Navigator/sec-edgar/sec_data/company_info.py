import pandas as pd
import json

class CompanyInfo:
    def __init__(self, json_path):
        with open(json_path, "r") as file:
            self.data = json.load(file)

        self.fields = self.data["fields"]
        self.records = self.data["data"]

    def to_dataframe(self):
        return pd.DataFrame(self.records, columns=self.fields)

    def get_cik_by_ticker(self, ticker):
        df = self.to_dataframe()
        result = df[df["ticker"] == ticker]
        if not result.empty:
            return result.iloc[0]["cik"]
        else:
            raise ValueError(f"No company found with ticker: {ticker}")

    def search_by_name(self, substring):
        df = self.to_dataframe()
        return df[df["name"].str.contains(substring, case=False)]
