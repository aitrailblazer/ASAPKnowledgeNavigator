
docker image prune -a --force
docker network prune --force

docker build -t go-sec-edgar-ws .
docker run -p 8000:8000 go-sec-edgar-ws


curl -X POST http://localhost:8000/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < AAPL_10K.html) \
     -o AAPL_10K.pdf

curl -X POST http://localhost:8000/html-to-pdf \
     -H "Content-Type: application/json" \
     -d @<(jq -Rs '{html: .}' < TSLA_10K.html) \
     -o TSLA_10K.pdf
