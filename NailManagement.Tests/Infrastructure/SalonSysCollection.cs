namespace NailManagement.Tests.Infrastructure;

/// <summary>
/// Gom mọi lớp kiểm thử vào <b>một bộ duy nhất</b>, để máy chủ và database chỉ dựng một lần cho
/// cả lần chạy.
/// <para>
/// Không chỉ để nhanh: xUnit chạy các bộ khác nhau <b>song song</b>, và hai bộ cùng dựng một
/// database sẽ xóa lẫn của nhau. Vài phép thử ở đây còn khóa tiệm hoặc khóa tài khoản
/// rồi mở lại — chạy song song thì một phép thử khác sẽ thấy trạng thái giữa chừng ấy và đỏ
/// một cách ngẫu nhiên.
/// </para>
/// </summary>
[CollectionDefinition(Name)]
public sealed class SalonSysCollection : ICollectionFixture<SalonSysFactory>
{
    public const string Name = "SalonSys";
}
