#!/bin/bash

# Set variables
CIK="1045810"  # NVIDIA's CIK
USER_AGENT="Constantine (constantine@aitrailblazer.com)"
RAW_RESPONSE_FILE="submissions.json"
OUTPUT_10K_FILE="latest_10k.html"

fetch_latest_10k_info() {
    echo "Fetching the latest 10-K filing information for NVIDIA..."

    # Parse the JSON for the latest 10-K filing
    ACCESSION_NUMBER=$(jq -r '
        .filings.recent.form as $forms |
        .filings.recent.accessionNumber as $accessions |
        .filings.recent.primaryDocument as $documents |
        to_entries |
        map(
            select($forms[.key] == "10-K")
        ) |
        max_by(.key) |
        $accessions[.key]' "$RAW_RESPONSE_FILE")

    PRIMARY_DOCUMENT=$(jq -r '
        .filings.recent.form as $forms |
        .filings.recent.primaryDocument as $documents |
        to_entries |
        map(
            select($forms[.key] == "10-K")
        ) |
        max_by(.key) |
        $documents[.key]' "$RAW_RESPONSE_FILE")

    if [ -z "$ACCESSION_NUMBER" ] || [ -z "$PRIMARY_DOCUMENT" ]; then
        echo "No 10-K filings found in the response."
        exit 1
    fi

    # Format the accession number and construct the URL
    ACCESSION_NUMBER_NO_DASHES=$(echo "$ACCESSION_NUMBER" | tr -d '-')
    FILING_URL="https://www.sec.gov/Archives/edgar/data/${CIK}/${ACCESSION_NUMBER_NO_DASHES}/${PRIMARY_DOCUMENT}"

    echo "Constructed URL for 10-K filing: $FILING_URL"

    # Test the URL
    HTTP_STATUS=$(curl -o /dev/null -s -w "%{http_code}" -H "User-Agent: $USER_AGENT" "$FILING_URL")
    if [ "$HTTP_STATUS" -ne 200 ]; then
        echo "Error: Unable to access 10-K filing URL. HTTP Status: $HTTP_STATUS"
        exit 1
    fi

    # Download the filing
    echo "Downloading 10-K filing to $OUTPUT_10K_FILE..."
    curl -o "$OUTPUT_10K_FILE" -H "User-Agent: $USER_AGENT" "$FILING_URL"

    if [ -s "$OUTPUT_10K_FILE" ]; then
        echo "10-K filing saved to $OUTPUT_10K_FILE"
    else
        echo "Error: Failed to download 10-K filing. Check the URL: $FILING_URL"
        exit 1
    fi
}

fetch_latest_10k_info
