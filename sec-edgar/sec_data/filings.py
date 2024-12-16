import requests
import pandas as pd
from weasyprint import HTML  # Import WeasyPrint for PDF generation

class SECFilings:
    BASE_URL = "https://data.sec.gov/submissions/CIK{cik}.json"

    def __init__(self, email):
        self.headers = {"User-Agent": email}

    def get_company_filings(self, cik):
        url = self.BASE_URL.format(cik=str(cik).zfill(10))
        response = requests.get(url, headers=self.headers)
        if response.status_code == 200:
            return response.json()
        else:
            raise ValueError(f"Failed to fetch data for CIK {cik}: {response.status_code}")

    def filings_to_dataframe(self, filings):
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
