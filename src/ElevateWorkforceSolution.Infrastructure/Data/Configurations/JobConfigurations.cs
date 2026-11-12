using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElevateWorkforceSolution.Infrastructure.Data.Configurations;

public class JobConfiguration : IEntityTypeConfiguration<Job>
{
    public void Configure(EntityTypeBuilder<Job> builder)
    {
        builder.ToTable("Jobs");

        builder.Property(j => j.Title).HasMaxLength(200).IsRequired();
        builder.Property(j => j.Category).HasMaxLength(150);
        builder.Property(j => j.Description).HasMaxLength(8000);
        builder.Property(j => j.Responsibilities).HasMaxLength(5000);
        builder.Property(j => j.Requirements).HasMaxLength(5000);
        builder.Property(j => j.Benefits).HasMaxLength(3000);
        builder.Property(j => j.WorkLocation).HasMaxLength(100);
        builder.Property(j => j.SalaryCurrency).HasMaxLength(10);
        builder.Property(j => j.SalaryPeriod).HasMaxLength(20);
        builder.Property(j => j.VerifiedByUserId).HasMaxLength(450);
        builder.Property(j => j.ModerationNotes).HasMaxLength(2000);

        builder.Property(j => j.SalaryMin).HasPrecision(18, 2);
        builder.Property(j => j.SalaryMax).HasPrecision(18, 2);

        // Supports the public job search: filter by status/category/location, sort by date or salary.
        builder.HasIndex(j => new { j.Status, j.PostedDate });
        builder.HasIndex(j => new { j.Status, j.WorkLocation });
        builder.HasIndex(j => new { j.Status, j.Category });
        builder.HasIndex(j => j.Status);
        builder.HasIndex(j => j.SalaryMax);

        builder.HasOne(j => j.Employer)
            .WithMany(e => e.Jobs)
            .HasForeignKey(j => j.EmployerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(j => j.Company)
            .WithMany(c => c.Jobs)
            .HasForeignKey(j => j.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class JobRequiredSkillConfiguration : IEntityTypeConfiguration<JobRequiredSkill>
{
    public void Configure(EntityTypeBuilder<JobRequiredSkill> builder)
    {
        builder.ToTable("JobRequiredSkills");

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(s => new { s.JobId, s.Name }).IsUnique();

        builder.HasOne(s => s.Job)
            .WithMany(j => j.RequiredSkills)
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SavedJobConfiguration : IEntityTypeConfiguration<SavedJob>
{
    public void Configure(EntityTypeBuilder<SavedJob> builder)
    {
        builder.ToTable("SavedJobs");

        // Database-level guarantee that a candidate cannot bookmark the same vacancy twice.
        builder.HasIndex(s => new { s.JobSeekerId, s.JobId }).IsUnique();

        builder.HasOne(s => s.JobSeeker)
            .WithMany(j => j.SavedJobs)
            .HasForeignKey(s => s.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(s => s.Job)
            .WithMany(j => j.SavedJobs)
            .HasForeignKey(s => s.JobId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");

        builder.Property(n => n.Title).HasMaxLength(200).IsRequired();
        builder.Property(n => n.Message).HasMaxLength(1000).IsRequired();
        builder.Property(n => n.RelatedEntityId).HasMaxLength(50);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(50);

        // Notifications are always read scoped to a user, most-recent first.
        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedDate });
        builder.HasIndex(n => new { n.UserId, n.CreatedDate });

        builder.HasOne(n => n.User)
            .WithMany(u => u.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
