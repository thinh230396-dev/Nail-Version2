using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Subscriptions;
using NailManagement.Domain.Access;
using NailManagement.Domain.Platform.Subscriptions;

namespace NailManagement.Application.Features.Subscriptions.UseCases;

/// <summary>
/// Sổ hóa đơn đăng ký — BR-INV-030…033, và là nguồn của **doanh thu nền tảng** ở BR-REV-008.
///
/// <para>
/// Lát cắt gói đăng ký đã bị cắt khỏi MVP (§0 mục 13), nên đây cố ý chỉ là một phép <b>đọc</b>:
/// không có lệnh nộp chứng từ, không có lệnh xác nhận thanh toán, không có luồng nâng cấp năm
/// bước. Nó tồn tại vì thiếu nó thì màn Tổng quan của Superadmin phải bịa ra con số doanh thu
/// nền tảng — và tài liệu nghiệp vụ đã cảnh báo sẵn điều đó ở chính BR-REV-008.
/// </para>
/// <para>
/// Phạm vi thu hẹp theo vai trò, cùng khuôn với <c>ListSessionsUseCase</c> và
/// <c>ListAuditLogsUseCase</c>: Superadmin đọc toàn hệ thống, chủ tiệm đọc hóa đơn của tiệm
/// mình (BR-INV-032), lễ tân không đọc được gì — tiền tiệm trả cho SalonSys không phải việc
/// của quầy.
/// </para>
/// <para>
/// ⚠️ Phép thu hẹp này phải nằm ở đây chứ không thể trông vào tầng dữ liệu, vì bảng hóa đơn
/// đăng ký cố ý KHÔNG mang bộ lọc theo tiệm (BR-TENANT-022 — hóa đơn của tiệm đã xóa mềm vẫn
/// phải đọc được). Trước ngày 24 ma trận quyền cấp cho chủ tiệm ô này với ghi chú "phạm vi do
/// bộ lọc dữ liệu lo", trong khi bộ lọc ấy chưa từng tồn tại cho bảng này — và chủ tiệm đọc
/// được hóa đơn của mọi tiệm. Đó là lý do vai trò và mã tiệm là tham số <b>bắt buộc</b> ở đây.
/// </para>
/// </summary>
public sealed class ListSubscriptionInvoicesUseCase(
    ISubscriptionInvoiceRepository invoices,
    IClock clock)
{
    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> ExecuteAsync(
        UserRole role,
        string? activeTenantId,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var found = role switch
        {
            UserRole.SuperAdmin => await invoices.ListAllAsync(cancellationToken),

            UserRole.TenantAdmin => await invoices.ListByTenantAsync(
                activeTenantId ?? throw new TenantNotSelectedException(), cancellationToken),

            _ => throw new ForbiddenException("Vai trò này không xem được hóa đơn đăng ký.")
        };

        return [.. found.Select(invoice => SubscriptionInvoiceMapper.ToDto(invoice, now))];
    }
}
