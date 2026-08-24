using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NailManagement.Domain.Entities;

namespace NailManagement.Infrastructure.Persistence.Configurations;

public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");
        builder.HasKey(log => log.Id);

        builder.Property(log => log.Id).HasMaxLength(64).IsRequired();
        builder.Property(log => log.Event).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(log => log.ActorUserId).HasMaxLength(64);
        builder.Property(log => log.ActorRole).HasConversion<string>().HasMaxLength(20);
        builder.Property(log => log.TenantId).HasMaxLength(64);
        builder.Property(log => log.TargetType).HasMaxLength(64);
        builder.Property(log => log.TargetId).HasMaxLength(64);
        builder.Property(log => log.Ip).HasMaxLength(64);
        builder.Property(log => log.MetadataJson).IsRequired();
        builder.Property(log => log.CreatedAt).IsRequired();

        // Bảng này KHÔNG mang bộ lọc theo tiệm dù có cột tiệm: BR-AUD-005 cho Superadmin
        // xem toàn bộ, còn chủ tiệm chỉ xem tiệm mình. Hai phạm vi khác nhau nên phép lọc
        // nằm ở use case đọc nhật ký, và cột dưới đây là chỗ nó dựa vào.
        builder.HasIndex(log => new { log.TenantId, log.CreatedAt });
        builder.HasIndex(log => new { log.Event, log.CreatedAt });
        builder.HasIndex(log => log.ActorUserId);
    }
}
