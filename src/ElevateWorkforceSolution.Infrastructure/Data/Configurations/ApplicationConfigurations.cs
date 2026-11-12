using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using ApplicationEntity = ElevateWorkforceSolution.Domain.Entities.Application;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElevateWorkforceSolution.Infrastructure.Data.Configurations;

public class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.ToTable("Applications");

        builder.Property(a => a.CoverLetter).HasMaxLength(4000);
        builder.Property(a => a.EmployerNotes).HasMaxLength(2000);

        // Core database rule: one candidate cannot apply to the same vacancy twice.
        builder.HasIndex(a => new { a.JobId, a.JobSeekerId }).IsUnique();

        builder.HasIndex(a => new { a.JobSeekerId, a.Status });
        builder.HasIndex(a => new { a.EmployerId, a.Status });
        builder.HasIndex(a => new { a.JobId, a.Status });
        builder.HasIndex(a => a.AppliedDate);

        builder.HasOne(a => a.Job)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.JobSeeker)
            .WithMany(j => j.Applications)
            .HasForeignKey(a => a.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Employer)
            .WithMany()
            .HasForeignKey(a => a.EmployerId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Resume)
            .WithMany()
            .HasForeignKey(a => a.ResumeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class ApplicationStatusHistoryConfiguration : IEntityTypeConfiguration<ApplicationStatusHistory>
{
    public void Configure(EntityTypeBuilder<ApplicationStatusHistory> builder)
    {
        builder.ToTable("ApplicationStatusHistories");

        builder.Property(h => h.Remarks).HasMaxLength(1000);
        builder.Property(h => h.ChangedByUserId).HasMaxLength(450);

        builder.HasIndex(h => new { h.ApplicationId, h.ChangedDate });

        builder.HasOne(h => h.Application)
            .WithMany(a => a.StatusHistory)
            .HasForeignKey(h => h.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ResumeConfiguration : IEntityTypeConfiguration<Resume>
{
    public void Configure(EntityTypeBuilder<Resume> builder)
    {
        builder.ToTable("Resumes");

        builder.Property(r => r.OriginalFileName).HasMaxLength(255).IsRequired();
        builder.Property(r => r.StoredFileName).HasMaxLength(255).IsRequired();
        builder.Property(r => r.ContentType).HasMaxLength(120).IsRequired();
        builder.Property(r => r.Label).HasMaxLength(120);

        builder.HasIndex(r => new { r.JobSeekerId, r.UploadedDate });

        builder.HasOne(r => r.JobSeeker)
            .WithMany(j => j.Resumes)
            .HasForeignKey(r => r.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
