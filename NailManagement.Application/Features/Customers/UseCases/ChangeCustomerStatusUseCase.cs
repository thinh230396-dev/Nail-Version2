using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Customers;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.Features.Customers.UseCases;

/// <summary>
/// Ngừng hoặc bật lại một hồ sơ khách — BR-CUS-006.
/// <para>
/// Đây chính là thứ mà giao diện gọi là xóa khách hàng. Không có lệnh xóa cứng nào
/// (BR-DEL-001): hồ sơ đã ngừng vẫn ở lại để hóa đơn và lịch hẹn cũ đọc đúng tên
/// (BR-DEL-003), và <b>lịch sử chi tiêu của họ không mất đi</b> — bật lại là thấy nguyên
/// tổng chi tiêu cùng hạng khách cũ, vì hai con số đó vốn tính từ hóa đơn chứ không lưu
/// trên hồ sơ.
/// </para>
/// <para>
/// Khác <c>ChangeStaffStatusUseCase</c> ở hai chỗ: không có hạn mức nào để đếm khi bật lại,
/// và ngừng một khách không kéo theo tài khoản đăng nhập nào — BR-AUTH-003 nói khách hàng
/// không đăng nhập vào hệ thống.
/// </para>
/// </summary>
public sealed class ChangeCustomerStatusUseCase(
    ICustomerRepository customers,
    IClock clock)
{
    public async Task<CustomerDto> ExecuteAsync(
        ChangeCustomerStatusCommand command, CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;
        var status = CustomerMapper.ParseStatus(command.Status);

        var customer = await customers.FindByIdAsync(command.CustomerId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khách hàng.");

        if (status == CustomerStatus.Inactive) customer.Deactivate(now);
        else customer.Activate(now);

        await customers.UpdateAsync(customer, cancellationToken);

        var spend = await customers.GetSpendSummaryAsync(customer.Id, cancellationToken);

        return CustomerMapper.ToDto(customer, spend);
    }
}
