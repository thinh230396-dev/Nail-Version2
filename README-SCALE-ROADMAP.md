# Roadmap phát triển SalonSys cho khách hàng thực tế

Ngày tạo: **18/09/2026**. Phiên bản tài liệu: **1.0**.

**Trạng thái: đang chốt yêu cầu, chưa triển khai thay đổi code theo roadmap này.**

Roadmap này theo dõi việc tổ chức lại backend và chuyển các nghiệp vụ còn demo thành chức năng dùng thật. Đây là kế hoạch mới cho mục tiêu thương mại hóa dự án cá nhân; không phải báo cáo rằng hệ thống đã sẵn sàng vận hành thực tế.

## 1. Mục tiêu và phạm vi

### Đã thống nhất với chủ dự án

- Dự án cá nhân, mục tiêu đưa cho khách hàng sử dụng.
- Hoàn thiện các tính năng còn demo hoặc chưa hoạt động đầy đủ.
- Cấu trúc cần sạch, dễ mở rộng và chỉnh sửa.
- Tổ chức Application theo từng feature là hướng được chủ dự án lựa chọn.
- Hiện tại chỉ tạo tài liệu; chưa sửa code ứng dụng.

### Phương án kỹ thuật đề xuất

- Tiếp tục phát triển trên các tầng Domain, Application, Infrastructure và API hiện có.
- Gom Application theo nghiệp vụ, làm từng phần và kiểm chứng sau mỗi phần.
- Làm backend trước, sau đó nối frontend của từng feature vào API tương ứng.
- Giữ ổn định API đang được frontend sử dụng khi tổ chức lại folder; ghi rõ mọi thay đổi contract nếu nghiệp vụ mới yêu cầu.
- Xác định quyền, tenant, giao dịch và audit cho mỗi luồng ghi dữ liệu.
- Thứ tự các đợt dưới đây là **đề xuất**, chưa phải lựa chọn tính năng cho bản giao khách hàng đầu tiên.

### Hai kho mã liên quan

| Phần | Thư mục hiện tại |
|---|---|
| Backend | `C:\Users\letru\source\repos\NailManagement` |
| Frontend | `C:\QLTiemNail_vs1` |

Backend gồm `NailManagement.Domain`, `NailManagement.Application`, `NailManagement.Infrastructure`, `NailManagement.API` và `NailManagement.Tests`.

## 2. Cách sử dụng roadmap

- `[ ]`: chưa hoàn thành. `[x]`: đã hoàn thành và có bằng chứng kiểm chứng.
- Không đánh dấu xong chỉ vì đã viết code hoặc màn hình đã hiện dữ liệu.
- Bắt đầu mỗi đợt bằng việc chốt yêu cầu của đợt đó, phạm vi file và tiêu chí nghiệm thu.
- Khi kết thúc một đợt, cập nhật checklist, kết quả kiểm tra và mục thay đổi ở cuối file.
- Giữ nguyên các mục chưa triển khai; cập nhật thứ tự hoặc phạm vi khi chủ dự án quyết định.
- Ghi rõ kết quả là đọc mã nguồn, kiểm tra kiểu/build, kiểm thử tự động hay thao tác thực tế.

**Hiện trạng xác minh:** đã đọc mã nguồn và đối chiếu nguồn dữ liệu. Các phát hiện nghiệp vụ chưa được tái hiện bằng runtime trong lượt lập roadmap. Kiểm tra TypeScript trước đó đã qua; bộ kiểm thử backend chưa được chạy trong lượt phân tích.

## 3. Hiện trạng tính năng

### 3.1. Đã có luồng API và SQL Server

