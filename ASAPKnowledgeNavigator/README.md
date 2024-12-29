# ASAPKnowledgeNavigator

ASAPKnowledgeNavigator is an advanced AI-powered project designed to enhance knowledge navigation and retrieval. It leverages cutting-edge AI models, seamless Azure integration, and environment variables for efficient configuration and operation in diverse environments.

---

## Environment Variables

The following environment variables are required for proper configuration:

- `AZURE_OPENAI_ENDPOINT`: Endpoint URI for Azure OpenAI services.
- `AZURE_OPENAI_KEY`: API key to authenticate Azure OpenAI services.
- `PHI_ENDPOINT`: Endpoint URI for the PHI service.
- `PHI_KEY`: API key for the PHI service.
- `AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME`: Deployment name for text completion service.
- `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME`: Deployment name for embedding service.
- `COSMOS_DB_CONNECTION_STRING`: Connection string for Azure Cosmos DB.
- `COSMOS_DB_DATABASE_ID`: Database ID in Cosmos DB.

### Exporting Environment Variables

Export these variables as follows:

```bash
export AZURE_OPENAI_ENDPOINT=<...>
export AZURE_OPENAI_KEY=<...>
export AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME=<...>
export AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME=<...>
export PHI_ENDPOINT=<...>
export PHI_KEY=<...>
export COSMOS_DB_CONNECTION_STRING="<...>"
export COSMOS_DB_DATABASE_ID="<...>"
```

---

## Project Description

ASAPKnowledgeNavigator enhances knowledge retrieval by utilizing modern AI technologies and Azure services. Key features include:

1. Processing complex user queries with precision.
2. Delivering accurate, contextually relevant information.
3. Leveraging environment variables to ensure smooth deployment.

---

## Azure Resources Used

### 1. **Azure OpenAI Service**
   - **Purpose**: Natural language processing tasks (e.g., text completion, embeddings).
   - **Environment Variables**:
     - `AZURE_OPENAI_ENDPOINT`
     - `AZURE_OPENAI_KEY`
     - `AZURE_OPENAI_COMPLETION_DEPLOYMENT_NAME`
     - `AZURE_OPENAI_EMBEDDING_DEPLOYMENT_NAME`

### 2. **Azure Cosmos DB**
   - **Purpose**: Storing globally distributed knowledge base data with multi-model capabilities.
   - **Configuration**:
     - Connection String: `COSMOS_DB_CONNECTION_STRING`
     - Database ID: `COSMOS_DB_DATABASE_ID`

### 3. **Azure Container Registry (ACR)**
   - **Purpose**: Stores container images for deployment.
   - Registry Name: acr-${resourceToken}
   - Role Assignment: Managed identity has the AcrPull role.

### 4. **Azure Container Apps Environment**
   - **Purpose**: Runs containerized applications using Azure Container Apps.
   - Configuration:
     - Workload Profile: Consumption type (optimized resource usage).
     - Log Analytics Integration enabled.

### 5. **Log Analytics Workspace**
   - Collects logs from various Azure services for monitoring and diagnostics.

### 6. **User Assigned Managed Identity**
   Provides secure authentication without embedding credentials in code.

---

## Initialization and Deployment

To initialize the app:

```bash
azd init
```

Follow prompts to configure the app (e.g., set environment name).

To provision and deploy the app:

```bash
azd up
```

Alternatively, use separate commands:

```bash
azd provision
azd deploy
```

---

## Resources Defined in resources.bicep

1. User Assigned Managed Identity  
    Type: Microsoft.ManagedIdentity/userAssignedIdentities  
    Description: Secure authentication mechanism.

2. Azure Container Registry  
    Type: Microsoft.ContainerRegistry/registries  
    Description: Stores container images securely.

3. Role Assignment  
    Type: Microsoft.Authorization/roleAssignments  
    Description: Grants managed identity permission to pull images from ACR.

4. Log Analytics Workspace  
    Type: Microsoft.OperationalInsights/workspaces  
    Description: Monitors application logs effectively.

5. Azure Container Apps Environment  
    Type: Microsoft.App/managedEnvironments  
    Description: Hosts scalable containerized applications efficiently.

---

## Outputs from Deployment Process

The deployment process provides outputs such as IDs, names, and endpoints of critical components like:

- Managed Identity
- Log Analytics Workspace
- Container Registry
- Container Apps Environment

---

## Summary

ASAPKnowledgeNavigator integrates seamlessly with multiple Azure services to deliver powerful knowledge retrieval capabilities through its scalable architecture optimized for efficiency and flexibility—a robust solution tailored for advanced knowledge navigation needs!
