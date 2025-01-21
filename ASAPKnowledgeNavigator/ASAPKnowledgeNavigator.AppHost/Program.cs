var builder = DistributedApplication.CreateBuilder(args);

// Add the API service project
var apiService = builder.AddProject<Projects.ASAPKnowledgeNavigator_ApiService>("apiservice");

// Add the web frontend project
//builder.AddProject<Projects.ASAPKnowledgeNavigator_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithReference(apiService) // Reference the API service
//    .WaitFor(apiService);      // Ensure webfrontend waits for the API service


// Add the sec-edgar-ws Python app
//var pythonApp = builder.AddPythonApp("sec-edgar-ws", "./sec-edgar-ws", "main.py") // Path to sec-edgar-ws relative to AppHost root.
//    .WithHttpEndpoint(env: "PORT") // Expose HTTP endpoint based on PORT environment variable.
//    .WithExternalHttpEndpoints()  // Make it accessible externally.
//    .WithOtlpExporter();          // Enable OpenTelemetry for observability.



//var uvicornapp = builder.AddPythonApp(
//       "uvicornapp", 
//       "./uvicornapp-api",   // Path to the directory containing main.py
//       "main.py")           // Module:Callable reference
//       .WithHttpEndpoint(env: "PORT")
//       .WithExternalHttpEndpoints()
//       .WithOtlpExporter();
//builder.AddProject<Projects.ASAPKnowledgeNavigator_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithReference(apiService) // Reference the API service
//    .WithReference(uvicornapp) // Reference the Python app
//    .WaitFor(apiService)       // Ensure webfrontend waits for the API service
//    .WaitFor(uvicornapp);      // Ensure webfrontend waits for uvicornapp

// Add the Python app as a Dockerfile
//var uvicornapp = builder.AddDockerfile("uvicornapp", "./uvicornapp-api")
    //.WithHttpEndpoint(targetPort: 8000, env: "PORT") // Expose HTTP endpoint based on PORT environment variable
//    .WithHttpEndpoint(targetPort: 8000, port: 8000) // Bind host port 8000 to container port 8000
//    .WithExternalHttpEndpoints()  // Make it accessible externally
//    .WithOtlpExporter();          // Enable OpenTelemetry for observability

// Add the web frontend project (combine all references here)
// The uvicornapp API is now passed as a project reference to the front end web app.
//builder.AddProject<Projects.ASAPKnowledgeNavigator_Web>("webfrontend")
//    .WithExternalHttpEndpoints()
//    .WithReference(apiService) // Reference the API service
//    .WaitFor(apiService)       // Ensure webfrontend waits for the API service
//    .WaitFor(uvicornapp);      // Ensure webfrontend waits for uvicornapp

// Add the Python app as a Dockerfile
var secedgarwsapp = builder.AddDockerfile("secedgarwsapp", "./sec-edgar-ws")
    //.WithHttpEndpoint(targetPort: 8000, env: "PORT") // Expose HTTP endpoint based on PORT environment variable
    .WithHttpEndpoint(targetPort: 8000, port: 8000) // Bind host port 8000 to container port 8000
    .WithExternalHttpEndpoints()  // Make it accessible externally
    .WithOtlpExporter();          // Enable OpenTelemetry for observability

// Add the Golang app as a Dockerfile
var gosecedgarwsapp = builder.AddDockerfile("gosecedgarwsapp", "./go-sec-edgar-ws")
    .WithHttpEndpoint(targetPort: 8001, port: 8001) // Bind host port 8000 to container port 8000
    .WithExternalHttpEndpoints()  // Make it accessible externally
    .WithOtlpExporter();          // Enable OpenTelemetry for observability

// Add the Golang app as a Dockerfile
var gotenberg = builder.AddDockerfile("gotenberg", "./gotenberg")
    .WithHttpEndpoint(targetPort: 3000, port: 3000) // Bind host port 8000 to container port 8000
    .WithExternalHttpEndpoints()  // Make it accessible externally
    .WithOtlpExporter();          // Enable OpenTelemetry for observability

// Add the web frontend project (combine all references here)
// The uvicornapp API is now passed as a project reference to the front end web app.
builder.AddProject<Projects.ASAPKnowledgeNavigator_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(apiService) // Reference the API service
    .WaitFor(apiService)       // Ensure webfrontend waits for the API service
    .WaitFor(secedgarwsapp)
    .WaitFor(gosecedgarwsapp)
    .WaitFor(gotenberg);      // Ensure webfrontend waits for uvicornapp


// Build and run the application
builder.Build().Run();
