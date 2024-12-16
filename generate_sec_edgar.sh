#!/bin/bash
# generate_sec_edgar.sh
# Define project structure
PROJECT_NAME="sec-edgar"
DIRS=(
    "$PROJECT_NAME"
    "$PROJECT_NAME/sec_data"
    "$PROJECT_NAME/data"
    "$PROJECT_NAME/tests"
)
FILES=(
    "$PROJECT_NAME/sec_data/__init__.py"
    "$PROJECT_NAME/sec_data/company_info.py"
    "$PROJECT_NAME/sec_data/filings.py"
    "$PROJECT_NAME/tests/__init__.py"
    "$PROJECT_NAME/tests/test_filings.py"
    "$PROJECT_NAME/main.py"
    "$PROJECT_NAME/README.md"
    "$PROJECT_NAME/requirements.txt"
    "$PROJECT_NAME/.gitignore"
)

# Create directories
echo "Creating project directories..."
for dir in "${DIRS[@]}"; do
    mkdir -p "$dir"
done

# Create files
echo "Creating project files..."
for file in "${FILES[@]}"; do
    touch "$file"
done

# Add content to Python files
echo "Adding content to project files..."

# sec_data/company_info.py
cat <<EOF > "$PROJECT_NAME/sec_data/company_info.py"
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
EOF

# sec_data/filings.py
cat <<EOF > "$PROJECT_NAME/sec_data/filings.py"
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
EOF

# main.py
cat <<EOF > "$PROJECT_NAME/main.py"
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
    ticker = "AMZN"
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
EOF
# requirements.txt
cat <<EOF > "$PROJECT_NAME/requirements.txt"
pandas
requests
weasyprint
EOF

# tests/test_filings.py
cat <<EOF > "$PROJECT_NAME/tests/test_filings.py"
import unittest
from sec_data.company_info import CompanyInfo

class TestCompanyInfo(unittest.TestCase):
    def setUp(self):
        self.json_path = "data/company_tickers_exchange.json"
        self.company_info = CompanyInfo(self.json_path)

    def test_get_cik_by_ticker(self):
        cik = self.company_info.get_cik_by_ticker("AAPL")
        self.assertEqual(cik, 320193)

    def test_search_by_name(self):
        result = self.company_info.search_by_name("Amazon")
        self.assertGreater(len(result), 0)

if __name__ == "__main__":
    unittest.main()
EOF

# README.md
# README.md continued
cat <<EOF >> "$PROJECT_NAME/README.md"
3. Add your Kaggle dataset file, \`company_tickers_exchange.json\`, to the \`data/\` directory.

## Usage
Run the main script to fetch and process SEC EDGAR data:

\`\`\`bash
python main.py
\`\`\`

## Testing
Run the test cases to verify the implementation:

\`\`\`bash
python -m unittest discover tests
\`\`\`

## Features
- Fetch a company's Central Index Key (CIK) by its ticker symbol.
- Search for companies by name substring.
- Retrieve filing histories for a company using its CIK.
- Download and save specific filings as HTML and PDF (requires WeasyPrint).

## Requirements
- Python 3.7 or higher
- Libraries in \`requirements.txt\`
- Kaggle dataset file (\`company_tickers_exchange.json\`) containing company tickers, names, and exchanges.

## Limitations
- WeasyPrint requires system dependencies such as GTK, Cairo, and Pango. Make sure these are installed on your machine.
- API calls to SEC EDGAR RESTful services must follow the SEC's fair use policy (maximum 10 requests per second).

## Troubleshooting
If you encounter issues with WeasyPrint, ensure the required libraries are installed on your system. Follow [WeasyPrint's installation guide](https://doc.courtbouillon.org/weasyprint/stable/first_steps.html#installation).

## License
This project is licensed under the MIT License. See the LICENSE file for details.
EOF

# Add .gitignore content
cat <<EOF > "$PROJECT_NAME/.gitignore"
__pycache__/
*.pyc
*.log
.env
*.json
*.html
*.pdf
EOF

# Final Message
echo "Project structure created successfully in $PROJECT_NAME!"
echo "Navigate to the project directory and start coding!"
