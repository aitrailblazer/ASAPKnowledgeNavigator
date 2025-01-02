# Use an official Python runtime as the base image
FROM python:3.11-slim

# Set environment variables to avoid interactive prompts during apt-get
ENV DEBIAN_FRONTEND=noninteractive

# Set the working directory inside the container
WORKDIR /app

# Install system dependencies required by WeasyPrint
RUN apt-get update && apt-get install -y --no-install-recommends \
    libpango-1.0-0 \
    libpangoft2-1.0-0 \
    libcairo2 \
    libgdk-pixbuf2.0-0 \
    libffi-dev \
    libjpeg-dev \
    libopenjp2-7-dev \
    zlib1g-dev \
    fonts-liberation \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

# Copy the requirements file to the container
COPY requirements.txt requirements.txt

# Install Python dependencies
RUN pip install --upgrade pip && pip install --no-cache-dir -r requirements.txt

# Copy the application code to the container
COPY . .

# Expose the port that the app runs on
EXPOSE 8000

# Set environment variables for Uvicorn
ENV PORT=8000
ENV HOST=0.0.0.0

# Use non-root user for better security
RUN addgroup --system appgroup && adduser --system --group appuser
USER appuser

# Command to run the app using Uvicorn ASGI server
CMD ["uvicorn", "main:app", "--host", "0.0.0.0", "--port", "8000", "--log-level", "info"]
