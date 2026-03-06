using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using StudyCoach.BackendApi.Services;
using StudyCoach.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

var authDisabled = builder.Configuration.GetValue<bool>("Auth:DisableAuth");

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.Configure<FoundryOptions>(builder.Configuration.GetSection("Foundry"));
builder.Services.Configure<LearnMcpOptions>(builder.Configuration.GetSection("LearnMcp"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<ILearnMcpClient, LearnMcpHttpClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<LearnMcpOptions>>().Value;
    if (!string.IsNullOrWhiteSpace(options.BaseUrl))
    {
        client.BaseAddress = new Uri(options.BaseUrl);
    }
});

var appInsightsConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
{
    builder.Services.AddApplicationInsightsTelemetry();
}

if (!authDisabled)
{
    builder.Services
        .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = builder.Configuration["Auth:Authority"];
            options.Audience = builder.Configuration["Auth:Audience"];
            options.RequireHttpsMetadata = true;
        });
    builder.Services.AddAuthorization();
}

builder.Services.AddSingleton<IFoundryStudyCoachClient, FoundryStudyCoachClient>();
builder.Services.AddSingleton<ISkillsOutlineProvider, CachedSkillsOutlineProvider>();
builder.Services.AddSingleton<ISessionStore, InMemorySessionStore>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();

if (!authDisabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

if (!authDisabled)
{
    app.MapControllers().RequireAuthorization();
}
else
{
    app.MapControllers();
}

app.MapDefaultEndpoints();

app.Run();

public partial class Program;
