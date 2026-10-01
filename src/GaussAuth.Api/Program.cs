using GaussAuth.Api.DependencyInjection;
using GaussAuth.Api.Users;
using GaussAuth.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiServices(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseRateLimiter();
app.MapGet("/health/live", () => Results.NoContent());
app.MapUsersEndpoints();

app.Run();
