using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// <see cref="ITransactionLock"/> bằng khóa ứng dụng của SQL Server (<c>sp_getapplock</c>).
/// <para>
/// Khóa ứng dụng thay vì khóa dòng hay mức cô lập SERIALIZABLE: nó khóa đúng một <i>khái niệm</i>
/// — lịch của một người, hạn mức của một tiệm — không đụng dòng nào của bảng, không giữ khóa phạm
/// vi trên chỉ mục, và không có cặp khóa chéo nào để sinh deadlock, vì mỗi giao dịch chỉ xin đúng
/// một khóa.
/// </para>
/// </summary>
public sealed class SqlTransactionLock(NailDbContext db) : ITransactionLock
{
    // Đủ dài để chờ vài lượt khác xong, đủ ngắn để quầy không treo khi có gì bất thường.
    private const int TimeoutMilliseconds = 10_000;

    public async Task<bool> TryAcquireAsync(string resource, CancellationToken cancellationToken = default)
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"Khóa '{resource}' phải được xin bên trong một giao dịch (IUnitOfWork). "
                + "Ngoài giao dịch, khóa nhả ngay lập tức và không bảo vệ được gì.");
        }

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
        // nhân deadlock — trường hợp nào cũng chưa giữ khóa.
        return outcome.Single() >= 0;
    }
}
