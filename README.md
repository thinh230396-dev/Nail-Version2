# NailManagement — máy chủ SalonSys

Backend của **SalonSys**, hệ thống SaaS đa tiệm quản lý chuỗi tiệm nail. ASP.NET Core 10 +
EF Core + SQL Server 2022, tổ chức theo Clean Architecture với năm project.

Giao diện nằm ở một repo khác: `C:\QLTiemNail_vs1` (React 19 + Vite). Lúc phát triển, máy chủ này
chỉ trả `/api` và Vite phục vụ giao diện; khi có bản build trong `wwwroot/` thì nó phục vụ luôn cả
giao diện trên cùng một cổng (xem §1).

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

Do `DemoAccountSeeder` nạp, chỉ khi bảng tài khoản còn trống — và **chỉ ở môi trường Development, khi cờ `DemoSeed:Enabled` được bật** (`NailManagement.API/Startup/DemoSeedPolicy.cs`). Cờ ấy nằm sẵn trong `appsettings.Development.json` nên máy phát triển không phải làm gì thêm.

Ngoài Development thì máy chủ **không** tạo tài khoản nào. Muốn có lối vào đầu tiên thì đặt hai biến môi trường `Bootstrap__AdminEmail` và `Bootstrap__AdminPassword`; thiếu chúng, máy chủ ghi một dòng cảnh báo rồi chạy tiếp với database trống. Mật khẩu cố định trong mã nguồn cố ý không còn được dùng ở bất kỳ đâu ngoài máy phát triển.

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

> ⚠️ Các mật khẩu trên nằm cứng trong mã nguồn, nên chúng **chỉ được nạp ở Development** và chỉ
> khi cờ `DemoSeed:Enabled` bật — `DemoSeedPolicy` bỏ qua cờ ấy ở mọi môi trường khác. Đừng bật
> môi trường Development trên một máy chủ mà người ngoài truy cập được.

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
dotnet test                              # cả hai project: 207 phép thử, ~20 giây
dotnet test NailManagement.UnitTests     # chỉ luật Domain: 59 phép thử, dưới 1 giây, không cần SQL Server
```

Hai tầng, hai mục đích:

- **`NailManagement.UnitTests`** — công thức tiền, trạng thái hóa đơn, vòng đời lịch hẹn, hạng khách,
  chuẩn hóa số điện thoại và email. Chỉ tham chiếu Domain; chạy được ở bất kỳ máy nào.
- **`NailManagement.Tests`** — 148 phép thử đi trọn đường ống HTTP trên SQL Server thật: phân quyền,
  cách ly tiệm, giao dịch, tranh chấp ghi.

Bộ tích hợp dựng máy chủ **trong bộ nhớ** qua `WebApplicationFactory` và chạy trên một database
dùng một lần — `NailManagementTests`, xóa và dựng lại ở đầu mỗi lần chạy — nên nó **không đụng
tới database demo**, chạy bao nhiêu lượt cũng không làm bẩn dữ liệu trình bày.

Nhưng phải **dừng máy chủ trước khi chạy**: tiến trình đang chạy giữ khóa các tệp DLL, và
`dotnet test` sẽ đỏ ngay ở bước build với `MSB3027 … being used by another process` — một lỗi
trông như hỏng mã nguồn trong khi thật ra chỉ là hai tiến trình tranh nhau một tệp.

Bố cục: `Infrastructure/` là bộ khung (client, factory, database tạm), `Scenarios/SalonScenario.cs`
dựng dữ liệu nghiệp vụ dùng chung, và các lớp kiểm thử gom theo vùng luật — `Authorization/`,
`Isolation/`, `Appointments/`, `Invoices/`, `Payments/`, `Sessions/`, `Startup/`.

Máy khác thì đặt biến `NAILMANAGEMENT_TEST_DB` để thay cả chuỗi kết nối của bộ kiểm thử — ví dụ
SQL Server chạy trong Docker, nơi không có đăng nhập Windows:

```bash
NAILMANAGEMENT_TEST_DB="Server=localhost,1433;Database=NailManagementTests;User Id=sa;Password=...;TrustServerCertificate=True" dotnet test
```

CI trên GitHub Actions (`.github/workflows/ci.yml`) chạy đúng cách đó ở mỗi push và pull request:
build Release, kiểm model khớp migration (`dotnet ef migrations has-pending-model-changes`), rồi
chạy toàn bộ bộ kiểm thử trên một container SQL Server 2022.

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
NailManagement.Domain          Theo aggregate: Salon/{Appointments, Invoices, Customers, …} · Platform/{Tenants, Packages, Subscriptions}
                               · Auth · Auditing · Access · Shared · ValueObjects — mỗi thư mục chứa entity, enum, policy và cổng repository của nó
NailManagement.Application     Features/<nghiệp vụ>/{UseCases, DTO, Mapper, ReadService, DI} · Abstractions · Common
NailManagement.Infrastructure  Persistence/<cùng cây với Domain>/{Configuration, Repository} · Migrations · Seed · Security · Auditing
NailManagement.API             Controllers · Middleware · Security · Startup · Program.cs
NailManagement.Tests           xUnit chạy qua HTTP thật, trên SQL Server thật
```

