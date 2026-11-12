using ElevateWorkforceSolution.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElevateWorkforceSolution.Infrastructure.Data.Configurations;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.LastName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.ProfileImageUrl).HasMaxLength(500);

        builder.HasIndex(u => u.CreatedDate);
    }
}

public class JobSeekerConfiguration : IEntityTypeConfiguration<JobSeeker>
{
    public void Configure(EntityTypeBuilder<JobSeeker> builder)
    {
        builder.ToTable("JobSeekers");

        builder.Property(j => j.ProfessionalTitle).HasMaxLength(150);
        builder.Property(j => j.Location).HasMaxLength(100);
        builder.Property(j => j.Phone).HasMaxLength(30);
        builder.Property(j => j.About).HasMaxLength(4000);

        builder.HasIndex(j => j.UserId).IsUnique();
        builder.HasIndex(j => j.Location);
        builder.HasIndex(j => j.ProfessionalTitle);

        builder.HasOne(j => j.User)
            .WithOne(u => u.JobSeeker)
            .HasForeignKey<JobSeeker>(j => j.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class EmployerConfiguration : IEntityTypeConfiguration<Employer>
{
    public void Configure(EntityTypeBuilder<Employer> builder)
    {
        builder.ToTable("Employers");

        builder.Property(e => e.CompanyName).HasMaxLength(200).IsRequired();
        builder.Property(e => e.BusinessRegistrationNumber).HasMaxLength(100);
        builder.Property(e => e.Designation).HasMaxLength(150);

        builder.HasIndex(e => e.UserId).IsUnique();
        builder.HasIndex(e => e.CompanyName);

        builder.HasOne(e => e.User)
            .WithOne(u => u.Employer)
            .HasForeignKey<Employer>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(e => e.Company)
            .WithOne(c => c.Employer)
            .HasForeignKey<Company>(c => c.EmployerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.ToTable("Companies");

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.LogoUrl).HasMaxLength(500);
        builder.Property(c => c.Industry).HasMaxLength(150);
        builder.Property(c => c.Website).HasMaxLength(300);
        builder.Property(c => c.Location).HasMaxLength(100);
        builder.Property(c => c.ContactEmail).HasMaxLength(256);
        builder.Property(c => c.ContactPhone).HasMaxLength(30);
        builder.Property(c => c.Description).HasMaxLength(1000);
        builder.Property(c => c.About).HasMaxLength(4000);

        builder.HasIndex(c => c.EmployerId).IsUnique();
        builder.HasIndex(c => c.Name);
        builder.HasIndex(c => c.Industry);
        builder.HasIndex(c => c.Location);
    }
}
