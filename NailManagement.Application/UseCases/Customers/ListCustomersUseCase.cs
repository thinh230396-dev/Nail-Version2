using NailManagement.Application.DTOs.Salon;
using NailManagement.Application.Mappings.Salon;
using NailManagement.Domain.Repositories.Salon;

namespace NailManagement.Application.UseCases.Customers;

/// <summary>
/// Danh bạ khách của tiệm đang làm việc — BR-CUS-001.
/// <para>
/// Không nhận tham số nào, kể cả mã chi nhánh. Khách thuộc tiệm và dùng chung cho mọi chi
/// nhánh, nên <b>lễ tân nhìn thấy đúng cùng một danh sách với chủ tiệm</b> — khác hẳn màn
/// nhân viên, nơi lễ tân bị thu hẹp về chi nhánh mình. Đó là điều BR-ISO-004 nói: cách ly
/// nằm ở ranh giới tiệm, không phải ranh giới chi nhánh.
/// </para>
/// <para>
/// Hai lời gọi kho dữ liệu chứ không phải một cho mỗi khách: câu thứ hai là một
/// <c>GROUP BY</c> gom chi tiêu của cả tiệm, để bảng khách hiện được hạng và tổng chi tiêu
/// ngay trên dòng mà không phải hỏi lại từng người.
/// </para>
/// </summary>
public sealed class ListCustomersUseCase(ICustomerRepository customers)
{
    public async Task<IReadOnlyList<CustomerDto>> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var all = await customers.ListAsync(cancellationToken);
        var spending = await customers.ListSpendSummariesAsync(cancellationToken);

        return
        [
            .. all.Select(customer => CustomerMapper.ToDto(
                customer,
                // Khách chưa có hóa đơn nào không nằm trong kết quả gom nhóm. Vắng mặt ở đó
                // nghĩa là chưa chi đồng nào, và BR-CUS-007 xếp họ vào hạng NEW.
                spending.TryGetValue(customer.Id, out var spend)
                    ? spend
                    : CustomerSpendSummary.Empty(customer.Id)))
        ];
    }
}
