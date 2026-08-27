using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Customers;

/// <summary>
/// Sửa hồ sơ khách, kể cả đổi số điện thoại.
/// <para>
/// Đổi số điện thoại là thao tác thật chứ không phải trường hợp hiếm: khách đổi số là
/// chuyện thường, và số điện thoại lại chính là khóa tra cứu ở quầy (BR-CUS-002). Vì vậy
/// phép kiểm trùng phải bỏ qua chính hồ sơ đang sửa, nếu không thì bấm lưu mà không đổi số
/// cũng hỏng.
/// </para>
/// <para>
/// Hồ sơ không mang tổng chi tiêu nên sửa hồ sơ không đụng tới hạng khách. Nhưng DTO trả về
/// vẫn phải mang hạng đúng, nên nó hỏi lại phần tổng hợp — rẻ hơn nhiều so với việc để giao
/// diện tự đoán rồi hiện sai cho tới lần nạp kế tiếp.
/// </para>
/// </summary>
public sealed class UpdateCustomerUseCase(
    ICustomerRepository customers,
    IClock clock)
{
    public async Task<CustomerDto> ExecuteAsync(
        UpdateCustomerCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var customer = await customers.FindByIdAsync(command.CustomerId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khách hàng.");

        var birthDate = CustomerMapper.ParseBirthDate(command.BirthDate);

        if (await customers.PhoneExistsAsync(command.Phone, customer.Id, cancellationToken))
        {
            throw DomainException.ForField(
                "phone", "Tiệm đã có khách khác mang số điện thoại này.");
        }

        customer.UpdateProfile(
            command.Phone,
            command.FullName,
            command.Email,
            birthDate,
            command.Note,
            now);

        await customers.UpdateAsync(customer, cancellationToken);

        var spend = await customers.GetSpendSummaryAsync(customer.Id, cancellationToken);

        return CustomerMapper.ToDto(customer, spend);
    }
}
