import os
import re
import logging
from io import BytesIO
import httpx
from io import BytesIO
import logging
from starlette.applications import Starlette
from starlette.requests import Request

from starlette.responses import JSONResponse, StreamingResponse, PlainTextResponse, HTMLResponse
from httpx import AsyncClient, RequestError
from starlette.routing import Route
from weasyprint import HTML
from weasyprint import default_url_fetcher
import plotly.express as px

from sec_data.company_info import CompanyInfo
from sec_data.filings import SECFilings
from tenacity import retry, stop_after_attempt, wait_exponential

# Configure logging
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

# Paths and setup
dataset_path = "data/company_tickers_exchange.json"
email = "FinanceDataCorp your-email@example.com"

# Initialize components
company_info = CompanyInfo(dataset_path)
sec_filings = SECFilings(email)

# Retry logic for fetching SEC filings
@retry(stop=stop_after_attempt(5), wait=wait_exponential(multiplier=1, min=2, max=10))
async def fetch_filing_with_retry(client, url):
    """Fetch filing content with retry logic."""
    response = await client.get(url)
    response.raise_for_status()
    return response.text

async def get_cik(request):
    """Endpoint to fetch the CIK for a given company ticker."""
    ticker = request.path_params["ticker"]
    try:
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Fetch the CIK
        cik = company_info.get_cik_by_ticker(ticker)

        # Ensure the CIK is returned as a string
        return PlainTextResponse(str(cik))

    except ValueError as e:
        logger.error(f"Error fetching CIK for ticker {ticker}: {e}")
        return PlainTextResponse(f"Error: {str(e)}", status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching CIK for ticker {ticker}: {e}")
        return PlainTextResponse(f"Server Error: {str(e)}", status_code=500)

async def get_name(request):
    """
    Endpoint to fetch the Company Name for a given company ticker.
    """
    ticker = request.path_params["ticker"]
    try:
        # Validate the ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")
        
        # Fetch the company name
        name = company_info.get_name_by_ticker(ticker)
        
        # Return the company name as plain text
        return PlainTextResponse(name)
    
    except ValueError as e:
        logger.error(f"Error fetching Company Name for ticker {ticker}: {e}")
        return PlainTextResponse(f"Error: {str(e)}", status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching Company Name for ticker {ticker}: {e}")
        return PlainTextResponse(f"Error: An unexpected error occurred: {str(e)}", status_code=500)

async def get_exchange(request):
    """
    Endpoint to fetch the exchange for a given company ticker.
    """
    ticker = request.path_params["ticker"]
    try:
        # Validate the ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")
        
        # Fetch the exchange
        exchange = company_info.get_exchange_by_ticker(ticker)
        
        # Return the exchange as plain text
        return PlainTextResponse(exchange)
    
    except ValueError as e:
        logger.error(f"Error fetching exchange for ticker {ticker}: {e}")
        return PlainTextResponse(f"Error: {str(e)}", status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching exchange for ticker {ticker}: {e}")
        return PlainTextResponse(f"Error: An unexpected error occurred: {str(e)}", status_code=500)


async def get_all_exchanges(request: Request):
    """
    Endpoint to fetch all unique exchanges from the dataset.

    Args:
        request (Request): The HTTP request.

    Returns:
        JSONResponse: A response containing the list of unique exchanges.
    """
    try:
        # Fetch all unique exchanges
        exchanges = company_info.get_all_exchanges()
        return JSONResponse({"exchanges": exchanges})
    
    except Exception as e:
        logger.error(f"Unexpected error fetching exchanges: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)


async def tickers_by_exchange(request: Request):
    """
    Endpoint to fetch a list of companies (with CIK, name, and ticker) listed on a specific exchange.

    Args:
        request (Request): The HTTP request containing the path parameter.

    Returns:
        JSONResponse: A response containing a list of companies or an error message.
    """
    exchange = request.path_params.get("exchange")
    try:
        # Validate exchange input
        if not exchange:
            raise ValueError("Exchange name is required.")
        
        # Fetch companies for the specified exchange
        companies = company_info.get_companies_by_exchange(exchange)
        
        if not companies:
            return JSONResponse({"exchange": exchange, "companies": []}, status_code=404)
        
        # Return the list of companies
        return JSONResponse({"exchange": exchange, "companies": companies})
    
    except ValueError as e:
        logger.error(f"Error fetching companies for exchange {exchange}: {e}")
        return JSONResponse({"error": str(e)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching companies for exchange {exchange}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)
async def get_filings(request):
    """Endpoint to fetch filing history for a given company ticker."""
    ticker = request.path_params["ticker"]
    try:
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")
        cik = company_info.get_cik_by_ticker(ticker)
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)
        return JSONResponse(filings_df.to_dict(orient="records"))
    except ValueError as e:
        logger.error(f"Error fetching filings for ticker {ticker}: {e}")
        return JSONResponse({"error": str(e)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching filings for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)


async def get_available_forms(request):
    """
    Endpoint to fetch all unique forms available for a given company ticker.
    """
    ticker = request.path_params["ticker"]
    try:
        # Validate ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Get the CIK and filings data
        cik = company_info.get_cik_by_ticker(ticker)
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)

        # Fetch all unique forms
        unique_forms = filings_df["form"].unique().tolist()

        # Return the unique forms
        return JSONResponse({"ticker": ticker, "forms": unique_forms})

    except ValueError as e:
        logger.error(f"Error fetching forms for ticker {ticker}: {e}")
        return JSONResponse({"error": str(e)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error fetching forms for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)


async def download_latest_10k(request):
    """Fetch the latest 10-K report as HTML and convert it to a PDF."""
    ticker = request.path_params["ticker"]
    try:
        # Fetch the HTML content and base URL directly using the Python function
        html_content, base_url = await download_latest_10k_html_python(ticker)

        # Convert HTML to PDF using the base_url for resource resolution
        pdf_buffer = convert_html_to_pdf(html_content, base_url=base_url)

        # Return the PDF as a streaming response
        headers = {"Content-Disposition": f"attachment; filename={ticker}_latest_10K.pdf"}
        return StreamingResponse(pdf_buffer, media_type="application/pdf", headers=headers)

    except IndexError:
        logger.error(f"No 10-K filings found for ticker {ticker}")
        return JSONResponse({"error": f"No 10-K filings found for ticker: {ticker}"}, status_code=404)
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)

async def download_latest_filing_pdf(request):
    """
    Web service endpoint to download the latest filing of a specified form type as a PDF.
    """
    ticker = request.path_params["ticker"]
    form_type = request.path_params["form_type"]

    try:
        # Validate the ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Fetch available forms for the ticker
        cik = company_info.get_cik_by_ticker(ticker)
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)
        available_forms = filings_df["form"].unique().tolist()

        # Check if the requested form exists
        if form_type not in available_forms:
            raise ValueError(f"The requested form '{form_type}' does not exist for ticker {ticker}. Available forms: {available_forms}")

        # Fetch the HTML content and base URL for the requested form
        html_content, base_url = await download_latest_filing_html_python(ticker, form_type)

        # Convert HTML to PDF
        pdf_buffer = convert_html_to_pdf(html_content, base_url=base_url)

        # Return the PDF as a streaming response
        headers = {"Content-Disposition": f"attachment; filename={ticker}_latest_{form_type}.pdf"}
        return StreamingResponse(pdf_buffer, media_type="application/pdf", headers=headers)

    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except IndexError:
        logger.error(f"No {form_type} filings found for ticker {ticker}")
        return JSONResponse({"error": f"No {form_type} filings found for ticker: {ticker}"}, status_code=404)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)

def custom_url_fetcher(url):
    """
    Custom URL fetcher using httpx to handle SSL issues, add custom headers, and provide better control over resource fetching.

    Args:
        url (str): The URL to fetch.

    Returns:
        dict: A dictionary containing the fetched resource data.

    Raises:
        Exception: If the resource cannot be fetched.
    """
    headers = {
        "User-Agent": email,  # Replace with your email address
        "Referer": "https://www.sec.gov/"  # Add Referer header if required
    }

    try:
        # Use httpx to fetch the resource
        with httpx.Client(timeout=30.0, headers=headers) as client:
            response = client.get(url)
            response.raise_for_status()  # Raise exception for HTTP errors

            # Return the resource data in the format expected by WeasyPrint
            return {
                "string": response.content,  # Resource content as bytes
                "mime_type": response.headers.get("Content-Type"),  # Content-Type of the resource
                "encoding": None  # Encoding is usually None for binary resources like images
            }

    except httpx.RequestError as exc:
        logger.error(f"An error occurred while requesting {exc.request.url!r}: {exc}")
        raise

    except httpx.HTTPStatusError as exc:
        logger.error(f"HTTP error {exc.response.status_code} while requesting {exc.request.url!r}")
        raise

    except Exception as e:
        logger.error(f"Failed to fetch resource: {url}. Error: {e}")
        raise

def convert_html_to_pdf(html_content, base_url=None):
    """
    Helper function to convert an HTML string to a PDF buffer.

    Args:
        html_content (str): The HTML content to convert to PDF.
        base_url (str, optional): The base URL for resolving relative paths in the HTML.

    Returns:
        BytesIO: A buffer containing the generated PDF.

    Raises:
        ValueError: If the HTML content is empty.
        Exception: For any other errors during conversion.
    """
    try:
        # Validate HTML content
        if not html_content or not html_content.strip():
            raise ValueError("Empty HTML content provided.")

        # Log the base_url for debugging purposes
        if base_url:
            logger.info(f"Base URL provided: {base_url}")
        else:
            logger.warning("No base URL provided. Relative paths may not resolve correctly.")

        # Log the start of the conversion process
        logger.info("Starting HTML to PDF conversion.")

        # Convert HTML to PDF
        pdf_buffer = BytesIO()
        
        # Pass the HTML content and base URL for resource resolution
        html_obj = HTML(string=html_content, base_url=base_url, url_fetcher=custom_url_fetcher)
        
        # Add additional options like optimizing images or setting quality if needed
        html_obj.write_pdf(target=pdf_buffer)

        # Reset buffer position for reading/streaming
        pdf_buffer.seek(0)

        # Log success
        logger.info("Successfully converted HTML to PDF.")
        
        return pdf_buffer

    except ValueError as ve:
        logger.error(f"Validation error: {ve}")
        raise

    except FileNotFoundError as fnfe:
        logger.error(f"Resource not found: {fnfe}. Check image paths or base_url.")
        raise

    except Exception as e:
        logger.error(f"Unexpected error during HTML to PDF conversion: {e}")
        raise

def convert_html_to_pdf1(html_content, base_url=None):
    """
    Helper function to convert an HTML string to a PDF buffer.

    Args:
        html_content (str): The HTML content to convert to PDF.
        base_url (str, optional): The base URL for resolving relative paths in the HTML.

    Returns:
        BytesIO: A buffer containing the generated PDF.

    Raises:
        ValueError: If the HTML content is empty.
        Exception: For any other errors during conversion.
    """
    try:
        # Validate HTML content
        if not html_content or not html_content.strip():
            raise ValueError("Empty HTML content provided.")
        # Log the base_url for debugging purposes
        if base_url:
            logger.info(f"Base URL provided: {base_url}")
        else:
            logger.warning("No base URL provided. Relative paths may not resolve correctly.")
        # Log the start of the conversion process
        logger.info("Starting HTML to PDF conversion.")

        # Convert HTML to PDF
        pdf_buffer = BytesIO()
        
        # Pass the HTML content and base URL for resource resolution
        html_obj = HTML(string=html_content, base_url=base_url)
        html_obj.write_pdf(target=pdf_buffer)
        
        # Reset buffer position for reading/streaming
        pdf_buffer.seek(0)

        # Log success
        logger.info("Successfully converted HTML to PDF.")
        
        return pdf_buffer

    except ValueError as ve:
        logger.error(f"Validation error: {ve}")
        raise

    except FileNotFoundError as fnfe:
        logger.error(f"Resource not found: {fnfe}. Check image paths or base_url.")
        raise

    except Exception as e:
        logger.error(f"Unexpected error during HTML to PDF conversion: {e}")
        raise

async def html_to_pdf(request):
    """Endpoint to convert an HTML string to a PDF via POST."""
    if request.method != "POST":
        return JSONResponse({"error": "Method not allowed. Use POST to submit HTML content."}, status_code=405)

    try:
        # Parse JSON body to retrieve the HTML string
        body = await request.json()
        html_content = body.get("html", "")

        # Use the helper function to convert HTML to PDF
        pdf_buffer = convert_html_to_pdf(html_content)

        # Prepare and return the PDF as a streaming response
        headers = {"Content-Disposition": "attachment; filename=converted.pdf"}
        return StreamingResponse(pdf_buffer, media_type="application/pdf", headers=headers)

    except ValueError as ve:
        logger.error(f"Value error during HTML to PDF conversion: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error during HTML to PDF conversion: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)


async def download_latest_10k_html_python(ticker: str) -> tuple[str, str]:
    """
    Fetch the latest 10-K report for a given company ticker as raw HTML with fixed image links.
    
    Returns:
        tuple: (html_content, base_url)
    """
    try:
        # Validate the ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Get the company's CIK
        cik = company_info.get_cik_by_ticker(ticker)
        if not cik:
            raise ValueError(f"CIK not found for ticker: {ticker}")

        # Fetch filing data
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)

        if filings_df.empty or "10-K" not in filings_df["form"].values:
            raise IndexError(f"No 10-K filings found for ticker: {ticker}")

        # Get the latest 10-K filing details
        latest_10k = filings_df[filings_df["form"] == "10-K"].iloc[0]
        accession_number = latest_10k["accessionNumber"].replace("-", "")
        file_name = latest_10k["primaryDocument"]

        # Build the SEC filing base URL and full filing URL
        base_url = f"https://www.sec.gov/Archives/edgar/data/{cik}/{accession_number}/"
        filing_url = f"{base_url}{file_name}"

        # Fetch the filing content
        async with AsyncClient(timeout=120, headers={"User-Agent": email}) as client:
            try:
                html_content = await fetch_filing_with_retry(client, filing_url)
            except RequestError as e:
                logger.error(f"Failed to fetch filing for {ticker}: {e}")
                raise Exception(f"Failed to fetch filing from SEC for {ticker}.")

        # Replace relative image links with absolute URLs
        html_content = re.sub(
            r'(?<=<img src=")([^":]+)',  # Match the src attribute of <img> tags that do not contain a full URL
            lambda match: f"{base_url}{match.group(1)}",  # Replace with the full URL
            html_content,
        )

        # Return the modified HTML content and base URL
        return html_content, base_url

    except IndexError:
        logger.error(f"No 10-K filings found for ticker {ticker}")
        raise IndexError(f"No 10-K filings found for ticker: {ticker}")
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        raise
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        raise

async def download_latest_filing_html_python(ticker: str, form_type: str) -> tuple[str, str]:
    """
    Fetch the latest filing of a given form type for a company ticker as raw HTML with fixed image links.
    
    Args:
        ticker (str): The company ticker symbol.
        form_type (str): The type of SEC form to fetch (e.g., "10-K", "10-Q").
    
    Returns:
        tuple: (html_content, base_url)
    """
    try:
        # Validate the ticker and form type format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")
        if not re.match(r"^[A-Za-z0-9/ -]+$", form_type):
            raise ValueError("Invalid form type format. Only alphanumeric characters, dashes (-), slashes (/), and spaces are allowed.")

        # Get the company's CIK
        cik = company_info.get_cik_by_ticker(ticker)
        if not cik:
            raise ValueError(f"CIK not found for ticker: {ticker}")

        # Fetch filing data
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)

        if filings_df.empty or form_type not in filings_df["form"].values:
            raise IndexError(f"No {form_type} filings found for ticker: {ticker}")

        # Get the latest filing details for the specified form type
        latest_filing = filings_df[filings_df["form"] == form_type].iloc[0]
        accession_number = latest_filing["accessionNumber"].replace("-", "")
        file_name = latest_filing["primaryDocument"]

        # Build the SEC filing base URL and full filing URL
        base_url = f"https://www.sec.gov/Archives/edgar/data/{cik}/{accession_number}/"
        filing_url = f"{base_url}{file_name}"

        print(filing_url)

        # Fetch the filing content
        async with AsyncClient(timeout=120, headers={"User-Agent": email}) as client:
            try:
                html_content = await fetch_filing_with_retry(client, filing_url)
            except RequestError as e:
                logger.error(f"Failed to fetch filing for {ticker}: {e}")
                raise Exception(f"Failed to fetch filing from SEC for {ticker}.")

        # Replace relative image links with absolute URLs
        html_content = re.sub(
            r'(?<=<img src=")([^":]+)',  # Match the src attribute of <img> tags that do not contain a full URL
            lambda match: f"{base_url}{match.group(1)}",  # Replace with the full URL
            html_content,
        )

        # Return the modified HTML content and base URL
        return html_content, base_url

    except IndexError:
        logger.error(f"No {form_type} filings found for ticker {ticker}")
        raise IndexError(f"No {form_type} filings found for ticker: {ticker}")
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        raise
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        raise

async def get_filing_url(request: Request):
    """
    Endpoint to get the URL of the filing for a given form type and company ticker.

    Args:
        request (Request): The HTTP request containing the path parameters.

    Returns:
        PlainTextResponse: A plain-text response containing the filing URL.
    """
    try:
        # Extract ticker and form_type from the path parameters
        ticker = request.path_params.get("ticker")
        form_type = request.path_params.get("form_type")

        # Validate the ticker and form type format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")
        if not re.match(r"^[A-Za-z0-9/ -]+$", form_type):
            raise ValueError("Invalid form type format. Only alphanumeric characters, dashes (-), slashes (/), and spaces are allowed.")

        # Get the company's CIK
        cik = company_info.get_cik_by_ticker(ticker)
        if not cik:
            raise ValueError(f"CIK not found for ticker: {ticker}")

        # Fetch filing data
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)

        if filings_df.empty or form_type not in filings_df["form"].values:
            raise IndexError(f"No {form_type} filings found for ticker: {ticker}")

        # Get the latest filing details for the specified form type
        latest_filing = filings_df[filings_df["form"] == form_type].iloc[0]
        accession_number = latest_filing["accessionNumber"].replace("-", "")
        file_name = latest_filing["primaryDocument"]

        # Build the SEC filing base URL and full filing URL
        base_url = f"https://www.sec.gov/Archives/edgar/data/{cik}/{accession_number}/"
        filing_url = f"{base_url}{file_name}"

        # Return the filing URL as plain text
        return PlainTextResponse(filing_url)

    except IndexError:
        logger.error(f"No {form_type} filings found for ticker {ticker}")
        return PlainTextResponse(f"No {form_type} filings found for ticker: {ticker}", status_code=404)
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return PlainTextResponse(str(ve), status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return PlainTextResponse(f"An unexpected error occurred: {str(e)}", status_code=500)


async def download_latest_10k_html(request):
    """Web service endpoint to download the latest 10-K report as raw HTML with fixed image links."""
    ticker = request.path_params["ticker"]
    try:
        # Call the Python function to fetch HTML content and base URL
        html_content, base_url = await download_latest_10k_html_python(ticker)

        # Return the modified HTML content as a streaming response
        headers = {"Content-Disposition": f"attachment; filename={ticker}_latest_10K.html"}
        return StreamingResponse(BytesIO(html_content.encode("utf-8")), media_type="text/html", headers=headers)

    except IndexError:
        logger.error(f"No 10-K filings found for ticker {ticker}")
        return JSONResponse({"error": f"No 10-K filings found for ticker: {ticker}"}, status_code=404)
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)

