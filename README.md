# RAG-GPT-Insight

## Inspiration

RAG-GPT-Insight emerged as an exploratory venture to harness the capabilities of Retrieval-Augmented Generation (RAG) for uncovering use cases across diverse industries. By integrating RAG with Azure's robust infrastructure, this project demonstrates the potential of AI to simplify and transform complex tasks like Kubernetes diagnostics and SEC EDGAR filings analysis into actionable insights, showcasing the versatility and utility of RAG in enhancing operational efficiency and strategic decision-making.

---

## What it does

### 1. **Kubernetes Pod Failure Detection**
   - **Tool**: **GitHubActionTriggerCLI**
   - **Functionality**: Integrates Azure OpenAI's GPT models with Azure Cosmos NoSQL DiskANN for semantic log analysis, identifying Kubernetes pod failures and automating GitHub issue creation.
   - **Outcome**: Reduces manual intervention, enhances system reliability, and accelerates IT operations.

### 2. **SEC EDGAR Filings Analysis**
   - **Tool**: **ASAP SEC-RAG-Navigator**
   - **Functionality**: Uses RAG to analyze SEC filings, extracting precise financial insights with Azure Cosmos NoSQL DiskANN's semantic capabilities.
   - **Outcome**: Converts regulatory data into actionable insights, supporting compliance, investment analysis, and strategic planning.

---

## Challenges we ran into
1. **Integration Complexity**: Harmonizing diverse tech stacks for seamless performance.
2. **Security and Compliance**: Ensuring data protection while adhering to regulatory standards.
3. **Model Adaptation**: Tuning AI models to interpret both technical logs and financial texts.

---

## What we learned
1. **Azure's Versatility**: Azure's infrastructure supports diverse applications effectively.
2. **RAG's Power**: Demonstrated RAG's ability to extract meaningful insights from unstructured data.
3. **Development Acceleration**: GitHub Copilot streamlined development cycles and model refinement.
4. **Iterative Feedback**: Highlighted the importance of continuous feedback for performance improvement.

---

## What's next for RAG-GPT-Insight
1. **Industry Expansion**: Explore applications in healthcare, education, and legal sectors.
2. **Enhanced User Experience**: Build intuitive dashboards for actionable insights.
3. **Community Collaboration**: Foster open-source contributions for innovation and scalability.
4. **Model Refinement**: Address edge cases and domain-specific challenges for higher accuracy.

---

## Strategic Validation of RAG-GPT-Insight

### **Core Drivers Validation**

#### Technical Diagnostics (Kubernetes):
- **Driver**: Automating log analysis and issue resolution.
- **Validation**: Integration of Azure OpenAI and DiskANN demonstrates measurable efficiency gains, reducing manual intervention.
- **Risk**: Over-reliance on semantic search accuracy; mitigated by continuous model retraining.

#### Financial Analytics (SEC Filings):
- **Driver**: Simplifying regulatory complexity into actionable insights.
- **Validation**: Proven utility in extracting structured data from unstructured filings, aligning with compliance needs.
- **Risk**: Potential gaps in domain-specific nuance; mitigated by expert-in-the-loop validation.

---

### **Feedback Loop Stress Testing**

#### Positive Feedback Loops:
- **Kubernetes**: Diagnostics improve as more failure patterns are logged and analyzed.
- **SEC Filings**: Iterative learning enhances precision in extracting key financial metrics.

#### Stability Under Varying Conditions:
- **Kubernetes**: Tested across diverse pod configurations; feedback loop remains robust.
- **SEC Filings**: Effective across different filing types (10-K, 8-K), though edge cases (e.g., atypical filings) require further tuning.

---

### **Cross-Domain Verification**
- **IT to Finance**: Demonstrates RAG's versatility in transforming unstructured data into structured insights.
- **Finance to Healthcare**: Preliminary tests show promise in analyzing medical records, though privacy and regulatory challenges remain.

---

### **Probability and Uncertainty Assessment**
- **Bayesian Updates**: Models dynamically recalibrate predictions with new data, balancing historical baselines.
- **Confidence Intervals**: Predictions (e.g., failure likelihood, financial risks) align with empirical outcomes, ensuring robustness.

---

### **Purpose Alignment and Integrity**
- **Long-Term Clarity**: Outputs prioritize actionable insights over short-term outputs, aligning with strategic objectives.
- **Truth-Seeking**: Transparent reasoning processes and reproducible results foster trust in AI-driven decisions.

---

## Technologies

### **.NET 9.0 SDK**
A development kit for building cross-platform applications, supporting multiple languages like C#, F#, and Visual Basic.

### **Azure Kubernetes Service (AKS)**
A managed Kubernetes service simplifying deployment, management, and scaling of Kubernetes clusters.

### **Azure.AI.OpenAI**
Provides access to OpenAI's advanced language models within Azure's secure infrastructure.

### **KubernetesClient**
A .NET library for programmatically managing Kubernetes resources, such as pods and deployments.

### **Microsoft.SemanticKernel**
A library for building AI applications with natural language processing and semantic search capabilities.

### **Microsoft.ML.OnnxRuntime**
A high-performance engine for running ONNX models across platforms, supporting diverse machine learning frameworks.

### **Microsoft.ML.OnnxRuntimeGenAI**
Enhances ONNX Runtime with generative AI capabilities for text, image, and content generation.

### **Microsoft.SemanticKernel.Connectors.Onnx**
Integrates ONNX models with Semantic Kernel, enabling advanced semantic search and natural language processing.

### **Azure.AI.Inference**
Scalable AI inference solutions using Azure's infrastructure for deploying and running models.

### **Azure.Identity**
Simplifies authentication for Azure services, supporting managed identities and secure access.

### **Microsoft.Azure.Cosmos**
A multi-model database service with global distribution, supporting document, key-value, graph, and column-family data models.

---

## Strategic Outlook
RAG-GPT-Insight is a validated, scalable framework with proven cross-domain applicability. By addressing identified risks, enhancing model performance, and expanding into new sectors, it is poised to become a transformative tool for solving complex, data-intensive challenges across industries.


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


## Detailed Project Descriptions

For detailed descriptions of each project, please refer to the [ProjectDetails.md](ProjectDetails.md) file.
