# Project Details

## GitHubActionTriggerCLI

### Inspiration
The inspiration behind GitHubActionTriggerCLI was to automate the process of identifying and reporting issues in Kubernetes clusters, making it easier for developers and DevOps teams to maintain the health of their applications.

### What it does
GitHubActionTriggerCLI is a C# application designed to analyze Kubernetes pods and create GitHub issues for failing pods. This tool integrates with GitHub Actions to automate the process of identifying and reporting issues in your Kubernetes cluster.

### How we built it
We built GitHubActionTriggerCLI using C# and .NET SDK. The application interacts with Kubernetes clusters using `kubectl` and integrates with GitHub Actions for automation. It also leverages OpenAI or Azure OpenAI services for generating GPT-based analysis.

### Challenges we ran into
One of the main challenges was ensuring the application could accurately identify and describe failing pods. Integrating with GitHub Actions and setting up the necessary secrets for secure access also required careful configuration.

### Accomplishments that we're proud of
We are proud of successfully automating the process of identifying and reporting issues in Kubernetes clusters. The integration with GitHub Actions and the use of GPT-based analysis have significantly improved the efficiency of our DevOps workflows.

### What we learned
We learned a lot about Kubernetes, GitHub Actions, and integrating AI services into our applications. This project also helped us improve our skills in C# and .NET development.

### What's next for RAG-GPT-Insight
We plan to enhance the application's capabilities by adding more advanced analysis features and improving the accuracy of issue identification. We also aim to expand the integration with other DevOps tools and platforms.

## GitHubActionTriggerOnnxRAGCLI

### Inspiration
The inspiration behind GitHubActionTriggerOnnxRAGCLI was to leverage Retrieval-Augmented Generation (RAG) techniques and ONNX models to provide context-aware answers to domain-specific questions, enhancing the log analysis process for AKS clusters.

### What it does
GitHubActionTriggerOnnxRAGCLI is a command-line tool that utilizes Retrieval-Augmented Generation (RAG) techniques and ONNX models to answer questions using domain-specific context. It is designed to integrate with GitHub Actions workflows and Azure Kubernetes Service (AKS) log analysis scenarios.

### How we built it
We built GitHubActionTriggerOnnxRAGCLI using C# and .NET SDK. The application uses ONNX-based generative AI models and embedding models for chat completion and semantic search. It retrieves context from a local vector store of facts before generating answers.

### Challenges we ran into
One of the main challenges was ensuring the application could accurately retrieve and utilize context from the local vector store. Integrating the ONNX models and setting up the necessary environment variables also required careful configuration.

### Accomplishments that we're proud of
We are proud of successfully leveraging RAG techniques and ONNX models to enhance the log analysis process for AKS clusters. The application provides more accurate and context-rich responses, improving the efficiency of our DevOps workflows.

### What we learned
We learned a lot about ONNX models, RAG techniques, and integrating AI services into our applications. This project also helped us improve our skills in C# and .NET development.

### What's next for RAG-GPT-Insight
We plan to enhance the application's capabilities by adding more advanced analysis features and improving the accuracy of context retrieval. We also aim to expand the integration with other DevOps tools and platforms.

## SEC EDGAR Data Project

### Inspiration
The inspiration behind the SEC EDGAR Data Project was to simplify the process of retrieving and processing financial data from the SEC EDGAR RESTful APIs, making it easier for financial analysts and investors to access and analyze this data.

### What it does
The SEC EDGAR Data Project retrieves and processes financial data from the SEC EDGAR RESTful APIs. It includes functionality for:
- Retrieving company CIKs by ticker.
- Fetching filing histories.
- Downloading specific filings.

### How we built it
We built the SEC EDGAR Data Project using Python. The application interacts with the SEC EDGAR RESTful APIs to retrieve and process financial data. It also leverages various Python libraries for data manipulation and analysis.

### Challenges we ran into
One of the main challenges was ensuring the application could accurately retrieve and process the financial data from the SEC EDGAR APIs. Handling the large volume of data and ensuring compliance with the SEC's fair use policy also required careful planning.

### Accomplishments that we're proud of
We are proud of successfully simplifying the process of retrieving and processing financial data from the SEC EDGAR APIs. The application provides financial analysts and investors with easy access to valuable data, improving their ability to make informed decisions.

### What we learned
We learned a lot about the SEC EDGAR APIs, data manipulation, and integrating external APIs into our applications. This project also helped us improve our skills in Python development.

### What's next for RAG-GPT-Insight
We plan to enhance the application's capabilities by adding more advanced data analysis features and improving the accuracy of data retrieval. We also aim to expand the integration with other financial data sources and platforms.

## ASAP SEC-RAG-Navigator

### Inspiration
The inspiration behind ASAP SEC-RAG-Navigator was to revolutionize how professionals interact with SEC EDGAR filings by leveraging Retrieval-Augmented Generation (RAG) to provide deep, actionable insights from complex financial data.

### What it does
ASAP SEC-RAG-Navigator is a cutting-edge SaaS platform that leverages Retrieval-Augmented Generation (RAG) to revolutionize how professionals interact with SEC EDGAR filings. This tool combines AI to provide deep, actionable insights from complex financial data.

### How we built it
We built ASAP SEC-RAG-Navigator using a combination of AI technologies and cloud services. The platform leverages RAG techniques to provide context-aware insights from SEC EDGAR filings. It also integrates with various cloud services to ensure scalability and security.

### Challenges we ran into
One of the main challenges was ensuring the platform could accurately retrieve and analyze the complex financial data from the SEC EDGAR filings. Integrating the various AI technologies and cloud services also required careful planning and configuration.

### Accomplishments that we're proud of
We are proud of successfully leveraging RAG techniques to provide deep, actionable insights from SEC EDGAR filings. The platform significantly improves the efficiency and accuracy of financial data analysis for professionals.

### What we learned
We learned a lot about RAG techniques, AI technologies, and integrating cloud services into our applications. This project also helped us improve our skills in developing scalable and secure SaaS platforms.

### What's next for RAG-GPT-Insight
We plan to enhance the platform's capabilities by adding more advanced analysis features and improving the accuracy of data retrieval. We also aim to expand the integration with other financial data sources and platforms.
