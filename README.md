# NailManagement — máy chủ SalonSys

Backend của **SalonSys**, hệ thống SaaS đa tiệm quản lý chuỗi tiệm nail. ASP.NET Core 10 +
EF Core + SQL Server 2022, tổ chức theo Clean Architecture với năm project.

Giao diện nằm ở một repo khác: `C:\QLTiemNail_vs1` (React 19 + Vite). Máy chủ này không phục vụ
tệp tĩnh; nó chỉ trả `/api`.

| | |
|---|---|
| **Nghiệp vụ** | `README-BUSINESS-RULES.md` ở repo giao diện — nguồn sự thật duy nhất, mọi rule có mã `BR-*` |
| **Lịch dựng** | `README-BACKEND-ROADMAP.md` ở repo giao diện — kế hoạch 20 ngày và nhật ký từng ngày |
| **Đánh giá kỹ thuật** | `BAO_CAO_PHAN_TICH_NAILMANAGEMENT.md` (cùng thư mục này) |

---

## 1. Chạy lên trong ba bước

Cần sẵn: **.NET SDK 10** và **SQL Server 2022** — bản Developer Edition miễn phí là đủ
(`winget install --id Microsoft.SQLServer.2022.Developer`). Cài dạng **default instance**
(`MSSQLSERVER`) và bật **Windows authentication**, vì chuỗi kết nối mặc định là
`Server=localhost;Trusted_Connection=True`.

Lược đồ không phụ thuộc phiên bản nào cả — nó chạy được từ SQL Server 2016 trở lên, kể cả
LocalDB. Đổi máy chủ chỉ là đổi chuỗi kết nối ở §6, nhưng đọc phần collation ở đó trước.

```bash
cd C:\Users\letru\source\repos\NailManagement
dotnet run --project NailManagement.API --launch-profile http
```

Máy chủ lên ở **http://localhost:5282**. Kiểm tra nhanh:

```bash
curl http://localhost:5282/api/health
```

Lần chạy đầu trên máy sạch **không cần thao tác tay nào**: `Program.cs` tự áp migration, tự nạp
tài khoản demo rồi nạp dữ liệu nghiệp vụ. Xem log khởi động — có hai dòng
`Đã nạp … tài khoản demo` và `Đã nạp dữ liệu mẫu: …` khi database còn trống.

Rồi mở giao diện ở repo kia:

```bash
cd C:\QLTiemNail_vs1
npm run dev        # http://localhost:3000, proxy /api sang cổng 5282
```

Trỏ sang cổng khác thì đặt `API_ORIGIN` cho Vite:

```bash
API_ORIGIN=http://localhost:5000 npm run dev
```

### Cách gọn hơn khi trình bày: một cổng, một lệnh

Hai cửa sổ lệnh và một thứ tự phải nhớ là hai thứ thừa lúc đứng trước hội đồng. Dựng sẵn giao
diện vào `wwwroot/` **một lần**, sau đó chỉ còn `dotnet run`:

```bash
cd C:\QLTiemNail_vs1
npm run build:server          # build rồi chép dist/ sang NailManagement.API/wwwroot

cd C:\Users\letru\source\repos\NailManagement
dotnet run --project NailManagement.API --launch-profile http
```

Mở **http://localhost:5282** — cả giao diện lẫn API cùng một cổng, và không cần Node chạy nữa.

Vài điều đáng biết:

- **Chỉ phục vụ khi có bản build.** `Program.cs` kiểm `wwwroot/index.html` trước; không có thì
  khối này im lặng bỏ qua và máy chủ chạy đúng như cũ. Máy chưa build không hỏng gì.
- **Sửa frontend thì phải chép lại.** Bản trong `wwwroot/` là một bản sao đông cứng; chạy lại
  `npm run build:server`.
- **`/api` vẫn là `/api`.** Phần dự phòng SPA khai báo sau phần dự phòng của API, nên gõ sai một
  endpoint vẫn nhận JSON `NOT_FOUND` chứ không phải một trang HTML.
