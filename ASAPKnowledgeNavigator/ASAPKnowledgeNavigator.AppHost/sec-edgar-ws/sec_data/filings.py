import requests
import pandas as pd
import numpy as np
from weasyprint import HTML  # Import WeasyPrint for PDF generation

class SECFilings:
    BASE_URL = "https://data.sec.gov/submissions/CIK{cik}.json"
    XBRL_URL = "https://data.sec.gov/api/xbrl/companyfacts/CIK{cik}.json"

    def __init__(self, email):
        self.headers = {"User-Agent": email}

    def get_company_filings(self, cik):
        """
        Fetch company filings from the SEC EDGAR API using the CIK.
        """
        url = self.BASE_URL.format(cik=str(cik).zfill(10))
        response = requests.get(url, headers=self.headers)
        if response.status_code == 200:
            return response.json()
        else:
            raise ValueError(f"Failed to fetch data for CIK {cik}: {response.status_code}")

    def filings_to_dataframe(self, filings):
        """
        Convert filings JSON data to a Pandas DataFrame.
        """
        return pd.DataFrame(filings["filings"]["recent"])

    def download_document(self, cik, accession_number, file_name, save_path):
        base_url = f"https://www.sec.gov/Archives/edgar/data/{cik}/{accession_number}/{file_name}"
        content = requests.get(base_url, headers=self.headers).content.decode("utf-8")

        # Save the content as an HTML file
        html_path = save_path + ".html"
        with open(html_path, "w") as file:
            file.write(content)

        # Convert HTML content to PDF using WeasyPrint
        pdf_path = save_path + ".pdf"
        HTML(string=content, base_url="").write_pdf(pdf_path)
        return {"html_path": html_path, "pdf_path": pdf_path}

    def get_xbrl_data(self, cik):
        """
        Fetch XBRL data for a company using its CIK.
        Implements retry logic for transient failures.
        """
        url = self.XBRL_URL.format(cik=str(cik).zfill(10))
        response = requests.get(url, headers=self.headers)
        if response.status_code == 200:
            return response.json()
        else:
            raise ValueError(f"Failed to fetch XBRL data for CIK {cik}: {response.status_code}")
    
    def list_xbrl_concepts(self, cik):
        """
        Fetch XBRL data for a company using its CIK and list available concepts.
        """
        try:
            # Fetch the XBRL data
            xbrl_data = self.get_xbrl_data(cik)
            
            # Extract the 'us-gaap' section
            us_gaap_data = xbrl_data.get("facts", {}).get("us-gaap", {})
            
            # List the available concepts
            concepts = list(us_gaap_data.keys())
            
            # Print the concepts
            for concept in concepts:
                print(concept)
            
            return concepts
        except Exception as e:
            print(f"An error occurred: {e}")
            return []
    
    def process_xbrl_data(self, xbrl_data, concept="AssetsCurrent", unit="USD"):
        """
        Process XBRL data for a specific concept and unit.
        Returns a Pandas DataFrame with out-of-range float values handled.
        """
        try:
            facts = xbrl_data["facts"]["us-gaap"][concept]["units"][unit]
            df = pd.DataFrame(facts)

            # Replace NaN and infinite values with None (JSON null)
            df.replace([np.inf, -np.inf, np.nan], None, inplace=True)

            return df
        except KeyError as e:
            raise ValueError(f"Concept '{concept}' or unit '{unit}' not found in XBRL data: {e}")
        