from sec_edgar_downloader import Downloader

# Initialize a downloader instance with your company name and email
dl = Downloader("Constantine", "your.email@example.com")
# Download the latest 10-K filing for Apple Inc. (ticker: AAPL)
dl.get("10-K", "NVDA", limit=1)
