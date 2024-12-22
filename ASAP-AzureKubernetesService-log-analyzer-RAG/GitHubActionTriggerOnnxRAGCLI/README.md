# GitHubActionTriggerOnnxRAGCLI

**GitHubActionTriggerOnnxRAGCLI** is a command-line tool that utilizes Retrieval-Augmented Generation (RAG) techniques and ONNX models to answer questions using domain-specific context. It is designed to integrate with GitHub Actions workflows and Azure Kubernetes Service (AKS) log analysis scenarios.

## Features

- **ONNX-based Chat Model (Phi-3)**: Uses an ONNX-based generative AI model (`PHI-3`) for chat completion.
- **Embedding Model (BGE-MICRO-V2)**: Leverages a small embedding model (`BGE-MICRO-V2`) for semantic search.
- **Retrieval-Augmented Generation (RAG)**: Retrieves context from a local vector store of facts before generating answers.
- **In-Memory Vector Store**: Stores embeddings of factual documents (`.txt` files) locally for quick lookups.
- **Context-Aware Answers**: The assistant can produce more accurate and context-rich responses based on the provided facts.Generates a Microsoft Phi-3 local model-based analysis

## Prerequisites

1. **.NET 9.0 SDK**:  
   Ensure you have .NET 9.0 installed.  
   [Download .NET 9.0](https://dotnet.microsoft.com/en-us/download/dotnet/9.0)

2. **ONNX Runtime**:  
   The application uses ONNX runtime for executing the models.  
   [ONNX Runtime Documentation](https://onnxruntime.ai/)

3. **Models & Vocab Files**:  
Models and Vocabulary Files:
Obtain the following:


Phi-3.5 Mini Model (ONNX): A lightweight, state-of-the-art open language model developed by Microsoft, trained on high-quality, reasoning-dense data, suitable for tasks such as text generation and conversational AI. The model is available in different configurations, including a "mini" version with 3.8 billion parameters and context lengths of 4K and 128K tokens. 

[Phi-3.5 Mini](https://huggingface.co/microsoft/Phi-3.5-mini-instruct-onnx)

BGE-MICRO-V2 Model (ONNX): A compact embedding model designed for generating dense vector representations of text, useful for tasks like clustering and semantic search. Due to its small size, BGE-MICRO-V2 is well-suited for deployment on devices with limited resources, offering fast inference times with a slight trade-off in accuracy compared to larger models. 

BGE-MICRO-V2 Vocabulary File: The corresponding vocabulary file for the BGE-MICRO-V2 model.

[BGE-MICRO-V2](https://huggingface.co/TaylorAI/bge-micro-v2?utm_source=chatgpt.com)

ONNX (Open Neural Network Exchange) is an open-source format designed to facilitate the interoperability of AI models across various frameworks and tools. It enables developers to train models in one framework and deploy them in another, streamlining the AI development process.

PHI-3 is a lightweight, state-of-the-art open language model developed by Microsoft. It has been trained on high-quality, reasoning-dense data, making it suitable for tasks such as text generation and conversational AI. The model is available in different configurations, including a "mini" version with 3.3 billion parameters and context lengths of 4K and 128K tokens. 
HUGGING FACE

BGE-MICRO-V2 is a compact embedding model designed for generating dense vector representations of text. These embeddings are useful for tasks like clustering and semantic search. Due to its small size, BGE-MICRO-V2 is well-suited for deployment on devices with limited resources, offering fast inference times with a slight trade-off in accuracy compared to larger models. 
HUGGING FACE

In the context of the GitHubActionTriggerOnnxRAGCLI project, PHI-3 serves as the generative AI model for producing responses, while BGE-MICRO-V2 is utilized for embedding generation to support retrieval-augmented generation (RAG) techniques. Both models are executed using ONNX Runtime, ensuring efficient performance across diverse platforms.

## Setup

1. **Environment Variables**:  
   Set the environment variables for your ONNX models and vocab paths:

```bash
   export PHI3_MODEL_PATH="/path/to/phi3.onnx"
   export BGE_MICRO_V2_MODEL_PATH="/path/to/bge_micro_v2.onnx"
   export BGE_MICRO_V2_VOCAB_PATH="/path/to/vocab.txt"
```

## Build & Run:

```bash
dotnet build
dotnet run
```

This sample demonstrates how you can do RAG using Semantic Kernel with the ONNX Connector that enables running Local Models straight from files. 

Here are some example questions a user might ask to test the RAG system and verify that the facts are being utilized effectively:

Kubernetes Basics:
"What is Kubernetes?"
"How does Kubernetes manage and scale containerized applications?"
"What type of environments can Kubernetes be deployed in?"

Azure Kubernetes Service (AKS) Context:
"What is Azure Kubernetes Service (AKS)?"
"How does AKS simplify the management of Kubernetes clusters?"
"What Azure services does AKS integrate with?"

Logs and Analysis:
"What are logs in a Kubernetes environment?"
"Why is log analysis important in AKS clusters?"
"How can AI-driven log analysis improve troubleshooting?"

Retrieval-Augmented Generation (RAG):
"What does RAG stand for and what does it do?"
"How can RAG improve the answers provided by an AI assistant?"
"Why is RAG beneficial for dealing with domain-specific knowledge?"

K8sLogBotRAG Specific:
"What is K8sLogBotRAG?"
"How does K8sLogBotRAG leverage RAG for log analysis?"
"What benefits does K8sLogBotRAG offer for AKS log troubleshooting?"