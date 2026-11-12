using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Application.Interfaces.Repositories;
using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ElevateWorkforceSolution.Application.Services;

/// <summary>Candidate profile management. Every mutation is scoped to the owning job seeker.</summary>
public class CandidateProfileService : ICandidateProfileService
{
    private readonly IRepository<JobSeeker> _jobSeekers;
    private readonly IRepository<Education> _educations;
    private readonly IRepository<Experience> _experiences;
    private readonly IRepository<Skill> _skills;
    private readonly IRepository<Certification> _certifications;
    private readonly IRepository<Language> _languages;
    private readonly IRepository<ApplicationUser> _users;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CandidateProfileService> _logger;

    public CandidateProfileService(
        IRepository<JobSeeker> jobSeekers,
        IRepository<Education> educations,
        IRepository<Experience> experiences,
        IRepository<Skill> skills,
        IRepository<Certification> certifications,
        IRepository<Language> languages,
        IRepository<ApplicationUser> users,
        IUnitOfWork unitOfWork,
        ILogger<CandidateProfileService> logger)
    {
        _jobSeekers = jobSeekers;
        _educations = educations;
        _experiences = experiences;
        _skills = skills;
        _certifications = certifications;
        _languages = languages;
        _users = users;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CandidateProfileDto?> GetAsync(int jobSeekerId, CancellationToken cancellationToken = default)
    {
        return await _jobSeekers.Query()
            .Where(s => s.Id == jobSeekerId)
            .Select(s => new CandidateProfileDto
            {
                JobSeekerId = s.Id,
                FullName = s.User == null ? string.Empty : s.User.FirstName + " " + s.User.LastName,
                Email = s.User == null ? string.Empty : s.User.Email!,
                Phone = s.Phone,
                Location = s.Location,
                ProfessionalTitle = s.ProfessionalTitle,
                About = s.About,
                YearsOfExperience = s.YearsOfExperience,
                ProfileCompletion = 0,
                HasResume = s.Resumes.Any(),
                LatestResumeFileName = s.Resumes.OrderByDescending(r => r.UploadedDate)
                    .Select(r => r.OriginalFileName).FirstOrDefault(),
                Skills = s.Skills.OrderBy(k => k.Name).Select(k => new SkillDto
                {
                    Id = k.Id,
                    Name = k.Name,
                    Proficiency = k.Proficiency,
                    YearsOfExperience = k.YearsOfExperience
                }).ToList(),
                Educations = s.Educations.OrderByDescending(e => e.GraduationYear).Select(e => new EducationDto
                {
                    Id = e.Id,
                    Institution = e.Institution,
                    Degree = e.Degree,
                    FieldOfStudy = e.FieldOfStudy,
                    GraduationYear = e.GraduationYear,
                    Grade = e.Grade,
                    Description = e.Description
                }).ToList(),
                Experiences = s.Experiences.OrderByDescending(x => x.StartDate).Select(x => new ExperienceDto
                {
                    Id = x.Id,
                    CompanyName = x.CompanyName,
                    Position = x.Position,
                    Location = x.Location,
                    StartDate = x.StartDate,
                    EndDate = x.EndDate,
                    IsCurrent = x.IsCurrent,
                    Description = x.Description
                }).ToList(),
                Certifications = s.Certifications.Select(c => new CertificationDto
                {
                    Id = c.Id,
                    Name = c.Name,
                    IssuingOrganization = c.IssuingOrganization,
                    IssueDate = c.IssueDate,
                    CredentialId = c.CredentialId
                }).ToList(),
                Languages = s.Languages.Select(l => new LanguageDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    Proficiency = l.Proficiency
                }).ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Result<int>> EnsureProfileAsync(
        string userId, string? professionalTitle, string? location, CancellationToken cancellationToken = default)
    {
        var existing = await _jobSeekers.Query()
            .Where(s => s.UserId == userId)
            .Select(s => new { s.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            return Result.Success(existing.Id);
        }

        var seeker = new JobSeeker
        {
            UserId = userId,
            ProfessionalTitle = professionalTitle?.Trim(),
            Location = location?.Trim(),
            Phone = (await _users.GetByIdAsync(userId, cancellationToken))?.PhoneNumber,
            CreatedDate = DateTime.UtcNow
        };

        await _jobSeekers.AddAsync(seeker, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Candidate profile {JobSeekerId} created for user {UserId}.", seeker.Id, userId);
        return Result.Success(seeker.Id);
    }

    public async Task<Result> UpdateBasicsAsync(
        int jobSeekerId, CandidateBasicsDto model, CancellationToken cancellationToken = default)
    {
        var seeker = await _jobSeekers.GetByIdAsync(jobSeekerId, cancellationToken);
        if (seeker is null)
        {
            return Result.Failure("Candidate profile not found.");
        }

        seeker.ProfessionalTitle = model.ProfessionalTitle?.Trim();
        seeker.Location = model.Location?.Trim();
        seeker.Phone = model.Phone?.Trim();
        seeker.About = model.About?.Trim();
        seeker.YearsOfExperience = model.YearsOfExperience;
        seeker.IsOpenToWork = model.IsOpenToWork;
        seeker.UpdatedDate = DateTime.UtcNow;

        _jobSeekers.Update(seeker);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<int>> AddEducationAsync(int jobSeekerId, EducationDto model, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Candidate profile not found.");
        }

        if (string.IsNullOrWhiteSpace(model.Institution) || string.IsNullOrWhiteSpace(model.Degree))
        {
            return Result.Failure<int>("Institution and degree are required.");
        }

        var education = new Education
        {
            JobSeekerId = jobSeekerId,
            Institution = model.Institution.Trim(),
            Degree = model.Degree.Trim(),
            FieldOfStudy = model.FieldOfStudy?.Trim(),
            GraduationYear = model.GraduationYear,
            Grade = model.Grade?.Trim(),
            Description = model.Description?.Trim()
        };

        await _educations.AddAsync(education, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success(education.Id);
    }

    public async Task<Result> RemoveEducationAsync(int educationId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var education = await _educations.GetByIdAsync(educationId, cancellationToken);
        if (education is null || education.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Education record not found.");
        }

        _educations.Remove(education);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<int>> AddExperienceAsync(int jobSeekerId, ExperienceDto model, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Candidate profile not found.");
        }

        if (string.IsNullOrWhiteSpace(model.CompanyName) || string.IsNullOrWhiteSpace(model.Position))
        {
            return Result.Failure<int>("Company name and position are required.");
        }

        if (!model.IsCurrent && !model.EndDate.HasValue)
        {
            return Result.Failure<int>("Provide an end date, or mark the role as current.");
        }

        if (model.EndDate.HasValue && model.EndDate.Value < model.StartDate)
        {
            return Result.Failure<int>("The end date cannot be before the start date.");
        }

        var experience = new Experience
        {
            JobSeekerId = jobSeekerId,
            CompanyName = model.CompanyName.Trim(),
            Position = model.Position.Trim(),
            Location = model.Location?.Trim(),
            StartDate = model.StartDate,
            EndDate = model.IsCurrent ? null : model.EndDate,
            IsCurrent = model.IsCurrent,
            Description = model.Description?.Trim()
        };

        await _experiences.AddAsync(experience, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Derive total experience from stored roles so the figure stays honest.
        var seeker = await _jobSeekers.GetByIdAsync(jobSeekerId, cancellationToken);
        if (seeker is not null)
        {
            seeker.YearsOfExperience = await CalculateTotalYearsAsync(jobSeekerId, cancellationToken);
            _jobSeekers.Update(seeker);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);
        return Result.Success(experience.Id);
    }

    public async Task<Result> RemoveExperienceAsync(int experienceId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var experience = await _experiences.GetByIdAsync(experienceId, cancellationToken);
        if (experience is null || experience.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Experience record not found.");
        }

        _experiences.Remove(experience);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var seeker = await _jobSeekers.GetByIdAsync(jobSeekerId, cancellationToken);
        if (seeker is not null)
        {
            seeker.YearsOfExperience = await CalculateTotalYearsAsync(jobSeekerId, cancellationToken);
            _jobSeekers.Update(seeker);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);
        return Result.Success();
    }

    public async Task<Result<int>> AddSkillsAsync(int jobSeekerId, string? commaSeparatedSkills, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Candidate profile not found.");
        }

        var names = VacancyFormDto.NormaliseSkills(commaSeparatedSkills);
        if (names.Count == 0)
        {
            return Result.Failure<int>("Enter at least one skill.");
        }

        var existing = await _skills.Query()
            .Where(s => s.JobSeekerId == jobSeekerId)
            .Select(s => s.Name)
            .ToListAsync(cancellationToken);

        var toAdd = names
            .Where(n => !existing.Contains(n, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (toAdd.Count == 0)
        {
            return Result.Failure<int>("Those skills are already on your profile.");
        }

        foreach (var name in toAdd)
        {
            await _skills.AddAsync(new Skill
            {
                JobSeekerId = jobSeekerId,
                Name = name,
                Proficiency = SkillProficiency.Intermediate
            }, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success(toAdd.Count);
    }

    public async Task<Result> RemoveSkillAsync(int skillId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var skill = await _skills.GetByIdAsync(skillId, cancellationToken);
        if (skill is null || skill.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Skill not found.");
        }

        _skills.Remove(skill);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<int>> AddCertificationAsync(int jobSeekerId, CertificationDto model, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Candidate profile not found.");
        }

        if (string.IsNullOrWhiteSpace(model.Name) || string.IsNullOrWhiteSpace(model.IssuingOrganization))
        {
            return Result.Failure<int>("Certification name and issuing organisation are required.");
        }

        var certification = new Certification
        {
            JobSeekerId = jobSeekerId,
            Name = model.Name.Trim(),
            IssuingOrganization = model.IssuingOrganization.Trim(),
            IssueDate = model.IssueDate,
            CredentialId = model.CredentialId?.Trim()
        };

        await _certifications.AddAsync(certification, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success(certification.Id);
    }

    public async Task<Result> RemoveCertificationAsync(int certificationId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var certification = await _certifications.GetByIdAsync(certificationId, cancellationToken);
        if (certification is null || certification.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Certification not found.");
        }

        _certifications.Remove(certification);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success();
    }

    public async Task<Result<int>> AddLanguageAsync(int jobSeekerId, LanguageDto model, CancellationToken cancellationToken = default)
    {
        if (!await _jobSeekers.AnyAsync(s => s.Id == jobSeekerId, cancellationToken))
        {
            return Result.Failure<int>("Candidate profile not found.");
        }

        if (string.IsNullOrWhiteSpace(model.Name))
        {
            return Result.Failure<int>("Enter a language name.");
        }

        var language = new Language
        {
            JobSeekerId = jobSeekerId,
            Name = model.Name.Trim(),
            Proficiency = model.Proficiency
        };

        await _languages.AddAsync(language, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success(language.Id);
    }

    public async Task<Result> RemoveLanguageAsync(int languageId, int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var language = await _languages.GetByIdAsync(languageId, cancellationToken);
        if (language is null || language.JobSeekerId != jobSeekerId)
        {
            return Result.Failure("Language not found.");
        }

        _languages.Remove(language);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        return Result.Success();
    }

    public async Task<ProfileCompletionDto> RecalculateCompletionAsync(int jobSeekerId, CancellationToken cancellationToken = default)
    {
        var seeker = await _jobSeekers.Query()
            .Where(s => s.Id == jobSeekerId)
            .Select(s => new
            {
                s.Id,
                s.ProfessionalTitle,
                s.Location,
                s.Phone,
                s.About,
                s.YearsOfExperience,
                HasSkills = s.Skills.Any(),
                HasEducation = s.Educations.Any(),
                HasExperience = s.Experiences.Any(),
                HasCertification = s.Certifications.Any(),
                HasLanguage = s.Languages.Any(),
                HasResume = s.Resumes.Any()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (seeker is null)
        {
            return new ProfileCompletionDto { JobSeekerId = jobSeekerId };
        }

        var sections = new List<ProfileSectionDto>
        {
            new() { Key = "title", Label = "Professional title", Weight = 10, Hint = "Tell employers what you do", IsComplete = !string.IsNullOrWhiteSpace(seeker.ProfessionalTitle) },
            new() { Key = "location", Label = "Location", Weight = 8, Hint = "Where you are based", IsComplete = !string.IsNullOrWhiteSpace(seeker.Location) },
            new() { Key = "phone", Label = "Phone number", Weight = 5, Hint = "Add a contact number", IsComplete = !string.IsNullOrWhiteSpace(seeker.Phone) },
            new() { Key = "about", Label = "About you", Weight = 12, Hint = "At least 80 characters", IsComplete = !string.IsNullOrWhiteSpace(seeker.About) && seeker.About!.Length >= 80 },
            new() { Key = "skills", Label = "Skills", Weight = 20, Hint = "Add at least one skill", IsComplete = seeker.HasSkills },
            new() { Key = "experience", Label = "Work experience", Weight = 20, Hint = "Add a role you have held", IsComplete = seeker.HasExperience },
            new() { Key = "education", Label = "Education", Weight = 15, Hint = "Add your qualification", IsComplete = seeker.HasEducation },
            new() { Key = "resume", Label = "Resume / CV", Weight = 8, Hint = "Upload a PDF, DOC or DOCX", IsComplete = seeker.HasResume },
            new() { Key = "certifications", Label = "Certifications", Weight = 1, Hint = "Optional but helpful", IsComplete = seeker.HasCertification },
            new() { Key = "languages", Label = "Languages", Weight = 1, Hint = "Optional but helpful", IsComplete = seeker.HasLanguage }
        };

        var earned = sections.Sum(s => s.IsComplete ? s.Weight : 0);
        var percentage = Math.Clamp(earned, 0, 100);

        var dto = new ProfileCompletionDto
        {
            JobSeekerId = jobSeekerId,
            Percentage = percentage,
            Sections = sections,
            NextSteps = sections.Where(s => !s.IsComplete)
                .OrderByDescending(s => s.Weight)
                .Select(s => $"Add {s.Label.ToLowerInvariant()}")
                .Take(4)
                .ToList()
        };

        var entity = await _jobSeekers.GetByIdAsync(jobSeekerId, cancellationToken);
        if (entity is not null)
        {
            entity.UpdatedDate = DateTime.UtcNow;
            _jobSeekers.Update(entity);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return dto;
    }

    private async Task<decimal> CalculateTotalYearsAsync(int jobSeekerId, CancellationToken cancellationToken)
    {
        var ranges = await _experiences.Query()
            .Where(x => x.JobSeekerId == jobSeekerId)
            .Select(x => new { x.StartDate, x.EndDate, x.IsCurrent })
            .ToListAsync(cancellationToken);

        var totalDays = ranges.Sum(range =>
        {
            var end = range.IsCurrent || !range.EndDate.HasValue ? DateTime.UtcNow : range.EndDate.Value;
            return Math.Max(0, (end - range.StartDate).TotalDays);
        });

        return Math.Round((decimal)(totalDays / 365.25), 1);
    }
}
