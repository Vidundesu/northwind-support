using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportDesk.Application.Abstractions;
using SupportDesk.Domain.Policies.Tickets;
using SupportDesk.Domain.Repositories;
using SupportDesk.Infrastructure.Data;
using SupportDesk.Infrastructure.Queries;
using SupportDesk.Infrastructure.Repositories;
using SupportDesk.Infrastructure.Services;

namespace SupportDesk.Infrastructure;

/// <summary>
/// Wires the domain's repositories and the application's abstractions to their SQL Server /
/// system implementations.
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Connection string 'Default' is missing. See README.md for local setup.");

        services.AddDbContext<SupportDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<SupportDbContext>());

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(_ => ReadSlaSettings(configuration));

        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentRepository, AgentRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<ITicketQueries, TicketQueries>();
        services.AddScoped<ICustomerQueries, CustomerQueries>();
        services.AddScoped<IAgentQueries, AgentQueries>();
        services.AddScoped<ICategoryQueries, CategoryQueries>();

        services.AddScoped<SupportDbSeeder>();

        return services;
    }

    private static SlaSettings ReadSlaSettings(IConfiguration configuration)
    {
        int ReadInt(string key) => int.TryParse(configuration[$"Sla:{key}"], out var value)
            ? value : throw new InvalidOperationException($"Sla:{key} must be configured as an integer.");
        decimal ReadDecimal(string key) => decimal.TryParse(configuration[$"Sla:{key}"], System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture, out var value)
            ? value : throw new InvalidOperationException($"Sla:{key} must be configured as a decimal number.");

        var settings = new SlaSettings(
            new SlaBaseWindowHours(ReadInt("BaseWindowHours:Low"), ReadInt("BaseWindowHours:Medium"),
                ReadInt("BaseWindowHours:High"), ReadInt("BaseWindowHours:Critical")),
            ReadDecimal("PremiumCustomerMultiplier"), ReadInt("MinimumWindowHours"), ReadInt("AtRiskThresholdPercent"));
        settings.Validate();
        return settings;
    }
}
