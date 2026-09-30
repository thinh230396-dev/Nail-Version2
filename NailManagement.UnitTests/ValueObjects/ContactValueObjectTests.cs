using NailManagement.Domain.ValueObjects;

namespace NailManagement.UnitTests.ValueObjects;

/// <summary>
/// Chuẩn hóa số điện thoại và email. Hai hồ sơ khách trùng nhau chỉ vì một bên gõ dấu cách là
/// lỗi mà ràng buộc duy nhất ở database không bắt được — nó chỉ so chuỗi đã lưu.
/// </summary>
public sealed class ContactValueObjectTests
{
    [Theory]
    [InlineData("0901234567")]
    [InlineData("090 123 4567")]
    [InlineData("090-123-4567")]
    [InlineData("090.123.4567")]
    [InlineData("(090) 1234567")]
    public void So_dien_thoai_go_kieu_nao_cung_ra_mot_gia_tri(string typed)
        => Assert.Equal(PhoneNumber.Create("0901234567"), PhoneNumber.Create(typed));

    [Theory]
    [InlineData("+84901234567")]
    [InlineData("02839123456")]
    public void Chap_nhan_dau_so_quoc_te_va_so_ban(string valid)
        => Assert.Equal(valid, PhoneNumber.Create(valid).Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345")]
    [InlineData("0901234abc")]
    [InlineData("84901234567")]
    public void Tu_choi_so_sai_dinh_dang(string invalid)
        => DomainAssert.RejectsField("phone", () => PhoneNumber.Create(invalid));

    [Fact]
    public void Email_duoc_cat_khoang_trang_va_chuyen_chu_thuong()
        => Assert.Equal("chu.tiem@salon.vn", Email.Create("  Chu.Tiem@Salon.VN ").Value);

    [Theory]
    [InlineData("")]
    [InlineData("khong-co-a-cong")]
    [InlineData("a@b")]
    [InlineData("co khoang@trang.vn")]
    public void Tu_choi_email_sai_dinh_dang(string invalid)
        => DomainAssert.RejectsField("email", () => Email.Create(invalid));
}
