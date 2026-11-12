using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Application.Interfaces;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElevateWorkforceSolution.Web.Controllers;

/// <summary>
/// Candidate workspace: dashboard, profile sections, skills, resume uploads and the
/// rule-based job recommendations.
/// </summary>
[Authorize(Policy = "JobSeekerOnly")]
public class CandidateController : ApplicationControllerBase
{
    private const long MaxResumeBytes = 5 * 1024 * 1024;

    private readonly IDashboardService _dashboard;
    private readonly ICandidateProfileService _profiles;
    private readonly IResumeService _resumes;
    private readonly ISkillMatchService _skillMatch;

    public CandidateController(
        ICurrentUserService currentUser,
        IDashboardService dashboard,
        ICandidateProfileService profiles,
        IResumeService resumes,
        ISkillMatchService skillMatch)
        : base(currentUser)
    {
        _dashboard = dashboard;
        _profiles = profiles;
        _resumes = resumes;
        _skillMatch = skillMatch;
    }

    public async Task<IActionResult> Dashboard(CancellationToken cancellationToken)
    {
        var id = await RequireJobSeekerIdAsync();
        if (id is null)
        {
            return RedirectToAction(nameof(Profile));
        }

        var dashboard = await _dashboard.GetForJobSeekerAsync(id.Value, cancellationToken);
        var completion = await _profiles.RecalculateCompletionAsync(id.Value, cancellationToken);

        return View(new CandidateDashboardViewModel { Dashboard = dashboard, Completion = completion });
    }

    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        var id = await RequireJobSeekerIdAsync();
        if (id is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var profile = await _profiles.GetAsync(id.Value, cancellationToken);
        if (profile is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var completion = await _profiles.RecalculateCompletionAsync(id.Value, cancellationToken);

        return View(new CandidateProfileViewModel
        {
            Profile = profile,
            Completion = completion,
            Basics = new CandidateBasicsDto
            {
                ProfessionalTitle = profile.ProfessionalTitle,
                Location = profile.Location,
                Phone = profile.Phone,
                About = profile.About,
                YearsOfExperience = profile.YearsOfExperience,
                IsOpenToWork = true
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateBasics(int jobSeekerId, CandidateBasicsDto model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            FlashError("Please correct the highlighted fields.");
            return RedirectToAction(nameof(Profile));
        }

        var result = await _profiles.UpdateBasicsAsync(jobSeekerId, model, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);
            FlashSuccess("Your profile basics have been saved.");
        }
        else
        {
            FlashError(result.Error ?? "We could not save your profile basics.");
        }

        return RedirectToAction(nameof(Profile));
    }

    public async Task<IActionResult> Recommendations(CancellationToken cancellationToken)
    {
        var id = await RequireJobSeekerIdAsync();
        if (id is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var jobs = await _skillMatch.RecommendAsync(id.Value, 12, cancellationToken);
        return View(jobs);
    }

    // ---------- Education ----------

    [HttpGet]
    public async Task<IActionResult> AddEducation(int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        return View("EducationForm", new EducationFormViewModel { JobSeekerId = jobSeekerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEducation(EducationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(model.JobSeekerId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("EducationForm", model);
        }

        var result = await _profiles.AddEducationAsync(model.JobSeekerId, new EducationDto
        {
            Institution = model.Institution.Trim(),
            Degree = model.Degree.Trim(),
            FieldOfStudy = model.FieldOfStudy?.Trim(),
            GraduationYear = model.GraduationYear,
            Grade = model.Grade?.Trim(),
            Description = model.Description?.Trim()
        }, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(model.JobSeekerId, cancellationToken);
            FlashSuccess("Education added to your profile.");
        }
        else
        {
            FlashError(result.Error ?? "We could not add that education entry.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveEducation(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.RemoveEducationAsync(id, jobSeekerId, cancellationToken);
        await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Education entry removed.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that entry.");
        }

        return RedirectToAction(nameof(Profile));
    }

    // ---------- Experience ----------

    [HttpGet]
    public async Task<IActionResult> AddExperience(int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        return View("ExperienceForm", new ExperienceFormViewModel { JobSeekerId = jobSeekerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddExperience(ExperienceFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(model.JobSeekerId))
        {
            return Forbid();
        }

        if (model.IsCurrent)
        {
            model.EndDate = null;
        }

        if (!ModelState.IsValid)
        {
            return View("ExperienceForm", model);
        }

        var result = await _profiles.AddExperienceAsync(model.JobSeekerId, new ExperienceDto
        {
            CompanyName = model.CompanyName.Trim(),
            Position = model.Position.Trim(),
            Location = model.Location?.Trim(),
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            IsCurrent = model.IsCurrent,
            Description = model.Description?.Trim()
        }, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(model.JobSeekerId, cancellationToken);
            FlashSuccess("Work experience added to your profile.");
        }
        else
        {
            FlashError(result.Error ?? "We could not add that experience entry.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveExperience(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.RemoveExperienceAsync(id, jobSeekerId, cancellationToken);
        await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Experience entry removed.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that entry.");
        }

        return RedirectToAction(nameof(Profile));
    }

    // ---------- Skills ----------

    [HttpGet]
    public async Task<IActionResult> Skills(int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var profile = await _profiles.GetAsync(jobSeekerId, cancellationToken);
        if (profile is null)
        {
            return RedirectToAction(nameof(Profile));
        }

        return View(new SkillsFormViewModel { JobSeekerId = jobSeekerId, Skills = profile.Skills });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddSkills(SkillsFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(model.JobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.AddSkillsAsync(model.JobSeekerId, model.NewSkills, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(model.JobSeekerId, cancellationToken);
            FlashSuccess(result.Value > 0
                ? $"Added {result.Value} skill{(result.Value == 1 ? string.Empty : "s")}."
                : "No new skills were added. Some may already be on your profile.");
        }
        else
        {
            FlashError(result.Error ?? "We could not add those skills.");
        }

        return RedirectToAction(nameof(Skills), new { jobSeekerId = model.JobSeekerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveSkill(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.RemoveSkillAsync(id, jobSeekerId, cancellationToken);
        await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Skill removed.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that skill.");
        }

        return RedirectToAction(nameof(Skills), new { jobSeekerId });
    }

    // ---------- Certifications ----------

    [HttpGet]
    public async Task<IActionResult> AddCertification(int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        return View("CertificationForm", new CertificationFormViewModel { JobSeekerId = jobSeekerId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCertification(CertificationFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(model.JobSeekerId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View("CertificationForm", model);
        }

        var result = await _profiles.AddCertificationAsync(model.JobSeekerId, new CertificationDto
        {
            Name = model.Name.Trim(),
            IssuingOrganization = model.IssuingOrganization.Trim(),
            IssueDate = model.IssueDate,
            CredentialId = model.CredentialId?.Trim()
        }, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(model.JobSeekerId, cancellationToken);
            FlashSuccess("Certification added to your profile.");
        }
        else
        {
            FlashError(result.Error ?? "We could not add that certification.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveCertification(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.RemoveCertificationAsync(id, jobSeekerId, cancellationToken);
        await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Certification removed.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that certification.");
        }

        return RedirectToAction(nameof(Profile));
    }

    // ---------- Languages ----------

    [HttpGet]
    public async Task<IActionResult> AddLanguage(int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        return View("LanguageForm", new LanguageFormViewModel
        {
            JobSeekerId = jobSeekerId,
            ProficiencyOptions = SelectListFactory.LanguageProficiencies()
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLanguage(LanguageFormViewModel model, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(model.JobSeekerId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            model.ProficiencyOptions = SelectListFactory.LanguageProficiencies(model.Proficiency);
            return View("LanguageForm", model);
        }

        var result = await _profiles.AddLanguageAsync(model.JobSeekerId, new LanguageDto
        {
            Name = model.Name.Trim(),
            Proficiency = model.Proficiency
        }, cancellationToken);

        if (result.Succeeded)
        {
            await _profiles.RecalculateCompletionAsync(model.JobSeekerId, cancellationToken);
            FlashSuccess("Language added to your profile.");
        }
        else
        {
            FlashError(result.Error ?? "We could not add that language.");
        }

        return RedirectToAction(nameof(Profile));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveLanguage(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _profiles.RemoveLanguageAsync(id, jobSeekerId, cancellationToken);
        await _profiles.RecalculateCompletionAsync(jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Language removed.");
        }
        else
        {
            FlashError(result.Error ?? "We could not remove that language.");
        }

        return RedirectToAction(nameof(Profile));
    }

    // ---------- Resumes ----------

    public async Task<IActionResult> Resumes(CancellationToken cancellationToken)
    {
        var id = await RequireJobSeekerIdAsync();
        if (id is null)
        {
            return RedirectToAction(nameof(Dashboard));
        }

        var resumes = await _resumes.ListForCandidateAsync(id.Value, cancellationToken);

        return View(new ResumeListViewModel { JobSeekerId = id.Value, Resumes = resumes.ToList() });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxResumeBytes)]
    public async Task<IActionResult> UploadResume(int jobSeekerId, IFormFile file, string? label, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        if (file is null || file.Length == 0)
        {
            FlashError("Choose a file to upload.");
            return RedirectToAction(nameof(Resumes));
        }

        if (file.Length > MaxResumeBytes)
        {
            FlashError("That file is larger than 5 MB. Please upload a smaller document.");
            return RedirectToAction(nameof(Resumes));
        }

        var allowed = new[] { "application/pdf", "application/msword", "application/vnd.openxmlformats-officedocument.wordprocessingml.document" };
        var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
        var extensionAllowed = new[] { ".pdf", ".doc", ".docx" }.Contains(Path.GetExtension(file.FileName).ToLowerInvariant());

        if ((!allowed.Contains(contentType) && contentType.Length > 0) || !extensionAllowed)
        {
            FlashError("Only PDF, DOC and DOCX files are accepted.");
            return RedirectToAction(nameof(Resumes));
        }

        await using var stream = file.OpenReadStream();

        var result = await _resumes.UploadAsync(
            jobSeekerId,
            stream,
            Path.GetFileName(file.FileName),
            file.Length,
            contentType.Length == 0 ? "application/octet-stream" : contentType,
            label?.Trim(),
            cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Resume uploaded.");
        }
        else
        {
            FlashError(result.Error ?? "We could not upload that resume.");
        }

        return RedirectToAction(nameof(Resumes));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteResume(int id, int jobSeekerId, CancellationToken cancellationToken)
    {
        if (!await OwnsProfileAsync(jobSeekerId))
        {
            return Forbid();
        }

        var result = await _resumes.DeleteAsync(id, jobSeekerId, cancellationToken);

        if (result.Succeeded)
        {
            FlashSuccess("Resume deleted.");
        }
        else
        {
            FlashError(result.Error ?? "We could not delete that resume.");
        }

        return RedirectToAction(nameof(Resumes));
    }

    /// <summary>
    /// Prevents a candidate editing or removing another candidate's profile rows by posting a
    /// different jobSeekerId to the form.
    /// </summary>
    private async Task<bool> OwnsProfileAsync(int jobSeekerId)
    {
        var current = await CurrentUser.GetJobSeekerIdAsync(HttpContext.RequestAborted);
        return current is not null && current.Value == jobSeekerId;
    }
}
