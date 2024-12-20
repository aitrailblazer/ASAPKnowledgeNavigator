# RAG-GPT-Insight

RAG-GPT-Insight is an AI-driven solution designed to streamline log analysis for Azure Kubernetes Service (AKS) clusters. By integrating a C# application with GitHub Actions, it provides automated retrieval and intelligent analysis of logs, delivering results directly to your GitHub repository for seamless collaboration. This repository includes multiple tools, each serving a specific purpose to enhance the monitoring capabilities of developers and DevOps teams.

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

Project Tree
<!--
This project directory structure represents a .NET application with various components organized into folders. 

- `Models`: Contains the `Embedding.cs` file, which likely defines data models for embeddings.
- `ModelsCopilotCosmosDB`: Contains `KnowledgeBaseItem.cs` and `UserParameters.cs`, which are models related to Cosmos DB.
- `OptionsCosmosDB`: Contains configuration files for Cosmos DB, OpenAI, and Semantic Kernel.
- `Program.cs`: The main entry point of the application.
- `RAG`: Contains data loading services, options configurations, and other related services for the RAG (Retrieval-Augmented Generation) functionality.
- `SEC-RAG-Navigator-db`: Contains the `RAGChatService.cs` file, which is likely a service for chat functionalities.
- `SEC-RAG-Navigator-db.csproj`: The project file for the .NET application.
- `ServicesCosmosDB`: Contains services for chat, Cosmos DB, and Semantic Kernel.
- `bin` and `obj`: Directories for compiled binaries and object files.
- `tsla-20231231.htm.html.pdf`: A PDF file, possibly containing documentation or reports.

Each folder and file serves a specific purpose in the overall architecture of the application, contributing to its functionality and organization.
-->

```bash
.
├── Models
│   └── Embedding.cs
├── ModelsCopilotCosmosDB
│   ├── KnowledgeBaseItem.cs
│   └── UserParameters.cs
├── OptionsCosmosDB
│   ├── CosmosDb.cs
│   ├── OpenAi.cs
│   └── SemanticKernel.cs
├── Program.cs
├── RAG
│   ├── DataLoader.cs
│   ├── IDataLoader.cs
│   ├── OptionsRAG
│   │   ├── ApplicationConfig.cs
│   │   ├── AzureCosmosDBConfig.cs
│   │   ├── AzureOpenAIConfig.cs
│   │   ├── AzureOpenAIEmbeddingsConfig.cs
│   │   └── RagConfig.cs
│   ├── RAGChatService-cs
│   ├── TextSnippet.cs
│   └── UniqueKeyGenerator.cs
├── [README.md](http://_vscodecontentref_/1)
├── SEC-RAG-Navigator-db
│   └── RAG
│       └── RAGChatService.cs
├── SEC-RAG-Navigator-db.csproj
├── ServicesCosmosDB
│   ├── ChatService.cs
│   ├── CosmosDbService.cs
│   └── SemanticKernelService.cs
├── bin
│   └── Debug
│       └── net9.0
├── obj
│   ├── Debug
│   │   └── net9.0
│   ├── SEC-RAG-Navigator-db.csproj.nuget.dgspec.json
│   ├── SEC-RAG-Navigator-db.csproj.nuget.g.props
│   ├── SEC-RAG-Navigator-db.csproj.nuget.g.targets
│   ├── project.assets.json
│   └── project.nuget.cache
└── tsla-20231231.htm.html.pdf

```


## Overview

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


## Generating a Native-AOT Application for `osx-arm64`

To generate a Native-AOT application for `osx-arm64`, follow these steps:

1. **Publish the Application:**

   ```bash
   dotnet publish -r osx-arm64 --self-contained -c Release
   ```

2. **Verify the Output:**

   After publishing, verify that the output directory contains the necessary files for the `osx-arm64` runtime.

3. **Run the Application:**

   Navigate to the output directory and run the application:

   ```bash
   ./YourApplicationName
   ```

This will generate a self-contained, Native-AOT application for the `osx-arm64` runtime.
