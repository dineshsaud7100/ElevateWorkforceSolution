using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElevateWorkforceSolution.Infrastructure.Data.Configurations;

public class EducationConfiguration : IEntityTypeConfiguration<Education>
{
    public void Configure(EntityTypeBuilder<Education> builder)
    {
        builder.ToTable("Educations");

        builder.Property(e => e.Institution).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Degree).HasMaxLength(150).IsRequired();
        builder.Property(e => e.FieldOfStudy).HasMaxLength(150);
        builder.Property(e => e.Grade).HasMaxLength(50);
        builder.Property(e => e.Description).HasMaxLength(1000);

        builder.HasIndex(e => e.JobSeekerId);

        builder.HasOne(e => e.JobSeeker)
            .WithMany(j => j.Educations)
            .HasForeignKey(e => e.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class ExperienceConfiguration : IEntityTypeConfiguration<Experience>
{
    public void Configure(EntityTypeBuilder<Experience> builder)
    {
        builder.ToTable("Experiences");

        builder.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Position).HasMaxLength(200).IsRequired();
        builder.Property(e => e.Location).HasMaxLength(100);
        builder.Property(e => e.Description).HasMaxLength(2000);

        builder.HasIndex(e => e.JobSeekerId);

        builder.HasOne(e => e.JobSeeker)
            .WithMany(j => j.Experiences)
            .HasForeignKey(e => e.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills");

        builder.Property(s => s.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(s => s.Name);

        builder.HasOne(s => s.JobSeeker)
            .WithMany(j => j.Skills)
            .HasForeignKey(s => s.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CertificationConfiguration : IEntityTypeConfiguration<Certification>
{
    public void Configure(EntityTypeBuilder<Certification> builder)
    {
        builder.ToTable("Certifications");

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.IssuingOrganization).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CredentialId).HasMaxLength(120);
        builder.Property(c => c.Description).HasMaxLength(1000);

        builder.HasIndex(c => c.JobSeekerId);

        builder.HasOne(c => c.JobSeeker)
            .WithMany(j => j.Certifications)
            .HasForeignKey(c => c.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("Languages");

        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();
        builder.Property(l => l.IsoCodes).HasMaxLength(200);

        builder.HasIndex(l => l.JobSeekerId);

        builder.HasOne(l => l.JobSeeker)
            .WithMany(j => j.Languages)
            .HasForeignKey(l => l.JobSeekerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
