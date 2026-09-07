using NailManagement.Application.Abstractions;
using NailManagement.Application.Common.Exceptions;
using NailManagement.Application.DTOs;
using NailManagement.Application.Mappings;
using NailManagement.Domain.Entities.Auth;
using NailManagement.Domain.Enums.Auditing;
using NailManagement.Domain.Enums.Auth;
using NailManagement.Domain.Repositories;

namespace NailManagement.Application.UseCases.Sessions;

/// <summary>
/// Thu hồi một phiên đăng nhập đang mở — BR-AUTH-033.
/// <para>
/// Không cần đụng gì tới việc cưỡng chế: BR-AUTH-022 đã bắt mọi request phải đọc lại phiên và
/// kiểm tính hợp lệ, nên đặt <c>RevokedAt</c> là người kia mất quyền ngay ở request kế tiếp.
/// Phần khó của tính năng này nằm ở <b>ai được thu hồi phiên của ai</b>, và đó là toàn bộ nội
/// dung của lớp này.
/// </para>
/// </summary>
public sealed class RevokeSessionUseCase(ISessionRepository sessions, IAuditLogger audit, IClock clock)
{
    /// <param name="actor">
    /// Người bấm nút, dựng từ phiên đăng nhập — BR-AUD-003. Thay cho tham số <c>role</c> của bản
    /// trước chứ không nằm cạnh nó: vai trò đã có sẵn trong này, và giữ cả hai là mở đường cho
    /// một lời gọi truyền vai trò khác với vai trò của chính người đang thao tác.
    /// </param>
    public async Task<SessionDto> ExecuteAsync(
        ActorContext actor,
        string? activeTenantId,
        string currentSessionId,
        string targetSessionId,
        CancellationToken cancellationToken = default)
    {
        var role = actor.Role;

        // Lễ tân chặn ngay ở đây chứ không chỉ dựa vào ma trận quyền ở tầng ngoài: use case
        // phải tự đứng vững kể cả khi có người gọi nó từ một đường khác về sau.
        if (role is not (UserRole.SuperAdmin or UserRole.TenantAdmin))
            throw new ForbiddenException("Vai trò này không thu hồi được phiên đăng nhập.");

        // Tự thu hồi phiên của chính mình là chuyện của nút Đăng xuất. Cho phép ở đây thì
        // người dùng tự đá mình ra giữa lúc đang thao tác và không hiểu vì sao — trong khi
        // POST /api/auth/logout làm đúng việc đó, có dọn cookie tử tế.
        if (targetSessionId == currentSessionId)
            throw new ForbiddenException(
                "Không thu hồi được phiên bạn đang dùng. Hãy đăng xuất nếu muốn kết thúc phiên này.");

        var session = await sessions.FindByIdAsync(targetSessionId, cancellationToken)
            ?? throw new NotFoundException("Không tìm thấy phiên đăng nhập này.");

        // Chủ tiệm chỉ đóng được phiên của người thuộc tiệm mình. Phép kiểm đi qua bảng
        // UserTenants chứ không qua ActiveTenantId của phiên — xem chú thích ở ISessionRepository
        // để biết vì sao lọc theo phiên là giấu mất đúng những phiên đáng lo nhất.
        if (role == UserRole.TenantAdmin)
        {
            var tenantId = activeTenantId ?? throw new TenantNotSelectedException();
            var belongs = await sessions.UserBelongsToTenantAsync(
                session.UserId, tenantId, cancellationToken);

            // Trả 404 chứ không phải 403: nói "bạn không có quyền với phiên này" là xác nhận
            // phiên đó tồn tại, tức là rò rỉ một mẩu thông tin về tiệm khác. Cùng lối mà
            // BR-ISO-003 đã đặt ra cho mọi tài nguyên xuyên tiệm.
            if (!belongs) throw new NotFoundException("Không tìm thấy phiên đăng nhập này.");
        }

        var now = clock.UtcNow;

        // Thu hồi hai lần không phải lỗi. Màn hình đã ẩn nút với phiên đã đóng, nên lần gọi
        // thứ hai gần như chắc chắn là một cú bấm đúp hoặc một tab cũ — trả về trạng thái
        // hiện có thì người dùng thấy đúng thứ họ muốn, còn ném lỗi thì họ tưởng mình làm sai.
        if (session.RevokedAt is null)
        {
            session.Revoke(now);
            await sessions.UpdateAsync(session, cancellationToken);

            // Ghi vết chỉ khi thật sự có gì đổi. Lần bấm thứ hai không đổi trạng thái nào, nên
            // một dòng nhật ký cho nó là làm nhiễu đúng cái sổ sinh ra để đọc — cùng nguyên tắc
            // đã đặt ở `ChangeAccountStatusUseCase`.
            await audit.RecordAsync(
                new AuditEntry(
                    AuditEvent.SessionRevoked,
                    actor.UserId,
                    actor.Role,
                    // Tiệm của phiên bị đóng, không phải tiệm của người bấm. Chủ tiệm chỉ đóng
                    // được phiên trong tiệm mình nên hai giá trị trùng nhau; Superadmin thì
                    // không thuộc tiệm nào, và khi ấy đây là mẩu thông tin duy nhất cho biết
                    // thao tác vừa rồi chạm vào tiệm nào.
                    session.ActiveTenantId,
                    nameof(AppSession),
                    session.Id,
                    actor.Ip,
                    new Dictionary<string, string>
                    {
                        ["target"] = session.UserId,
                        ["device"] = SessionMapper.ToDto(session, now, currentSessionId).Device
                    }),
                cancellationToken);
        }

        return SessionMapper.ToDto(session, now, currentSessionId);
    }
}
