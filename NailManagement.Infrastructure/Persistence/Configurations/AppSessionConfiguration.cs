using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities;

namespace NailManagement.Infrastructure.Persistence.Configurations;

public sealed class AppSessionConfiguration : IEntityTypeConfiguration<AppSession>
{
    public void Configure(EntityTypeBuilder<AppSession> builder)
    {
        builder.ToTable("AppSessions");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Id).HasMaxLength(64).IsRequired();
        builder.Property(s => s.UserId).HasMaxLength(64).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.Property(s => s.ExpiresAt).IsRequired();
        builder.Property(s => s.LastActive).IsRequired();
        builder.Property(s => s.RevokedAt);
        builder.Property(s => s.Ip).HasMaxLength(64);
        builder.Property(s => s.UserAgent).HasMaxLength(512);

        // BR-AUTH-024 — tiệm đang làm việc nằm trong phiên. Khóa ngoại sang bảng Tenants
        // đã được nối ở ngày 2.
        builder.Property(s => s.ActiveTenantId).HasMaxLength(64);

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(s => s.ActiveTenantId)
            .OnDelete(DeleteBehavior.NoAction);

        builder.HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.ExpiresAt);
    }
}
