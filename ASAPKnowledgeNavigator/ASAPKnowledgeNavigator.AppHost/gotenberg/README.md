```bash
curl -X POST http://localhost:3000/forms/chromium/convert/html \
  -F "files=@AAPL_10K.html" \
  -o AAPL_10K.pdf

curl \
--request POST http://localhost:3000/forms/chromium/convert/html \
--form files=@index1.html \
-o AAPL_10K.pdf

curl \
--request POST http://localhost:3000/forms/chromium/convert/html \
--form files=@index.html \
-o my.pdf



curl \
--request POST http://localhost:3000/forms/chromium/convert/url \
--form url=https://www.sec.gov/Archives/edgar/data/1318605/000162828024002390/tsla-20231231.htm \
-H "User-Agent: constantine@aitrailblazer.com" \
-o tsla.pdf

curl \
--request POST http://localhost:3000/forms/chromium/convert/url \
--form url=https://www.sec.gov/Archives/edgar/data/1318605/000162828024002390/tsla-20231231.htm \
--form 'userAgent=Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36' \
--form-string 'extraHttpHeaders={"User-Agent":"your_email@example.com","X-Header":"value","X-Scoped-Header":"value;scope=https?:\\/\\/([a-zA-Z0-9-]+\\.)*domain\\.com\\/.*"}' \
-o tsla.pdf

```

