using GaussAuth.Api.DependencyInjection;
using GaussAuth.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapGet("/health/live", () => Results.NoContent());

app.Run();
