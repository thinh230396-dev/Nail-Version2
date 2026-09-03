namespace NailManagement.Application.DTOs;

/// <summary>
/// Một hóa đơn đăng ký — BR-INV-001, thứ <b>tiệm trả cho SalonSys</b>.
///
/// <para>
/// ⚠️ Không nhầm với <c>SalesInvoiceDto</c>, thứ khách trả cho tiệm. Hai bảng tách hoàn toàn,
/// và nhầm chúng là nhầm luôn ý nghĩa của mọi con số doanh thu — BR-REV-008 nói rõ doanh thu
/// nền tảng tính từ bảng này, không phải từ doanh thu bán hàng của tiệm.
/// </para>
/// <para>
/// Mang sẵn <paramref name="TenantName"/> và <paramref name="PackageName"/> vì chúng được
/// <b>chốt lúc lập hóa đơn</b>: tiệm đổi tên hay đổi gói về sau không được làm sai một chứng
/// từ đã phát hành.
/// </para>
/// </summary>
/// <param name="Status">BR-INV-030…033 — <c>PENDING</c>, <c>PAID</c>, <c>OVERDUE</c>, <c>CANCELLED</c>.</param>
/// <param name="PaidAt">Rỗng khi chưa thu. Đây là mốc mà doanh thu nền tảng ghi nhận.</param>
public sealed record SubscriptionInvoiceDto(
    string Id,
    string Code,
    string TenantId,
    string TenantName,
    string PackageId,
    string PackageName,
    long Amount,
    string BillingCycle,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset DueAt,
    string Reason,
    string Status,
    string? PaymentReference,
    string? PaymentNote,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? PaidAt,
    DateTimeOffset CreatedAt);
