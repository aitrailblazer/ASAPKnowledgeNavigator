#!/bin/bash

# Project Name
PROJECT_NAME="SEC-RAG-Navigator-db"

# Define directory structure
DIRS=(
    "$PROJECT_NAME"
    "$PROJECT_NAME/Models"
)

# Create directories
echo "Creating project directories..."
for dir in "${DIRS[@]}"; do
    mkdir -p "$dir"
done

# Initialize a new .NET console application
echo "Initializing .NET console project..."
dotnet new console -o $PROJECT_NAME

# Add NuGet packages
echo "Adding required NuGet packages..."
cd $PROJECT_NAME
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Microsoft.Azure.Cosmos.Fluent
dotnet add package Newtonsoft.Json --version 13.0.3 # Add Newtonsoft.Json explicitly
cd ..

# Create Program.cs
echo "Creating Program.cs..."
cat <<EOF > "$PROJECT_NAME/Program.cs"
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using SEC_RAG_Navigator.Models; // Namespace for custom models

namespace SEC_RAG_Navigator
{
    class Program
    {
        private static readonly string EndpointUri = Environment.GetEnvironmentVariable("COSMOS_DB_ENDPOINT") 
            ?? throw new ArgumentNullException("COSMOS_DB_ENDPOINT", "Cosmos DB endpoint is not set in environment variables.");
        private static readonly string PrimaryKey = Environment.GetEnvironmentVariable("COSMOS_DB_PRIMARY_KEY") 
            ?? throw new ArgumentNullException("COSMOS_DB_PRIMARY_KEY", "Cosmos DB primary key is not set in environment variables.");
        private static readonly string DatabaseId = Environment.GetEnvironmentVariable("COSMOS_DB_DATABASE_ID") 
            ?? throw new ArgumentNullException("COSMOS_DB_DATABASE_ID", "Cosmos DB database ID is not set in environment variables.");

        private CosmosClient cosmosClient = null!;
        private Database database = null!;
        private Container container = null!;

        static async Task Main(string[] args)
        {
            try
            {
                Console.WriteLine("Starting SEC-RAG Navigator...\n");
                Program program = new Program();
                await program.RunAsync();
            }
            catch (CosmosException ex)
            {
                Console.WriteLine($"\n{ex.StatusCode} error occurred: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nError: {ex.Message}");
            }
            finally
            {
                Console.WriteLine("\nApplication ended. Press any key to exit.");
                Console.ReadKey();
            }
        }

        public async Task RunAsync()
        {
            this.cosmosClient = new CosmosClientBuilder(EndpointUri, PrimaryKey)
                                    .WithApplicationName("SEC-RAG-Navigator")
                                    .Build();

            await this.CreateDatabaseAsync();

            List<string> partitionKeyPaths = new List<string> { "/tenantId", "/userId", "/categoryId" };
            await this.CreateContainerAsync(
                "rag",
                "/vectors",
                partitionKeyPaths,
                new List<string> { "/*" },
                3072
            );

            Console.WriteLine("SEC-RAG Navigator setup completed successfully!");
        }

        private async Task CreateDatabaseAsync()
        {
            this.database = await this.cosmosClient.CreateDatabaseIfNotExistsAsync(DatabaseId);
            Console.WriteLine($"\nCreated Database: {this.database.Id}");
        }

        private async Task CreateContainerAsync(
            string containerName,
            string vectorPath,
            List<string> partitionKeyPaths,
            List<string> includedPaths,
            ulong dimensions)
        {
            // Use the fully qualified name for Microsoft.Azure.Cosmos.Embedding
            Collection<Microsoft.Azure.Cosmos.Embedding> embeddings = new Collection<Microsoft.Azure.Cosmos.Embedding>()
            {
                new Microsoft.Azure.Cosmos.Embedding()
                {
                    Path = vectorPath,
                    DataType = VectorDataType.Float32,
                    DistanceFunction = DistanceFunction.Cosine,
                    Dimensions = dimensions
                }
            };

            ContainerProperties containerProperties = new ContainerProperties(
                id: containerName,
                partitionKeyPaths: partitionKeyPaths
            )
            {
                VectorEmbeddingPolicy = new VectorEmbeddingPolicy(embeddings),
                IndexingPolicy = new IndexingPolicy()
                {
                    VectorIndexes = new Collection<VectorIndexPath>()
                    {
                        new VectorIndexPath()
                        {
                            Path = vectorPath,
                            Type = VectorIndexType.DiskANN,
                        }
                    }
                }
            };

            foreach (string path in includedPaths)
            {
                containerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = path });
            }

            containerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/\"_etag\"/?" });

            this.container = await this.database.CreateContainerIfNotExistsAsync(containerProperties);
            Console.WriteLine($"\nCreated Container: {this.container.Id}");
        }
    }
}
EOF

# Create Models/Embedding.cs
echo "Creating Embedding.cs..."
cat <<EOF > "$PROJECT_NAME/Models/Embedding.cs"
namespace SEC_RAG_Navigator.Models
{
    public class Embedding
    {
        public string Path { get; set; }
        public string DataType { get; set; }
        public string DistanceFunction { get; set; }
        public int Dimensions { get; set; }
    }
}
EOF

# Add a .gitignore file
echo "Creating .gitignore..."
cat <<EOF > "$PROJECT_NAME/.gitignore"
bin/
obj/
*.user
*.suo
*.DS_Store
EOF

# Final Message
echo "Project $PROJECT_NAME created successfully!"
echo "Navigate to the $PROJECT_NAME directory, set the required environment variables, and run the application:"
echo "  export COSMOS_DB_ENDPOINT='<your_cosmos_db_endpoint>'"
echo "  export COSMOS_DB_PRIMARY_KEY='<your_cosmos_db_primary_key>'"
echo "  export COSMOS_DB_DATABASE_ID='<your_database_id>'"
echo "Then run the application:"
echo "  cd $PROJECT_NAME"
echo "  dotnet run"
