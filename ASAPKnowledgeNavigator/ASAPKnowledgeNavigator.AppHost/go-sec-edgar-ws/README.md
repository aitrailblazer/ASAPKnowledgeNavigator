# go-sec-edgar-ws

## Prerequisites

Before you begin, ensure you have the following installed:

- Docker
- jq

## Building the Docker Image

To build the Docker image for `go-sec-edgar-ws`, run the following command:

```bash
docker build -t go-sec-edgar-ws .
```

## Running the Docker Container

To run the Docker container, use the following command:

```bash
docker run -p 8001:8001 go-sec-edgar-ws
```

## Using the `/html-to-pdf` Endpoint

You can convert HTML to PDF using the `/html-to-pdf` endpoint. Here are some example curl commands:

```bash
curl -X POST http://localhost:8001/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < AAPL_10K.html) \
     -o AAPL_10K.pdf

curl -X POST http://localhost:8001/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < TSLA_10K.html) \
     -o TSLA_10K.pdf


```

## Using the `/` Root Endpoint

You can access the root endpoint to get a simple "Hello" response. Here is an example curl command:

```bash
curl http://localhost:8001/
```
