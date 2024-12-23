# sec-edgar

SEC EDGAR Data Project

## Overview
This project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

# main.py Documentation

## main()
The main function that orchestrates the fetching and downloading of SEC filings for a given company ticker.

## Variables
- **dataset_path**: Path to the JSON file containing company tickers and exchange information.
- **output_dir**: Directory where the downloaded files will be saved.
- **email**: Email address used for SEC filings requests.
- **company_info**: Instance of `CompanyInfo` initialized with the dataset path.
- **sec_filings**: Instance of `SECFilings` initialized with the email address.
- **ticker**: Example ticker symbol for which the filings will be fetched.
- **cik**: Central Index Key (CIK) fetched using the ticker symbol.
- **filings**: Filing history fetched using the CIK.
- **filings_df**: DataFrame containing the filing history.
- **latest_10k**: The latest 10-K filing from the filing history.
- **accession_number**: Formatted accession number of the latest 10-K filing.
- **file_name**: Name of the primary document in the latest 10-K filing.
- **save_path**: Path where the latest 10-K filing will be saved.


## Setup
1. Clone the repository:
   ```bash
   git clone https://github.com/your-repo/sec-edgar-project.git
   ```
2. Install dependencies:
   ```bash
   pip install -r requirements.txt
   ```
3. Add your Ka
3. Add your Kaggle dataset file, `company_tickers_exchange.json`, to the `data/` directory.

## Usage
Run the main script to fetch and process SEC EDGAR data:

```bash
python main.py
```

## Testing
Run the test cases to verify the implementation:

```bash
python -m unittest discover tests
```

## Features
- Fetch a company's Central Index Key (CIK) by its ticker symbol.
- Search for companies by name substring.
- Retrieve filing histories for a company using its CIK.
- Download and save specific filings as HTML and PDF (requires WeasyPrint).

## Requirements
- Python 3.7 or higher
- Libraries in `requirements.txt`
- Kaggle dataset file (`company_tickers_exchange.json`) containing company tickers, names, and exchanges.

## Limitations
- WeasyPrint requires system dependencies such as GTK, Cairo, and Pango. Make sure these are installed on your machine.
- API calls to SEC EDGAR RESTful services must follow the SEC's fair use policy (maximum 10 requests per second).

## Troubleshooting
If you encounter issues with WeasyPrint, ensure the required libraries are installed on your system. Follow [WeasyPrint's installation guide](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html#installation).

## License
This project is licensed under the MIT License. See the LICENSE file for details.
