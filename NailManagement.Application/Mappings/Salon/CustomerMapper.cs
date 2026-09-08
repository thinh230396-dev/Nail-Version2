using System.Globalization;
using NailManagement.Application.DTOs.Salon;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.Mappings.Salon;

/// <summary>Chuyển entity <see cref="Customer"/> sang DTO gửi ra ngoài, và đọc ngược các chuỗi từ client.</summary>
public static class CustomerMapper
{
    /// <summary>
    /// Ngày sinh đi ra dưới dạng <c>yyyy-MM-dd</c> theo văn hóa bất biến.
    /// <para>
    /// Không dùng <c>dd/MM/yyyy</c> như màn hình cũ: định dạng ngày của người Việt và của
    /// người Mỹ chỉ khác nhau ở thứ tự hai số đầu, nên một chuỗi như 03/04/1995 đi qua mạng
    /// là một ngày mơ hồ. Giao diện muốn hiện kiểu Việt thì tự định dạng lúc hiển thị.
    /// </para>
    /// </summary>
    private const string BirthDateFormat = "yyyy-MM-dd";

    /// <summary>BR-CUS-005 — hai trạng thái, không hơn.</summary>
    public static string ToWireFormat(CustomerStatus status) => status switch
    {
        CustomerStatus.Active => "ACTIVE",
        CustomerStatus.Inactive => "INACTIVE",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Trạng thái khách hàng không hợp lệ.")
    };

    /// <summary>BR-CUS-007 — bốn hạng, và không hạng nào được đặt tay.</summary>
    public static string ToWireFormat(CustomerTier tier) => tier switch
    {
        CustomerTier.New => "NEW",
        CustomerTier.Standard => "STANDARD",
        CustomerTier.Loyal => "LOYAL",
        CustomerTier.Vip => "VIP",
        _ => throw new ArgumentOutOfRangeException(nameof(tier), tier, "Hạng khách hàng không hợp lệ.")
    };

    public static CustomerStatus ParseStatus(string? value)
        => (value ?? string.Empty).Trim().ToUpperInvariant() switch
        {
            "ACTIVE" => CustomerStatus.Active,
            "INACTIVE" => CustomerStatus.Inactive,
            _ => throw DomainException.ForField(
                "status", "Trạng thái khách hàng chỉ nhận ACTIVE hoặc INACTIVE.")
        };

    /// <summary>
    /// Đọc ngày sinh từ client. Chuỗi rỗng là hợp lệ và có nghĩa khách không khai —
    /// BR-CUS-003 để ngày sinh ở dạng tùy chọn.
    /// </summary>
    public static DateOnly? ParseBirthDate(string? value)
    {
        var trimmed = (value ?? string.Empty).Trim();

        if (trimmed.Length == 0) return null;

        if (!DateOnly.TryParseExact(
                trimmed, BirthDateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            throw DomainException.ForField(
                "birthDate", "Ngày sinh phải ở dạng yyyy-MM-dd, ví dụ 1995-04-03.");
        }

        return parsed;
    }

    /// <summary>
    /// Dựng DTO từ hồ sơ và phần tổng hợp chi tiêu.
    /// <para>
    /// Hạng khách tính ở đây bằng chính <c>Customer.TierFrom</c> chứ không phải một phép so
    /// sánh viết lại trong use case: BR-CUS-007 chỉ có một bộ ngưỡng, và nó sống ở
    /// <c>CustomerTierPolicy</c>.
    /// </para>
    /// </summary>
    public static CustomerDto ToDto(Customer customer, CustomerSpendSummary spend) => new(
        customer.Id,
        customer.TenantId,
        customer.Phone.Value,
        customer.FullName,
        customer.Email,
        customer.BirthDate?.ToString(BirthDateFormat, CultureInfo.InvariantCulture),
        customer.Note,
        ToWireFormat(customer.Status),
        ToWireFormat(customer.TierFrom(spend.PaidInvoiceCount, spend.TotalSpent)),
        spend.PaidInvoiceCount,
        spend.TotalSpent,
        spend.LastVisitAt,
        customer.CreatedAt,
        customer.UpdatedAt);

    public static CustomerVisitDto ToDto(CustomerVisit visit) => new(
        visit.InvoiceId,
        visit.InvoiceCode,
        visit.IssuedAt,
        visit.BranchName,
        visit.StaffName,
        visit.Total,
        visit.ServiceNames);
}