| Nhóm | Hiện trạng và giới hạn cần lưu ý |
|---|---|
| Đăng nhập, phiên, chọn tiệm | Có API thật; đọc lại trạng thái tài khoản và quyền với tiệm mỗi request |
| Tiệm và tài khoản quản trị | Có API thật; cần rà tính nhất quán giao dịch/audit ở các luồng ghi |
| Chi nhánh, dịch vụ, nhân viên, khách hàng | Có API thật ở chế độ live |
| Lịch hẹn lõi | Có API thật; trang lịch hẹn riêng của lễ tân đang thiếu dữ liệu đầu vào để bật live |
| Hóa đơn bán hàng, thu và hoàn tiền | Có API thật; bàn lễ tân đã sử dụng luồng này |
| Báo cáo doanh thu | Có API thật; cần sửa cách phân bổ theo ngày và phần dư làm tròn theo dịch vụ |
| Gói và hóa đơn đăng ký | Có đường đọc thật; các luồng quản lý/thanh toán chưa đầy đủ |

**Dữ liệu được seed để demo khác với nghiệp vụ chỉ chạy demo.** Một entity có dữ liệu mẫu trong SQL Server vẫn có thể được xử lý qua API thật. Các component hỗ trợ cả demo/live phải được đánh giá theo đường đang chạy trong portal.

### 3.2. Cần xây backend hoặc hoàn thiện kết nối

| Nhóm | Hiện trạng từ code | Hướng xử lý dự kiến |
|---|---|---|
| Trang POS/Thanh toán riêng | Lưu giao dịch và dữ liệu liên quan cục bộ | Dùng chung backend hóa đơn/thu tiền với bàn lễ tân |
| Kho và vật tư | Tồn kho, định mức, phiếu nhập lưu trên trình duyệt | Xây nghiệp vụ nhập/xuất/điều chỉnh và lưu SQL Server |
| Loyalty/thành viên/ưu đãi | Chương trình, hạng thẻ và dữ liệu điểm lưu cục bộ | Xây quy tắc tích/tiêu/hoàn điểm và lịch sử giao dịch |
| Đặt lịch online | Lịch ở màn này chưa vào bảng lịch hẹn thật | Kết nối với lịch hẹn lõi và xây luồng đặt lịch công khai theo yêu cầu |
| Ghế/khu vực phục vụ | Sơ đồ, trạng thái và công suất cục bộ | Xây dữ liệu theo chi nhánh; chốt ý nghĩa việc phân ghế |
| Thư viện mẫu nail/màu | Mẫu, ảnh và bộ sưu tập lưu trên trình duyệt | Lưu metadata và xây cách lưu/đọc ảnh thực tế |
| Thu và chi | Sổ thu chi cục bộ | Xây chi phí và báo cáo liên kết với dòng thu/hoàn tiền thật |
| Vệ sinh/an toàn | Checklist, khử khuẩn, sự cố cục bộ | Xây nhật ký và quyền thao tác theo tiệm/chi nhánh |
| Báo cáo ngoài doanh thu | Một số tab dùng dữ liệu trình diễn | Chốt chỉ số và xây truy vấn từ dữ liệu thật |
| Quản lý gói | API hiện chỉ đọc danh sách | Bổ sung quản lý gói, phiên bản, quyền và hạn mức |
| Nâng gói/phí phần mềm | Đọc thông tin thật; nhiều thao tác ghi còn cục bộ | Xây yêu cầu nâng gói, chứng từ và xác nhận thanh toán |
| Ticket hỗ trợ/bản tin | Nội dung và thao tác chưa có backend đầy đủ | Xây lưu trữ, phân quyền và đối tượng được xem |
| Cài đặt tiệm | Nhiều thiết lập lưu trên trình duyệt | Tách tùy chọn giao diện khỏi cấu hình nghiệp vụ cần backend áp dụng |
| Sao lưu/khôi phục trên giao diện | Trạng thái snapshot/job được mô phỏng/lưu cục bộ | Thiết lập backup SQL Server thật, kiểm chứng restore trước khi xây UI quản lý |

`localStorage` có thể phù hợp cho tùy chọn giao diện hoặc bản nháp được thiết kế rõ. Dữ liệu nghiệp vụ cần chia sẻ giữa người dùng phải có nguồn dữ liệu và quy tắc kiểm soát ở backend. Không chuyển toàn bộ dữ liệu demo vào database một cách máy móc.

