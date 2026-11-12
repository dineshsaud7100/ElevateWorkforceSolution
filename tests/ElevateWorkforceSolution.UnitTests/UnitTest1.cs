using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;

namespace ElevateWorkforceSolution.UnitTests;

public class StatusRulesTests
{
    [Fact]
    public void IsPubliclyVisible_OnlyActiveVacancies_AreVisible()
    {
        Assert.True(StatusRules.IsPubliclyVisible(VacancyStatus.Active));
        Assert.False(StatusRules.IsPubliclyVisible(VacancyStatus.PendingVerification));
        Assert.False(StatusRules.IsPubliclyVisible(VacancyStatus.Rejected));
    }

    [Fact]
    public void AcceptsApplications_OnlyActiveVacancies_AcceptNewApplications()
    {
        Assert.True(StatusRules.AcceptsApplications(VacancyStatus.Active));
        Assert.False(StatusRules.AcceptsApplications(VacancyStatus.Closed));
        Assert.False(StatusRules.AcceptsApplications(VacancyStatus.Expired));
    }

    [Fact]
    public void EmployerCanTransition_AppliedToUnderReview_AndShortlistedToInterview()
    {
        Assert.True(StatusRules.IsValidEmployerTransition(ApplicationStatus.Applied, ApplicationStatus.UnderReview));
        Assert.True(StatusRules.IsValidEmployerTransition(ApplicationStatus.Shortlisted, ApplicationStatus.Interview));
        Assert.False(StatusRules.IsValidEmployerTransition(ApplicationStatus.Selected, ApplicationStatus.UnderReview));
    }
}

public class JobWorkflowTests
{
    [Fact]
    public void JobVisibility_AndApplicationState_AreDerivedFromVacancyStatus()
    {
        var job = new Job
        {
            Status = VacancyStatus.Active,
            ApplicationDeadline = DateTime.UtcNow.AddDays(7)
        };

        Assert.True(job.IsPublic);
        Assert.True(job.AcceptsApplications);
        Assert.False(job.IsDeadlinePassed);

        job.Status = VacancyStatus.PendingVerification;
        Assert.False(job.IsPublic);
        Assert.False(job.AcceptsApplications);
    }

    [Fact]
    public void InitialVacancyStatus_AlwaysRequiresAdministratorApproval()
    {
        Assert.Equal(VacancyStatus.PendingVerification, StatusRules.InitialVacancyStatus());
    }
}

public class ApplicationWorkflowTests
{
    [Fact]
    public void ApplicationOpenState_ReflectsPipelineProgress()
    {
        var application = new Application { Status = ApplicationStatus.Applied };
        Assert.True(application.IsOpen);

        application.Status = ApplicationStatus.Rejected;
        Assert.False(application.IsOpen);
    }
}
