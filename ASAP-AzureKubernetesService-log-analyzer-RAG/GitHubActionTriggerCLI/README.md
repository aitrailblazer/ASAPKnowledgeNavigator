# GitHubActionTriggerCLI

GitHubActionTriggerCLI is a powerful C# application that streamlines Kubernetes pod failure detection and reporting. Designed to enhance operational efficiency, this tool integrates seamlessly with GitHub Actions to automate the analysis of Kubernetes clusters and the creation of GitHub issues for failing pods.

The application leverages Azure OpenAI's GPT models for advanced semantic log analysis, enabling it to pinpoint the root causes of Kubernetes pod failures with precision. It also incorporates Azure Cosmos DB NoSQL with DiskANN for efficient vector-based indexing and retrieval of log data, ensuring rapid and reliable insights into complex system issues.

By automating the identification of failures and generating actionable GitHub issues, GitHubActionTriggerCLI empowers DevOps teams to proactively address problems, minimize downtime, and maintain system reliability.

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

GitHubActionTriggerCLI performs the following tasks:

1. Validates an access code.
2. Lists Kubernetes pods and filters out failing ones.
3. Describes the failing pods.
4. Generates a Azure OpenAI GPT-4o based analysis for each failing pod.
5. Creates GitHub issues for each failing pod with the analysis results.

## Prerequisites

Before using GitHubActionTriggerCLI, ensure you have:

- An Azure Kubernetes Service (AKS) cluster.
- A GitHub repository.
- `kubectl` installed and configured to access your AKS cluster.
- GitHub Actions enabled in your repository.
- .NET SDK installed.
- Access to OpenAI or Azure OpenAI services.

## Setup

1. **Clone the Repository:**

   ```bash
   git clone https://github.com/yourusername/GitHubActionTriggerCLI.git
   cd GitHubActionTriggerCLI
   ```

2. **Create Workflow Directory**: Ensure you have a `.github/workflows` directory in your repository.

3. **Create Workflow File**: Create a new file in the `.github/workflows` directory, for example, `run-csharp-app.yml`.

4. **Define the Workflow**: Add the following content to the `run-csharp-app.yml` file:

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

5. **Set Up Secrets**: Ensure you have set the required secrets (`ACCESS_CODE_HASH`, `ENDPOINT`, `API_KEY`, `MODEL`) in your GitHub repository settings under `Settings > Secrets and variables > Actions`.

## Running the Workflow

The workflow is configured to run automatically on a push to the `main` branch or can be manually triggered via the GitHub Actions UI.

- **Automatic Trigger**: Push changes to the `main` branch to automatically trigger the workflow.
- **Manual Trigger**: Go to the "Actions" tab in your GitHub repository, select the workflow, and click the "Run workflow" button.

## Example Command


The app assumes your AKS context is AIT:

```sh
kubectl config use-context AIT
```

To run the application manually, you can use the following command:

```sh
dotnet run --project ./path/to/your/Project.csproj -- <accessCode>
```
dotnet run Secret123