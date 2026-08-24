using NailManagement.Application.Abstractions;

namespace NailManagement.Infrastructure.SystemServices;

/// <summary>
/// Sinh định danh bằng <c>Guid.NewGuid()</c>.
/// <para>
/// Id phiên bắt buộc phải không đoán được — đoán được id phiên là chiếm được phiên.
/// GUID phiên bản 4 lấy từ nguồn ngẫu nhiên mã hóa của hệ điều hành, khác hẳn
/// <c>Random</c> vốn có thể dự đoán khi biết hạt giống.
/// </para>
/// </summary>
public sealed class GuidIdGenerator : IIdGenerator
{
    public string NewId() => Guid.NewGuid().ToString("N");

    public string NewId(string prefix)
        => $"{prefix}-{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
