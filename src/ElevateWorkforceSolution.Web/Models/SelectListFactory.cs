using ElevateWorkforceSolution.Application.DTOs;
using ElevateWorkforceSolution.Domain.Common;
using ElevateWorkforceSolution.Domain.Entities;
using ElevateWorkforceSolution.Domain.Enums;
using ElevateWorkforceSolution.Web.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;
using DomainRoles = ElevateWorkforceSolution.Domain.Common.Roles;

namespace ElevateWorkforceSolution.Web.Models;

/// <summary>
/// Builds the option lists the forms and filters need. Enum lists are generated from the enum
/// itself, so adding a value to an enum is enough to make it appear in the UI.
/// </summary>
public static class SelectListFactory
{
    /// <summary>
    /// Options for a nullable enum filter, where the empty value means "no filter". The label
    /// text comes from the shared display helpers, so wording stays consistent.
    /// </summary>
    public static List<SelectListItem> FilterOptions<TEnum>(TEnum? selected, string anyLabel)
        where TEnum : struct, Enum
    {
        var options = new List<SelectListItem>
        {
            new() { Value = "", Text = anyLabel, Selected = selected is null }
        };

        options.AddRange(Enum.GetValues<TEnum>().Select(v => new SelectListItem
        {
            Value = Convert.ToInt32(v).ToString(),
            Text = v.Display(),
            Selected = selected.HasValue && selected.Value.Equals(v)
        }));

        return options;
    }

    /// <summary>Required (non-nullable) enum options for form fields, with a leading prompt.</summary>
    public static List<SelectListItem> RequiredOptions<TEnum>(TEnum selected, string prompt)
        where TEnum : struct, Enum
    {
        var options = new List<SelectListItem>
        {
            new() { Value = "", Text = prompt }
        };

        options.AddRange(Enum.GetValues<TEnum>().Select(v => new SelectListItem
        {
            Value = Convert.ToInt32(v).ToString(),
            Text = v.Display(),
            Selected = selected.Equals(v)
        }));

        return options;
    }

    public static List<SelectListItem> JobTypes(JobType? selected = null, string anyLabel = "Any job type")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> RequiredJobTypes(JobType selected)
        => RequiredOptions(selected, "Select job type");

    public static List<SelectListItem> WorkArrangements(WorkArrangement? selected = null, string anyLabel = "Any arrangement")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> RequiredWorkArrangements(WorkArrangement selected)
        => RequiredOptions(selected, "Select arrangement");

    public static List<SelectListItem> ExperienceLevels(ExperienceLevel? selected = null, string anyLabel = "Any experience level")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> RequiredExperienceLevels(ExperienceLevel selected)
        => RequiredOptions(selected, "Select experience level");

    public static List<SelectListItem> VacancyStatuses(VacancyStatus? selected = null, string anyLabel = "All statuses")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> ApplicationStatuses(ApplicationStatus? selected = null, string anyLabel = "All statuses")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> RoleNames(string? selected = null, string? anyLabel = null)
    {
        var options = DomainRoles.All
            .Select(r => new SelectListItem { Value = r, Text = r, Selected = r == selected })
            .ToList();

        if (anyLabel is not null)
        {
            options.Insert(0, new SelectListItem { Value = "", Text = anyLabel, Selected = string.IsNullOrEmpty(selected) });
        }

        return options;
    }

    /// <summary>Distinct database values for a text filter, de-duplicated and alphabetised.</summary>
    public static List<SelectListItem> Strings(
        IEnumerable<string?> values, string? selected = null, string? anyLabel = null)
    {
        var options = values
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(v => new SelectListItem
            {
                Value = v,
                Text = v,
                Selected = string.Equals(v, selected, StringComparison.OrdinalIgnoreCase)
            })
            .ToList();

        if (anyLabel is not null)
        {
            options.Insert(0, new SelectListItem { Value = "", Text = anyLabel, Selected = string.IsNullOrWhiteSpace(selected) });
        }

        return options;
    }

    public static List<SelectListItem> SkillProficiencies(SkillProficiency? selected = null, string anyLabel = "Select proficiency")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> LanguageProficiencies(LanguageProficiency? selected = null, string anyLabel = "Select proficiency")
        => FilterOptions(selected, anyLabel);

    public static List<SelectListItem> JobSortOptions(JobSortOption? selected = null)
    {
        var labels = new Dictionary<JobSortOption, string>
        {
            [JobSortOption.Newest] = "Newest first",
            [JobSortOption.SalaryHighToLow] = "Salary: high to low",
            [JobSortOption.SalaryLowToHigh] = "Salary: low to high",
            [JobSortOption.ClosingDateSoonest] = "Closing soonest"
        };

        var current = selected ?? JobSortOption.Newest;

        return labels
            .Select(kv => new SelectListItem
            {
                Value = ((int)kv.Key).ToString(),
                Text = kv.Value,
                Selected = kv.Key == current
            })
            .ToList();
    }
}
