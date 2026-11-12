using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Bookmarked vacancies. Uniqueness is enforced by a database constraint as well.</summary>
public class SavedJobService : ISavedJobService
{
    private readonly IRepository<SavedJob> _savedJobs;
    private readonly IRepository<Job> _jobs;
    private readonly IUnitOfWork _unitOfWork;

    public SavedJobService(IRepository<SavedJob> savedJobs, IRepository<Job> jobs, IUnitOfWork unitOfWork)
    {
        _savedJobs = savedJobs;
        _jobs = jobs;
        _unitOfWork = unitOfWork;
    }

    public Task<bool> IsSavedAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default) =>
        _savedJobs.AnyAsync(s => s.JobId == jobId && s.JobSeekerId == jobSeekerId, cancellationToken);

    public async Task<Result<int>> SaveAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.GetByIdAsync(jobId, cancellationToken);

        if (job is null)
        {
            return Result.Failure<int>("This vacancy no longer exists.");
        }

        if (!StatusRules.IsPubliclyVisible(job.Status))
        {
            return Result.Failure<int>("This vacancy is no longer available publicly.");
        }

        if (await _savedJobs.AnyAsync(s => s.JobId == jobId && s.JobSeekerId == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("You have already saved this vacancy.");
        }

        var saved = new SavedJob
        {
            JobId = jobId,
            JobSeekerId = jobSeekerId,
            SavedDate = DateTime.UtcNow
        };

        await _savedJobs.AddAsync(saved, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(saved.Id);
    }

    public async Task<Result> UnsaveAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var saved = await _savedJobs.Query()
            .FirstOrDefaultAsync(s => s.JobId == jobId && s.JobSeekerId == jobSeekerId, cancellationToken);

        if (saved is null)
        {
            return Result.Failure("This vacancy is not in your saved list.");
        }

        _savedJobs.Remove(saved);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<PagedResult<JobCardDto>> ListAsync(
        int jobSeekerId, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 12 : pageSize;

        var query = _savedJobs.Query()
            .Where(s => s.JobSeekerId == jobSeekerId)
            .Select(s => s.Job!);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(j => j.PostedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(j => new JobCardDto
            {
                Id = j.Id,
                Title = j.Title,
                CompanyName = j.Company!.Name,
                CompanyLogoUrl = j.Company!.LogoUrl,
                WorkLocation = j.WorkLocation,
                Category = j.Category,
                JobType = j.JobType,
                WorkArrangement = j.WorkArrangement,
                ExperienceLevel = j.ExperienceLevel,
                SalaryMin = j.SalaryMin,
                SalaryMax = j.SalaryMax,
                SalaryCurrency = j.SalaryCurrency,
                SalaryPeriod = j.SalaryPeriod,
                PostedDate = j.PostedDate,
                ApplicationDeadline = j.ApplicationDeadline,
                Status = j.Status,
                EmployerId = j.EmployerId,
                CompanyId = j.CompanyId,
                IsSaved = true,
                HasApplied = j.Applications.Any(a => a.JobSeekerId == jobSeekerId),
                Skills = j.RequiredSkills.Select(s2 => s2.Name).Take(6).ToList()
            })
            .ToListAsync(cancellationToken);

        return new PagedResult<JobCardDto>(items, total, page, pageSize);
    }
}
