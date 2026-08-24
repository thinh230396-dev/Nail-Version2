using NailManagement.Domain.ValueObjects;

namespace NailManagement.Application.Abstractions;

public sealed record HashedPassword(string Hash, string Salt);

/// <summary>
/// Cổng băm mật khẩu.
/// <para>
/// Là cổng của tầng Application chứ không phải Domain, vì "băm bằng thuật toán nào" là
/// quyết định kỹ thuật, không phải quy tắc nghiệp vụ. Đổi thuật toán chỉ cần thay bản cài
/// đặt ở tầng Infrastructure, không đụng tới use case.
/// </para>
/// </summary>
public interface IPasswordHasher
{
    HashedPassword Hash(RawPassword password);

    /// <summary>So sánh phải chống được tấn công đo thời gian.</summary>
    bool Verify(string password, HashedPassword hashed);
}
