using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using StudyCoach.BackendApi.Application.Chat;
using StudyCoach.BackendApi.Application.Quiz;
using StudyCoach.BackendApi.Application.Session;
using StudyCoach.BackendApi.Application.Skills;
using StudyCoach.BackendApi.Infrastructure.Foundry;
using StudyCoach.BackendApi.Infrastructure.LearnMcp;
using StudyCoach.BackendApi.Infrastructure.Sessions;

namespace StudyCoach.BackendApi.Configuration;

public static class ServiceRegistrationExtensions
{
    public static IServiceCollection AddObservability(this IServiceCollection services, IConfiguration configuration)
    {
        var appInsightsConnectionString = configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrWhiteSpace(appInsightsConnectionString))
        {
            services.AddApplicationInsightsTelemetry();
        }

        return services;
    }

    public static IServiceCollection AddAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var authDisabled = configuration.GetValue<bool>("Auth:DisableAuth");
        if (authDisabled)
        {
            return services;
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = configuration["Auth:Authority"];
                options.Audience = configuration["Auth:Audience"];
                options.RequireHttpsMetadata = true;
            });

        services.AddAuthorization();
        return services;
    }

    public static IServiceCollection AddFoundry(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FoundryOptions>(configuration.GetSection("Foundry"));
        services.AddSingleton<FoundryStudyCoachClient>();
        services.AddSingleton<IOnboardingCoach>(sp => sp.GetRequiredService<FoundryStudyCoachClient>());
        services.AddSingleton<IChatCoach>(sp => sp.GetRequiredService<FoundryStudyCoachClient>());
        services.AddSingleton<IQuizCoach>(sp => sp.GetRequiredService<FoundryStudyCoachClient>());

        return services;
    }

    public static IServiceCollection AddStudyFeatures(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LearnMcpOptions>(configuration.GetSection("LearnMcp"));
        services.AddSingleton(TimeProvider.System);

        services.AddHttpClient<ILearnMcpClient, LearnMcpHttpClient>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<LearnMcpOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
            {
                client.BaseAddress = new Uri(options.BaseUrl);
            }
        });

        services.AddSingleton<IStudySessionRepository, InMemoryStudySessionRepository>();
        services.AddSingleton<ISkillsOutlineProvider, CachedSkillsOutlineProvider>();

        services.AddSingleton<IStudySessionService, StudySessionService>();
        services.AddSingleton<IStudyChatService, StudyChatService>();
        services.AddSingleton<IStudyQuizService, StudyQuizService>();
        services.AddSingleton<IStudySkillsService, StudySkillsService>();

        return services;
    }
}