## 4. Các quyết định còn cần chốt

| ID | Câu hỏi | Quyết định hiện tại |
|---|---|---|
| DEC-01 | Tính năng nào bắt buộc có ở bản đầu giao khách hàng? Thứ tự ưu tiên? | Chưa chốt |
| DEC-02 | Quy trình khách trả tiền cho tiệm và tiệm trả phí phần mềm cho chủ dự án? Ai xác nhận, ai hoàn tiền? | Chưa chốt |
| DEC-03 | Vai trò nào cần đăng nhập và quyền cụ thể của từng vai trò? | Chưa chốt |
| DEC-04 | Có dữ liệu hiện tại cần giữ hoặc chuyển đổi không? Dữ liệu SQL Server và dữ liệu trình duyệt nào là dữ liệu cần dùng? | Chưa chốt |
| DEC-05 | Khách hàng đầu tiên dự kiến bao nhiêu tiệm/chi nhánh và người cùng thao tác? | Chưa chốt |

Các vai trò chính đang có là Superadmin, chủ tiệm và lễ tân. Không mặc định rằng kỹ thuật viên, quản lý chi nhánh hoặc quản lý kho đã có luồng đăng nhập/quyền đầy đủ.

Backend hiện dùng VND và quy ước ngày theo giờ Việt Nam. Nếu khách hàng yêu cầu khác, cần bổ sung quyết định nghiệp vụ trước khi mở rộng.

## 5. Cấu trúc Application mục tiêu

Ví dụ minh họa, **chưa phải cấu trúc đã triển khai**:

```text
NailManagement.Application/
├── Features/
│   ├── Services/
│   │   ├── UseCases/
│   │   │   ├── ListServicesUseCase.cs
│   │   │   ├── CreateServiceUseCase.cs
│   │   │   ├── UpdateServiceUseCase.cs
│   │   │   └── ChangeServiceStatusUseCase.cs
│   │   ├── ServiceDtos.cs
│   │   ├── ServiceMapper.cs
│   │   └── DependencyInjection.cs
│   ├── Appointments/
│   │   ├── UseCases/
│   │   ├── AppointmentDtos.cs
│   │   ├── AppointmentMapper.cs
│   │   ├── AppointmentBookingGuard.cs
│   │   └── DependencyInjection.cs
│   ├── SalesInvoices/
│   ├── Customers/
│   ├── Staff/
│   ├── Branches/
│   ├── Auth/
│   ├── Accounts/
│   ├── Sessions/
│   ├── Tenants/
│   ├── Packages/
│   ├── Subscriptions/
│   ├── Reports/
│   └── Audit/
├── Abstractions/
├── Common/
└── DependencyInjection.cs
```

Feature mới như Inventory hay Loyalty được thêm khi bắt đầu triển khai; không tạo trước hàng loạt folder rỗng. Chỉ tách thêm thư mục DTOs/Mappings/Commands khi kích thước feature thực sự cần.

### Quy tắc trách nhiệm và phụ thuộc

- **Domain:** entity, value object, policy và quy tắc hợp lệ của nghiệp vụ; không phụ thuộc HTTP, EF Core hoặc frontend.
- **Application:** điều phối luồng, đọc dữ liệu qua interface, kiểm tra quyền theo nội dung/phạm vi và xác định ranh giới giao dịch.
- **API:** nhận HTTP, ánh xạ request sang đầu vào use case, xác thực/phân quyền endpoint và trả response.
- **Infrastructure:** EF Core, SQL Server, repository, hashing, lưu file và các tích hợp bên ngoài.
- DTO HTTP của API và đầu vào Application giữ đúng vai trò; không để Application phụ thuộc request type của API.
- `Common`/`Abstractions` chỉ chứa phần thực sự dùng chung; không gom mọi nghiệp vụ vào một service hoặc repository tổng quát.
- Luồng tác động nhiều feature phải mô tả rõ ai điều phối và dữ liệu nào phải cùng giao dịch. Ví dụ thu tiền và hoàn tất lịch hẹn.
- Các lớp trong cùng project vẫn có thể tham chiếu nhau dù đã chia folder. Cần ghi và kiểm tra quy tắc phụ thuộc; đổi folder không tự tạo ranh giới kiến trúc.

