
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var backendApi = builder.AddProject<StudyCoach_BackendApi>("backendApi");

builder.AddViteApp("web", "../../../frontend")
    .WithReference(backendApi)
    .WaitFor(backendApi)
    .WithEnvironment("VITE_API_BASE_URL", backendApi.GetEndpoint("http"))
    .WithNpm();

await builder.Build().RunAsync();
