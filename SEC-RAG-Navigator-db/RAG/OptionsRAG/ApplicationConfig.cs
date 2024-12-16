// Copyright (c) AITrailblazer. All rights reserved.

using Microsoft.Extensions.Configuration;

namespace VectorStoreRAG.Options;

/// <summary>
/// Helper class to load all configuration settings for the VectorStoreRAG project.
/// </summary>
internal sealed class ApplicationConfig
{
    private readonly AzureOpenAIConfig _azureOpenAIConfig;

    private readonly RagConfig _ragConfig = new();

    private readonly AzureCosmosDBConfig _azureCosmosDBNoSQLConfig = new();

    public ApplicationConfig(ConfigurationManager configurationManager)
    {
        this._azureOpenAIConfig = new();
        configurationManager
            .GetRequiredSection($"AIServices:{AzureOpenAIConfig.ConfigSectionName}")
            .Bind(this._azureOpenAIConfig);
        configurationManager
            .GetRequiredSection(RagConfig.ConfigSectionName)
            .Bind(this._ragConfig);
        configurationManager
            .GetRequiredSection($"VectorStores:{AzureCosmosDBConfig.NoSQLConfigSectionName}")
            .Bind(this._azureCosmosDBNoSQLConfig);
    }

    public RagConfig RagConfig => this._ragConfig;
    public AzureCosmosDBConfig AzureCosmosDBNoSQLConfig => this._azureCosmosDBNoSQLConfig;
}
