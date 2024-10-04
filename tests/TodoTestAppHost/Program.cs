var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TodoWebApp>("todo")
    .WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", "http://localhost:4317");
builder.AddContainer("odddotnet", "ghcr.io/odddotnet/odddotnet")
    .WithHttpEndpoint(port: 4317, targetPort: 4317, name: "grpc", isProxied: false);

builder.Build().Run();