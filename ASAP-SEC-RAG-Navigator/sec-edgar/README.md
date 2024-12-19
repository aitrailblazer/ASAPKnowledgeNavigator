# SEC EDGAR Data Project

## Overview
This project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

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
