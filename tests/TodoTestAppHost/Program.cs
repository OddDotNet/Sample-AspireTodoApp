var builder = DistributedApplication.CreateBuilder(args);

var oddDotNet = builder
    .AddContainer("odddotnet", "ghcr.io/odddotnet/odddotnet")
    .WithImageTag("v0.4.0")
    .WithHttpEndpoint(targetPort: 4317, name: "grpc")
    .WithHttpEndpoint(targetPort: 4318, name: "http"); // For the healthcheck endpoint
builder.AddProject<Projects.TodoWebApp>("todo")
    .WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", oddDotNet.GetEndpoint("grpc"));

builder.Build().Run();