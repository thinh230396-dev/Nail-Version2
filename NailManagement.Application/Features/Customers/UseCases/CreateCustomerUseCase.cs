using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Customers;
using NailManagement.Domain.Salon.Customers;
using NailManagement.Domain.Shared;

using CustomerEntity = NailManagement.Domain.Salon.Customers.Customer;

namespace NailManagement.Application.Features.Customers.UseCases;

/// <summary>
/// Thêm một khách vào danh bạ của tiệm đang làm việc.
/// <para>
/// Không có hạn mức nào cho số khách: BR-SUB-005 chỉ cưỡng chế <c>max_salons</c> và
/// <c>max_staff</c>. Bán được nhiều khách hơn là điều tiệm muốn, không phải điều gói bán.
/// </para>
/// <para>
/// Hồ sơ mới luôn ở hạng <c>NEW</c> và tổng chi tiêu bằng 0 — không phải vì use case đặt
/// như vậy, mà vì BR-CUS-007 suy hạng ra từ hóa đơn và khách vừa tạo thì chưa có hóa đơn
/// nào. Đó là lý do ở đây không có một dòng nào gán hạng.
/// </para>
/// <para>
/// Tên lớp thực thể được đặt bí danh <c>CustomerEntity</c> vì thư mục lát cắt này tên
/// <c>Customers</c>, giống cách <c>CreateServiceUseCase</c> đã làm.
/// </para>
/// </summary>
public sealed class CreateCustomerUseCase(
    ICustomerRepository customers,
    ITenantContext tenantContext,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<CustomerDto> ExecuteAsync(
        CreateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        // Tới đây thì RequirePermission đã bảo đảm phiên có tiệm; phép kiểm này chỉ để lỗi
        // lập trình lộ ra sớm thay vì biến thành một khách không thuộc tiệm nào.
        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        var birthDate = CustomerMapper.ParseBirthDate(command.BirthDate);

        // BR-CUS-002 — số điện thoại duy nhất TRONG MỘT tiệm. Kiểm trước khi ghi để lễ tân
        // nhận thông báo gắn đúng ô nhập, và để họ biết mà mở hồ sơ cũ thay vì tạo bản trùng.
        if (await customers.PhoneExistsAsync(command.Phone, null, cancellationToken))
        {
            throw DomainException.ForField(
                "phone", "Tiệm đã có khách mang số điện thoại này. Hãy mở hồ sơ hiện có thay vì tạo mới.");
        }

        var customer = CustomerEntity.Create(
            ids.NewId("CUS"),
            tenantId,
            command.Phone,
            command.FullName,
            command.Email,
            birthDate,
            command.Note,
            now);

        await customers.AddAsync(customer, cancellationToken);

        return CustomerMapper.ToDto(customer, CustomerSpendSummary.Empty(customer.Id));
    }
}
