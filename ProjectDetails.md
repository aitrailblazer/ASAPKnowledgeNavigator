### Inspiration  
Retrieval-Augmented Generation (RAG) demonstrates its versatility by addressing two key challenges with Azure Cosmos NoSQL DiskANN technology:

Kubernetes Pod Failure Detection:

The GitHubActionTriggerCLI tool analyzes failing pod logs in Azure environments using GPT-based models. By leveraging DiskANN for semantic search, it identifies root causes, generates diagnostics, and creates GitHub issues automatically—streamlining resolution without manual intervention.

Insights from SEC EDGAR Filings:

The ASAP SEC-RAG-Navigator processes financial data via SEC APIs using RAG techniques combined with DiskANN's precision. It transforms complex regulatory documents into concise insights while ensuring privacy and compliance.
Azure Cosmos NoSQL DiskANN plays a pivotal role in both cases by enabling efficient semantic search and precise information retrieval, enhancing decision-making across technical and regulatory domains.

### What it does  
RAG-GPT-Insight applies RAG’s flexibility to solve two major challenges:  

1. **Automating Kubernetes Pod Failure Detection**:  
   - Utilizing the GitHubActionTriggerCLI tool, failing Kubernetes pod logs in Azure environments are analyzed using GPT-based models. This process pinpoints root causes, generates detailed diagnostics, and automatically creates GitHub issues for each failure—enabling teams to resolve problems swiftly without manual log analysis.

2. **Extracting Actionable Insights from Complex SEC EDGAR Filings**:  
   - The ASAP SEC-RAG-Navigator employs RAG techniques with Azure Cosmos NoSQL DiskANN technology to process financial data retrieved via SEC EDGAR APIs. It transforms intricate regulatory filings into concise, actionable insights while ensuring privacy and compliance, empowering professionals to make informed decisions effortlessly.

### How we built it  
- **GitHubActionTriggerCLI**: Developed in C#, this tool integrates seamlessly with GitHub Actions for deployment within repositories. It uses OpenAI services for log analysis and leverages C# k8s libraries to securely interact with AKS clusters via GitHub Secrets.

- **ASAP SEC-RAG-Navigator**: Built on .NET frameworks alongside Azure services like Cosmos NoSQL DiskANN, this tool applies RAG techniques for large-scale financial data analysis while maintaining robust security measures.
- Both tools incorporate ONNX Runtime models for optimized execution speed and utilize semantic search capabilities powered by DiskANN for precise information retrieval.

#### Technologies

##### .NET 9.0 SDK  
The .NET 9.0 SDK provides the runtime, libraries, and tools for building applications on the .NET platform using languages like C#, F#, and Visual Basic.

##### Azure Kubernetes Service (AKS)  
AKS simplifies Kubernetes cluster management, enabling developers to focus on scaling applications without infrastructure overhead.

##### Azure.AI.OpenAI  
This service integrates OpenAI's language models into applications via Azure's secure infrastructure.

##### KubernetesClient  
A .NET library that programmatically manages Kubernetes resources like pods and deployments.

##### Microsoft.SemanticKernel  
Provides APIs for semantic search and natural language processing using advanced AI models.

##### Microsoft.ML.OnnxRuntime & OnnxRuntimeGenAI  
ONNX Runtime is a high-performance engine for running machine learning models across platforms. The GenAI extension optimizes generative AI tasks such as text or image creation.

##### Microsoft.SemanticKernel.Connectors.Onnx  
Integrates ONNX models with Semantic Kernel to enhance semantic search and NLP capabilities.

##### Azure.AI.Inference  
Enables scalable AI inference tasks on Azure using various frameworks and models.

##### Azure.Identity  
Simplifies authentication for accessing Azure services through managed identities or client secrets.

##### Microsoft.Azure.Cosmos  
A .NET SDK designed to interact with Cosmos DB—a globally distributed database supporting multiple data models including document-based structures ideal for RAG workflows.  

These technologies power **RAG-GPT-Insight**, automating technical workflows (e.g., diagnosing Kubernetes pod failures) while simplifying regulatory data analysis from SEC filings through Retrieval-Augmented Generation techniques.

### Challenges we ran into  
Developing RAG-GPT-Insight involved overcoming several obstacles:
- **Integration Complexity**: Ensuring smooth communication between GitHub Actions workflows, Azure services, and our applications required careful orchestration.
- **Security Concerns**: Protecting sensitive credentials during automated operations was paramount.
- **AI Model Tuning**: Crafting prompts capable of accurately detecting patterns in both Kubernetes logs and complex financial filings demanded significant iteration.

### Accomplishments that we're proud of  
We successfully created tools that automate labor-intensive tasks such as diagnosing Kubernetes pod failures and deriving meaningful insights from regulatory documents. These innovations reduce manual effort while enhancing decision-making through AI-driven precision. By applying cutting-edge Retrieval-Augmented Generation across diverse scenarios, we’ve demonstrated its transformative potential in real-world applications.

### What we learned  
This project offered several valuable lessons:
1. Cloud platforms like Azure provide essential scalability for handling vast datasets efficiently.
2. Effective integration of collaborative tools like GitHub optimizes DevOps workflows significantly.
3. Early feedback from users is crucial in refining features based on practical needs.

### What's next for RAG-GPT-Insight  
Our roadmap includes:
1. Expanding support beyond Azure by incorporating compatibility with other cloud providers such as AWS or GCP.
2. Integrating advanced machine learning models to deliver deeper log analyses and richer insights from financial data.
3. Building an intuitive dashboard interface to visualize trends across both Kubernetes logs and SEC filing analyses over time.
4. Encouraging open-source contributions by fostering a community-driven approach to feature development.

By focusing on these advancements, we aim to establish RAG-GPT-Insight as an indispensable solution for automating technical workflows while navigating complex regulatory landscapes effectively through the power of Retrieval-Augmented Generation!


