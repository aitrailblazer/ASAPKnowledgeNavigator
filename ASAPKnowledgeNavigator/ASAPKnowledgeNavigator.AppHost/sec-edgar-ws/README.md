# sec-edgar-ws

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

docker image prune -a
docker network prune

docker build -t sec-edgar-ws .
docker run -p 8000:8000 sec-edgar-ws

# Fetch the CIK for AAPL
curl http://localhost:8000/cik/AAPL
curl http://localhost:8000/forms/AAPL
{"ticker":"AAPL","forms":["4","144","S-3ASR","10-K","8-K","5","10-Q","UPLOAD","CORRESP","SD","3","SC 13G/A","PX14A6G","25-NSE","DEFA14A","DEF 14A","424B2","FWP","4/A","S-8","S-8 POS","PX14A6N","IRANNOTICE","CERT","8-A12B","3/A","25","SC 13G","8-K/A","CERTNYS","NO ACT"]}

# Fetch the CIK for TSLA
curl http://localhost:8000/cik/TSLA

# Fetch the filing history for TSLA
curl http://localhost:8000/filings/TSLA

curl http://localhost:8000/forms/TSLA
{"ticker":"TSLA","forms":["8-K","4","144","10-Q","DEFA14A","PX14A6G","SD","ARS","DEF 14A","PRE 14A","SC 13G/A","10-K","UPLOAD","CORRESP","8-K/A","CT ORDER","3","PX14A6N","5/A","5","4/A","10-K/A","SC 13G","424B5","SEC STAFF LETTER","S-8 POS","S-8","POS AM","SC TO-T/A","425","424B3","EFFECT","FWP","S-3ASR","S-4/A","SC TO-T","S-4","D","NO ACT"]}

curl -o TSLA_10K.html http://localhost:8000/filing/html/TSLA/10-K
curl -o TSLA_4.html http://localhost:8000/filing/html/TSLA/4

curl -o TSLA_4.pdf http://localhost:8000/filing/pdf/TSLA/4

# Download the latest 10-K as a PDF for AAPL
curl -o AAPL_10K.pdf http://localhost:8000/10k/pdf/AAPL

# Download the latest 10-K as a PDF for TSLA
curl -o TSLA_10K.pdf http://localhost:8000/10k/pdf/TSLA

# Download the latest 10-K HTML for AAPL
curl -o AAPL_10K.html http://localhost:8000/10k/html/AAPL

# Download the latest 10-K HTML for TSLA
curl -o TSLA_10K.html http://localhost:8000/10k/html/TSLA

curl -o AAPLconcepts.json http://localhost:8000/xbrl/concepts/AAPL


curl http://localhost:8000/xbrl/AAPL?concept=AssetsCurrent&unit=USD

curl http://localhost:8000/xbrl/plot/AAPL?concept=AssetsCurrent&unit=USD

curl -o AAPL_AssetsCurrent.html "http://localhost:8000/xbrl/plot/AAPL?concept=AssetsCurrent&unit=USD"


curl -X POST http://localhost:8000/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < AAPL_10K.html) \
     -o AAPL_10K.pdf

curl -X POST http://localhost:8000/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < TSLA_10K.html) \
     -o TSLA_10K.pdf