async def download_latest_filing_html(request):
    """
    Web service endpoint to download the latest filing of a specified form type as raw HTML with fixed image links.
    """
    ticker = request.path_params["ticker"]
    form_type = request.path_params["form_type"].replace("_", "/")  # Replace _ with /
    logger.info(f"ticker: {ticker}")

    try:
        # Validate the ticker format
        if not re.match(r"^[A-Za-z0-9.-]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters, dashes (-), and periods (.) are allowed.")
        # Fetch available forms for the ticker
        cik = company_info.get_cik_by_ticker(ticker)
        filings = sec_filings.get_company_filings(cik)
        filings_df = sec_filings.filings_to_dataframe(filings)
        available_forms = filings_df["form"].unique().tolist()

        # Check if the requested form exists
        if form_type not in available_forms:
            raise ValueError(f"The requested form '{form_type}' does not exist for ticker {ticker}. Available forms: {available_forms}")

        # Fetch the HTML content and base URL for the requested form
        html_content, base_url = await download_latest_filing_html_python(ticker, form_type)
        logger.info(f"Base URL provided: {base_url}")

        # Return the modified HTML content as a streaming response
        headers = {"Content-Disposition": f"attachment; filename={ticker}_latest_{form_type}.html"}
        return StreamingResponse(BytesIO(html_content.encode("utf-8")), media_type="text/html", headers=headers)

    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except IndexError:
        logger.error(f"No {form_type} filings found for ticker {ticker}")
        return JSONResponse({"error": f"No {form_type} filings found for ticker: {ticker}"}, status_code=404)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)

