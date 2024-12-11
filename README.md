# K8sLogBotRAG: Kubernetes Log Analyzer
<!-- Write an introduction for the project, including its purpose and main features. -->
K8sLogBotRAG is an AI-driven solution designed to streamline log analysis for Azure Kubernetes Service (AKS) clusters. By integrating a C# application with GitHub Actions, it provides automated retrieval and examination of Kubernetes logs. The main features include:

- **Automated Log Collection**: Retrieves logs from your AKS cluster without manual intervention.
- **Intelligent Analysis**: Utilizes AI to detect issues, errors, and anomalies within the logs.
- **GitHub Integration**: Delivers analysis results directly to your repository through comments or issue updates for seamless collaboration.

This tool enhances the monitoring capabilities of developers and DevOps teams, enabling swift identification and resolution of potential problems within Kubernetes deployments.


## Table of Contents

- [Overview](#overview)
- [Prerequisites](#prerequisites)
- [Setup](#setup)
- [Usage](#usage)
  - [Running the Analysis](#running-the-analysis)
- [Reviewing Results](#reviewing-results)
- [Workflow Configuration](#workflow-configuration)
- [Contributing](#contributing)
- [License](#license)

## Overview

K8sLogBotRAG automates Kubernetes log analysis within the GitHub ecosystem. Here's what it does:

- **Fetches Logs**: Connects to your AKS cluster to retrieve pod logs.
- **Analyzes Logs**: Uses AI to detect errors, warnings, or patterns indicative of issues.
- **Reports Findings**: Posts analysis results as comments in GitHub Issues for collaborative review and action.

## Prerequisites

Before using K8sLogBotRAG, ensure you have:

- An Azure Kubernetes Service (AKS) cluster.
- A GitHub repository.
- `kubectl` installed and configured to access your AKS cluster.
- GitHub Actions enabled in your repository.
- Basic understanding of GitHub Actions, Kubernetes, and C#.
- Access to OpenAI or Azure OpenAI services.

## Setup

1. **Clone the Repository:**

   ```bash
   git clone https://github.com/yourusername/K8sLogBotRAG.git
   cd K8sLogBotRAG
Configure GitHub Secrets:

KUBECONFIG: Add your kubeconfig file content as a secret in your GitHub repository settings.

Navigate to Settings > Secrets and variables > Actions in your GitHub repository.
Click New repository secret.
Click Add secret.
ACCESS_CODE_HASH: Compute the SHA256 hash of your access code.

echo -n "your_access_code" | sha256sum
Set the resulting hash as a secret named ACCESS_CODE_HASH.

ENDPOINT, API_KEY, MODEL: Set these for OpenAI service access.

ENDPOINT: Your OpenAI API endpoint.
API_KEY: Your OpenAI API key.
MODEL: The model name (e.g., gpt-4, gpt-3.5-turbo).
Install Dependencies:

Ensure you have the .NET SDK installed. Then, restore the dependencies:

dotnet restore
Build the Application:

dotnet build
Set Environment Variables:

Set the following environment variables in your shell or as GitHub secrets:

export ACCESS_CODE_HASH="your_sha256_hash_of_access_code"
export ENDPOINT="https://<...>.services.ai.azure.com"
export API_KEY="your_api_key"
export MODEL="gpt-4o"
Usage
Running the Analysis
Run the application by providing your access code:

dotnet run -- "your_access_code"

```bash
Access code is valid. Generating GPT-based analysis...
GPT-based analysis generated. Triggering GitHub Action...
GitHub Action Output:

Error when triggering GitHub Action:
To get started with GitHub CLI, please run:  gh auth login
Alternatively, populate the GH_TOKEN environment variable with a GitHub API authentication token.
```
```bash
dotnet run Secret123
Access code is valid. Generating GPT-based analysis...
GPT-based analysis generated. Triggering GitHub Action...
GitHub Action Output:

GitHub Action successfully triggered.
```
Reviewing Results
Once the analysis is complete, review the results posted as comments in the relevant GitHub Issues for further action.

Workflow Configuration
Configure the workflow to suit your needs by modifying the GitHub Actions YAML files in the .github/workflows directory.

Contributing
Contributions are welcome! Please fork the repository and submit a pull request with your changes.