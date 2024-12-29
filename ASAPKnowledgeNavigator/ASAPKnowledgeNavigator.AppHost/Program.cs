var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.ASAPKnowledgeNavigator_ApiService>("apiservice");

builder.AddProject<Projects.ASAPKnowledgeNavigator_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
