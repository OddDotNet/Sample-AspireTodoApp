var builder = DistributedApplication.CreateBuilder(args);

var oddDotNet = builder.AddContainer("odddotnet", "ghcr.io/odddotnet/odddotnet")
    .WithHttpEndpoint(targetPort: 4317, name: "grpc");
builder.AddProject<Projects.TodoWebApp>("todo")
    .WithEnvironment("OTEL_EXPORTER_OTLP_TRACES_ENDPOINT", oddDotNet.GetEndpoint("grpc"));

builder.Build().Run();