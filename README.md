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

## Project Tree

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
├── README.md
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

### SEC EDGAR Data Project

The SEC EDGAR Data Project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

### ASAP SEC-RAG-Navigator

ASAP SEC-RAG-Navigator is a cutting-edge SaaS platform that leverages Retrieval-Augmented Generation (RAG) to revolutionize how professionals interact with SEC EDGAR filings. This tool combines AI to provide deep, actionable insights from complex financial data. It offers:
- Instant SEC Data Access
- RAG-Powered Insights
- Natural Language Processing
- Secure and Compliant
- Scalable Architecture

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

## Technologies

### .NET 9.0 SDK
The .NET 9.0 SDK is a software development kit for building and running applications on the .NET platform. It includes the runtime, libraries, and tools needed to develop .NET applications. The SDK supports multiple programming languages, including C#, F#, and Visual Basic.

### OpenAI
OpenAI is an artificial intelligence research organization that provides advanced AI models and services. OpenAI's models, such as GPT-3, are used for natural language processing tasks, including text generation, translation, summarization, and more.

### Azure Kubernetes Service (AKS)
Azure Kubernetes Service (AKS) is a managed Kubernetes service provided by Microsoft Azure. It simplifies the deployment, management, and operations of Kubernetes clusters, allowing developers to focus on building and scaling applications without managing the underlying infrastructure.

### Azure.AI.OpenAI
Azure.AI.OpenAI is an Azure service that provides access to OpenAI's powerful language models through the Azure platform. It enables developers to integrate OpenAI's capabilities into their applications using Azure's infrastructure and security features.

### KubernetesClient
KubernetesClient is a .NET client library for interacting with Kubernetes clusters. It provides APIs for managing Kubernetes resources, such as pods, services, deployments, and more, allowing developers to automate and manage Kubernetes operations programmatically.

### Microsoft.SemanticKernel
Microsoft.SemanticKernel is a library that provides tools and APIs for building and deploying semantic search and natural language processing applications. It leverages advanced AI models to understand and process natural language queries, enabling developers to create intelligent search and analysis solutions.

### Microsoft.ML.OnnxRuntime
Microsoft.ML.OnnxRuntime is a cross-platform, high-performance scoring engine for Open Neural Network Exchange (ONNX) models. It allows developers to run machine learning models trained in various frameworks (such as PyTorch, TensorFlow, and scikit-learn) on different platforms, including Windows, Linux, and macOS.

### Microsoft.ML.OnnxRuntimeGenAI
Microsoft.ML.OnnxRuntimeGenAI is an extension of the ONNX Runtime that focuses on generative AI models. It provides tools and APIs for running and optimizing generative models, enabling developers to build applications that generate text, images, and other content.

### Microsoft.SemanticKernel.Connectors.Onnx
Microsoft.SemanticKernel.Connectors.Onnx is a library that integrates ONNX models with the Microsoft Semantic Kernel. It allows developers to use ONNX models for semantic search and natural language processing tasks, leveraging the capabilities of both ONNX Runtime and the Semantic Kernel.

### Azure.AI.Inference
Azure.AI.Inference is an Azure service that provides APIs for running AI inference tasks on Azure. It supports various AI models and frameworks, enabling developers to deploy and run AI models at scale using Azure's infrastructure.

### Azure.Identity
Azure.Identity is a library that provides authentication and authorization capabilities for Azure services. It simplifies the process of obtaining and managing access tokens for Azure resources, supporting various authentication methods, including managed identities, client secrets, and interactive login.

### Microsoft.Azure.Cosmos
Microsoft.Azure.Cosmos is a .NET SDK for interacting with Azure Cosmos DB, a globally distributed, multi-model database service. The SDK provides APIs for managing and querying data in Cosmos DB, supporting various data models, including document, key-value, graph, and column-family.

These technologies collectively enable the development of advanced AI-driven applications, leveraging the power of cloud services, machine learning models, and distributed computing.

## Detailed Project Descriptions

For detailed descriptions of each project, please refer to the [ProjectDetails.md](ProjectDetails.md) file.
