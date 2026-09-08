using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs.Platform;
using NailManagement.Domain.Repositories.Platform;

namespace NailManagement.Application.UseCases.Tenants;

/// <summary>Chi tiết một tiệm — cùng hình dạng dữ liệu với một dòng trong danh sách.</summary>
public sealed class GetTenantUseCase(
    ITenantRepository tenants,
    TenantReadService reader,
    IClock clock)
{
    public async Task<TenantDetailDto> ExecuteAsync(
        string tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await tenants.FindByIdAsync(tenantId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy tiệm.");

        return await reader.DescribeAsync(tenant, clock.UtcNow, cancellationToken);
    }
}
