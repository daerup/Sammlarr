var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.UI>("ui").WithExternalHttpEndpoints();

builder.Build().Run();
