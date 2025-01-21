ARG GOLANG_VERSION
ARG GOTENBERG_VERSION

FROM golang:1.23 AS builder

ENV CGO_ENABLED=0

# Define the working directory outside of $GOPATH (we're using go modules).
WORKDIR /home

# Install module dependencies.
COPY go.mod go.sum ./

RUN go mod download &&\
    go mod verify

# Copy the source code.
COPY cmd ./cmd
COPY pkg ./pkg

# Build the binary.
ARG GOTENBERG_VERSION
ARG APP_NAME
ARG APP_VERSION

RUN go build -o gotenberg -ldflags "-X 'github.com/gotenberg/gotenberg/v8/cmd.Version=8.15.3-ait-snapshot'" cmd/app/main.go

FROM gotenberg/gotenberg:8.15.3

ARG GOTENBERG_VERSION
ARG APP_AUTHOR
ARG APP_REPOSITORY
ARG APP_NAME
ARG APP_VERSION

LABEL org.opencontainers.image.title="Gotenberg ait" \
      org.opencontainers.image.description="A Docker-powered stateless API for PDF files." \
      org.opencontainers.image.version="8.15.3-ait-snapshot" \
      org.opencontainers.image.authors="app-aitrailblazer" \
      org.opencontainers.image.source="app-aitrailblazer"

USER root

# TODO: add your instructions.

# Copy the Gotenberg binary from the builder stage.
COPY --from=builder /home/gotenberg /usr/bin/

USER gotenberg