- **`wwwroot/` là thứ sinh ra**, đã nằm trong `.gitignore` và không commit.

> **Đừng bật hồ sơ `https` khi chạy dev.** `Program.cs` cố ý không bật `UseHttpsRedirection` ở
> môi trường Development: giao diện đi qua proxy của Vite trên HTTP, và một lần chuyển hướng 307
> sẽ làm cookie phiên (`Secure = false`) bị bỏ rơi.

---

## 2. Ba tài khoản demo

Do `DemoAccountSeeder` nạp, chỉ khi bảng tài khoản còn trống.

| Vai | Đăng nhập | Mật khẩu | Vào được gì |
|---|---|---|---|
| Superadmin | `superadmin@salonsys.vn` | `Super@2026` | Toàn nền tảng: tiệm, gói, hóa đơn đăng ký, nhật ký |
| Chủ tiệm | `tenantadmin@lumierehair.vn` | `Lumiere@2026` | **Hai tiệm** — Nailé Studio và Muse Nail Lab |
| Lễ tân | `receptionist@nailestudio.vn` | `Reception@2026` | Quầy của Nailé Studio, chi nhánh Quận 3 |

Đăng nhập được bằng **email hoặc tên đăng nhập** (`superadmin`, `nguyenvanboss`, `receptionist`).

Bốn tiệm phụ mỗi tiệm một chủ riêng, dùng chung mật khẩu `Tenant@2026`:

| Email | Tiệm | Trạng thái — để demo cái gì |
|---|---|---|
| `ha.vu@bloomsalon.vn` | Bloom Salon | `TRIAL` — tiệm đang dùng thử |
| `ngoc.trinh@oasiswellness.vn` | Oasis Wellness | `OVERDUE` — quá hạn, **đọc được nhưng chặn ghi** |
| `uyen.hoang@morningdew.vn` | Morning Dew Spa | `SUSPENDED` — bị khóa, cũng chặn ghi |
| `tu.pham@aurorabeauty.vn` | Aurora Beauty & Spa | `ACTIVE`, gói Enterprise |

Bốn tiệm này **cố ý không có chi nhánh, nhân sự hay khách**. Chúng tồn tại để có thứ mà kiểm
BR-TENANT-001/002 (bốn trạng thái hiển thị) và BR-TENANT-010 (chặn ghi). Đừng mở chúng ra khi
trình bày — mọi màn đều trống.

> ⚠️ Ba mật khẩu trên nằm cứng trong mã nguồn và được nạp ở **mọi môi trường**. Đây là điểm
> chặn triển khai production số 2 trong `BAO_CAO_PHAN_TICH_NAILMANAGEMENT.md`. Chấp nhận được
> cho một đồ án chạy trên máy cá nhân; đừng mang nguyên như vậy ra máy chủ thật.

---

## 3. Dựng lại database từ số 0

Cách duy nhất được hỗ trợ. Bộ nạp chỉ chạy khi bảng còn trống, nên **xóa hẳn database** rồi khởi
động lại là xong:

```bash
# 1. Dừng máy chủ (Ctrl+C) — EF không xóa được database đang có kết nối
# 2. Xóa
dotnet ef database drop --force --project NailManagement.Infrastructure --startup-project NailManagement.API
# 3. Chạy lại; migration và seed tự chạy
dotnet run --project NailManagement.API --launch-profile http
```

Cần `dotnet-ef`; chưa có thì `dotnet tool install --global dotnet-ef`.

Sau khi dựng lại, database ở đúng trạng thái này:

| Bảng | Số dòng |
|---|---:|
| Tenants | 6 |
| AppUsers | 7 |
| Branches | 3 |
| Staff | 8 |
| Services | 11 |
| Customers | 23 |
| Appointments | 176 |
| SalesInvoices | 151 |
| AuditLogs | 6 |
| AppSessions | 0 |

### Hạn dùng của một database đã dựng: bảy ngày

