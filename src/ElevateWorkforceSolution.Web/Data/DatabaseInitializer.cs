using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace ElevateWorkforceSolution.Web.Data;

/// <summary>
/// First-run setup: applies migrations, creates the three roles and, when seeding is enabled,
/// loads realistic Nepal-focused development data. The administrator is created here only -
/// there is deliberately no public route that can create an administrator account.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task InitializeAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var services = scope.ServiceProvider;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");

        var context = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var seedOptions = services.GetRequiredService<IOptions<SeedOptions>>().Value;

        await context.Database.MigrateAsync();

        await EnsureRolesAsync(roleManager, logger);

        if (!await context.Users.AnyAsync())
        {
            if (seedOptions.Enabled)
            {
                await DevelopmentSeeder.SeedAsync(context, userManager, seedOptions, logger);
            }
            else
            {
                logger.LogInformation("Seeding is disabled. Only roles were created.");
            }
        }

        // Vacancies past their deadline stop accepting applications without a background job.
        var vacancyService = services.GetRequiredService<Application.Interfaces.IVacancyService>();
        await vacancyService.ExpirePastDeadlineVacanciesAsync();
    }

    private static async Task EnsureRolesAsync(RoleManager<IdentityRole> roleManager, ILogger logger)
    {
        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"Could not create role '{role}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }

                logger.LogInformation("Created role {Role}.", role);
            }
        }
    }
}
