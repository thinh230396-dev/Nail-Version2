using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities;

namespace NailManagement.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> builder)
    {
        builder.ToTable("Services");
        builder.HasKey(service => service.Id);

        builder.Property(service => service.Id).HasMaxLength(64).IsRequired();
        builder.Property(service => service.TenantId).HasMaxLength(64).IsRequired();
        builder.Property(service => service.Name).HasMaxLength(160).IsRequired();
        builder.Property(service => service.Category).HasMaxLength(80);
        builder.Property(service => service.Price).IsRequired();
        builder.Property(service => service.DurationMinutes).IsRequired();
        builder.Property(service => service.BufferMinutes).IsRequired();
        builder.Property(service => service.Description).HasMaxLength(1000);
        builder.Property(service => service.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(service => service.CreatedAt).IsRequired();
        builder.Property(service => service.UpdatedAt).IsRequired();

        builder.HasOne(service => service.Tenant)
            .WithMany()
            .HasForeignKey(service => service.TenantId)
            .OnDelete(DeleteBehavior.NoAction);

        // BR-VAL-001 — tên dịch vụ duy nhất trong một tiệm.
        builder.HasIndex(service => new { service.TenantId, service.Name }).IsUnique();
        builder.HasIndex(service => new { service.TenantId, service.Status });
    }
}
