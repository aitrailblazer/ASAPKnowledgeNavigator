# Use Go 1.23 image for the build stage
FROM golang:1.23 as builder

# Set the working directory inside the container
WORKDIR /app

# Copy Go module files and download dependencies
COPY go.mod go.sum ./
RUN go mod download

# Copy the source code and build the application for Linux AMD64
COPY . .
RUN GOARCH=amd64 GOOS=linux go build -o app

# Use a pre-built wkhtmltopdf image
FROM openlabs/docker-wkhtmltopdf-aas:latest

# Set the working directory
WORKDIR /app

# Copy the Go binary from the builder stage
COPY --from=builder /app/app .

# Ensure the binary is executable
RUN chmod +x app

# Expose the application port
EXPOSE 8001

# Run the Go application
ENTRYPOINT ["./app"]
