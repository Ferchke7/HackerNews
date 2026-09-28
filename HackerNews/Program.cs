using HackerNews.Api.Endpoints.Stories;
using HackerNews.Api.Errors;
using HackerNews.Application;
using HackerNews.Application.Configuration;
using HackerNews.Common.Json;
using HackerNews.Common.Logging;
using HackerNews.Infrastructure;
using HackerNews.Infrastructure.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Services.AddLogger(builder.Configuration);

builder.Services.AddOpenApi(OpenApiConfigurator.Configure);
builder.Services.AddEndpointsApiExplorer();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new DateOnlyJsonConverter());
    options.SerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
});

builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddHealthChecks();

var storyCacheTtlMinutes = builder.Configuration.GetValue<int>(
    $"{HackerNewsOptions.SectionName}:CacheTtlMinutes",
    new HackerNewsOptions().CacheTtlMinutes);
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("BestStories", policy => policy
        .Expire(TimeSpan.FromMinutes(storyCacheTtlMinutes))
        .SetVaryByQuery("n"));
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/openapi/v1.json", "v1");
    });
}

app.UseHttpsRedirection();
app.UseOutputCache();

app.MapHealthChecks("/health");
app.MapStoriesEndpoints();

app.Run();

public partial class Program { }
