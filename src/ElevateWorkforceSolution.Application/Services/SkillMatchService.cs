using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>
/// Transparent, rule-based candidate/vacancy comparison. The score is a weighted sum of
/// skill overlap, experience, location and profile completeness. No external AI service is
/// involved and the result is advisory only.
/// </summary>
public class SkillMatchService : ISkillMatchService
{
    // Weights must total 100 so the headline figure stays easy to explain to candidates.
    private const int SkillWeight = 50;
    private const int ExperienceWeight = 25;
    private const int LocationWeight = 10;
    private const int ProfileWeight = 15;

    private readonly IRepository<Job> _jobs;
    private readonly IRepository<JobSeeker> _jobSeekers;

    public SkillMatchService(IRepository<Job> jobs, IRepository<JobSeeker> jobSeekers)
    {
        _jobs = jobs;
        _jobSeekers = jobSeekers;
    }

    public async Task<SkillMatchResultDto?> EvaluateAsync(int jobId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var job = await _jobs.Query()
            .Include(j => j.RequiredSkills)
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        var seeker = await _jobSeekers.Query()
            .Include(s => s.Skills)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == jobSeekerId, cancellationToken);

        if (job is null || seeker is null)
        {
            return null;
        }

        var requiredSkills = job.RequiredSkills.Select(s => s.Name).ToList();
        var candidateSkills = seeker.Skills
            .GroupBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Name = g.Key, Proficiency = g.Max(s => s.Proficiency) })
            .ToList();

        var matching = new List<SkillMatchItemDto>();
        var missing = new List<string>();

        foreach (var required in requiredSkills)
        {
            var hit = candidateSkills.FirstOrDefault(
                c => string.Equals(c.Name, required, StringComparison.OrdinalIgnoreCase));

            if (hit is not null)
            {
                matching.Add(new SkillMatchItemDto
                {
                    Skill = required,
                    IsMandatory = true,
                    CandidateProficiency = hit.Proficiency
                });
            }
            else
            {
                missing.Add(required);
            }
        }

        // Vacancy skills the candidate has that were not requested. Not scored, but useful context.
        var extraCandidateSkills = candidateSkills
            .Select(c => c.Name)
            .Where(name => !requiredSkills.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var skillScore = requiredSkills.Count == 0
            ? SkillWeight
            : (int)Math.Round(SkillWeight * (double)matching.Count / requiredSkills.Count);

        var (experienceScore, experienceNote) = ScoreExperience(job, seeker);
        var (locationScore, locationNote) = ScoreLocation(job, seeker);
        var profileScore = ScoreProfile(seeker);

        var total = skillScore + experienceScore + locationScore + profileScore;

        return new SkillMatchResultDto
        {
            JobId = job.Id,
            JobTitle = job.Title,
            CompatibilityPercentage = Math.Clamp(total, 0, 100),
            SkillMatchPercentage = requiredSkills.Count == 0
                ? 0
                : (int)Math.Round(100.0 * matching.Count / requiredSkills.Count),
            ExperienceScore = experienceScore,
            LocationScore = locationScore,
            ProfileCompletenessScore = profileScore,
            MatchingSkills = matching,
            MissingSkills = missing,
            LocationNote = locationNote,
            ExperienceNote = experienceNote
        };
    }

    public async Task<List<JobCardDto>> RecommendAsync(int jobSeekerId, int take, CancellationToken cancellationToken = default)
    {
        take = Math.Clamp(take, 1, 24);

        var seeker = await _jobSeekers.Query()
            .Include(s => s.Skills)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == jobSeekerId, cancellationToken);

        if (seeker is null)
        {
            return new List<JobCardDto>();
        }

        var skillNames = seeker.Skills.Select(s => s.Name).ToList();
        var location = seeker.Location;

        // Vacancies the candidate has not already applied to or saved.
        var excluded = await _jobs.Query()
            .Where(j => j.Applications.Any(a => a.JobSeekerId == jobSeekerId)
                        || j.SavedJobs.Any(s => s.JobSeekerId == jobSeekerId))
            .Select(j => j.Id)
            .ToListAsync(cancellationToken);

        var candidates = _jobs.Query()
            .Where(j => j.Status == VacancyStatus.Active && !excluded.Contains(j.Id))
            .AsNoTracking();

        // Rank by overlap between required and candidate skills, then by a location match.
        var ordered = await candidates
            .Select(j => new
            {
                Job = j,
                Overlap = skillNames.Count == 0
                    ? 0
                    : j.RequiredSkills.Count(s =>
                        skillNames.Any(n => EF.Functions.ILike(s.Name, n))),
                SameLocation = location != null && j.WorkLocation != null
                    && EF.Functions.ILike(j.WorkLocation, location)
            })
            .OrderByDescending(x => x.Overlap)
            .ThenByDescending(x => x.SameLocation)
            .ThenByDescending(x => x.Job.PostedDate)
            .Take(take)
            .Select(x => new JobCardDto
            {
                Id = x.Job.Id,
                Title = x.Job.Title,
                CompanyName = x.Job.Company!.Name,
                CompanyLogoUrl = x.Job.Company.LogoUrl,
                WorkLocation = x.Job.WorkLocation,
                Category = x.Job.Category,
                JobType = x.Job.JobType,
                WorkArrangement = x.Job.WorkArrangement,
                ExperienceLevel = x.Job.ExperienceLevel,
                SalaryMin = x.Job.SalaryMin,
                SalaryMax = x.Job.SalaryMax,
                SalaryCurrency = x.Job.SalaryCurrency,
                SalaryPeriod = x.Job.SalaryPeriod,
                PostedDate = x.Job.PostedDate,
                ApplicationDeadline = x.Job.ApplicationDeadline,
                Status = x.Job.Status,
                EmployerId = x.Job.EmployerId,
                CompanyId = x.Job.CompanyId,
                Skills = x.Job.RequiredSkills.Select(s => s.Name).Take(6).ToList()
            })
            .ToListAsync(cancellationToken);

        return ordered;
    }

    /// <summary>
    /// Compares the vacancy's seniority band with the candidate's years of experience.
    /// Purely arithmetic - no scoring model is trained or downloaded.
    /// </summary>
    private static (int Score, string Note) ScoreExperience(Job job, JobSeeker seeker)
    {
        var expectedYears = job.ExperienceLevel switch
        {
            ExperienceLevel.Entry => 0.5m,
            ExperienceLevel.Mid => 3m,
            ExperienceLevel.Senior => 6m,
            ExperienceLevel.Lead => 9m,
            ExperienceLevel.Executive => 12m,
            _ => 0m
        };

        var actualYears = seeker.YearsOfExperience ?? 0m;

        if (actualYears >= expectedYears)
        {
            return (ExperienceWeight, $"Meets the {job.ExperienceLevel} level expectation.");
        }

        var gap = expectedYears - actualYears;
        var penalty = gap switch
        {
            >= 6m => ExperienceWeight,
            >= 4m => (int)Math.Round(ExperienceWeight * 0.6),
            >= 2m => (int)Math.Round(ExperienceWeight * 0.8),
            _ => (int)Math.Round(ExperienceWeight * 0.9)
        };

        var score = Math.Max(0, ExperienceWeight - penalty);
        return (score, $"About {gap:0.#} years below the {job.ExperienceLevel} level expectation.");
    }

    private static (int Score, string Note) ScoreLocation(Job job, JobSeeker seeker)
    {
        // Remote vacancies place no constraint on where the candidate lives.
        if (job.WorkArrangement == WorkArrangement.Remote)
        {
            return (LocationWeight, "This vacancy is remote, so location does not matter.");
        }

        if (string.IsNullOrWhiteSpace(seeker.Location) || string.IsNullOrWhiteSpace(job.WorkLocation))
        {
            return (LocationWeight / 2, "Location details are incomplete on one side.");
        }

        var same = string.Equals(seeker.Location.Trim(), job.WorkLocation.Trim(), StringComparison.OrdinalIgnoreCase);
        if (same)
        {
            return (LocationWeight, $"Based in {job.WorkLocation}, the same city as this vacancy.");
        }

        return (LocationWeight / 2, $"Candidate is based in {seeker.Location} and the role is in {job.WorkLocation}.");
    }

    private static int ScoreProfile(JobSeeker seeker)
    {
        var score = 0;
        if (!string.IsNullOrWhiteSpace(seeker.ProfessionalTitle)) score += 3;
        if (!string.IsNullOrWhiteSpace(seeker.About)) score += 3;
        if (!string.IsNullOrWhiteSpace(seeker.Phone)) score += 2;
        if (seeker.Skills.Any()) score += 4;
        if (seeker.Experiences.Any()) score += 2;
        if (seeker.Educations.Any()) score += 1;
        return Math.Min(ProfileWeight, score);
    }
}