## 6. Các giai đoạn triển khai

### M0 — Chốt yêu cầu và đường cơ sở

**Đầu ra:** phạm vi đợt đầu, contract hiện tại và tiêu chí kiểm chứng.

- [ ] Chốt DEC-01 đến DEC-05 ở mức cần thiết cho đợt đầu.
- [ ] Ghi các màn hình/API liên quan và hành vi mong muốn của tính năng ưu tiên.
- [ ] Đối chiếu route, request/response, mã lỗi và quyền frontend đang sử dụng.
- [ ] Ghi kết quả build/kiểm tra kiểu và bộ kiểm thử hiện có trước khi sửa.
- [ ] Tái hiện các phát hiện cần sửa bằng tình huống kiểm thử phù hợp.
- [ ] Xác định database kiểm thử riêng và dữ liệu cần bảo toàn.

**Lưu ý thực thi có căn cứ từ code:** test factory backend dựng/xóa database kiểm thử; script frontend `rehearsal` tạo dữ liệu trên backend đang chạy. Chỉ dùng với môi trường được xác định cho kiểm thử.

**Nghiệm thu:** biết rõ sửa gì, giữ gì và cách xác định thay đổi có đúng hay không. Không lấy kết quả compile làm bằng chứng toàn bộ nghiệp vụ đúng.

### M1 — Tổ chức Application theo feature

**Đầu ra:** mẫu feature thống nhất, áp dụng dần cho nghiệp vụ hiện có.

- [ ] Gom Services làm feature mẫu: use case, DTO, mapper và đăng ký DI.
- [ ] Cập nhật namespace/import và các tham chiếu từ controller, DI, test.
- [ ] Kiểm tra build và các luồng Services liên quan; giữ nguyên hành vi/API trong đợt di chuyển.
- [ ] Áp dụng mẫu cho Customers, Staff và Branches.
- [ ] Áp dụng cho Appointments, SalesInvoices và Reports theo các quan hệ hiện có.
- [ ] Áp dụng cho Auth, Accounts, Sessions, Tenants, Packages, Subscriptions và Audit.
- [ ] Rà phần dùng chung, loại bỏ bản sao và ghi quy tắc phụ thuộc giữa feature.
- [ ] Cập nhật tài liệu thêm feature mới theo mẫu đã dùng.

**Nghiệm thu:** tìm được code của một nghiệp vụ trong một vùng rõ ràng; tầng trong vẫn không phụ thuộc HTTP/EF Core; frontend sử dụng được contract hiện tại. Tách phần di chuyển code khỏi thay đổi nghiệp vụ để dễ review.

### M2 — Hoàn thiện vận hành lõi và nối dữ liệu thật

**Đầu ra:** lịch hẹn, POS/hóa đơn và báo cáo nhất quán trên dữ liệu backend.