async def get_xbrl_data(request):
    """
    Endpoint to fetch and process XBRL data for a given company ticker.
    """
    ticker = request.path_params["ticker"]
    concept = request.query_params.get("concept", "AssetsCurrent")
    unit = request.query_params.get("unit", "USD")

    try:
        # Validate ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Get CIK for the ticker
        cik = company_info.get_cik_by_ticker(ticker)
        if not cik:
            raise ValueError(f"CIK not found for ticker: {ticker}")

        # Fetch and process XBRL data
        xbrl_data = sec_filings.get_xbrl_data(cik)
        xbrl_df = sec_filings.process_xbrl_data(xbrl_data, concept=concept, unit=unit)

        # Convert DataFrame to JSON for response
        return JSONResponse(xbrl_df.to_dict(orient="records"))
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)

async def plot_xbrl_data(request):
    """
    Endpoint to fetch, process, and plot XBRL data for a given company ticker.
    """
    ticker = request.path_params["ticker"]
    concept = request.query_params.get("concept", "AssetsCurrent")
    unit = request.query_params.get("unit", "USD")

    try:
        # Get CIK and fetch XBRL data
        cik = company_info.get_cik_by_ticker(ticker)
        xbrl_data = sec_filings.get_xbrl_data(cik)
        xbrl_df = sec_filings.process_xbrl_data(xbrl_data, concept=concept, unit=unit)

        # Filter valid frames and plot
        valid_frame_df = xbrl_df[xbrl_df.frame.notna()]

        # Create the plot with enhancements
        fig = px.line(
            valid_frame_df,
            x="end",
            y="val",
            title=f"{ticker}: {concept} Over Time",
            labels={"end": "Quarter End", "val": f"Value ({unit})"},
            template="plotly_white"  # Apply a clean theme
        )

        # Customize line and markers
        fig.update_traces(
            mode="lines+markers",
            line=dict(width=2),
            marker=dict(size=6)
        )

        # Remove gridlines and set background color
        fig.update_layout(
            plot_bgcolor='white',
            xaxis=dict(showgrid=False, linecolor='black'),
            yaxis=dict(showgrid=False, linecolor='black')
        )

        # Add hover information
        fig.update_traces(
            hovertemplate='Quarter End: %{x}<br>Value: %{y}<extra></extra>'
        )

        # Return Plotly HTML
        return HTMLResponse(fig.to_html())
    except Exception as e:
        logger.error(f"Error plotting XBRL data for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)
"""
 the following concepts from your list are among the most significant:

Assets: Represents the total resources owned by the company.
AssetsCurrent: Denotes assets expected to be converted to cash or used within a year.
Liabilities: Indicates the company's total obligations.
LiabilitiesCurrent: Shows obligations the company needs to settle within a year.
StockholdersEquity: Reflects the residual interest in the company's assets after deducting liabilities.
Revenues: Measures the total income generated from the sale of goods or services.
CostOfGoodsAndServicesSold: Represents the direct costs attributable to the production of goods sold by the company.
GrossProfit: Calculated as Revenues minus CostOfGoodsAndServicesSold, indicating the efficiency of production and pricing.
OperatingIncomeLoss: Shows the profit or loss from primary business operations, excluding non-operational income and expenses.
NetIncomeLoss: Represents the company's total earnings or losses, providing insight into overall profitability.
EarningsPerShareBasic: Indicates the portion of a company's profit allocated to each outstanding share of common stock.
CashAndCashEquivalentsAtCarryingValue: Shows the company's liquidity position, essential for meeting short-term obligations.
LongTermDebt: Represents the total amount of debt due beyond one year, impacting the company's long-term financial stability.
ResearchAndDevelopmentExpense: Reflects the company's investment in innovation and future growth.
SellingGeneralAndAdministrativeExpense: Encompasses the overhead costs related to selling products and managing the business.
"""
async def list_xbrl_concepts(request: Request):
    """
    Endpoint to list all available XBRL concepts for a given company ticker.
    """
    ticker = request.path_params["ticker"]

    try:
        # Validate ticker format
        if not re.match(r"^[A-Za-z0-9]+$", ticker):
            raise ValueError("Invalid ticker format. Only alphanumeric characters are allowed.")

        # Get CIK for the ticker
        cik = company_info.get_cik_by_ticker(ticker)
        if not cik:
            raise ValueError(f"CIK not found for ticker: {ticker}")

        # Fetch XBRL data
        xbrl_data = sec_filings.get_xbrl_data(cik)

        # Extract available concepts
        concepts = list(xbrl_data.get("facts", {}).get("us-gaap", {}).keys())

        return JSONResponse({"ticker": ticker, "concepts": concepts})
    except ValueError as ve:
        logger.error(f"Value error for ticker {ticker}: {ve}")
        return JSONResponse({"error": str(ve)}, status_code=400)
    except Exception as e:
        logger.error(f"Unexpected error for ticker {ticker}: {e}")
        return JSONResponse({"error": f"An unexpected error occurred: {str(e)}"}, status_code=500)
    
async def root(request):
    """Root endpoint that says Hello."""
    return PlainTextResponse("Hello")

# Define routes
routes = [
    Route("/", root),  # Root endpoint
    Route("/cik/{ticker}", get_cik),  # Fetch the CIK (Central Index Key) for a ticker
    Route("/name/{ticker}", get_name),  # Fetch the CIK (Central Index Key) for a ticker
    Route("/exchanges", get_all_exchanges, methods=["GET"]),  # Fetch all unique exchanges
    Route("/exchange/{ticker}", get_exchange),  # Fetch the CIK (Central Index Key) for a ticker
    Route("/tickersbyexchange/{exchange}", tickers_by_exchange, methods=["GET"]),  # Fetch tickers for a specific exchange
    Route("/filings/{ticker}", get_filings),  # Fetch all filings for a ticker
    Route("/filing/url/{ticker}/{form_type}", get_filing_url, methods=["GET"]),    
    Route("/forms/{ticker}", get_available_forms, methods=["GET"]),
    Route("/filing/html/{ticker}/{form_type}", download_latest_filing_html, methods=["GET"]),  # Fetch the latest filing of a specified form type as raw HTML
    Route("/filing/pdf/{ticker}/{form_type}", download_latest_filing_pdf, methods=["GET"]),    
    Route("/forms/{ticker}", get_available_forms, methods=["GET"]),
    Route("/10k/pdf/{ticker}", download_latest_10k),  # Fetch the latest 10-K as a PDF
    Route("/10k/html/{ticker}", download_latest_10k_html),  # Fetch the latest 10-K as raw HTML
    Route("/xbrl/{ticker}", get_xbrl_data),  # Fetch XBRL data as JSON
    Route("/xbrl/concepts/{ticker}", list_xbrl_concepts, methods=["GET"]),
    Route("/xbrl/plot/{ticker}", plot_xbrl_data),  # Plot XBRL data
#    Route("/html-to-pdf", html_to_pdf, methods=["POST"]),  # Convert HTML content to a PDF
]


# Create Starlette app
app = Starlette(debug=True, routes=routes)

if __name__ == "__main__":
    port = int(os.getenv("PORT", 8000))
    import uvicorn
    uvicorn.run("main:app", host="0.0.0.0", port=port, log_level="info")
