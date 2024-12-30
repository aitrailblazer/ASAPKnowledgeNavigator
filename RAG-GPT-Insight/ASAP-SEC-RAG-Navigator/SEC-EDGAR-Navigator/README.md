# SEC-EDGAR-Navigator

## Overview
This project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

## Program.cs Documentation

### Main Function
The main function orchestrates the fetching and downloading of SEC filings for a given company ticker.

### Variables
- **datasetPath**: Path to the JSON file containing company tickers and exchange information.
- **outputDir**: Directory where the downloaded files will be saved.
- **email**: Email address used for SEC filings requests.
- **companyInfo**: Instance of `CompanyInfo` initialized with the dataset path.
- **secFilings**: Instance of `SECFilings` initialized with the email address.
- **ticker**: Example ticker symbol for which the filings will be fetched.
- **cik**: Central Index Key (CIK) fetched using the ticker symbol.
- **filings**: Filing history fetched using the CIK.
- **filingsDataFrame**: DataFrame containing the filing history.
- **latest10K**: The latest 10-K filing from the filing history.
- **accessionNumber**: Formatted accession number of the latest 10-K filing.
- **fileName**: Name of the primary document in the latest 10-K filing.
- **savePath**: Path where the latest 10-K filing will be saved.

## Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/your-repo/sec-edgar-navigator.git
   ```
2. Install dependencies:
   ```bash
   dotnet restore
   ```

## Usage
Run the main script to fetch and process SEC EDGAR data:

```bash
dotnet run
```

## Features
- Fetch a company's Central Index Key (CIK) by its ticker symbol.
- Search for companies by name substring.
- Retrieve filing histories for a company using its CIK.
- Download and save specific filings as HTML and PDF (requires WeasyPrint).

## Requirements
- .NET 6.0 or higher
- Libraries in `SEC-EDGAR-Navigator.csproj`
- Kaggle dataset file (`company_tickers_exchange.json`) containing company tickers, names, and exchanges.

## Limitations
- WeasyPrint requires system dependencies such as GTK, Cairo, and Pango. Make sure these are installed on your machine.
- API calls to SEC EDGAR RESTful services must follow the SEC's fair use policy (maximum 10 requests per second).

## Troubleshooting
If you encounter issues with WeasyPrint, ensure the required libraries are installed on your system. Follow [WeasyPrint's installation guide](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html#installation).

## License
This project is licensed under the MIT License. See the LICENSE file for details.