- [ ] Sửa đầu vào live của trang lịch hẹn riêng trong cổng lễ tân; kiểm tra cả đường đặt lịch từ hồ sơ khách.
- [ ] Bổ sung quy tắc kỹ thuật viên và trạng thái chi nhánh trong guard dùng chung; chốt ngoại lệ cho lịch cũ trước khi áp dụng.
- [ ] Chuyển POS riêng sang backend hóa đơn/thu/hoàn tiền hiện có; danh mục khách, dịch vụ và nhân viên phải dùng nguồn dữ liệu phù hợp.
- [ ] Tải lại hoặc reset đúng state khi đổi tiệm; xử lý response cũ để không hiển thị/ghi dữ liệu sang tiệm khác.
- [ ] Sửa báo cáo theo từng ngày thu/hoàn thực tế; kiểm tra hóa đơn thu qua nhiều ngày và hoàn ở ngày sau.
- [ ] Bảo đảm tổng phân bổ theo dịch vụ khớp tổng tiền sau làm tròn.
- [ ] Thống nhất chính sách audit trong/ngoài giao dịch theo từng thao tác; xử lý trường hợp dữ liệu đã lưu nhưng ghi audit lỗi.
- [ ] Chống gửi lặp lệnh thu/hoàn tiền; định nghĩa cách nhận diện một yêu cầu lặp.
- [ ] Củng cố xử lý đồng thời cho lịch chồng giờ, thu/hoàn tiền, cấp mã hóa đơn và hạn mức theo quy mô đã chốt.
- [ ] Bổ sung kiểm thử có ý nghĩa cho lỗi nghiệp vụ, quyền, tenant/chi nhánh và các tình huống đồng thời liên quan.

**Nghiệm thu:** thao tác trên hai phiên/trình duyệt cùng tiệm nhìn thấy dữ liệu đã lưu sau khi tải lại; các tiệm khác không truy cập được; request thất bại không tự rơi sang dữ liệu demo. Giao dịch tài chính không bị nhân đôi bởi một lần gửi lại.

### M3 — Hoàn thiện quản lý gói và phí phần mềm

Đợt này phụ thuộc DEC-02 và mô hình bán phần mềm. Thứ tự có thể đổi theo DEC-01.

- [ ] Chốt chu kỳ phí, trial, gia hạn, nâng/hạ gói, thời điểm có hiệu lực và cách tính phần chênh lệch.
- [ ] Xây quản lý gói và phiên bản; bảo toàn thông tin đã chốt trên chứng từ cũ.
- [ ] Xây yêu cầu nâng gói: gửi, hủy, duyệt/từ chối và lịch sử trạng thái.
- [ ] Xây nộp chứng từ/xác nhận thanh toán phí phần mềm; chốt cách lưu tệp nếu có.
- [ ] Liên kết hóa đơn đăng ký, thanh toán, quyền sử dụng và hạn dùng một cách nhất quán.
- [ ] Mở rộng quyền theo gói và hạn mức ở backend cho các module mới; không chỉ khóa nút trên frontend.
- [ ] Nối các màn Superadmin và chủ tiệm; bỏ việc chỉ sửa bản sao cục bộ trong chế độ dùng thật.
- [ ] Kiểm tra lịch sử hóa đơn khi đổi giá/gói, tiệm hết hạn, tiệm bị khóa và request gửi lặp.

**Nghiệm thu:** thay đổi được lưu ở backend, đúng người duyệt, đúng thời điểm hiệu lực; tiệm bị hết hạn hoặc thiếu quyền không vượt kiểm tra bằng cách gọi API trực tiếp.

### M4 — Chuyển các module demo còn lại thành nghiệp vụ thật

Mỗi nhóm dưới đây là một đợt độc lập sau khi chốt yêu cầu. Không mặc định tất cả cùng thuộc bản giao khách hàng đầu tiên.

| Nhóm | Cần chốt trước khi xây | Phụ thuộc chính |
|---|---|---|
| Kho/vật tư | Đơn vị, nơi giữ tồn, nhập/xuất/kiểm kê, mức cho phép tồn âm, định mức tiêu hao và hoàn/hủy | Chi nhánh, Services, POS/hóa đơn nếu tự trừ kho |
| Loyalty | Cách tích/tiêu điểm, hạn điểm, ưu đãi, hoàn tiền và thời điểm ghi nhận | Customers, SalesInvoices |
| Đặt lịch online | Khách tự đặt hay gửi yêu cầu, xác nhận, hủy, cọc và thông tin công khai | Appointments, Staff, Services |
| Ghế/khu vực | Phân theo chi nhánh, công suất, giữ ghế và quan hệ với lịch hẹn | Branches, Appointments |
| Mẫu nail/màu | Quyền quản lý, quan hệ với dịch vụ/vật tư, lưu ảnh và phạm vi công khai | Services, Inventory nếu liên kết vật tư |
| Thu chi/báo cáo | Loại chi phí, quyền duyệt, nguồn số liệu, chỉ số và định nghĩa lợi nhuận | SalesInvoices, Inventory nếu có giá vốn |
| Nhân sự mở rộng | Ca thực tế, nhiều người làm một hóa đơn, cách phân hoa hồng và kỳ chốt | Staff, Appointments, SalesInvoices |
| Vệ sinh/an toàn | Checklist, lịch thực hiện, người xác nhận và lịch sử | Branches, Staff |
| Hỗ trợ/bản tin | Người gửi/nhận, quyền xem, trạng thái, tệp đính kèm | Auth, Tenants, lưu file nếu cần |
| Cài đặt nghiệp vụ | Thiết lập nào có hiệu lực toàn tiệm, chi nhánh hoặc tài khoản | Feature áp dụng thiết lập đó |