`DemoDataSeeder` sinh phần "lịch sử" bằng cách **lùi từ thời điểm chạy seed**, nên mọi thứ trong
đó đều neo vào ngày dựng. Trước đây nó không đặt gì ở tương lai, và database dựng hôm trước thì
hôm sau **cổng lễ tân trống trơn** — 0 khách, 0 ca, doanh thu ca 0đ. Màn hình xử lý đúng cách và
không lỗi, nhưng đó là màn trung tâm của buổi demo.

Nay bộ nạp dựng thêm **bảy ngày lịch hẹn phía trước** (`DemoDataSeeder.UpcomingDays`), nên một
database dựng trong vòng một tuần vẫn có ca để chạy ở quầy. Lịch tương lai chỉ dừng ở PENDING
hoặc CONFIRMED và **không kèm hóa đơn nào**, nên báo cáo doanh thu không hề đổi.

> Vẫn nên dựng lại nếu database đã quá bảy ngày — qua mốc đó thì cổng lễ tân lại trống. Và nhớ
> rằng bộ nạp **chỉ chạy khi bảng gói còn rỗng**: một database đã có sẵn sẽ không tự mọc thêm
> lịch tương lai chỉ vì chạy lại máy chủ, phải xóa rồi dựng từ số 0.

---

## 4. Kiểm thử

```bash
dotnet test        # 82 phép thử, ~11 giây
```

Bộ xUnit dựng máy chủ **trong bộ nhớ** qua `WebApplicationFactory` và chạy trên một database
dùng một lần — `NailManagementTests`, xóa và dựng lại ở đầu mỗi lần chạy — nên nó **không đụng
tới database demo**, chạy bao nhiêu lượt cũng không làm bẩn dữ liệu trình bày.

Nhưng phải **dừng máy chủ trước khi chạy**: tiến trình đang chạy giữ khóa các tệp DLL, và
`dotnet test` sẽ đỏ ngay ở bước build với `MSB3027 … being used by another process` — một lỗi
trông như hỏng mã nguồn trong khi thật ra chỉ là hai tiến trình tranh nhau một tệp.

Bố cục: `Infrastructure/` là bộ khung (client, factory, database tạm), `Scenarios/SalonScenario.cs`
dựng dữ liệu nghiệp vụ dùng chung, và các lớp kiểm thử gom theo vùng luật — `Authorization/`,
`Isolation/`, `Appointments/`, `Invoices/`, `Payments/`, `Sessions/`.

> `SalonScenario.NextSlot()` phải là **bộ đếm khung giờ duy nhất** của cả lần chạy. Mọi lớp kiểm
> thử đều đặt lịch cho cùng một kỹ thuật viên, nên hai bộ đếm riêng sẽ đụng BR-APT-011 và làm lớp
> chạy sau đỏ vì lý do không liên quan gì tới thứ nó kiểm.

Còn một lớp kiểm chứng nữa nằm ở repo giao diện: `npm run rehearsal` — 174 bước gọi **máy chủ
đang chạy** trên **database thật**, đi trọn kịch bản ba vai và ấn vào từng ranh giới nghiệp vụ.
Nó bắt được thứ xUnit không bắt được, ví dụ dữ liệu mẫu đã lệch ngày. Mỗi lượt để lại một tiệm
thử, nên dựng lại database sau khi chạy.

---

## 5. Bố cục năm project

Chiều phụ thuộc chỉ đi **vào trong**; `Domain` không tham chiếu project nào.

```
NailManagement.Domain          Entities · Enums · ValueObjects · Policies · Repositories (cổng)
NailManagement.Application     UseCases (mỗi lớp một ExecuteAsync) · DTOs · Mappings · Abstractions
NailManagement.Infrastructure  Persistence (DbContext, Migrations, Seed, Repositories) · Security · Auditing
NailManagement.API             Controllers · Middleware · Security · Program.cs
NailManagement.Tests           xUnit chạy qua HTTP thật
```

`AddApplication()` và `AddInfrastructure()` là **điểm ráp nối duy nhất** — API không biết use
case cần gì, cũng không biết repository cài bằng gì.

