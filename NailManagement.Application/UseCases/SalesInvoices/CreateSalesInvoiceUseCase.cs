using NailManagement.Application.Abstractions;
using NailManagement.Application.Common;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Common;
using NailManagement.Domain.Entities.Salon;
using NailManagement.Domain.Enums.Salon;
using NailManagement.Domain.Policies;
using NailManagement.Domain.Repositories;
using InvoiceEntity = NailManagement.Domain.Entities.Salon.SalesInvoice;

namespace NailManagement.Application.UseCases.SalesInvoices;

/// <summary>
/// Lập hóa đơn bán hàng — hai đường vào, một kết quả.
/// <para>
/// BR-INV-010 — lễ tân bấm "Thanh toán" trên một lịch hẹn: các dòng lấy từ dịch vụ của lịch,
/// khách và kỹ thuật viên và chi nhánh đều đi theo lịch hẹn đó.
/// BR-INV-011 — khách mua lẻ: không có lịch hẹn nào, các dòng do người lập nhập vào.
/// </para>
/// <para>
/// Cả hai chạy trong <b>một giao dịch</b>. Không phải để phòng lỗi chung chung, mà vì
/// BR-INV-016 nói rõ số hóa đơn phải sinh trong giao dịch: bộ đếm tăng lên rồi mà việc tạo hóa
/// đơn hỏng thì số ấy mất hẳn, và sổ hóa đơn có một lỗ không giải thích được với người kiểm tra
/// sổ sách.
/// </para>
/// </summary>
public sealed class CreateSalesInvoiceUseCase(
    ISalesInvoiceRepository invoices,
    IAppointmentRepository appointments,
    ICustomerRepository customers,
    IBranchRepository branches,
    IStaffRepository staffMembers,
    SalesInvoiceLineBuilder lineBuilder,
    ITenantContext tenantContext,
    IUnitOfWork unitOfWork,
    IIdGenerator ids,
    IClock clock)
{
    public async Task<SalesInvoiceDto> ExecuteAsync(
        CreateSalesInvoiceCommand command,
        ActorContext actor,
        CancellationToken cancellationToken = default)
    {
        var now = clock.UtcNow;

        var tenantId = tenantContext.ActiveTenantId
            ?? throw new TenantNotSelectedException();

        return await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var draft = string.IsNullOrWhiteSpace(command.AppointmentId)
                ? await FromWalkInAsync(command, actor, ct)
                : await FromAppointmentAsync(command.AppointmentId, actor, ct);

            var customer = await customers.FindByIdAsync(draft.CustomerId, ct)
                ?? throw new NotFoundException("Không tìm thấy khách hàng cho hóa đơn này.");

            var code = await invoices.NextCodeAsync(SalonTime.Today(now), ct);

            var invoice = InvoiceEntity.Create(
                ids.NewId("INV"),
                tenantId,
                draft.BranchId,
                customer.Id,
                draft.Appointment?.Id,
                draft.StaffId,
                code,
                command.Note,
                actor.UserId,
                now);

            foreach (var line in draft.Lines)
                invoice.AddLine(ids.NewId("INL"), line.ServiceId, line.Name, line.UnitPrice, line.Quantity, now);

            // Thứ tự bắt buộc: giảm giá SAU khi đã có đủ dòng hàng. BR-INV-021 chặn giảm giá
            // vượt tổng tiền hàng, mà tổng ấy chỉ đúng khi mọi dòng đã nằm trong hóa đơn.
            //
            // Điều kiện là `!= 0` chứ KHÔNG phải `> 0`, và khác biệt ấy là cả nội dung của lần
            // sửa này. Với `> 0`, một số âm không lọt được vào `ApplyDiscount` — nơi duy nhất
            // biết từ chối nó — nên máy chủ trả 201 với hóa đơn giảm giá 0đ, và người gửi
            // `discount: -50000` không có cách nào biết yêu cầu của mình đã bị bỏ qua. Im lặng
            // nuốt một giá trị sai còn tệ hơn từ chối nó: người dùng tin là đã áp dụng.
            //
            // Số 0 vẫn bỏ qua, có chủ đích: `0` là giá trị mặc định của một trường `long` không
            // nullable, nên "khách không gửi gì" và "khách gửi số 0" là một. Gọi
            // `ApplyDiscount(0, reason, ...)` cho cả hai sẽ ghi một lý do giảm giá vào hóa đơn
            // không hề được giảm giá.
            //
            // `UpdateSalesInvoiceUseCase` gọi thẳng hai hàm này không kèm điều kiện nào và vì
            // vậy vẫn luôn đúng. Hai đường đi của cùng một luật nay nói giống nhau.
            if (command.Discount != 0) invoice.ApplyDiscount(command.Discount, command.DiscountReason, now);
            if (command.Tip != 0) invoice.SetTip(command.Tip, now);

            RegisterDepositIfAny(invoice, draft.Appointment, actor, now);

            await invoices.AddAsync(invoice, ct);

            // Đọc lại thay vì dựng DTO từ đối tượng vừa ghi: chi nhánh và khách hàng phải có mặt
            // dưới dạng thuộc tính điều hướng thì mapper mới lấy được tên, và đọc lại còn bảo
            // đảm phản hồi của lệnh tạo giống hệt thứ một lệnh đọc sau đó sẽ trả về.
            var saved = await invoices.FindByIdAsync(invoice.Id, ct)
                ?? throw new InvalidOperationException($"Hóa đơn {invoice.Id} vừa ghi xong nhưng đọc lại không thấy.");

            return SalesInvoiceMapper.ToDto(saved);
        }, cancellationToken);
    }

    /// <summary>Những gì cần biết trước khi dựng được hóa đơn, gom lại cho hai đường vào cùng trả.</summary>
    private sealed record InvoiceDraft(
        string BranchId,
        string CustomerId,
        string? StaffId,
        Appointment? Appointment,
        IReadOnlyList<ResolvedInvoiceLine> Lines);

    /// <summary>
    /// BR-INV-010 — lập từ một lịch hẹn. Khách, kỹ thuật viên và chi nhánh <b>không nhận từ thân
    /// request</b>: chúng đã nằm trên lịch hẹn, và nhận thêm một lần nữa là mở đường cho một hóa
    /// đơn ghi tên khách khác với người đang ngồi trên ghế.
    /// </summary>
    private async Task<InvoiceDraft> FromAppointmentAsync(
        string appointmentId, ActorContext actor, CancellationToken cancellationToken)
    {
        var appointment = BranchScope.EnsureInScope(
            await appointments.FindByIdAsync(appointmentId, cancellationToken),
            actor,
            "Không tìm thấy lịch hẹn để lập hóa đơn.");

        // BR-INV-010 — chỉ lập được cho lịch khách đã tới. Lịch còn chờ xác nhận thì chưa chắc
        // khách sẽ đến, và lập hóa đơn trước là dựng một khoản phải thu cho một buổi chưa xảy ra.
        if (appointment.Status is not (AppointmentStatus.CheckedIn or AppointmentStatus.InService))
        {
            throw DomainException.ForField(
                "appointmentId",
                "Chỉ lập hóa đơn cho lịch hẹn khách đã đến hoặc đang được phục vụ. "
                + $"Lịch này đang ở trạng thái “{AppointmentStatusText.Label(appointment.Status)}”.");
        }

        // Chặn hóa đơn thứ hai cho cùng một lịch. Lễ tân bấm "Thanh toán" hai lần vì màn hình
        // chậm là chuyện thường, và hậu quả là khách bị tính tiền hai lần.
        var existing = await invoices.FindOpenByAppointmentAsync(appointment.Id, cancellationToken);

        if (existing is not null)
        {
            throw DomainException.ForField(
                "appointmentId",
                $"Lịch hẹn này đã có hóa đơn {existing.Code}. Mở hóa đơn đó để thu tiền thay vì lập cái mới.");
        }

        return new InvoiceDraft(
            appointment.BranchId,
            appointment.CustomerId,
            appointment.StaffId,
            appointment,
            await lineBuilder.FromAppointmentAsync(appointment, cancellationToken));
    }

    /// <summary>BR-INV-011 — khách mua lẻ, không đi từ lịch hẹn nào.</summary>
    private async Task<InvoiceDraft> FromWalkInAsync(
        CreateSalesInvoiceCommand command, ActorContext actor, CancellationToken cancellationToken)
    {
        // BR-CUS-004 — mọi hóa đơn bắt buộc gắn một hồ sơ khách. Không có khách vãng lai ẩn danh,
        // vì tổng chi tiêu và hạng khách ở BR-CUS-007 đều đọc ngược từ chính bảng hóa đơn này.
        if (string.IsNullOrWhiteSpace(command.CustomerId))
        {
            throw DomainException.ForField(
                "customerId", "Hóa đơn bán lẻ vẫn phải gắn một hồ sơ khách. Tạo hồ sơ trước nếu khách chưa có.");
        }

        var staffId = await ResolveStaffAsync(command.StaffId, actor, cancellationToken);

        return new InvoiceDraft(
            await ResolveWalkInBranchAsync(command.BranchId, actor, cancellationToken),
            command.CustomerId,
            staffId,
            null,
            await lineBuilder.FromInputAsync(command.Lines, cancellationToken));
    }

    /// <summary>
    /// Chi nhánh của một hóa đơn bán lẻ.
    /// <para>
    /// Lễ tân thì lấy từ phiên đăng nhập và <b>bỏ qua giá trị client gửi lên</b>: BR-ISO-004 chỉ
    /// cho họ thao tác trong chi nhánh mình, và nhận từ thân request là để họ tự khai. Chủ tiệm
    /// thì không thuộc chi nhánh nào nên buộc phải chọn — đây là chỗ duy nhất trong lát cắt này
    /// mà mã chi nhánh đến từ client, và nó vẫn được đối chiếu với bảng chi nhánh của tiệm.
    /// </para>
    /// </summary>
    private async Task<string> ResolveWalkInBranchAsync(
        string? requestedBranchId, ActorContext actor, CancellationToken cancellationToken)
    {
        var scoped = BranchScope.Resolve(actor, "hóa đơn");

        if (scoped is not null) return scoped;

        if (string.IsNullOrWhiteSpace(requestedBranchId))
            throw DomainException.ForField("branchId", "Chọn chi nhánh lập hóa đơn.");

        var branch = await branches.FindByIdAsync(requestedBranchId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy chi nhánh cho hóa đơn này.");

        return branch.Id;
    }

    /// <summary>
    /// Kỹ thuật viên được ghi công — chiều "nhân viên" của báo cáo doanh thu (BR-REV-004) và là
    /// nguồn tính hoa hồng ở BR-EMP-011. Không bắt buộc: một hóa đơn bán lẻ thuần túy không có
    /// ai đứng làm.
    /// </summary>
    private async Task<string?> ResolveStaffAsync(
        string? staffId, ActorContext actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(staffId)) return null;

        var staff = await staffMembers.FindByIdAsync(staffId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy kỹ thuật viên cho hóa đơn này.");

        return BranchScope.EnsureInScope(
            staff, actor, "Không tìm thấy kỹ thuật viên này trong chi nhánh của bạn.").Id;
    }

    /// <summary>
    /// BR-APT-031 — tiền cọc của lịch hẹn trở thành một dòng thu loại <c>DEPOSIT</c> ngay khi lập
    /// hóa đơn, nên số còn lại khách phải trả đã trừ sẵn phần đã đặt cọc.
    /// <para>
    /// ⚠️ Ghi nhận bằng phương thức <c>CASH</c> vì <b>lịch hẹn không lưu khách đã đặt cọc bằng
    /// cách nào</b> — BR-APT-030 chỉ có một cột số tiền. Tiền mặt là mặc định trung thực nhất ở
    /// quầy một tiệm nail, và dòng này mang sẵn mã lịch hẹn ở ô mã giao dịch nên vẫn truy ngược
    /// được. Muốn đúng hơn thì phải thêm một cột vào bảng lịch hẹn, việc nằm ngoài phạm vi MVP.
    /// </para>
    /// </summary>
    private void RegisterDepositIfAny(
        InvoiceEntity invoice, Appointment? appointment, ActorContext actor, DateTimeOffset now)
    {
        if (appointment is null || appointment.Deposit <= 0) return;

        invoice.RegisterPayment(
            ids.NewId("PAY"),
            PaymentType.Deposit,
            PaymentMethod.Cash,
            appointment.Deposit,
            now,
            $"Tiền cọc lịch hẹn {appointment.Id}",
            actor.UserId,
            now);
    }
}