`AddApplication()` và `AddInfrastructure()` là **điểm ráp nối duy nhất** — API không biết use
case cần gì, cũng không biết repository cài bằng gì.

51 endpoint nghiệp vụ trên 14 controller: xác thực và phiên, tiệm, chi nhánh, dịch vụ, nhân sự,
khách hàng, lịch hẹn, hóa đơn bán hàng và thu tiền, báo cáo doanh thu, nhật ký kiểm toán, quản trị
phiên đăng nhập — cộng hai endpoint sức khỏe ở §6.

Thiết lập chung của cả solution nằm ở gốc repo: `Directory.Build.props` (net10.0, nullable, cảnh
báo là lỗi), `Directory.Packages.props` (mỗi package **một** phiên bản cho cả năm project),
`global.json` (SDK 10), `.editorconfig` và `.gitattributes`.

### Giữa các aggregate chỉ tham chiếu bằng mã

Lịch hẹn giữ `CustomerId`, `StaffId`, `BranchId` — **không** giữ thuộc tính điều hướng sang
`Customer`, `Staff`, `Branch`. Tiệm giữ `PackageId`, không giữ `Package`. Khóa ngoại ở database vẫn
nguyên (kể cả khóa ghép kèm cột tiệm); chỉ phía C# bỏ đường đi tắt từ aggregate này sang aggregate
khác.

- **Ghi:** một use case muốn sửa khách thì đọc khách qua `ICustomerRepository`, không "tiện tay"
  sửa qua lịch hẹn.
- **Đọc:** tên hiển thị được nối ở tầng Application. `SalonDirectoryReader` đọc chi nhánh, khách
  và nhân viên của cả một danh sách bằng ba truy vấn; `SalesInvoiceReadService` và
  `AppointmentReadService` ghép chúng vào DTO. `TenantPlanReader` làm việc tương tự cho tiệm + gói.
- Tham chiếu ngược **trong cùng** một aggregate (dòng hóa đơn → hóa đơn) vẫn giữ.

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
| Chuỗi kết nối của bộ kiểm thử | biến môi trường `NAILMANAGEMENT_TEST_DB` | `Server=localhost`, database `NailManagementTests` |
| `Database:MigrateOnStartup` | `appsettings.*.json` hoặc biến `Database__MigrateOnStartup` | `true` ở Development, `false` ở nơi khác |
| `DemoSeed:Enabled` | `appsettings.Development.json` | `true` — bị bỏ qua ngoài Development |
| `Bootstrap:AdminEmail`, `Bootstrap:AdminPassword` | biến môi trường | trống — tài khoản quản trị đầu tiên ngoài Development |
| `Auth:LoginRateLimit` | `appsettings.json` | 30 lần / 300 giây mỗi IP |
| `ReverseProxy:KnownProxies`, `ReverseProxy:KnownNetworks` | biến môi trường khi triển khai | trống — chỉ tin proxy trên loopback |
| Cổng HTTP | `Properties/launchSettings.json`, hồ sơ `http` | `5282` |

