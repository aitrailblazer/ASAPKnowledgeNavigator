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
    
    def get_name_by_ticker(self, ticker):
        df = self.to_dataframe()
        result = df[df["ticker"] == ticker]
        if not result.empty:
            return result.iloc[0]["name"]
        else:
            raise ValueError(f"No company found with ticker: {ticker}")
    
    def get_exchange_by_ticker(self, ticker):
        df = self.to_dataframe()
        result = df[df["ticker"] == ticker]
        if not result.empty:
            return result.iloc[0]["exchange"]
        else:
            raise ValueError(f"No company found with ticker: {ticker}")
    def get_tickers_by_exchange(self, exchange):
            """
            Get a list of tickers for companies listed on a specific exchange.
            
            Args:
                exchange (str): The name of the exchange to filter by.

            Returns:
                list: A list of tickers for companies on the specified exchange.
            """
            df = self.to_dataframe()
            result = df[df["exchange"].str.lower() == exchange.lower()]
            return result["ticker"].tolist()
    
    def get_all_exchanges(self):
            """
            Get a list of all unique exchanges in the dataset.

            Returns:
                list: A list of unique exchanges.
            """
            df = self.to_dataframe()
            return df["exchange"].dropna().unique().tolist()


    def get_companies_by_exchange(self, exchange):
        """
        Get a list of companies (CIK, name, and ticker) listed on a specific exchange.

        Args:
            exchange (str): The exchange name.

        Returns:
            list[dict]: A list of dictionaries containing CIK, name, and ticker for each company.
        """
        df = self.to_dataframe()
        result = df[df["exchange"] == exchange]
        if not result.empty:
            return result[["cik", "name", "ticker"]].to_dict(orient="records")
        else:
            raise ValueError(f"No companies found for exchange: {exchange}")



    def search_by_name(self, substring):
        df = self.to_dataframe()
        return df[df["name"].str.contains(substring, case=False)]
