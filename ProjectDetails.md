# Project Details

## Inspiration

The idea for RAG-GPT-Insight was sparked by the growing complexity of managing Kubernetes clusters, particularly within Azure environments. As organizations expand, the volume of logs generated can become daunting. Identifying a need for an automated solution to fetch and analyze these logs while providing actionable insights led to the creation of RAG-GPT-Insight.

## What it does

RAG-GPT-Insight includes multiple tools designed to simplify Kubernetes log management and improve question-answering capabilities in Azure settings:

### GitHubActionTriggerCLI

GitHubActionTriggerCLI is a C# application designed to analyze Kubernetes pods and create GitHub issues for failing pods. This tool integrates with GitHub Actions to automate the process of identifying and reporting issues in your Kubernetes cluster. It performs the following tasks:
- Validates an access code.
- Lists Kubernetes pods and filters out failing ones.
- Describes the failing pods.
- Generates a GPT-based analysis for each failing pod.
- Creates GitHub issues for each failing pod with the analysis results.

### GitHubActionTriggerOnnxRAGCLI

GitHubActionTriggerOnnxRAGCLI is a command-line tool employing RAG techniques and ONNX models to answer questions based on domain-specific context. It includes an ONNX-based chat model (PHI-3) for generating responses, a small embedding model (BGE-MICRO-V2) for semantic search, and stores embeddings locally for quick retrieval. This configuration enables context-aware answers by retrieving relevant facts before generating responses.

### SEC EDGAR Data Project

The SEC EDGAR Data Project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

### ASAP SEC-RAG-Navigator

ASAP SEC-RAG-Navigator is a cutting-edge SaaS platform that leverages Retrieval-Augmented Generation (RAG) to revolutionize how professionals interact with SEC EDGAR filings. This tool combines AI to provide deep, actionable insights from complex financial data.

## How we built it

### GitHubActionTriggerCLI

GitHubActionTriggerCLI was developed using C# and integrates seamlessly with GitHub Actions for deployment within repositories. We utilized OpenAI's services to provide advanced log analysis capabilities. The application leverages C# k8s interface libraries to interact directly with AKS clusters, ensuring secure handling of sensitive information through GitHub Secrets.

### GitHubActionTriggerOnnxRAGCLI

GitHubActionTriggerOnnxRAGCLI was built using .NET 9.0 SDK and ONNX Runtime for executing models. It integrates with GitHub Actions workflows and Azure Kubernetes Service (AKS) log analysis scenarios. The tool uses ONNX-based generative AI models and embedding models for chat completion and semantic search.

### SEC EDGAR Data Project

The SEC EDGAR Data Project was built using Python and leverages the SEC EDGAR RESTful APIs to retrieve and process financial data. It includes functionality for retrieving company CIKs by ticker, fetching filing histories, and downloading specific filings.

### ASAP SEC-RAG-Navigator

ASAP SEC-RAG-Navigator was developed using a combination of .NET, Azure services, and AI models. It leverages Retrieval-Augmented Generation (RAG) techniques to provide deep, actionable insights from SEC EDGAR filings. The platform ensures data privacy and regulatory compliance while offering scalable architecture to handle growing data demands efficiently.

## Challenges we ran into

During development, several challenges arose:

- **Integration Complexity**: Ensuring smooth communication between GitHub Actions, Azure services, and our applications required meticulous configuration.
- **Security Concerns**: Safeguarding sensitive credentials while allowing access for automated processes was critical.
- **AI Model Limitations**: Tuning the AI models with prompts for accurate detection of relevant patterns in logs and financial data took considerable effort.

## Accomplishments that we're proud of

We are proud to have created tools that significantly reduce manual log analysis time and enhance financial data analysis. By automating these processes, teams can focus more on resolving issues and making informed decisions rather than identifying problems. Additionally, integrating AI-powered insights has enhanced our ability to preemptively address potential problems before they escalate.

## What we learned

Through this project, we gained valuable insights into:

- **Collaboration Tools**: Effective use of collaborative platforms like GitHub can streamline workflows.
- **Cloud Services Integration**: Navigating cloud service configurations enhances understanding of modern DevOps practices.
- **User Feedback Importance**: Engaging early users helped refine features based on real-world needs.

## What's next for RAG-GPT-Insight

Looking ahead, we plan to enhance RAG-GPT-Insight by:

1. Expanding support for additional cloud providers beyond Azure.
2. Integrating more sophisticated machine learning models for deeper log and financial data insights.
3. Developing a user-friendly dashboard interface for visualizing log and financial data trends over time.
4. Encouraging community contributions to foster continuous improvement through feedback and feature requests.

By pursuing these initiatives, we aim to make RAG-GPT-Insight an indispensable tool in the Kubernetes and financial data analysis ecosystems!
