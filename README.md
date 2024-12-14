# K8sLogBotRAG: Kubernetes Log Analyzer

Copyright© 2024 AITrailblazer, LLC. All rights reserved

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

Integrating Azure Databricks into the K8sLogBotRAG framework can significantly enhance the analysis of Kubernetes logs by leveraging scalable data processing and advanced machine learning capabilities. Here's a structured approach to achieve this integration:

1. Data Ingestion and Preparation:

Data Collection: Aggregate historical Kubernetes log data from Azure Kubernetes Service (AKS) clusters.
Data Storage: Store the collected logs in a centralized repository, such as Azure Data Lake Storage, to facilitate efficient access and processing.
Data Cleaning and Transformation: Utilize Azure Databricks to clean and preprocess the log data, ensuring it's structured appropriately for analysis.
2. Feature Engineering:

Feature Extraction: Identify and extract relevant features from the log data that can aid in detecting anomalies or patterns indicative of potential issues.
Feature Scaling: Apply scaling techniques to normalize the features, improving the performance of machine learning models.
3. Model Development:

Model Training: Use Azure Databricks to train machine learning models on the prepared log data, focusing on identifying complex patterns and predicting potential issues.
Experiment Tracking: Leverage MLflow, integrated within Azure Databricks, to track experiments, model parameters, and performance metrics.
Model Evaluation: Assess the trained models using appropriate evaluation metrics to ensure their effectiveness in real-world scenarios.
4. Model Deployment:

Model Packaging: Package the trained models using MLflow for deployment.
Containerization: Deploy the packaged models as RESTful APIs on Azure Kubernetes Service (AKS) for real-time inference.
API Management: Utilize Azure API Management to expose the model endpoints securely and manage access.
5. Automated Model Retraining Pipelines:

Data Monitoring: Continuously monitor incoming log data to detect shifts or drifts in patterns that may affect model accuracy.
Scheduled Retraining: Set up automated pipelines in Azure Databricks to periodically retrain models using the latest log data, ensuring they adapt to new patterns or anomalies.
CI/CD Integration: Implement Continuous Integration/Continuous Deployment (CI/CD) practices using tools like Azure DevOps or GitHub Actions to automate the deployment of retrained models, minimizing downtime and manual intervention.
6. Implementing MLOps Practices:

Version Control: Use version control systems to track changes in data, model parameters, and code, facilitating reproducibility and collaboration.
Monitoring and Logging: Establish robust monitoring to track model performance in production, and implement logging mechanisms to capture any issues or anomalies.
Feedback Loops: Create mechanisms for stakeholders to provide feedback on model outputs, enabling continuous improvement and alignment with business objectives.
By following this approach, K8sLogBotRAG can leverage Azure Databricks to enhance its log analysis capabilities, ensuring it remains effective and responsive to evolving patterns within Kubernetes environments.

Native-AOT (Ahead-of-Time) is a feature in .NET that allows for the compilation of .NET applications 
directly to native code, bypassing the need for a Just-In-Time (JIT) compiler at runtime. 
This can result in faster startup times and reduced memory usage.

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