**Checklist áp dụng cho từng module khi triển khai:**

- [ ] Ghi yêu cầu, quyền, tenant/chi nhánh, nguồn dữ liệu và các trạng thái.
- [ ] Thiết kế entity/policy và migration cần thiết; xác định xử lý dữ liệu hiện tại.
- [ ] Xây use case, interface cần thiết và repository/query implementation.
- [ ] Xây API, validation, giao dịch và audit phù hợp.
- [ ] Nối frontend, xử lý loading/error, đổi tiệm và dữ liệu sau khi tải lại.
- [ ] Kiểm chứng luồng thành công, luồng lỗi và quyền truy cập liên quan.
- [ ] Cập nhật nhãn demo tại đúng phần đã hoàn thiện; không gỡ nhãn của chức năng chưa nối backend.

**Quy tắc cần giữ:** sổ thu chi không đếm lại cùng khoản thu từ hóa đơn; điểm có lịch sử và quy tắc đảo khi hoàn; báo cáo hoa hồng phải chốt cách bảo toàn lịch sử nếu thay đổi tỷ lệ. Nghiệp vụ liên quan phải thống nhất trước khi viết code.

### M5 — Chuẩn bị giao khách hàng và vận hành

- [ ] Chốt cấu hình môi trường Development, Test/Staging và Production.
- [ ] Kiểm chứng quy trình tạo khách hàng/tiệm và bàn giao tài khoản; xác định cách đặt lại mật khẩu.
- [ ] Rà xác thực, quyền vai trò, quyền theo gói và cách ly dữ liệu ở các API mới.
- [ ] Bổ sung log/đo đạc để tìm lỗi và truy vấn chậm; xác định health check phục vụ vận hành.
- [ ] Thiết lập backup SQL Server thật và thử restore vào môi trường riêng; xác định lịch, thời gian lưu và người vận hành.
- [ ] Tách quản lý backup khỏi màn mô phỏng; chỉ hiển thị trạng thái có nguồn xác minh thực tế.
- [ ] Xác định cách chạy migration khi triển khai và cách xử lý bản phát hành thất bại.
- [ ] Thiết lập kiểm tra build/test tự động khi thay đổi code.
- [ ] Kiểm thử tải theo số người/tiệm và dữ liệu mục tiêu, ghi cấu hình máy và kết quả đo.
- [ ] Giao bản thử nghiệm cho khách hàng đầu tiên, ghi phản hồi và hoàn thiện các luồng sử dụng chính.

**Nghiệm thu:** có hướng dẫn triển khai, hỗ trợ, backup/restore và kết quả kiểm chứng phạm vi đã chọn. Không cam kết số người dùng chỉ dựa trên cách chia tầng hoặc folder.

## 7. Mẫu ghi một tính năng trước khi sửa

Sao chép mẫu này khi bắt đầu feature cụ thể; tên và phạm vi phải đủ rõ để review.

