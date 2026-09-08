using NailManagement.Application.Common;
using NailManagement.Application.DTOs.Auth;
using NailManagement.Domain.Common;

namespace NailManagement.API.Security;

/// <summary>
/// Kết quả xác thực của request hiện tại, dùng chung cho middleware, bộ lọc quyền và
/// controller.
/// <para>
/// Là một dịch vụ theo vòng đời request thay vì một mục trong <c>HttpContext.Items</c>: nhờ
/// vậy nó có kiểu rõ ràng, tiêm được vào controller, và không ai phải nhớ tên khóa chuỗi.
/// </para>
/// <para>
/// Chỉ <c>SessionMiddleware</c> được gọi <see cref="Attach"/>. Mọi chỗ khác chỉ đọc.
/// </para>
/// </summary>
public sealed class RequestScope
{
    public CurrentAccountResult? Context { get; private set; }

    /// <summary>Mã phiên lấy từ cookie, cần cho các thao tác ghi lại chính phiên đó.</summary>
    public string? SessionId { get; private set; }

    /// <summary>
    /// Lý do phiên bị từ chối, nếu có: hết hạn, bị thu hồi, hoặc tài khoản không còn Active.
    /// <para>
    /// Giữ lại để bộ lọc quyền ném đúng lý do đó ra ngoài thay vì một câu chung chung. Người
    /// dùng bị khóa tài khoản cần đọc được "tài khoản đã bị khóa", chứ không phải "phiên hết
    /// hạn" rồi loay hoay đăng nhập lại mãi không hiểu vì sao.
    /// </para>
    /// </summary>
    public AppException? Rejection { get; private set; }

    public bool IsAuthenticated => Context is not null;

    /// <summary>Tài khoản đã đăng nhập, hoặc ném lỗi nếu chưa — dùng ở chỗ đã qua bộ lọc RequireAuth.</summary>
    public CurrentAccountResult Require() => Context
        ?? throw new InvalidOperationException(
            "Request chưa qua xác thực. Endpoint này phải gắn RequireAuth trước khi đọc RequestScope.");

    public void Attach(string sessionId, CurrentAccountResult context)
    {
        SessionId = sessionId;
        Context = context;
    }

    public void Reject(AppException reason) => Rejection = reason;

    /// <summary>
    /// Người đang thực hiện request, dựng từ phiên đăng nhập chứ không từ thân request —
    /// BR-AUD-003. Nhận từ thân request thì bản ghi nhật ký chỉ là lời khai của trình duyệt.
    /// <para>
    /// Gom về đây thay vì để mỗi controller tự dựng: chi nhánh của người thao tác là thứ
    /// <c>ListStaffUseCase</c> dùng để thu hẹp phạm vi theo ma trận mục 3.4, và một
    /// controller quên gắn nó vào sẽ lặng lẽ cho lễ tân xem cả tiệm.
    /// </para>
    /// </summary>
    public ActorContext ToActor(string? ip)
    {
        var current = Require();

        return new ActorContext(current.Account.Id, current.Role, ip, current.Branch?.Id);
    }
}
