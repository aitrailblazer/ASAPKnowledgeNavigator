#!/usr/bin/env bash
set -e

# Ensure the Facts directory exists
mkdir -p Facts

# Create Kubernetes.txt
cat > Facts/Kubernetes.txt <<EOF
Kubernetes is an open-source system for automating the deployment, scaling, and management of containerized applications.
It groups containers that make up an application into logical units for easy management and discovery.
Kubernetes is often deployed in cloud environments, including Azure Kubernetes Service (AKS).
EOF

# Create AKS.txt
cat > Facts/AKS.txt <<EOF
Azure Kubernetes Service (AKS) is a managed Kubernetes service offered by Microsoft Azure.
AKS simplifies the deployment and management of Kubernetes clusters in the cloud.
Users can create, upgrade, and scale Kubernetes clusters more easily with AKS.
AKS integrates with other Azure services, offering robust security, monitoring, and scalability features.
EOF

# Create Logs.txt
cat > Facts/Logs.txt <<EOF
Logs are records of events that occur within systems and applications.
Kubernetes and AKS generate various logs: application logs, cluster logs, and system logs.
Log analysis helps identify issues, performance bottlenecks, and security incidents.
AI-driven log analysis can streamline troubleshooting, reduce time-to-resolution, and improve reliability.
EOF

# Create RAG.txt
cat > Facts/RAG.txt <<EOF
RAG (Retrieval-Augmented Generation) is a technique that combines Large Language Models (LLMs) with external data sources.
By retrieving relevant documents (facts), RAG systems can provide more accurate and contextually rich answers.
RAG can improve the reliability of AI-driven solutions like K8sLogBotRAG by leveraging domain-specific knowledge in real-time.
EOF

# Create K8sLogBotRAG.txt
cat > Facts/K8sLogBotRAG.txt <<EOF
K8sLogBotRAG is an AI-driven solution designed to streamline log analysis for Azure Kubernetes Service (AKS) clusters.
It utilizes Retrieval-Augmented Generation (RAG) techniques to provide context-aware responses to log analysis queries.
K8sLogBotRAG can help operators quickly troubleshoot issues, identify anomalies, and gain insights from logs.
EOF

# Create ONNX.txt
cat > Facts/ONNX.txt <<EOF
ONNX (Open Neural Network Exchange) is an open-source format designed to facilitate the interoperability of AI models across various frameworks and tools. It enables developers to train models in one framework and deploy them in another, streamlining the AI development process.
EOF

# Create PHI-3.txt
cat > Facts/PHI-3.txt <<EOF
PHI-3 is a lightweight, state-of-the-art open language model developed by Microsoft. It has been trained on high-quality, reasoning-dense data, making it suitable for tasks such as text generation and conversational AI. The model is available in different configurations, including a "mini" version with 3.3 billion parameters and context lengths of 4K and 128K tokens.
EOF

# Create BGE-MICRO-V2.txt
cat > Facts/BGE-MICRO-V2.txt <<EOF
BGE-MICRO-V2 is a compact embedding model designed for generating dense vector representations of text. These embeddings are useful for tasks like clustering and semantic search. Due to its small size, BGE-MICRO-V2 is well-suited for deployment on devices with limited resources, offering fast inference times with a slight trade-off in accuracy compared to larger models.
EOF

# Publish the application for osx-arm64
dotnet publish -r osx-arm64 --self-contained -c Release

echo "Facts directory and files created successfully."
