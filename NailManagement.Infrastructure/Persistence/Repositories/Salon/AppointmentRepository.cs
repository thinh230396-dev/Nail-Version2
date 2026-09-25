using Microsoft.EntityFrameworkCore;
using NailManagement.Domain.Salon.Appointments;
using NailManagement.Domain.Salon.StaffMembers;
using NailManagement.Domain.Shared;

namespace NailManagement.Infrastructure.Persistence.Repositories.Salon;

/// <summary>
/// Bản cài đặt <see cref="IAppointmentRepository"/> bằng EF Core.
/// <para>
/// Không câu truy vấn nào ở đây viết <c>Where(a =&gt; a.TenantId == ...)</c>: điều kiện đó đã
/// được <c>NailDbContext</c> gắn sẵn cho mọi entity mang <c>ITenantOwned</c> (BR-ISO-002), và
/// lịch hẹn cùng các dòng dịch vụ của nó đều mang giao diện ấy.
/// </para>
/// <para>
/// Ba phép nối <c>Include</c> lặp lại ở cả hai đường đọc nên chúng đi qua một hàm dùng chung.
/// Đó không phải chuyện gọn mã: <c>AppointmentMapper</c> đọc thẳng khách và kỹ thuật viên từ
/// thuộc tính điều hướng, nên một đường đọc quên nối là một màn hình toàn tên rỗng.
/// </para>
/// </summary>
public sealed class AppointmentRepository(NailDbContext db) : IAppointmentRepository
{
    public async Task<IReadOnlyList<Appointment>> ListAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        // Lọc theo GIỜ BẮT ĐẦU, và chỉ theo nó. Một buổi làm kéo dài qua mốc cuối khoảng vẫn
        // thuộc về ngày mà khách đến — đó là cách người ở quầy đọc bảng lịch, và cũng là cách
        // index (StaffId, StartAt) cùng (BranchId, StartAt) được dựng để phục vụ.
        var query = Readable().Where(
            appointment => appointment.StartAt >= from && appointment.StartAt < to);

        // BR-APT-002 — phép thu hẹp theo chi nhánh cho vai trò lễ tân. Hợp lệ ở đây vì chi
        // nhánh không phải ranh giới cách ly — bộ lọc theo tiệm đã làm việc đó — mà chỉ là một
        // ô trong ma trận quyền, giống hệt cách IStaffRepository.ListAsync nhận tham số này.
        if (branchId is not null)
            query = query.Where(appointment => appointment.BranchId == branchId);

        return await query
            .OrderBy(appointment => appointment.StartAt)
            .ThenBy(appointment => appointment.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Appointment?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => await Readable().FirstOrDefaultAsync(appointment => appointment.Id == id, cancellationToken);

    public async Task<Appointment?> FindBlockingAsync(
        string staffId,
        DateTimeOffset start,
        DateTimeOffset end,
        string? exceptAppointmentId,
        CancellationToken cancellationToken = default)
        => await db.Appointments
            // Điều kiện chồng lấn KHÔNG được chép vào đây. Nó đến nguyên vẹn từ tầng Domain,
            // nơi BR-APT-011 và BR-APT-012 có đúng một định nghĩa. Viết tay lại ở đây là dựng
            // bản sao thứ hai của một luật mà lộ trình xếp vào nhóm tuyệt đối không được sai —
            // và bản sao ấy sẽ im lặng khi ai đó sửa bản gốc.
            .Where(AppointmentSchedulePolicy.BlockingSlot(staffId, start, end, exceptAppointmentId))

            // Chỉ cần tên khách để dựng câu thông báo, nên nối đúng một bảng chứ không dùng
            // lại Readable(): dòng dịch vụ và hồ sơ nhân viên không xuất hiện trong câu ấy.
            .Include(appointment => appointment.Customer)
            .OrderBy(appointment => appointment.StartAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task AddAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        await db.Appointments.AddAsync(appointment, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Appointment appointment, CancellationToken cancellationToken = default)
    {
        // Cố ý KHÔNG gọi db.Appointments.Update(...) như các kho dữ liệu khác. Hàm đó đánh dấu
        // cả cây đối tượng là "đã sửa", nên những dòng dịch vụ bị Appointment.Revise bỏ đi sẽ
        // không được nhận ra là mồ côi và không bị xóa. Bản ghi đọc lên đã nằm trong bộ theo
        // dõi thay đổi của DbContext này rồi, nên chỉ cần lưu là đủ, và đó cũng là cách duy
        // nhất để EF Core dọn đúng các dòng con đã bị gỡ.
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Hình dạng chung của hai đường đọc: lịch hẹn kèm dòng dịch vụ, khách và kỹ thuật viên.
    /// <para>
    /// <c>AsSplitQuery</c> vì đây là một phép nối một–nhiều lồng cùng hai phép nối một–một:
    /// gộp vào một câu thì mỗi lịch hẹn bị nhân lên đúng bằng số dòng dịch vụ của nó, và toàn
    /// bộ hồ sơ khách cùng hồ sơ nhân viên bị chép lại ở từng dòng ấy.
    /// </para>
    /// </summary>
    private IQueryable<Appointment> Readable()
        => db.Appointments
            .Include(appointment => appointment.Services)
            .Include(appointment => appointment.Customer)
            .Include(appointment => appointment.Staff)
            .AsSplitQuery();
}
