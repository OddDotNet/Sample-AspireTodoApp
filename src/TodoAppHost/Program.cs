var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.TodoWebApp>("todo");

builder.Build().Run();