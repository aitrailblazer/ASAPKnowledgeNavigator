# Project details

https://devpost.com/submit-to/22799-accelerate-app-development-with-github-copilot/manage/submissions/578245-RAG-GPT-Insight-kubernetes-log-analyzer/project_details/edit


Project Story

About the project

## Inspiration

The idea for RAG-GPT-Insight was sparked by the growing complexity of managing Kubernetes clusters, particularly within Azure environments. As organizations expand, the volume of logs generated can become daunting. Identifying a need for an automated solution to fetch and analyze these logs while providing actionable insights led to the creation of RAG-GPT-Insight.

## What it does

RAG-GPT-Insight and GitHubActionTriggerOnnxRAGCLI are tools designed to simplify Kubernetes log management and improve question-answering capabilities in Azure settings. RAG-GPT-Insight automates log analysis for AKS clusters, using AI to detect issues and share findings on GitHub for collaborative resolution. Developed with C# and integrated with GitHub Actions, it tackles integration complexities, security concerns, and AI model tuning challenges.

GitHubActionTriggerOnnxRAGCLI is a command-line tool employing RAG techniques and ONNX models to answer questions based on domain-specific context. It includes an ONNX-based chat model (PHI-3) for generating responses, a small embedding model (BGE-MICRO-V2) for semantic search, and stores embeddings locally for quick retrieval. This configuration enables context-aware answers by retrieving relevant facts before generating responses.

Both tools aim to enhance efficiency in managing Kubernetes logs within Azure while delivering accurate insights through advanced AI integrations.

## How we built it

RAG-GPT-Insight was developed using C# and integrates seamlessly with GitHub Actions for deployment within repositories. We utilized OpenAI's services to provide advanced log analysis capabilities. The application leverages C# k8s interface libraries to interact directly with AKS clusters, ensuring secure handling of sensitive information through GitHub Secrets.

## Challenges we ran into

During development, several challenges arose:

- **Integration Complexity**: Ensuring smooth communication between GitHub Actions, Azure services, and our C# application required meticulous configuration.
- **Security Concerns**: Safeguarding sensitive credentials while allowing access for automated processes was critical.
- **AI Model Limitations**: Tuning the AI model with prompts for accurate detection of relevant patterns in logs took considerable effort.

## Accomplishments that we're proud of

We are proud to have created a tool that significantly reduces manual log analysis time. By automating this process, teams can focus more on resolving issues rather than identifying them. Additionally, integrating AI-powered insights has enhanced our ability to preemptively address potential problems before they escalate.

## What we learned

Through this project, we gained valuable insights into:

- **Collaboration Tools**: Effective use of collaborative platforms like GitHub can streamline workflows.
- **Cloud Services Integration**: Navigating cloud service configurations enhances understanding of modern DevOps practices.
- **User Feedback Importance**: Engaging early users helped refine features based on real-world needs.

## What's next for RAG-GPT-Insight: Kubernetes Log Analyzer

Looking ahead, we plan to enhance RAG-GPT-Insight by:

1. Expanding support for additional cloud providers beyond Azure.
2. Integrating more sophisticated machine learning models for deeper log insights.
3. Developing a user-friendly dashboard interface for visualizing log data trends over time.
4. Encouraging community contributions to foster continuous improvement through feedback and feature requests.

By pursuing these initiatives, we aim to make RAG-GPT-Insight an indispensable tool in the Kubernetes ecosystem!