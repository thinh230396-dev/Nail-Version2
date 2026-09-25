using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.Features.Customers;
using NailManagement.Domain.Salon.Customers;

namespace NailManagement.Application.Features.Customers.UseCases;

/// <summary>
/// Một hồ sơ khách kèm những lần ghé gần nhất.
/// <para>
/// Lịch sử ở đây là <b>hóa đơn đã trả đủ</b> chứ không phải lịch hẹn — quyết định 42. Lịch
/// hẹn có thể bị hủy hoặc khách không đến, còn hóa đơn đã trả đủ thì chắc chắn là một lần
/// khách được phục vụ thật, và nó cũng chính là thứ đã cộng nên tổng chi tiêu ở dòng trên.
/// Hai con số vì vậy không thể nói ngược nhau.
/// </para>
/// <para>
/// Khách của tiệm khác không tìm thấy được ở đây, và câu trả lời là <c>NOT_FOUND</c> chứ
/// không phải <c>FORBIDDEN</c> — BR-TENANT-013 bước 4.
/// </para>
/// </summary>
public sealed class GetCustomerUseCase(ICustomerRepository customers)
{
    /// <summary>
    /// Số lần ghé trả về cho ngăn chi tiết.
    /// <para>
    /// Cắt ở mười vì đây là một ngăn để lễ tân liếc qua trước khi phục vụ, không phải một
    /// bảng sao kê. Khách quen ba năm có thể có hàng trăm hóa đơn, và kéo hết về chỉ để
    /// hiện ba dòng đầu là tốn công cả hai đầu.
    /// </para>
    /// </summary>
    private const int RecentVisitLimit = 10;

    public async Task<CustomerDetailDto> ExecuteAsync(
        string customerId, CancellationToken cancellationToken = default)
    {
        var customer = await customers.FindByIdAsync(customerId ?? string.Empty, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy khách hàng.");

        var spend = await customers.GetSpendSummaryAsync(customer.Id, cancellationToken);
        var visits = await customers.ListVisitsAsync(customer.Id, RecentVisitLimit, cancellationToken);

        return new CustomerDetailDto(
            CustomerMapper.ToDto(customer, spend),
            [.. visits.Select(CustomerMapper.ToDto)]);
    }
}
