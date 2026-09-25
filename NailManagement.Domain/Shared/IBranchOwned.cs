namespace NailManagement.Domain.Shared;

/// <summary>
/// Đánh dấu một entity thuộc về đúng một chi nhánh.
/// <para>
/// Sinh ra vì cùng một lý do với <see cref="ITenantOwned"/>, nhưng cưỡng chế một luật khác.
/// <c>ITenantOwned</c> là <b>ranh giới cách ly</b> (BR-ISO-001/002): dữ liệu của tiệm khác
/// không được lọt ra, và bộ lọc toàn cục ở <c>NailDbContext</c> lo việc đó cho mọi truy vấn.
/// Giao diện này thì là một <b>ô trong ma trận quyền</b> ở mục 3.4: theo BR-ISO-004, lễ tân
/// chỉ thao tác được lịch hẹn, nhân viên và hóa đơn bán hàng <i>của chi nhánh mình</i>, còn
/// chủ tiệm thì thấy cả tiệm.
/// </para>
/// <para>
/// Hai luật ấy không gộp được: bộ lọc toàn cục lọc như nhau cho mọi vai trò, nên nó không
/// biểu diễn nổi một phép thu hẹp chỉ áp cho một vai. Nhưng luật thu hẹp vẫn cần <b>một chỗ
/// duy nhất</b> — trước khi có giao diện này nó đã có hai bản chép ở <c>ListStaffUseCase</c>
/// và <c>AppointmentScope</c>, và hóa đơn bán hàng sắp thành bản thứ ba.
/// </para>
/// <para>
/// Ba bảng mang giao diện này là đúng ba dòng cuối của bảng BR-ISO-004. Khách hàng và dịch
/// vụ cố ý KHÔNG mang: BR-CUS-001 cho khách thuộc tiệm chứ không thuộc chi nhánh, và lễ tân
/// chi nhánh nào cũng phải tra được toàn bộ danh bạ.
/// </para>
/// </summary>
public interface IBranchOwned
{
    string BranchId { get; }
}
