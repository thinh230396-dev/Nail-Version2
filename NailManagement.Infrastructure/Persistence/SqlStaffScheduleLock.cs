using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// <see cref="IStaffScheduleLock"/> bằng khóa ứng dụng của SQL Server (<c>sp_getapplock</c>).
/// <para>
/// Khóa ứng dụng thay vì khóa dòng hay mức cô lập SERIALIZABLE: nó khóa đúng một <i>khái niệm</i>
/// — lịch của một người — không đụng tới dòng nào của bảng nhân viên, không giữ khóa phạm vi trên
/// chỉ mục lịch hẹn, và không có cặp khóa chéo nào để sinh deadlock, vì mỗi giao dịch chỉ xin
/// đúng một khóa.
/// </para>
/// </summary>
public sealed class SqlStaffScheduleLock(NailDbContext db) : IStaffScheduleLock
{
    // Đủ dài để chờ vài lượt đặt lịch khác xong, đủ ngắn để quầy không treo khi có gì bất thường.
    private const int TimeoutMilliseconds = 10_000;

    public async Task AcquireAsync(string staffId, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                "Khóa lịch kỹ thuật viên phải được xin bên trong một giao dịch (IUnitOfWork). "
                + "Ngoài giao dịch, khóa nhả ngay lập tức và không bảo vệ được gì.");
        }

        // Mã nhân viên là duy nhất toàn hệ thống nên không cần kèm mã tiệm vào tên khóa.
        var resource = $"staff-schedule:{staffId}";

        var outcome = await db.Database
            .SqlQuery<int>($"""
                DECLARE @result int;
                EXEC @result = sp_getapplock
                    @Resource = {resource},
                    @LockMode = 'Exclusive',
                    @LockOwner = 'Transaction',
                    @LockTimeout = {TimeoutMilliseconds};
                SELECT @result AS [Value];
                """)
            .ToListAsync(cancellationToken);

        // 0: được ngay; 1: được sau khi chờ. Số âm: hết giờ chờ, bị hủy, hoặc bị chọn làm nạn
        // nhân deadlock — trường hợp nào cũng không được đi tiếp khi chưa giữ khóa.
        if (outcome.Single() < 0)
        {
            throw new SlotConflictException(
                "Lịch của kỹ thuật viên này đang được một quầy khác cập nhật. Vui lòng thử lại sau giây lát.");
        }
    }
}
