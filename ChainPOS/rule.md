# Rule cho AI khi làm dự án ChainPOS

File này dùng để AI ở các lượt sau hiểu đúng bối cảnh, quyền hạn và cách làm việc trong dự án `ChainPOS`.

## 1. Bối cảnh dự án

- Dự án hiện tại là `ChainPOS`, ASP.NET Core MVC Razor Views, database-first từ SQL Server.
- Model/entity đã scaffold từ database, nằm trong thư mục `Models`.
- Backlog và trạng thái công việc nằm trong `Task.md`.
- Không tự suy đoán lại kiến trúc nếu trong code hoặc `Task.md` đã có hướng triển khai.

## 2. Quyền hạn của AI

AI được phép:

- Đọc source code, model, controller, service, view và `Task.md` trước khi làm.
- Tạo hoặc sửa file code cần thiết để hoàn thành đúng hạng mục người dùng yêu cầu.
- Tạo controller, service, ViewModel, Razor view, CSS/JS nếu hạng mục yêu cầu.
- Chạy build/test/manual check để xác nhận chức năng sau khi code.
- Cập nhật `Task.md` sau khi hoàn thành từng phần.

AI không được phép:

- Tự ý đổi schema database hoặc sửa entity scaffolded nếu không thật sự cần.
- Tự ý xóa hoặc revert code người dùng đã làm.
- Tự ý thiết kế UI mới khi đã có UI mẫu.
- Tự ý bỏ qua `Task.md`.
- Tự ý tick task chưa làm hoặc chưa kiểm tra hợp lý.

## 3. Rule bắt buộc về UI

Khi code bất kỳ màn hình UI nào, AI bắt buộc phải lấy mẫu UI từ thư mục:

`D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI`

Quy tắc cụ thể:

- Trước khi làm UI, phải kiểm tra trong thư mục UI xem có file HTML/CSS/ảnh mẫu phù hợp không.
- Nếu có file mẫu phù hợp, phải bám theo layout, màu sắc, spacing, component và style của file đó.
- Không tự design UI từ đầu nếu trong thư mục UI đã có mẫu liên quan.
- Nếu thiếu một phần nhỏ trong mẫu, chỉ được bổ sung theo đúng phong cách UI hiện có.
- Dashboard phải ưu tiên lấy từ `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\dashboard.html`.
- Login phải ưu tiên lấy từ `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\index.html`.

## 4. Rule bắt buộc về Task.md

Sau khi code xong một phần, AI bắt buộc phải cập nhật `Task.md`.

Quy tắc cụ thể:

- Làm xong hạng mục nào thì tick `[x]` đúng hạng mục đó.
- Nếu chỉ làm một phần, chỉ tick phần thật sự đã hoàn thành.
- Nếu phát sinh hạng mục mới cần theo dõi, thêm vào `Task.md` ở phase phù hợp.
- Không tick task chỉ vì đã tạo file, nếu nghiệp vụ chưa chạy hoặc chưa đủ điều kiện hoàn thành.
- Khi test được bằng build/manual check, cập nhật thêm các checklist test tương ứng nếu có trong `Task.md`.

## 5. Rule khi bắt đầu một yêu cầu mới

Trước khi code, AI nên làm theo thứ tự:

1. Đọc yêu cầu mới nhất của người dùng.
2. Đọc `Task.md` để biết đang ở phase nào.
3. Đọc model/entity liên quan trong `Models`.
4. Đọc controller/service/view hiện có để theo đúng pattern dự án.
5. Nếu có UI, đọc file mẫu trong `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI`.
6. Code đúng phạm vi yêu cầu.
7. Build/test.
8. Cập nhật `Task.md`.
9. Báo lại ngắn gọn đã làm gì, test gì, còn gì tiếp theo.

## 6. Rule về nghiệp vụ và phân quyền

- `ADMIN` quản lý platform: owner, tenant, subscription, system payment, audit.
- `OWNER` quản lý dữ liệu trong tenant của mình: store, staff, product, inventory, order.
- `STAFF` chỉ thao tác trong tenant và store được gán qua `UserStores`.
- Mọi query của `OWNER` và `STAFF` phải lọc theo `TenantId`.
- Mọi thao tác theo store của `STAFF` phải kiểm tra `UserStores.IsActive = true`.
- Không tin `TenantId`, `StoreId`, `UserId` gửi từ client nếu có thể lấy từ current user.

## 7. Rule về code

- Không bind trực tiếp entity scaffolded từ request; dùng ViewModel/InputModel.
- Controller chỉ điều phối request/response, nghiệp vụ đặt trong service.
- Các thao tác quan trọng phải ghi audit log nếu đã có audit service.
- Action POST phải có antiforgery token.
- Action nguy hiểm phải có confirm modal.
- Validate server-side đầy đủ.
- Không hard-code password production.

## 8. Trạng thái ưu tiên hiện tại

Theo `Task.md`, các phần đã làm gần đây:

- Authentication/login theo 3 role.
- Dashboard/layout theo role.
- Tenant/store access nền tảng.
- Admin quản lý Owner.
- Admin quản lý Tenant.

Hướng nên làm tiếp theo:

1. Owner quản lý Store.
2. Owner quản lý Staff và gán Store.
3. Product/Category.
4. Inventory.
5. Shift/POS.