```markdown
### [ID] Tên tính năng
- Trạng thái: Chưa làm / Đang làm / Đang kiểm tra / Hoàn thành
- Mục tiêu và người sử dụng:
- Quy tắc đã chốt:
- Câu hỏi còn mở:
- Phụ thuộc và luồng tác động nhiều feature:
- Backend: entity/policy, use case, repository/query, API, quyền, audit/giao dịch
- Database: migration và dữ liệu cần giữ/chuyển đổi
- Frontend: màn hình, nguồn dữ liệu và thay đổi contract
- Tiêu chí nghiệm thu:
- Kết quả kiểm chứng và giới hạn còn lại:
```

## 8. Tiêu chí hoàn thành một đợt

Một đợt chỉ hoàn thành khi đáp ứng các tiêu chí áp dụng cho phạm vi đó:

- Hành vi đúng quy tắc đã chốt, dữ liệu nghiệp vụ được lưu đúng nguồn.
- Build/kiểm tra kiểu và các kiểm thử liên quan qua; ghi rõ kiểm tra nào chưa chạy.
- Quyền và phạm vi tenant/chi nhánh không bị nới lỏng khi thêm tính năng.
- Contract frontend/backend được kiểm tra; thay đổi contract được ghi rõ.
- Luồng nhiều thao tác ghi có chính sách giao dịch, lỗi và audit rõ.
- Migration và cách xử lý dữ liệu được ghi nếu schema thay đổi.
- Chế độ dùng thật báo lỗi đúng; nhãn demo phản ánh đúng phần còn demo.
- Roadmap được cập nhật cùng kết quả và các vấn đề còn mở.

## 9. Nguồn đối chiếu hiện trạng

Những tài liệu cũ có phạm vi MVP; cần phân biệt quyết định cũ với mục tiêu mở rộng của roadmap này. Khi tài liệu lệch code, ghi rõ chỗ lệch và đối chiếu đường chạy hiện tại.

- [README backend](C:/Users/letru/source/repos/NailManagement/README.md)
- [Lộ trình backend MVP trước đây](C:/QLTiemNail_vs1/README-BACKEND-ROADMAP.md)
- [Tài liệu nghiệp vụ hiện có](C:/QLTiemNail_vs1/README-BUSINESS-RULES.md)
- [Phân loại demo tại cổng chủ tiệm](C:/QLTiemNail_vs1/src/components/NailTenantAdminPortal.tsx:239)
- [Trang lịch hẹn lễ tân thiếu đầu vào live](C:/QLTiemNail_vs1/src/components/ReceptionistPortal.tsx:1986)
- [POS lưu dữ liệu cục bộ](C:/QLTiemNail_vs1/src/components/TenantAdminPayments.tsx:315)
- [Yêu cầu nâng gói lưu cục bộ](C:/QLTiemNail_vs1/src/utils/packageUpgradeRequests.ts:4)
- [Guard lịch hẹn cần bổ sung kiểm tra](C:/Users/letru/source/repos/NailManagement/NailManagement.Application/UseCases/Appointments/AppointmentBookingGuard.cs:65)
- [Báo cáo phân bổ theo ngày](C:/Users/letru/source/repos/NailManagement/NailManagement.Application/UseCases/Reports/GetRevenueReportUseCase.cs:97)
- [API gói hiện chỉ đọc](C:/Users/letru/source/repos/NailManagement/NailManagement.API/Controllers/PackagesController.cs:24)
- [API hóa đơn đăng ký hiện chỉ đọc](C:/Users/letru/source/repos/NailManagement/NailManagement.API/Controllers/SubscriptionInvoicesController.cs:39)

## 10. Nhật ký cập nhật

| Ngày | Cập nhật | Kết quả triển khai |
|---|---|---|
| 18/09/2026 | Tạo roadmap 1.0 từ yêu cầu của chủ dự án và hiện trạng mã nguồn | Chỉ thêm tài liệu; chưa sửa code ứng dụng |

**Bước tiếp theo:** chốt các quyết định trong M0, chọn phạm vi đợt đầu và điền mẫu tính năng trước khi bắt đầu sửa code.