Chuỗi kết nối của bộ kiểm thử **không đọc `appsettings`** — nó phải sẵn sàng trước cả khi host được
dựng, lý do đầy đủ ở chú thích trong `SalonSysFactory`. Đổi máy chủ thì đặt `NAILMANAGEMENT_TEST_DB`.

### Chạy sau reverse proxy

Đứng sau nginx, IIS hay một bộ cân bằng tải thì máy chủ chỉ thấy IP **của proxy**. Khai IP proxy để nó
đọc IP thật từ `X-Forwarded-For` — không khai thì giới hạn đăng nhập theo IP gom mọi người dùng vào một
ngăn, và nhật ký, danh sách phiên ghi toàn một địa chỉ:

```bash
ReverseProxy__KnownProxies__0=10.0.0.5        # hoặc cả dải: ReverseProxy__KnownNetworks__0=10.0.0.0/24
```

`X-Forwarded-For` từ bất kỳ nguồn nào khác đều bị bỏ qua, để không ai tự xưng một IP mới cho mỗi lần
dò mật khẩu. Ngoài Development máy chủ còn bật HSTS, và mọi phản hồi mang `X-Content-Type-Options`,
`X-Frame-Options`, `Referrer-Policy`.

Mỗi thân lỗi mang `traceId` — cùng giá trị với trường `TraceId` trong log JSON của máy chủ (log ở dạng
JSON ngoài Development). Người dùng chép mã ấy khi báo lỗi là tra được đúng request.

### Kiểm tra sức khỏe

| Endpoint | Nghĩa | Chạm database |
|---|---|---|
| `GET /api/health` | **Sống** — tiến trình còn trả lời | Không |
| `GET /api/health/ready` | **Sẵn sàng** — nối được database; `503` khi không | Có |

Cả hai không cần đăng nhập, và không trả chi tiết lỗi ra ngoài.

### Migration khi triển khai

Ở Development máy chủ tự áp migration lúc khởi động. Ở mọi môi trường khác thì **không**: nhiều
bản chạy song song sẽ tranh nhau sửa lược đồ, và tài khoản database của ứng dụng không nên có quyền
DDL. Migration là một bước riêng, chạy một lần trước khi mở bản mới:

```bash
dotnet ef migrations bundle --project NailManagement.Infrastructure --startup-project NailManagement.API -o efbundle
./efbundle --connection "<chuỗi kết nối production>"
```

Mở máy chủ khi database còn migration chưa áp thì nó **dừng ngay lúc khởi động** và liệt kê tên
migration còn thiếu, thay vì hỏng ở request đầu tiên chạm vào cột mới.

Bundle không cần `appsettings.json` bên cạnh: công cụ EF dựng DbContext qua
`DesignTimeNailDbContextFactory`, không chạy máy chủ web. Cũng vì vậy các lệnh `dotnet ef` trên máy
phát triển nhắm vào `Server=localhost;Database=NailManagement`, trừ khi đặt `ConnectionStrings__Default`.

### Docker

```bash
docker build --target migrator -t salonsys-migrate .
docker build --target runtime  -t salonsys-api .

docker run --rm salonsys-migrate --connection "<chuỗi kết nối>"          # một lần, trước khi mở bản mới
docker run -p 8080:8080 \
  -e ConnectionStrings__Default="<chuỗi kết nối>" \
  -e Bootstrap__AdminEmail=... -e Bootstrap__AdminPassword=... \
  salonsys-api
```

Ảnh `runtime` chạy môi trường `Production` dưới người dùng không phải root, cổng 8080, và **không**
kèm giao diện: `wwwroot/` bị loại khỏi ngữ cảnh build để ảnh giống nhau dù build trên máy nào. Máy
phát triển chưa có Docker nên Dockerfile được kiểm ở CI (job `docker-image`), chưa chạy thử tại chỗ.

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