51 endpoint trên 14 controller: xác thực và phiên, tiệm, chi nhánh, dịch vụ, nhân sự, khách hàng,
lịch hẹn, hóa đơn bán hàng và thu tiền, báo cáo doanh thu, nhật ký kiểm toán, quản trị phiên
đăng nhập.

### Chuỗi kiểm tra quyền — thứ tự không được đảo

Ghi ở `Program.cs`, và bốn bước này là bốn hàng rào khác nhau:

1. **Tiệm còn hạn không?** — `TenantWriteGuardMiddleware` (BR-TENANT-010…013)
2. **Gói có mở tính năng?** — `RequirePermissionAttribute`
3. **Vai trò có quyền?** — `RequirePermissionAttribute`
4. **Dữ liệu thuộc tiệm nào?** — bộ lọc toàn cục ở `NailDbContext` (BR-ISO)

### Phiên là cookie + bảng, không phải JWT

Phiên lưu ở `AppSessions` và được đọc lại ở **mỗi request** (BR-AUTH-022). Nhờ vậy khóa một tiệm
hay một tài khoản có hiệu lực ngay trên phiên đang mở, không phải chờ token hết hạn. Cookie đặt
`SameSite=Strict`, và vì giao diện đi qua proxy cùng nguồn của Vite nên máy chủ **không mở CORS
cho ai cả**.

---

## 6. Cấu hình

| Khóa | Ở đâu | Mặc định |
|---|---|---|
| `ConnectionStrings:Default` | `NailManagement.API/appsettings.json` | `Server=localhost`, database `NailManagement` |
| Chuỗi kết nối của bộ kiểm thử | `NailManagement.Tests/Infrastructure/SalonSysFactory.cs` | `Server=localhost`, database `NailManagementTests` |
| Cổng HTTP | `Properties/launchSettings.json`, hồ sơ `http` | `5282` |

Chuỗi kết nối của bộ kiểm thử **nằm cứng trong mã nguồn, không đọc `appsettings`** — nó phải sẵn
sàng trước cả khi host được dựng, lý do đầy đủ ở chú thích ngay trên hằng số ấy. Nghĩa là đổi máy
chủ thì phải sửa **cả hai** chỗ, sửa một chỗ thì bộ kiểm thử vẫn nối vào máy chủ cũ.

Nhớ đọc lại hai điểm chặn production ở `BAO_CAO_PHAN_TICH_NAILMANAGEMENT.md` trước khi mang đi
đâu: cookie phiên luôn `Secure = false`, và ứng dụng tự chạy migration + tự nạp tài khoản demo ở
mọi môi trường.

### Collation — đọc trước khi đổi máy chủ

Máy chủ hiện dùng **`Vietnamese_CI_AS`** (trình cài đặt SQL Server chọn theo ngôn ngữ Windows).
Đây là lựa chọn đúng cho một phần mềm tiếng Việt: tiếng Việt coi "Ch" là một chữ cái riêng đứng
sau "C", nên danh sách dịch vụ và nhân viên sắp đúng thứ tự bảng chữ cái.

Điều đó **quan sát được từ ứng dụng**, không phải chi tiết vô hình: `ServiceRepository` và
`StaffRepository` sắp danh sách bằng `ThenBy(Name)`, tức sắp **trong database**, nên thứ tự trả
về đổi theo collation của máy chủ:

| | `Vietnamese_CI_AS` | `SQL_Latin1_General_CP1_CI_AS` |
|---|---|---|
| Dịch vụ đầu danh sách | Combo cưới (200 phút) | Chăm sóc da chân (55 phút) |

Mọi cột chữ đều là `nvarchar` nên tiếng Việt lưu và đọc đúng ở mọi collation — khác biệt chỉ nằm
ở **thứ tự sắp xếp**. Nhưng thứ tự ấy từng làm 11 phép thử đỏ, vì `SalonScenario` lấy dịch vụ đầu
danh sách và trước đây giả định nó luôn ngắn. Chỗ ấy đã sửa (khung giờ cách nhau 8 tiếng, rộng
hơn dịch vụ dài nhất), và bộ kiểm thử nay xanh trên cả hai collation.
