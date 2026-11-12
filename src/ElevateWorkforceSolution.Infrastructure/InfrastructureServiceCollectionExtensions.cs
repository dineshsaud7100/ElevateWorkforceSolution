using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Application.Services;
using ElevateWorkforceSolution.Infrastructure.Repositories;
using ElevateWorkforceSolution.Infrastructure.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ElevateWorkforceSolution.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    /// <summary>Registers repositories, file storage and application services.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<FileStorageOptions>(configuration.GetSection(FileStorageOptions.SectionName));
        services.Configure<FileStoragePolicy>(configuration.GetSection(FileStoragePolicy.SectionName));

        services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IFileStorageService, FileStorageService>();

        services.AddScoped<IJobSearchService, JobSearchService>();
        services.AddScoped<IVacancyService, VacancyService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ISavedJobService, SavedJobService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IResumeService, ResumeService>();
        services.AddScoped<ICandidateProfileService, CandidateProfileService>();
        services.AddScoped<IEmployerProfileService, EmployerProfileService>();
        services.AddScoped<ISkillMatchService, SkillMatchService>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<IAdminService, AdminService>();

        return services;
    }
}
