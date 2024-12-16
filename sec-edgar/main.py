import os
from sec_data.company_info import CompanyInfo
from sec_data.filings import SECFilings

def main():
    # Paths and setup
    dataset_path = "data/company_tickers_exchange.json"
    output_dir = "data"
    os.makedirs(output_dir, exist_ok=True)
    email = "your.email@example.com"

    # Initialize components
    company_info = CompanyInfo(dataset_path)
    sec_filings = SECFilings(email)
    # Fetch company CIK by ticker
    ticker = "TSLA" # TSLA, AAPL, NVDA, MSFT,AMZN, GOOGLE, META
    try:
        cik = company_info.get_cik_by_ticker(ticker)
        print(f"CIK for {ticker}: {cik}")
    except ValueError as e:
        print(e)
        return

    # Fetch filing history
    try:
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)
        print(filings_df.head())
    except ValueError as e:
        print(e)
        return

    # Download the latest 10-K report
    latest_10k = filings_df[filings_df["form"] == "10-K"].iloc[0]
    accession_number = latest_10k["accessionNumber"].replace("-", "")
    file_name = latest_10k["primaryDocument"]
    save_path = os.path.join(output_dir, f"{file_name}.html")

    try:
        sec_filings.download_document(cik, accession_number, file_name, save_path)
        print(f"Downloaded 10-K to {save_path}")
    except Exception as e:
        print(f"Failed to download document: {e}")

if __name__ == "__main__":
    main()
