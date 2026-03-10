using Scalar.AspNetCore;
using StudyCoach.BackendApi.Configuration;
using StudyCoach.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services
    .AddObservability(builder.Configuration)
    .AddAuth(builder.Configuration)
    .AddFoundry(builder.Configuration)
    .AddStudyFeatures(builder.Configuration);

var authDisabled = builder.Configuration.GetValue<bool>("Auth:DisableAuth");

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

