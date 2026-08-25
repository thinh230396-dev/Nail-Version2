using Microsoft.EntityFrameworkCore;
using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.Persistence;

/// <summary>
/// Bản cài đặt <see cref="IUnitOfWork"/> bằng giao dịch của EF Core.
/// <para>
/// Các repository trong hệ thống này tự gọi <c>SaveChangesAsync</c> ngay khi được gọi, nên
/// lớp ở đây không gom lệnh lưu lại mà chỉ <b>mở một giao dịch bao quanh chúng</b>. Mọi
/// <c>SaveChanges</c> chạy bên trong khối lệnh sẽ tự tham gia vào giao dịch đó, và chỉ có
/// hiệu lực khi khối lệnh chạy xong trọn vẹn.
/// </para>
/// <para>
/// Chọn cách này thay vì đổi toàn bộ repository sang kiểu "theo dõi rồi lưu một lần" vì hai
/// lẽ. Một là tầng Application không phải học thêm khái niệm nào — nó vẫn gọi repository y
/// như cũ. Hai là những lệnh ghi cần độc lập với giao dịch nghiệp vụ vẫn giữ được tính độc
/// lập ấy: bản ghi nhật ký đăng nhập hỏng phải tồn tại đúng vào lúc thao tác nghiệp vụ thất
/// bại, chứ không được cuộn ngược theo.
/// </para>
/// <para>
/// Chiến lược thực thi của EF Core được dùng tường minh vì cấu hình SQL Server có thể bật
/// thử lại khi mất kết nối; thử lại một khối lệnh nhiều bước mà không khai báo qua chiến
/// lược sẽ bị EF từ chối thẳng lúc chạy.
/// </para>
/// </summary>
public sealed class EfUnitOfWork(NailDbContext db) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async ct =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);

            var result = await action(ct);

            await transaction.CommitAsync(ct);

            return result;
        }, cancellationToken);
    }
}
