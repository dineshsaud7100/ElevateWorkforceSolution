using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;

namespace ElevateWorkforceSolution.Web.Data;

/// <summary>
/// Loads development-only data: one administrator, several employers with company profiles,
/// a pool of candidates, and vacancies spread across the statuses the workflow uses so search,
/// filtering, pagination and the moderation queue all have something real to show.
/// </summary>
public static class DevelopmentSeeder
{
    public static async Task SeedAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SeedOptions options,
        ILogger logger)
    {
        logger.LogInformation("Seeding Elevate Workforce Solution development data.");

        await CreateAdministratorAsync(context, userManager, options, logger);

        var employers = await CreateEmployersAsync(context, userManager, options, logger);
        var candidates = await CreateCandidatesAsync(context, userManager, options, logger);
        var jobs = await CreateVacanciesAsync(context, employers, logger);
        await CreateApplicationsAsync(context, candidates, jobs, logger);
        await CreateNotificationsAsync(context, candidates, jobs, logger);

        logger.LogInformation(
            "Seed complete: {Employers} employers, {Candidates} candidates, {Jobs} vacancies.",
            employers.Count, candidates.Count, jobs.Count);
    }

    private static async Task CreateAdministratorAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SeedOptions options,
        ILogger logger)
    {
        // Administrators exist only through this controlled path - registration never assigns this role.
        if (await userManager.FindByEmailAsync(options.AdministratorEmail) is not null)
        {
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = options.AdministratorEmail,
            Email = options.AdministratorEmail,
            EmailConfirmed = true,
            FirstName = "Platform",
            LastName = "Administrator",
            PhoneNumber = "+977-01-5550100",
            CreatedDate = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, options.AdministratorPassword);
        Ensure(result, "administrator");

        await userManager.AddToRoleAsync(admin, Roles.Administrator);

        logger.LogInformation("Created administrator account {Email}.", options.AdministratorEmail);
    }

    private static async Task<List<Employer>> CreateEmployersAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SeedOptions options,
        ILogger logger)
    {
        var companies = new (string Name, string Industry, string Location, string Email, int Size, int Founded, string About)[]
        {
            ("Himalaya Software Labs", "Information Technology", "Lalitpur", "himalaya@elevateworkforce.com", 120, 2015,
                "Himalaya Software Labs builds logistics and inventory platforms for Nepali manufacturers and exporters."),
            ("Sagarmatha Fintech", "Financial Services", "Kathmandu", "sagarmatha@elevateworkforce.com", 65, 2019,
                "Sagarmatha Fintech provides digital payments and lending tools to small businesses across Nepal."),
            ("Annapurna Health Systems", "Healthcare", "Kathmandu", "annapurna@elevateworkforce.com", 210, 2011,
                "Annapurna Health Systems operates clinics and hospital information services in the Kathmandu Valley."),
            ("Machhapuchchhre Outfitters", "Retail", "Pokhara", "machhapuchchhre@elevateworkforce.com", 45, 2017,
                "Machhapuchchhre Outfitters designs and sells outdoor equipment manufactured in Pokhara."),
            ("Terai Agro Tech", "Agriculture", "Chitwan", "terai@elevateworkforce.com", 88, 2014,
                "Terai Agro Tech connects farmers in Chitwan with national buyers and cold-chain logistics."),
            ("Everest Digital Media", "Media and Advertising", "Kathmandu", "everestdigital@elevateworkforce.com", 30, 2020,
                "Everest Digital Media runs performance marketing campaigns for consumer brands."),
            ("Karnali Civil Engineering", "Construction", "Butwal", "karnali@elevateworkforce.com", 150, 2009,
                "Karnali Civil Engineering delivers infrastructure projects across the western Terai."),
            ("Biratnagar Textiles", "Manufacturing", "Biratnagar", "biratnagartex@elevateworkforce.com", 260, 1998,
                "Biratnagar Textiles is a vertically integrated yarn and fabric manufacturer.")
        };

        var employers = new List<Employer>();

        for (var i = 0; i < companies.Length; i++)
        {
            var spec = companies[i];
            var email = spec.Email;

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                var existing = await context.Employers.FirstOrDefaultAsync(e => e.User!.Email == email);
                if (existing is not null)
                {
                    employers.Add(existing);
                }

                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = HumaniseRoleTitle(i),
                LastName = "Recruiter",
                PhoneNumber = $"+977-98{(i % 9) + 1}{(1000000 + i * 1111)}",
                CreatedDate = DateTime.UtcNow.AddDays(-(120 - i * 7))
            };

            Ensure(await userManager.CreateAsync(user, "Employer@123"), $"employer {email}");
            await userManager.AddToRoleAsync(user, Roles.Employer);

            var employer = new Employer
            {
                UserId = user.Id,
                CompanyName = spec.Name,
                Designation = "Talent Acquisition Lead",
                CreatedDate = user.CreatedDate,
                IsVerified = true
            };

            context.Employers.Add(employer);
            await context.SaveChangesAsync();

            context.Companies.Add(new Company
            {
                EmployerId = employer.Id,
                Name = spec.Name,
                Industry = spec.Industry,
                Location = spec.Location,
                About = spec.About,
                Description = $"{spec.Name} is hiring across {spec.Industry.ToLowerInvariant()} roles in {spec.Location}.",
                Website = $"https://www.{spec.Name.Replace(" ", "").ToLowerInvariant()}.com.np",
                ContactEmail = email,
                ContactPhone = user.PhoneNumber,
                CompanySize = spec.Size,
                FoundedYear = spec.Founded,
                CreatedDate = user.CreatedDate
            });

            await context.SaveChangesAsync();
            employers.Add(employer);
        }

        return employers;
    }

    private static async Task<List<JobSeeker>> CreateCandidatesAsync(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager,
        SeedOptions options,
        ILogger logger)
    {
        var profiles = new[]
        {
            ("Aarav", "Shrestha", "Senior Backend Developer", "Kathmandu", 6.5m,
                new[] { "C#", ".NET", "PostgreSQL", "Docker", "REST API" },
                "Backend engineer focused on reliable payment and ledger services."),
            ("Sneha", "Adhikari", "Frontend Developer", "Lalitpur", 3.2m,
                new[] { "React", "TypeScript", "CSS", "Accessibility" },
                "Frontend developer who builds accessible, fast user interfaces."),
            ("Bikash", "Tamang", "Data Analyst", "Kathmandu", 2.4m,
                new[] { "SQL", "Python", "Power BI", "Excel" },
                "Analyst turning operational data into clear reporting for decision makers."),
            ("Pratiksha", "Karki", "UI/UX Designer", "Pokhara", 4.0m,
                new[] { "Figma", "Prototyping", "User Research", "Design Systems" },
                "Designer working across research, wireframes and polished interfaces."),
            ("Rohan", "Gurung", "DevOps Engineer", "Biratnagar", 5.1m,
                new[] { "Kubernetes", "Terraform", "AWS", "CI/CD", "Linux" },
                "Infrastructure engineer who automates delivery pipelines."),
            ("Nisha", "Thapa", "HR Officer", "Kathmandu", 2.0m,
                new[] { "Recruitment", "HRIS", "Employee Relations" },
                "HR generalist supporting hiring and employee relations."),
            ("Sanjay", "Rajbhandari", "Cybersecurity Analyst", "Dharan", 3.8m,
                new[] { "Penetration Testing", "SIEM", "Network Security" },
                "Security analyst focused on monitoring and incident response."),
            ("Anisha", "Poudel", "Accountant", "Chitwan", 2.9m,
                new[] { "Accounting", "Taxation", "Tally", "Audit" },
                "Accountant experienced in Nepali tax filing and auditing."),
            ("Dipesh", "Limbu", "Mobile Developer", "Butwal", 4.6m,
                new[] { "Flutter", "Dart", "REST", "Firebase" },
                "Mobile developer shipping cross-platform apps."),
            ("Sarita", "Magar", "Digital Marketing Specialist", "Kathmandu", 3.4m,
                new[] { "SEO", "Google Ads", "Content Marketing", "Analytics" },
                "Marketer running acquisition campaigns for Nepali consumer brands."),
            ("Nabin", "Chettri", "Cloud Engineer", "Kathmandu", 5.8m,
                new[] { "Azure", "Terraform", "Kubernetes", "Networking" },
                "Cloud engineer designing resilient hosting for Nepali businesses."),
            ("Manisha", "Bhattarai", "Customer Support Specialist", "Lalitpur", 1.8m,
                new[] { "Customer Service", "Ticketing", "Nepali", "English" },
                "Support specialist known for calm, clear customer communication.")
        };

        var candidates = new List<JobSeeker>();

        foreach (var (first, last, title, location, years, skills, about) in profiles)
        {
            var email = $"{first.ToLowerInvariant()}.{last.ToLowerInvariant()}@elevateworkforce.com";

            if (await userManager.FindByEmailAsync(email) is not null)
            {
                var existing = await context.JobSeekers.FirstOrDefaultAsync(s => s.User!.Email == email);
                if (existing is not null)
                {
                    candidates.Add(existing);
                }

                continue;
            }

            var user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                FirstName = first,
                LastName = last,
                PhoneNumber = $"+977-98{email.Length}{candidates.Count:D3}",
                CreatedDate = DateTime.UtcNow.AddDays(-(90 - candidates.Count * 6))
            };

            Ensure(await userManager.CreateAsync(user, "Seeker@123"), $"candidate {email}");
            await userManager.AddToRoleAsync(user, Roles.JobSeeker);

            var seeker = new JobSeeker
            {
                UserId = user.Id,
                ProfessionalTitle = title,
                Location = location,
                Phone = user.PhoneNumber,
                About = about,
                YearsOfExperience = years,
                IsOpenToWork = true,
                CreatedDate = user.CreatedDate
            };

            context.JobSeekers.Add(seeker);
            await context.SaveChangesAsync();

            foreach (var skill in skills)
            {
                context.Skills.Add(new Skill
                {
                    JobSeekerId = seeker.Id,
                    Name = skill,
                    Proficiency = SkillProficiency.Advanced
                });
            }

            context.Educations.Add(new Education
            {
                JobSeekerId = seeker.Id,
                Institution = "Tribhuvan University",
                Degree = "Bachelor",
                FieldOfStudy = "Computer Science",
                GraduationYear = 2018 + (candidates.Count % 4)
            });

            context.Experiences.Add(new Experience
            {
                JobSeekerId = seeker.Id,
                CompanyName = "Previous Employer Pvt. Ltd.",
                Position = title,
                Location = location,
                StartDate = DateTime.UtcNow.AddYears(-2),
                IsCurrent = true,
                Description = $"Delivered {title.ToLowerInvariant()} work for a Nepali organisation."
            });

            context.Languages.Add(new Language
            {
                JobSeekerId = seeker.Id,
                Name = "Nepali",
                Proficiency = LanguageProficiency.Native,
                IsoCodes = "ne"
            });

            await context.SaveChangesAsync();
            candidates.Add(seeker);
        }

        return candidates;
    }

    private static async Task<List<Job>> CreateVacanciesAsync(
        ApplicationDbContext context, List<Employer> employers, ILogger logger)
    {
        if (await context.Jobs.AnyAsync())
        {
            return await context.Jobs.Include(j => j.Company).ToListAsync();
        }

        var templates = new (string Title, string Category, JobType Type, WorkArrangement Arrangement, ExperienceLevel Level,
            decimal Min, decimal Max, int Headcount, string[] Skills)[]
        {
            ("Senior Backend Developer (C# / .NET)", "Backend Developer", JobType.FullTime, WorkArrangement.Hybrid, ExperienceLevel.Senior, 120000, 180000, 2, new[] { "C#", ".NET", "PostgreSQL", "Docker", "REST API" }),
            ("Frontend Developer (React)", "Frontend Developer", JobType.FullTime, WorkArrangement.Remote, ExperienceLevel.Mid, 80000, 120000, 2, new[] { "React", "TypeScript", "CSS", "Accessibility" }),
            ("Junior Web Developer", "Web Developer", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Entry, 35000, 50000, 3, new[] { "HTML", "CSS", "JavaScript" }),
            ("UI/UX Designer", "UI/UX Designer", JobType.FullTime, WorkArrangement.Hybrid, ExperienceLevel.Mid, 70000, 100000, 1, new[] { "Figma", "Prototyping", "User Research" }),
            ("Data Analyst", "Data Analyst", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Mid, 60000, 90000, 2, new[] { "SQL", "Python", "Power BI" }),
            ("Data Scientist", "Data Scientist", JobType.FullTime, WorkArrangement.Hybrid, ExperienceLevel.Senior, 140000, 200000, 1, new[] { "Python", "Machine Learning", "Statistics" }),
            ("DevOps Engineer", "DevOps Engineer", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Senior, 130000, 190000, 2, new[] { "Kubernetes", "Terraform", "CI/CD", "Linux" }),
            ("Cloud Engineer (Azure)", "Cloud Engineer", JobType.Contract, WorkArrangement.Remote, ExperienceLevel.Mid, 100000, 150000, 1, new[] { "Azure", "Terraform", "Networking" }),
            ("Cybersecurity Analyst", "Cybersecurity Analyst", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Mid, 90000, 130000, 1, new[] { "Penetration Testing", "SIEM", "Network Security" }),
            ("Digital Marketing Specialist", "Digital Marketing Specialist", JobType.FullTime, WorkArrangement.Hybrid, ExperienceLevel.Mid, 50000, 80000, 2, new[] { "SEO", "Google Ads", "Content Marketing" }),
            ("HR Officer", "HR Officer", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Entry, 30000, 45000, 1, new[] { "Recruitment", "HRIS" }),
            ("Sales Executive", "Sales Executive", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Mid, 40000, 70000, 4, new[] { "Sales", "Negotiation", "CRM" }),
            ("Customer Support Specialist", "Customer Support", JobType.FullTime, WorkArrangement.Remote, ExperienceLevel.Entry, 28000, 40000, 3, new[] { "Customer Service", "Ticketing" }),
            ("Accountant", "Accountant", JobType.FullTime, WorkArrangement.OnSite, ExperienceLevel.Mid, 45000, 65000, 1, new[] { "Accounting", "Taxation", "Tally" }),
            ("Software Engineering Intern", "Software Developer", JobType.Internship, WorkArrangement.OnSite, ExperienceLevel.Entry, 20000, 30000, 5, new[] { "JavaScript", "Git" }),
            ("Mobile Developer (Flutter)", "Software Developer", JobType.FullTime, WorkArrangement.Hybrid, ExperienceLevel.Mid, 90000, 140000, 1, new[] { "Flutter", "Dart", "Firebase" }),
            ("Freelance Frontend Consultant", "Frontend Developer", JobType.Freelance, WorkArrangement.Remote, ExperienceLevel.Senior, 0, 0, 1, new[] { "React", "TypeScript" }),
            ("Part-Time Accountant", "Accountant", JobType.PartTime, WorkArrangement.OnSite, ExperienceLevel.Entry, 25000, 35000, 1, new[] { "Accounting", "Excel" }),
            ("Temporary Warehouse Supervisor", "Sales Executive", JobType.Temporary, WorkArrangement.OnSite, ExperienceLevel.Entry, 22000, 30000, 2, new[] { "Leadership", "Inventory" }),
            ("Backend Developer (Go)", "Backend Developer", JobType.Contract, WorkArrangement.Remote, ExperienceLevel.Mid, 110000, 160000, 1, new[] { "Go", "PostgreSQL", "gRPC" })
        };

        var jobs = new List<Job>();
        var now = DateTime.UtcNow;
        var statusCycle = new[]
        {
            VacancyStatus.Active, VacancyStatus.Active, VacancyStatus.Active, VacancyStatus.Active,
            VacancyStatus.Active, VacancyStatus.Active, VacancyStatus.PendingVerification,
            VacancyStatus.PendingVerification, VacancyStatus.Rejected, VacancyStatus.Closed
        };

        for (var i = 0; i < templates.Length && employers.Count > 0; i++)
        {
            var template = templates[i];
            var employer = employers[i % employers.Count];
            var company = await context.Companies.FirstOrDefaultAsync(c => c.EmployerId == employer.Id);

            if (company is null)
            {
                continue;
            }

            var status = statusCycle[i % statusCycle.Length];
            var postedDate = now.AddDays(-(i * 3 + 1));
            var location = NepalReferenceData.Locations[i % NepalReferenceData.Locations.Count];

            var job = new Job
            {
                EmployerId = employer.Id,
                CompanyId = company.Id,
                Title = template.Title,
                Category = template.Category,
                Description = BuildDescription(template.Title, company.Name, location),
                Responsibilities = $"Work with the {template.Category} team in {location} to deliver and maintain production systems. " +
                                   "Review code, take part in planning, and document the work you hand over.",
                Requirements = "Relevant experience in the listed skills, clear written Nepali and English, " +
                               "and comfort working with a team across review cycles.",
                Benefits = "Five-day working week, festival bonus, health insurance and paid training budget.",
                JobType = template.Type,
                WorkArrangement = template.Arrangement,
                ExperienceLevel = template.Level,
                Headcount = template.Headcount,
                SalaryMin = template.Min > 0 ? template.Min : null,
                SalaryMax = template.Max > 0 ? template.Max : null,
                SalaryCurrency = "NPR",
                SalaryPeriod = "Monthly",
                WorkLocation = location,
                ApplicationDeadline = status is VacancyStatus.Active or VacancyStatus.PendingVerification
                    ? postedDate.AddDays(30)
                    : postedDate.AddDays(20),
                PostedDate = postedDate,
                Status = status,
                VerifiedDate = status is VacancyStatus.Active or VacancyStatus.Rejected
                    ? postedDate.AddHours(6)
                    : null,
                ModerationNotes = status == VacancyStatus.Rejected
                    ? "The salary range and headcount need to be confirmed before this vacancy can be published."
                    : null
            };

            foreach (var skill in template.Skills)
            {
                job.RequiredSkills.Add(new JobRequiredSkill { Job = job, Name = skill });
            }

            context.Jobs.Add(job);
            await context.SaveChangesAsync();
            jobs.Add(job);
        }

        logger.LogInformation("Seeded {Count} vacancies across all workflow statuses.", jobs.Count);
        return jobs;
    }

    private static async Task CreateApplicationsAsync(
        ApplicationDbContext context, List<JobSeeker> candidates, List<Job> jobs, ILogger logger)
    {
        if (await context.Applications.AnyAsync())
        {
            return;
        }

        var activeJobs = jobs.Where(j => j.Status == VacancyStatus.Active).ToList();
        if (activeJobs.Count == 0 || candidates.Count == 0)
        {
            return;
        }

        var statusPlan = new[]
        {
            ApplicationStatus.Applied,
            ApplicationStatus.UnderReview,
            ApplicationStatus.Shortlisted,
            ApplicationStatus.Interview,
            ApplicationStatus.Selected,
            ApplicationStatus.Rejected,
            ApplicationStatus.Applied
        };

        var seeded = 0;

        for (var i = 0; i < candidates.Count && i < activeJobs.Count; i++)
        {
            var candidate = candidates[i];
            var job = activeJobs[i];

            // Applications need a resume record, so generate a small placeholder document.
            var resume = new Resume
            {
                JobSeekerId = candidate.Id,
                OriginalFileName = $"{candidate.User?.FirstName ?? "candidate"}-{candidate.Id}-cv.pdf",
                StoredFileName = $"seed-{candidate.Id}-{Guid.NewGuid():N}.pdf",
                ContentType = "application/pdf",
                FileSizeBytes = 48_000 + (i * 512),
                Label = "Primary CV",
                UploadedDate = DateTime.UtcNow.AddDays(-(i + 2))
            };

            context.Resumes.Add(resume);
            await context.SaveChangesAsync();

            var status = statusPlan[i % statusPlan.Length];
            var appliedDate = DateTime.UtcNow.AddDays(-(i + 1));

            var application = new ApplicationEntity
            {
                JobId = job.Id,
                JobSeekerId = candidate.Id,
                EmployerId = job.EmployerId,
                ResumeId = resume.Id,
                Status = status,
                CoverLetter = $"I am writing to apply for the {job.Title} role. " +
                              "My experience lines up with the responsibilities described, and I would welcome the chance to discuss the team.",
                AppliedDate = appliedDate,
                LastUpdated = appliedDate.AddDays(1),
                CompatibilityScore = 55 + ((i * 7) % 40)
            };

            application.StatusHistory.Add(new ApplicationStatusHistory
            {
                Status = ApplicationStatus.Applied,
                Remarks = "Application submitted.",
                ChangedDate = appliedDate
            });

            if (status != ApplicationStatus.Applied)
            {
                application.StatusHistory.Add(new ApplicationStatusHistory
                {
                    Status = status,
                    Remarks = $"Moved to {status.ToString().ToLowerInvariant()} by the hiring team.",
                    ChangedDate = appliedDate.AddDays(1)
                });
            }

            context.Applications.Add(application);
            await context.SaveChangesAsync();
            seeded++;

            context.SavedJobs.Add(new SavedJob
            {
                JobSeekerId = candidate.Id,
                JobId = job.Id,
                SavedDate = appliedDate.AddHours(-6)
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} applications with status history.", seeded);
    }

    private static async Task CreateNotificationsAsync(
        ApplicationDbContext context, List<JobSeeker> candidates, List<Job> jobs, ILogger logger)
    {
        if (await context.Notifications.AnyAsync())
        {
            return;
        }

        foreach (var candidate in candidates)
        {
            var userId = candidate.UserId;
            var candidateName = candidate.User?.FirstName ?? "there";

            context.Notifications.Add(new Notification
            {
                UserId = userId,
                Title = "Profile tips",
                Message = $"Hello {candidateName}. Add a resume and at least five skills to appear in employer searches.",
                Type = NotificationType.ApplicationStatusChanged,
                CreatedDate = DateTime.UtcNow.AddDays(-5),
                IsRead = false
            });
        }

        await context.SaveChangesAsync();
        logger.LogInformation("Seeded starter notifications for candidates.");
    }

    private static string BuildDescription(string title, string company, string location) =>
        $"{company} is hiring a {title} to join our team in {location}. " +
        "You will work on real customer-facing products alongside engineers and designers, " +
        "with a clear path to owning features from design through to release.";

    private static string HumaniseRoleTitle(int index) => new[]
    {
        "Anisha", "Bishal", "Chandra", "Deepa", "Eak", "Gauri", "Hari", "Ishwor"
    }[index % 8];

    private static void Ensure(IdentityResult result, string subject)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the {subject}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }
    }
}
