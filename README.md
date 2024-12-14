# InsightGPT

InsightGPT is an AI-driven solution designed to streamline log analysis for Azure Kubernetes Service (AKS) clusters. By integrating a C# application with GitHub Actions, it provides automated retrieval and intelligent analysis of logs, delivering results directly to your GitHub repository for seamless collaboration. This repository includes multiple tools, each serving a specific purpose to enhance the monitoring capabilities of developers and DevOps teams.

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

### K8sLogBotRAG

K8sLogBotRAG automates Kubernetes log analysis within the GitHub ecosystem. It performs the following tasks:
- **Fetches Logs**: Connects to your AKS cluster to retrieve pod logs.
- **Analyzes Logs**: Uses AI to detect errors, warnings, or patterns indicative of issues.
- **Reports Findings**: Posts analysis results as comments in GitHub Issues for collaborative review and action.

### GitHubActionTriggerCLI

GitHubActionTriggerCLI is a C# application designed to analyze Kubernetes pods and create GitHub issues for failing pods. It integrates with GitHub Actions to automate the process of identifying and reporting issues in your Kubernetes cluster. It performs the following tasks:
- Validates an access code.
- Lists Kubernetes pods and filters out failing ones.
- Describes the failing pods.
- Generates a GPT-based analysis for each failing pod.
- Creates GitHub issues for each failing pod with the analysis results.

### GitHubActionTriggerOnnxRAGCLI

GitHubActionTriggerOnnxRAGCLI is a command-line tool that utilizes Retrieval-Augmented Generation (RAG) techniques and ONNX models to answer questions using domain-specific context. It performs the following tasks:
- Uses ONNX-based generative AI models and embedding models for chat completion and semantic search.
- Retrieves context from a local vector store of facts before generating answers.
- Stores embeddings of factual documents locally for quick lookups.

## Prerequisites

Before using the tools in this repository, ensure you have:
- An Azure Kubernetes Service (AKS) cluster.
- A GitHub repository.
- `kubectl` installed and configured to access your AKS cluster.
- GitHub Actions enabled in your repository.
- .NET 9.0 SDK installed.
- Access to OpenAI or Azure OpenAI services.

## Setup

1. **Clone the Repository:**

```bash
   git clone https://github.com/aitrailblazer/InsightGPT.git
   cd InsightGPT
```

Create Workflow Directory: Ensure you have a .github/workflows directory in your repository.

Create Workflow File: Create a new file in the .github/workflows directory, for example, run-csharp-app.yml.

Define the Workflow: Add the following content to the run-csharp-app.yml file:

```yaml
name: Run C# Application

on:
  push:
    branches:
      - main
  workflow_dispatch:

jobs:
  build-and-run:
    runs-on: ubuntu-latest

    steps:
    - name: Checkout repository
      uses: actions/checkout@v2

    - name: Setup .NET
      uses: actions/setup-dotnet@v2
      with:
        dotnet-version: '9.0.x' # Specify the .NET version you are using

    - name: Restore dependencies
      run: dotnet restore

    - name: Build the application
      run: dotnet build --configuration Release

    - name: Run the application
      env:
        ACCESS_CODE_HASH: ${{ secrets.ACCESS_CODE_HASH }}
        ENDPOINT: ${{ secrets.ENDPOINT }}
        API_KEY: ${{ secrets.API_KEY }}
        MODEL: ${{ secrets.MODEL }}
      run: dotnet run --project ./path/to/your/Project.csproj -- <accessCode>
```

Set Up Secrets: Ensure you have set the required secrets (ACCESS_CODE_HASH, ENDPOINT, API_KEY, MODEL) in your GitHub repository settings under Settings > Secrets and variables > Actions.

## Running the Analysis

The workflow is configured to run automatically on a push to the main branch or can be manually triggered via the GitHub Actions UI.

Automatic Trigger: Push changes to the main branch to automatically trigger the workflow.
Manual Trigger: Go to the "Actions" tab in your GitHub repository, select the workflow, and click the "Run workflow" button.

## Reviewing Results

Once the analysis is complete, review the results posted as comments in the relevant GitHub Issues for further action.

## Workflow Configuration

Configure the workflow to suit your needs by modifying the GitHub Actions YAML files in the .github/workflows directory.