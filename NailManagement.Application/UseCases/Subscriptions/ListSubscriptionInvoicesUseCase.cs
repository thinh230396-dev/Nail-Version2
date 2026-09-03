using NailManagement.Application.Abstractions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Subscriptions;

/// <summary>
/// Sổ hóa đơn đăng ký của toàn hệ thống — BR-INV-030…033, và là nguồn của **doanh thu nền
/// tảng** ở BR-REV-008.
///
/// <para>
/// Lát cắt gói đăng ký đã bị cắt khỏi MVP (§0 mục 13), nên đây cố ý chỉ là một phép <b>đọc</b>:
/// không có lệnh nộp chứng từ, không có lệnh xác nhận thanh toán, không có luồng nâng cấp năm
/// bước. Nó tồn tại vì thiếu nó thì màn Tổng quan của Superadmin phải bịa ra con số doanh thu
/// nền tảng — và tài liệu nghiệp vụ đã cảnh báo sẵn điều đó ở chính BR-REV-008.
/// </para>
/// <para>
/// Chỉ Superadmin gọi được, qua nhóm <c>SubscriptionInvoices</c> trong ma trận mục 3.4. Chủ
/// tiệm cũng có ô ở nhóm ấy — họ xem hóa đơn của tiệm mình và nộp chứng từ — nhưng phép lọc
/// theo tiệm cho họ chưa có, nên endpoint này gắn <c>RequiresTenant = false</c> và dành riêng
/// cho tầng nền tảng.
/// </para>
/// </summary>
public sealed class ListSubscriptionInvoicesUseCase(
    ISubscriptionInvoiceRepository invoices,
    IClock clock)
{
    public async Task<IReadOnlyList<SubscriptionInvoiceDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var found = await invoices.ListAllAsync(cancellationToken);

        return [.. found.Select(invoice => SubscriptionInvoiceMapper.ToDto(invoice, now))];
    }
}
