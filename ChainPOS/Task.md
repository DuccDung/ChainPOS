# Task triển khai ChainPOS sau khi Data First

Cập nhật ngày: 2026-06-05

Tài liệu này là backlog triển khai tiếp theo cho dự án `ChainPOS` sau khi đã scaffold model bằng EF Core từ SQL Server. Trạng thái hiện tại không còn là tạo project từ đầu, mà là phát triển tiếp trên nền database-first đã có.

## 0. AI handoff context hiện tại

Đọc phần này trước khi làm tiếp. Dự án hiện đang ở trạng thái **đã hoàn thành Phase 8.3 Audit log viewer, Phase 8.2 Subscription UI và Admin billing MVP**. Không cần làm lại các phần từ Phase 1 đến Phase 8, trừ khi phát hiện bug khi test hoặc khi hardening.

### 0.1. Trạng thái mới nhất

- [x] Phase 1: Authentication/login 3 role `ADMIN`, `OWNER`, `STAFF`.
- [x] Phase 2: Layout/dashboard theo role, sidebar/topbar/alert/confirm modal.
- [x] Phase 3: Admin quản lý Owner, Tenant, Subscription Plan và System Payment MVP.
- [x] Admin Settings MVP lưu cấu hình platform/security/billing/POS/notification bằng JSON local.
- [x] Phase 4: Owner quản lý Store, Staff và gán Staff vào Store.
- [x] Phase 5: Category, Product, Store Product.
- [x] Phase 6: Inventory import/export/adjust cho Owner/Staff.
- [x] Phase 7: Shift, POS checkout, Orders, receipt, cancel order.
- [x] Phase 8.1: Reports dùng các report views.
- [x] Phase 8.3: Audit log viewer cho Admin/Owner.
- [x] Có unit/integration test tự động bước đầu cho store access, inventory, POS, cancel order và close shift.
- [x] Phase 8.2 Subscription UI đã làm.
- [x] Admin Subscription Plan/System Payment ở Phase 3.3 và 3.4 đã làm MVP.
- [x] Có tài liệu nghiệp vụ và hướng dẫn test trực quan tại `BUSINESS_WORKFLOW_TEST_GUIDE.md`.
- [x] Có realtime SignalR cho inventory, POS order, cancel order, shift, subscription và system payment.
- [x] Có nghiệp vụ Owner quên mật khẩu bằng OTP gửi qua email SMTP.

### 0.2. Tài khoản demo

- Admin: `admin@chainpos.local` / `Admin@123`
- Owner: `owner@demo.local` / `Owner@123`
- Staff: `staff01@demo.local` / `Staff@123`

Seeder development hiện đã có dữ liệu demo cho owner/tenant/store/staff/category/product/store product/inventory. Khi cần test POS, ưu tiên store có product available và inventory còn tồn, ví dụ các store demo `TZ-HCM-01`, `TZ-HCM-02`.

Dữ liệu demo Phase 7 đã được bổ sung trong `DevelopmentDataSeeder` để nhìn trực quan:

- 4 ca bán demo cho `TZ-HCM-01` và `TZ-HCM-02`, gồm ca đã đóng và 1 ca đang mở cho `staff01@demo.local`.
- 6 đơn POS demo mã `POS-DEMO-*`, gồm đơn hoàn tất, đơn đã hủy và đơn trong ca đang mở.
- 6 payment demo với các phương thức `Cash`, `Card`, `BankTransfer`, `Momo`.
- Inventory transaction demo cho `Sale` và `Return`, có top-up tự động nếu tồn kho local không đủ để seed đơn.
- Audit log demo cho `OpenShift`, `CloseShift`, `CreateOrder`, `CancelOrder`.

Dữ liệu demo Subscription/Admin billing đã được bổ sung:

- Plan demo và tenant subscription active cho các tenant demo.
- System payment demo có trạng thái `Paid`, `Pending`, `Failed`, kèm invoice URL mẫu.
- Audit log demo cho login/logout và các thao tác subscription/payment.

### 0.3. Rule bắt buộc khi AI làm tiếp

- Trước khi code phải đọc `rule.md`, `Task.md`, model liên quan và service/controller/view hiện có.
- UI phải lấy mẫu từ `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI`; không tự design lại nếu có mẫu.
- Code xong phần nào thì tick đúng phần đó trong `Task.md`.
- Không sửa trực tiếp entity scaffolded trong `Models` nếu không thật sự cần.
- Không bind entity trực tiếp từ request; dùng ViewModel/InputModel.
- Owner/Staff phải lọc theo `TenantId`.
- Staff thao tác theo store phải check `UserStores.IsActive = true` qua `IStoreAccessService`.
- Action POST phải có antiforgery token và validate lại quyền ở server.
- Audit log các thao tác quan trọng.

### 0.4. Các module/vùng code quan trọng đã có

- Auth/current user:
  - `Services/Auth`
  - `Services/Common/ICurrentUserService.cs`
  - `Filters/RequireTenantFilter.cs`
- Store access:
  - `Services/Security/IStoreAccessService.cs`
  - `Services/Security/StoreAccessService.cs`
- Audit:
  - `Services/Audit/IAuditLogService.cs`
  - `Services/Audit/AuditLogService.cs`
- Realtime:
  - `Realtime/ChainPosHub.cs`
  - `Realtime/RealtimeGroups.cs`
  - `Services/Realtime/IRealtimeNotifier.cs`
  - `Services/Realtime/SignalRRealtimeNotifier.cs`
  - `wwwroot/js/realtime.js`
- Subscription/Admin billing:
  - `Services/Subscriptions/ISubscriptionManagementService.cs`
  - `Services/Subscriptions/SubscriptionManagementService.cs`
  - `Areas/Admin/Controllers/SubscriptionPlansController.cs`
  - `Areas/Admin/Controllers/SystemPaymentsController.cs`
  - `Areas/Admin/Controllers/SubscriptionsController.cs`
  - `Areas/Owner/Controllers/SubscriptionController.cs`
- Owner management:
  - `Services/Owner`
  - `Areas/Owner/Controllers`
  - `ViewModels/Owner`
- Inventory:
  - `Services/Inventory`
  - `ViewModels/Inventory`
  - `Views/Shared/Inventory`
  - `Areas/Owner/Controllers/InventoryController.cs`
  - `Areas/Staff/Controllers/InventoryController.cs`
- Sales/POS vừa làm:
  - `Services/Sales/IShiftService.cs`, `ShiftService.cs`
  - `Services/Sales/IPosService.cs`, `PosService.cs`
  - `Services/Sales/IOrderService.cs`, `OrderService.cs`
  - `ViewModels/Sales`
  - `Views/Shared/Shifts`
  - `Views/Shared/Pos`
  - `Views/Shared/Orders`
  - `Areas/Owner/Controllers/ShiftsController.cs`, `PosController.cs`, `OrdersController.cs`
  - `Areas/Staff/Controllers/ShiftsController.cs`, `PosController.cs`, `OrdersController.cs`

### 0.5. Phase 7 đã triển khai như thế nào

- Shift:
  - Owner/Staff mở ca tại store có quyền truy cập.
  - Chặn user mở ca thứ hai khi đang có ca `Open`.
  - Đóng ca tính `ExpectedCash = OpeningCash + tổng payment cash trong ca`.
  - Tính `DifferenceAmount = ClosingCash - ExpectedCash`.
  - Ghi audit `OpenShift`, `CloseShift`.
- POS:
  - UI chọn store, search product theo tên/SKU/barcode.
  - Chỉ lấy product `StoreProducts.IsAvailable = true`, product active, chưa soft delete.
  - Giá bán dùng `StoreProducts.SellingPrice ?? Products.Price`.
  - Hiển thị tồn kho từ `Inventories`.
  - Cart client-side bằng JavaScript nhưng backend vẫn validate lại toàn bộ.
- Checkout:
  - Bắt buộc current user có shift `Open` tại store đang bán.
  - Validate store access.
  - Validate cart không rỗng.
  - Validate tồn kho đủ.
  - Tạo `Order`, `OrderItems`, `Payments`.
  - Trừ kho trong `Inventories`.
  - Ghi `InventoryTransactions` type `Sale`.
  - Ghi audit `CreateOrder`.
  - Commit bằng database transaction.
- Orders:
  - Danh sách theo tenant/store, filter store/status/payment/date/search.
  - Chi tiết order/receipt, có nút print.
  - Cancel order đổi `OrderStatus = Cancelled`, `PaymentStatus = Cancelled`.
  - Cancel order hoàn kho, ghi `InventoryTransactions` type `Return`.
  - Ghi audit `CancelOrder`.

### 0.6. Đã test gần nhất

Đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- App chạy tại `http://localhost:5292`.
- HTTP smoke test:
  - Owner mở/đóng ca được.
  - Owner mở ca lần hai bị chặn.
  - Staff mở/đóng ca được.
  - Staff mở ca lần hai bị chặn.
  - Staff checkout khi chưa có ca mở bị chặn.
  - POS checkout quá tồn kho bị chặn.
  - Checkout tạo order và redirect sang receipt.
  - Cancel order hoàn kho đúng.
  - DB có audit `OpenShift`, `CloseShift`, `CreateOrder`, `CancelOrder`.
  - DB có inventory transaction `Sale`, `Return`.

Sau Phase 8.1 Reports đã chạy:

- `dotnet build .\ChainPOS\ChainPOS.csproj --no-restore -o .\artifacts\report-build` thành công, 0 warning, 0 error.
- HTTP smoke test tại `http://localhost:5293`:
  - Admin đăng nhập được và truy cập `/admin/reports` trả 200.
  - Owner đăng nhập được và truy cập `/owner/reports` trả 200.
  - Owner report không hiển thị System Revenue Report.

Sau khi bổ sung dữ liệu demo Phase 7 đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- Chạy app Development để seeder ghi dữ liệu vào database local.
- Query database xác nhận có 4 shift demo, 6 order `POS-DEMO-*`, 6 payment demo, 9 transaction `Sale` và 1 transaction `Return`.

Sau Phase 8.3 Audit log viewer đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- Chạy app Development tại `http://localhost:5292` để seeder ghi audit demo vào database local.
- HTTP smoke test:
  - Admin đăng nhập được và truy cập `/admin/auditlogs` trả 200.
  - Owner đăng nhập được và truy cập `/owner/auditlogs` trả 200.
  - Owner filter audit theo `Action=ImportStock` trả 200 và hiển thị log tương ứng.
- Query database xác nhận có audit `Login` cho Admin/Owner và các audit demo phục vụ màn audit viewer.

Sau Subscription/Admin billing + test hardening đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- `dotnet test .\ChainPOS.sln` thành công, 12 passed, 0 failed.
- Test tự động hiện có cho `StoreAccessService`, import/export/adjust stock, checkout yêu cầu ca mở, checkout tạo order/trừ kho, cancel order hoàn kho và close shift tính `ExpectedCash`/`DifferenceAmount`.
- Connection string máy cá nhân đã chuyển khỏi `appsettings.json`; Development đọc thêm `appsettings.Local.json` bị `.gitignore` chặn.
- Chạy app Development tại `http://localhost:5292` để seeder ghi dữ liệu demo billing vào database local.
- HTTP smoke test:
  - Admin đăng nhập được và truy cập `/admin/subscriptionplans` trả 200.
  - Admin truy cập `/admin/systempayments` trả 200.
  - Admin truy cập `/admin/subscriptions/create` trả 200.
  - Owner truy cập `/owner/subscription` trả 200.
- Query database xác nhận `SystemPayments` demo có `Paid`, `Pending`, `Failed`.

Sau Realtime SignalR đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- `dotnet test .\ChainPOS.sln` thành công, 12 passed, 0 failed.
- HTTP smoke test `/hubs/chainpos/negotiate?negotiateVersion=1` sau login Owner trả 200 và có `connectionToken`.
- HTTP smoke test `/owner/pos` xác nhận layout load `signalr.min.js`, `realtime.js` và có `data-live-page="pos"`.
- Test tự động đã assert các service phát realtime event cho import/export/adjust stock, checkout, cancel order và close shift.

Sau Owner forgot password OTP đã chạy:

- `dotnet build .\ChainPOS.sln` thành công, 0 warning, 0 error.
- `dotnet test .\ChainPOS.sln` thành công, 16 passed, 0 failed.
- SMTP Gmail được cấu hình qua .NET User Secrets, không lưu app password trong source code.
- Flow đã có UI `/forgot-password` và `/forgot-password/verify`; OTP lưu hash trong `AspNetUserTokens`, có hạn dùng và giới hạn số lần nhập sai.
- HTTP smoke test xác nhận `/forgot-password` và `/forgot-password/verify?email=owner%40demo.local` trả 200 và render đúng form chính.

Lưu ý: smoke test có thể để lại dữ liệu ca/order đã đóng/cancel trong database local. Đây là dữ liệu test hợp lệ, không tự ý xóa nếu người dùng không yêu cầu.

### 0.7. Việc nên làm tiếp theo

Phase 8.3 Audit log viewer đã có màn Admin/Owner dùng dữ liệu `AuditLogs`.

1. [x] Admin xem toàn bộ audit log.
2. [x] Owner chỉ xem audit log trong tenant của mình.
3. [x] Filter audit theo user, store, action, thời gian.

Ưu tiên tiếp theo:

1. Bổ sung test cho tạo owner/staff và các luồng admin billing chi tiết.
2. Export Excel cho report nếu cần.
3. Bổ sung low stock/recent orders vào Owner dashboard.
4. Rà soát security checklist còn lại bằng manual test.

### 0.8. Mẫu UI nên dùng khi làm tiếp

- Reports: `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\reports.html`
- Audit logs: `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\audit-logs.html`
- Orders/receipt: `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\orders.html`, `invoices.html`
- Inventory: `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\stock.html`
- Dashboard/layout: `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI\dashboard.html`

## 1. Hiện trạng dự án

### 1.1. Project hiện tại

- [x] Solution: `ChainPOS.sln`
- [x] Web project: `ChainPOS/ChainPOS.csproj`
- [x] Target framework: `.NET 9`
- [x] ASP.NET Core MVC server-rendered với Razor Views.
- [x] EF Core SQL Server đã được cài trong project.
- [x] `StoreFlowDbContext` đã được đăng ký trong `Program.cs`.
- [x] Connection string hiện đang đọc từ `ConnectionStrings:DefaultConnection`.
- [x] Có Dockerfile và `.dockerignore`.
- [x] Có `.gitignore` cho Visual Studio, .NET build output và file local.

### 1.2. Những phần đang có trong code

- [x] `Controllers/HomeController.cs`
- [x] `Views/Home/Index.cshtml`
- [x] `Views/Home/Privacy.cshtml`
- [x] `Views/Shared/_Layout.cshtml`
- [x] `Views/Shared/Error.cshtml`
- [x] Static assets trong `wwwroot`
- [x] Bootstrap, jQuery, jQuery Validation được vendored trong `wwwroot/lib`
- [x] File schema: `saas_store_pos_schema.sql`
- [x] Tài liệu bảng: `database_tables_documentation.md`
- [x] `Areas/Admin`, `Areas/Owner`, `Areas/Staff` đã có dashboard placeholder.
- [x] `AccountController` đã có login/logout/access denied.
- [x] `Services/Auth` và `Services/Common` đã có nền tảng đăng nhập và current user.
- [x] `ViewModels/Auth/LoginViewModel` đã có.
- [x] Cookie Authentication đã được cấu hình trong `Program.cs`.
- [x] Development seeder đã tạo role và admin mặc định.

### 1.3. Những phần chưa có

- [x] Đã có controller nghiệp vụ đầu tiên cho Admin quản lý owner và tenant.
- [x] Đã có service layer cơ bản cho auth, dashboard, security, audit và admin management.
- [x] Đã có ViewModel/InputModel cho login, dashboard, Admin Owners và Admin Tenants.
- [x] Đã có `RequireTenantFilter` và `IStoreAccessService` kiểm tra tenant/store access cơ bản.
- [x] Đã có layout/dashboard theo role và UI Admin Owners/Tenants cơ bản.
- [x] Đã có test tự động bước đầu trong `ChainPOS.Tests`.

## 2. Model đã scaffold từ SQL Server

### 2.1. DbContext

- [x] DbContext hiện tại: `ChainPOS.Models.StoreFlowDbContext`
- [x] Base class hiện tại: `DbContext`
- [x] Mapping Fluent API đã có trong `OnModelCreating`
- [x] Mapping default value: `newsequentialid()`, `sysutcdatetime()`
- [x] Mapping decimal precision cho tiền và số lượng
- [x] Mapping unique index và filtered index theo schema
- [x] Mapping các view báo cáo bằng `HasNoKey().ToView(...)`

Lưu ý: `StoreFlowDbContext` hiện không kế thừa `IdentityDbContext`. Các class `AspNetUser`, `AspNetRole` cũng là POCO scaffolded, chưa kế thừa `IdentityUser` hoặc `IdentityRole`.

### 2.2. Nhóm Identity scaffolded

- [x] `AspNetUser`
- [x] `AspNetRole`
- [x] `AspNetUserClaim`
- [x] `AspNetRoleClaim`
- [x] `AspNetUserLogin`
- [x] `AspNetUserToken`
- [x] Many-to-many `AspNetUserRoles` được map trong Fluent API

Các field mở rộng đã có trong `AspNetUser`:

- `FullName`
- `AvatarUrl`
- `Status`
- `TenantId`
- `CreatedAt`
- `CreatedBy`
- `UpdatedAt`
- `UpdatedBy`
- `LastLoginAt`

### 2.3. Nhóm tenant, store, phân quyền store

- [x] `Tenant`
- [x] `Store`
- [x] `UserStore`

Ràng buộc quan trọng:

- `Tenant.OwnerUserId` liên kết `AspNetUsers.Id`.
- `AspNetUsers.TenantId` xác định user thuộc tenant nào.
- `UserStores` là bảng quyết định staff được thao tác store nào.
- `Stores` có unique index `(TenantId, Code)` khi `IsDeleted = 0`.

### 2.4. Nhóm catalog và bán hàng

- [x] `Category`
- [x] `Product`
- [x] `StoreProduct`
- [x] `Order`
- [x] `OrderItem`
- [x] `Payment`

Ràng buộc quan trọng:

- `Products` có unique index `(TenantId, Sku)` và `(TenantId, Barcode)` khi chưa xóa mềm.
- `StoreProducts` có unique index `(TenantId, StoreId, ProductId)`.
- `Orders` có unique index `(TenantId, OrderCode)`.
- `OrderItems` lưu snapshot `ProductName`, `Sku`, `UnitPrice`.
- `Payments` là thanh toán đơn POS, không nhầm với `SystemPayments`.

### 2.5. Nhóm kho và ca làm

- [x] `Inventory`
- [x] `InventoryTransaction`
- [x] `Shift`

Ràng buộc quan trọng:

- `Inventories` có unique index `(TenantId, StoreId, ProductId)`.
- Mọi biến động kho phải ghi `InventoryTransactions`.
- `InventoryTransaction.Type` dùng các giá trị nghiệp vụ như `Import`, `Export`, `Sale`, `Adjust`, `Return`.
- `Shifts` gắn với tenant, store và user mở/đóng ca.

### 2.6. Nhóm subscription SaaS

- [x] `SubscriptionPlan`
- [x] `TenantSubscription`
- [x] `SystemPayment`

Ràng buộc quan trọng:

- `SubscriptionPlans` chứa giới hạn `MaxStores`, `MaxStaff`, `MaxProducts`.
- `TenantSubscriptions` lưu lịch sử gói của tenant.
- `SystemPayments` là thanh toán SaaS của tenant cho platform.

### 2.7. Nhóm audit và report views

- [x] `AuditLog`
- [x] `VwDailySalesReport`
- [x] `VwStaffSalesReport`
- [x] `VwInventoryStatusReport`
- [x] `VwSystemRevenueReport`

Các view report đã có DbSet:

- `VwDailySalesReports`
- `VwStaffSalesReports`
- `VwInventoryStatusReports`
- `VwSystemRevenueReports`

## 3. Nguyên tắc phát triển sau Data First

- Không tạo lại entity thủ công nếu model đã scaffold từ database.
- Hạn chế sửa trực tiếp file model scaffolded nếu thay đổi đó có thể mất khi scaffold lại.
- Nếu cần thêm helper logic cho model, dùng `partial class` trong file riêng.
- Nếu schema đổi, cập nhật SQL Server hoặc `saas_store_pos_schema.sql`, sau đó scaffold lại có kiểm soát.
- Không dùng EF migration để tạo schema mới trừ khi chốt chuyển sang code-first.
- Mọi query nghiệp vụ của `OWNER` và `STAFF` phải lọc `TenantId`.
- Mọi thao tác store của `STAFF` phải kiểm tra `UserStores.IsActive = true`.
- Không bind trực tiếp entity scaffolded từ request. Dùng ViewModel/InputModel.
- Không tin `TenantId`, `StoreId`, `UserId` gửi từ client nếu có thể lấy từ current user.
- Backend phải tự tính tiền đơn hàng, tồn kho, trạng thái thanh toán.

## 4. Quyết định kỹ thuật cần chốt sớm

### 4.1. Authentication

Hiện schema có bảng ASP.NET Identity, nhưng code scaffolded chưa dùng trực tiếp `UserManager` hoặc `SignInManager`.

Hướng triển khai ưu tiên cho MVP:

- [x] Dùng Cookie Authentication.
- [x] Dùng `PasswordHasher<AspNetUser>` để verify `PasswordHash` trong `AspNetUsers`.
- [x] Tự load role qua quan hệ `AspNetUsers.Roles`.
- [x] Tự tạo claims: `UserId`, `TenantId`, `Role`, `FullName`.
- [x] Tự cập nhật `LastLoginAt`, lockout/status theo rule nghiệp vụ.

Hướng thay thế nếu bắt buộc dùng full ASP.NET Core Identity:

- [ ] Tạo `ApplicationUser : IdentityUser`.
- [ ] Tạo `ApplicationRole : IdentityRole`.
- [ ] Tạo hoặc refactor DbContext kế thừa `IdentityDbContext`.
- [ ] Map về đúng bảng hiện tại.
- [ ] Kiểm tra kỹ vì hướng này dễ xung đột với model database-first đã scaffold.

Quy ước hiện tại cho backlog: ưu tiên Cookie Authentication để giữ nguyên model database-first.

### 4.2. Connection string và secrets

- [x] Không commit connection string máy cá nhân vào production config.
- [x] Chuyển connection string local sang User Secrets hoặc `appsettings.Local.json`.
- [x] Giữ `appsettings.json` chỉ chứa cấu hình an toàn hoặc placeholder.
- [x] Thêm hướng dẫn cấu hình connection string trong README nếu cần.

### 4.3. Constants thay cho magic string

Cần tạo constants để không rải string trong controller/service:

- [x] `AppRoles.Admin = "ADMIN"`
- [x] `AppRoles.Owner = "OWNER"`
- [x] `AppRoles.Staff = "STAFF"`
- [x] `UserStatuses.Active`, `Inactive`, `Locked`, `Pending`
- [x] `TenantStatuses.Active`, `Suspended`, `Cancelled`, `Trial`
- [x] `StoreStatuses.Active`, `Inactive`, `Closed`
- [x] `InventoryTransactionTypes.Import`, `Export`, `Sale`, `Adjust`, `Return`, `TransferIn`, `TransferOut`
- [x] `PaymentMethods.Cash`, `BankTransfer`, `Card`, `Momo`, `ZaloPay`, `Other`
- [x] `PaymentStatuses.Pending`, `Paid`, `Failed`, `Refunded`, `Cancelled`
- [x] `OrderStatuses.New`, `Completed`, `Cancelled`
- [x] `OrderPaymentStatuses.Unpaid`, `Partial`, `Paid`, `Refunded`, `Cancelled`
- [x] `ShiftStatuses.Open`, `Closed`
- [x] `BillingCycles.Monthly`, `Quarterly`, `Yearly`
- [x] `SubscriptionStatuses.Active`, `Trial`, `Suspended`, `Cancelled`, `Expired`

## 5. Phase 1: Nền tảng authentication và phân quyền

Mục tiêu: app đăng nhập được, phân quyền được, có current user context và tenant isolation cơ bản.

### 5.1. Cấu trúc thư mục cần thêm

- [x] `Areas/Admin/Controllers`
- [x] `Areas/Admin/Views`
- [x] `Areas/Owner/Controllers`
- [x] `Areas/Owner/Views`
- [x] `Areas/Staff/Controllers`
- [x] `Areas/Staff/Views`
- [x] `Services`
- [x] `Services/Auth`
- [x] `Services/Common`
- [x] `ViewModels`
- [x] `ViewModels/Auth`
- [x] `Filters`
- [x] `Constants`
- [ ] `Extensions`

### 5.2. Auth tasks

- [x] Tạo `AccountController`.
- [x] Tạo `LoginViewModel`.
- [x] Tạo `Login.cshtml`.
- [x] Tạo action `Login` GET.
- [x] Tạo action `Login` POST.
- [x] Verify user theo `Email` hoặc `UserName`.
- [x] Chặn user không có `Status = Active`.
- [x] Chặn tenant `Suspended` hoặc `Cancelled` cho role `OWNER` và `STAFF`.
- [x] Verify password bằng `PasswordHasher<AspNetUser>`.
- [x] Load role từ `AspNetUsers.Roles`.
- [x] Tạo auth cookie với claims cần thiết.
- [x] Cập nhật `LastLoginAt`.
- [x] Tạo action `Logout`.
- [x] Tạo action `AccessDenied`.
- [x] Cấu hình `LoginPath`, `LogoutPath`, `AccessDeniedPath`.
- [x] Redirect sau login theo role:
  - `ADMIN`: `/admin/dashboard`
  - `OWNER`: `/owner/dashboard`
  - `STAFF`: `/staff/dashboard`
- [x] Tạo flow Owner quên mật khẩu: nhập email, gửi OTP, nhập OTP và đổi mật khẩu.
- [x] Gửi OTP qua SMTP mail service đọc cấu hình từ configuration/User Secrets.
- [x] Lưu OTP dạng hash trong `AspNetUserTokens`, có thời hạn và giới hạn số lần nhập sai.
- [x] Ghi audit log `RequestOwnerPasswordResetOtp` và `ResetOwnerPassword`.
- [x] Link `Quên mật khẩu?` trên màn login chỉ hiển thị khi chọn role Owner.

### 5.3. Authorization và current context

- [x] Tạo `ICurrentUserService`.
- [x] Tạo `CurrentUserService`.
- [x] Lấy current `UserId`.
- [x] Lấy current `TenantId`.
- [x] Lấy current role.
- [x] Tạo policy hoặc attribute cho `ADMIN`, `OWNER`, `STAFF`.
- [x] Tạo `RequireTenantFilter`.
- [x] Tạo `IStoreAccessService`.
- [x] Implement rule:
  - `OWNER` truy cập store thuộc tenant của mình.
  - `STAFF` truy cập store có `UserStores.IsActive = true`.
  - `ADMIN` không dùng store access cho nghiệp vụ bán hàng/kho.

### 5.4. Seed dữ liệu nền

- [x] Tạo seeder roles `ADMIN`, `OWNER`, `STAFF`.
- [x] Tạo admin mặc định cho môi trường development.
- [x] Tạo owner demo cho môi trường development.
- [x] Tạo tenant demo cho owner demo.
- [x] Tạo store demo cho tenant demo.
- [x] Tạo staff demo cho môi trường development.
- [x] Gán staff demo vào store demo qua `UserStores`.
- [x] Hash password admin bằng `PasswordHasher<AspNetUser>`.
- [x] Không hard-code password production.
- [x] Seed dữ liệu demo đầy đủ cho owner/tenant/store/staff/category/product/store product.
- [x] Seed subscription plan demo để kiểm tra `MaxStores`, `MaxStaff`, `MaxProducts`.
- [x] Ghi audit log khi seed nếu cần.

### 5.5. Acceptance criteria Phase 1

- [x] App start không lỗi.
- [x] Admin đăng nhập được.
- [x] Owner demo đăng nhập được.
- [x] Staff demo đăng nhập được.
- [x] Logout được.
- [x] User sai password không đăng nhập được.
- [x] User bị khóa không đăng nhập được.
- [x] Role redirect đúng dashboard.
- [x] Anonymous vào `/admin`, `/owner`, `/staff` bị redirect login.
- [x] User sai role bị access denied.

## 6. Phase 2: Layout và dashboard theo role

Mục tiêu: có khung UI quản trị đủ để bắt đầu làm CRUD.

### 6.1. Layout

- [x] Tạo `_AdminLayout.cshtml`.
- [x] Tạo `_OwnerLayout.cshtml`.
- [x] Tạo `_StaffLayout.cshtml`.
- [x] Tạo partial `_Sidebar.cshtml`.
- [x] Tạo partial `_Topbar.cshtml`.
- [x] Tạo partial `_Alert.cshtml`.
- [x] Tạo partial `_Pagination.cshtml`.
- [x] Tạo partial `_ConfirmModal.cshtml`.
- [x] Tạo CSS theme riêng trong `wwwroot/css`.

### 6.2. Dashboard

- [x] `Areas/Admin/Controllers/DashboardController`
- [x] `Areas/Owner/Controllers/DashboardController`
- [x] `Areas/Staff/Controllers/DashboardController`
- [x] Admin dashboard hiển thị tổng tenant, store, owner, doanh thu SaaS.
- [x] Owner dashboard hiển thị doanh thu hôm nay và metrics tenant cơ bản.
- [x] Owner dashboard bổ sung low stock và recent orders khi triển khai inventory/order.
- [x] Staff dashboard hiển thị store được gán, ca hiện tại, đơn và doanh thu cá nhân hôm nay.

### 6.3. UI rules

- [x] Không để staff thấy menu owner/admin.
- [x] Không để owner thấy menu admin.
- [x] Table phải có search/filter/pagination.
- [x] Form dùng ViewModel, không bind entity trực tiếp.
- [x] Action nguy hiểm có confirm modal.
- [x] Server-side validation là bắt buộc.

## 7. Phase 3: Admin quản lý platform

Mục tiêu: admin quản lý owner, tenant, subscription plan và audit log.

### 7.1. Owner management

- [x] Tạo `Areas/Admin/Controllers/OwnersController`.
- [x] Danh sách owner.
- [x] Search owner theo tên/email.
- [x] Filter theo status.
- [x] Tạo owner.
- [x] Gán role `OWNER`.
- [x] Tạo tenant tương ứng.
- [x] Gán `Tenant.OwnerUserId`.
- [x] Gán `AspNetUsers.TenantId`.
- [x] Lock owner.
- [x] Unlock owner.
- [x] Xem chi tiết owner và tenant.
- [x] Ghi audit log.

### 7.2. Tenant management

- [x] Tạo `Areas/Admin/Controllers/TenantsController`.
- [x] Danh sách tenant.
- [x] Xem chi tiết tenant.
- [x] Suspend tenant.
- [x] Activate tenant.
- [x] Cancel tenant.
- [x] Không xóa vật lý tenant có dữ liệu.
- [x] Ghi audit log khi đổi trạng thái.

### 7.3. Subscription plan

- [x] Tạo `Areas/Admin/Controllers/SubscriptionPlansController`.
- [x] CRUD plan.
- [x] Validate `Price >= 0`.
- [x] Validate `MaxStores`, `MaxStaff`, `MaxProducts`.
- [x] Không xóa vật lý plan đã có tenant dùng.
- [x] Ghi audit log.

### 7.4. System payment

- [x] Tạo `Areas/Admin/Controllers/SystemPaymentsController`.
- [x] Danh sách payment SaaS.
- [x] Mark as paid.
- [x] Mark as failed.
- [x] Gắn `PaidAt`.
- [x] Link invoice nếu có.

### 7.5. Admin settings

- [x] Tạo `Areas/Admin/Controllers/SettingsController`.
- [x] Tạo service `IAdminSettingsService` đọc/ghi cấu hình.
- [x] Lưu cấu hình local bằng JSON trong `App_Data/settings`.
- [x] UI cấu hình General, Security, Billing, POS defaults và Notifications.
- [x] Validate server-side bằng ViewModel.
- [x] Ghi audit log `UpdateSystemSettings`.
- [x] Sidebar Settings trỏ về màn Settings thật.

## 8. Phase 4: Owner quản lý tenant, store, staff

Mục tiêu: owner tạo được store và staff, staff chỉ thao tác store được gán.

### 8.1. Store

- [x] Tạo `Areas/Owner/Controllers/StoresController`.
- [x] Danh sách store theo `TenantId`.
- [x] Search theo `Name`, `Code`.
- [x] Create store.
- [x] Check `Code` unique trong tenant.
- [x] Edit store.
- [x] Set status `Inactive`, `Active`, `Closed`.
- [x] Soft delete bằng `IsDeleted`.
- [x] Check giới hạn `MaxStores` theo subscription.
- [x] Ghi audit log.

### 8.2. Staff

- [x] Tạo `Areas/Owner/Controllers/StaffController`.
- [x] Danh sách staff theo tenant.
- [x] Create staff.
- [x] Không cho staff tự đăng ký.
- [x] Gán role `STAFF`.
- [x] Gán `TenantId` của owner.
- [x] Reset password staff.
- [x] Lock/unlock staff.
- [x] Gán staff vào store qua `UserStores`.
- [x] Bật/tắt `UserStores.IsActive`.
- [x] Check giới hạn `MaxStaff` theo subscription.
- [x] Ghi audit log.

### 8.3. Acceptance criteria Phase 4

- [x] Owner chỉ thấy store và staff của tenant mình.
- [x] Owner tạo được staff.
- [x] Staff mới đăng nhập được sau khi có password hợp lệ.
- [x] Staff không thấy store chưa được gán.
- [x] Staff bị tắt `UserStores.IsActive` không thao tác được store đó.

## 9. Phase 5: Product, category, store product

Mục tiêu: owner quản lý danh mục, sản phẩm và bật/tắt sản phẩm theo store.

### 9.1. Category

- [x] Tạo `Areas/Owner/Controllers/CategoriesController`.
- [x] Danh sách category theo tenant.
- [x] Create category.
- [x] Edit category.
- [x] Toggle `IsActive`.
- [x] Soft delete.
- [x] Check unique `(TenantId, Name)` khi chưa xóa.

### 9.2. Product

- [x] Tạo `Areas/Owner/Controllers/ProductsController`.
- [x] Danh sách product theo tenant.
- [x] Search theo name, sku, barcode.
- [x] Create product.
- [x] Validate category thuộc tenant.
- [x] Check `Sku` unique trong tenant.
- [x] Check `Barcode` unique trong tenant.
- [x] Validate `Price >= 0`, `CostPrice >= 0`.
- [x] Upload ảnh vào `wwwroot/uploads/products`.
- [x] Toggle `IsActive`.
- [x] Soft delete.
- [x] Check giới hạn `MaxProducts` theo subscription.
- [x] Ghi audit log.

### 9.3. Store product

- [x] Tạo UI gán product vào store.
- [x] Set `IsAvailable`.
- [x] Set `SellingPrice` riêng theo store nếu có.
- [x] Khi bán POS, ưu tiên `StoreProducts.SellingPrice`, fallback `Products.Price`.
  - [x] POS đã tích hợp logic `StoreProducts.SellingPrice ?? Products.Price`.

## 10. Phase 6: Inventory

Mục tiêu: quản lý tồn kho đúng rule, có lịch sử giao dịch kho.

### 10.1. Inventory views

- [x] Tạo `Areas/Owner/Controllers/InventoryController`.
- [x] Tạo `Areas/Staff/Controllers/InventoryController`.
- [x] Danh sách tồn kho theo tenant/store.
- [x] Low stock dựa trên `Quantity <= MinQuantity`.
- [x] Dùng `VwInventoryStatusReports` cho report nếu phù hợp.

### 10.2. Import stock

- [x] Check store access.
- [x] Validate product thuộc tenant.
- [x] Validate product available tại store nếu cần.
- [x] Validate quantity > 0.
- [x] Tạo `Inventory` nếu chưa có.
- [x] Tính `BeforeQuantity`.
- [x] Cập nhật `Quantity`.
- [x] Tính `AfterQuantity`.
- [x] Ghi `InventoryTransactions` type `Import`.
- [x] Ghi audit log.
- [x] Dùng database transaction.

### 10.3. Export stock

- [x] Check store access.
- [x] Validate quantity > 0.
- [x] Validate tồn đủ.
- [x] Cập nhật `Inventory`.
- [x] Ghi `InventoryTransactions` type `Export`.
- [x] Ghi audit log.
- [x] Dùng database transaction.

### 10.4. Adjust stock

- [x] Check store access.
- [x] Validate actual quantity >= 0.
- [x] Validate reason bắt buộc.
- [x] Cập nhật `Inventory`.
- [x] Ghi `InventoryTransactions` type `Adjust`.
- [x] Ghi audit log.
- [x] Dùng database transaction.

## 11. Phase 7: Shift và POS

Mục tiêu: staff mở ca, bán hàng, thanh toán, trừ kho, in hóa đơn.

Quy ước triển khai Phase 7:

- Làm theo thứ tự: `Shift` -> `POS UI/cart` -> `Checkout backend` -> `Orders/receipt/cancel`.
- POS checkout phải yêu cầu user có ca `Open` hợp lệ trước khi tạo đơn.
- Mọi thao tác theo store của `OWNER`/`STAFF` vẫn phải lọc `TenantId` và check store access.
- UI phải ưu tiên lấy mẫu từ `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI`, đặc biệt `dashboard.html`, `stock.html`, `orders.html`, `invoices.html`.
- Backend không tin giá, tồn kho, tổng tiền gửi từ client; phải tự tính lại trên server.

### 11.1. Shift foundation

- [x] Tạo ViewModel/InputModel cho shift: list, open, close.
- [x] Tạo `IShiftService` và `ShiftService`.
- [x] Tạo `Areas/Owner/Controllers/ShiftsController`.
- [x] Tạo `Areas/Staff/Controllers/ShiftsController`.
- [x] Màn danh sách ca theo tenant/store.
- [x] Màn xem ca đang mở của user hiện tại.
- [x] Open shift cho Owner/Staff.
- [x] Validate `OpeningCash >= 0`.
- [x] Check store access trước khi mở ca.
- [x] Không cho cùng user mở nhiều ca `Open` cùng lúc.
- [x] Ghi audit log `OpenShift`.
- [x] Close shift cho Owner/Staff.
- [x] Validate `ClosingCash >= 0`.
- [x] Tính `ExpectedCash` = `OpeningCash` + tổng payment cash trong ca.
- [x] Tính `DifferenceAmount = ClosingCash - ExpectedCash`.
- [x] Cập nhật `ClosedAt`, `ClosingCash`, `ExpectedCash`, `DifferenceAmount`, `Status = Closed`.
- [x] Ghi audit log `CloseShift`.

### 11.2. POS UI và cart

- [x] Tạo `Areas/Owner/Controllers/PosController`.
- [x] Tạo `Areas/Staff/Controllers/PosController`.
- [x] Tạo ViewModel/InputModel cho POS index và cart item.
- [x] UI chọn store từ danh sách store được phép truy cập.
- [x] UI hiển thị ca đang mở; nếu chưa mở ca thì điều hướng sang mở ca.
- [x] UI danh sách product available tại store.
- [x] Search product theo tên, SKU, barcode.
- [x] Hiển thị giá bán bằng helper `GetEffectiveSellingPriceAsync`: ưu tiên `StoreProducts.SellingPrice`, fallback `Products.Price`.
- [x] Hiển thị tồn kho hiện tại để staff biết sản phẩm còn hàng.
- [x] Cart client-side bằng JavaScript.
- [x] Cart cho phép add/remove sản phẩm.
- [x] Cart cho phép tăng/giảm số lượng.
- [x] Cart hiển thị subtotal, discount, tax, total tạm tính.
- [x] UI chọn payment method.
- [x] UI nhập số tiền khách đưa nếu payment cash.

### 11.3. POS checkout backend

- [x] Tạo Checkout InputModel riêng, không bind entity trực tiếp.
- [x] Validate user có ca `Open` hợp lệ tại store đang bán.
- [x] Check store access.
- [x] Validate cart không rỗng.
- [x] Backend validate lại toàn bộ cart.
- [x] Backend tự tính giá, subtotal, discount, tax, total.
- [x] Validate tồn kho đủ.
- [x] Sinh `OrderCode` unique theo tenant.
- [x] Tạo `Order`.
- [x] Tạo `OrderItems`.
- [x] Tạo `Payments`.
- [x] Trừ kho trong `Inventories`.
- [x] Ghi `InventoryTransactions` type `Sale`.
- [x] Cập nhật `Orders.PaymentStatus`.
- [x] Ghi audit log `CreateOrder`.
- [x] Commit bằng database transaction.
- [x] Redirect sang màn receipt/order detail sau checkout.

### 11.4. Order, receipt và cancel

- [x] Tạo `Areas/Owner/Controllers/OrdersController`.
- [x] Tạo `Areas/Staff/Controllers/OrdersController`.
- [x] Danh sách order theo tenant/store.
- [x] Filter order theo store, status, payment status, ngày bán.
- [x] Xem chi tiết order.
- [x] In receipt.
- [x] Cancel order.
- [x] Check quyền cancel theo tenant/store.
- [x] Khi cancel, cập nhật `OrderStatus = Cancelled`.
- [x] Cập nhật `CancelledAt`, `CancelledBy`.
- [x] Hoàn kho nếu order đã trừ kho.
- [x] Ghi `InventoryTransactions` type `Return`.
- [x] Cập nhật payment nếu cần.
- [x] Ghi audit log `CancelOrder`.
- [x] Commit cancel bằng database transaction.

### 11.5. Acceptance criteria Phase 7

- [x] Owner/Staff mở ca được tại store có quyền truy cập.
- [x] User không mở được ca thứ hai khi đang có ca `Open`.
- [x] Owner/Staff đóng ca được và hệ thống tính đúng `ExpectedCash`, `DifferenceAmount`.
- [x] Staff không checkout được nếu chưa có ca `Open`.
- [x] POS chỉ hiển thị product available tại store được chọn.
- [x] POS dùng đúng selling price riêng theo store, fallback product price.
- [x] Checkout tạo đủ `Order`, `OrderItems`, `Payments`.
- [x] Checkout trừ kho đúng và ghi `InventoryTransactions` type `Sale`.
- [x] Checkout thiếu tồn kho bị chặn.
- [x] Cancel order hoàn kho đúng và ghi `InventoryTransactions` type `Return`.
- [x] Receipt hiển thị đúng thông tin đơn, sản phẩm, thanh toán.

## 12. Phase 8: Report, subscription, audit

Mục tiêu: hoàn thiện các màn hình tổng hợp và vận hành SaaS.

### 12.1. Reports

- [x] Daily sales report dùng `VwDailySalesReports`.
- [x] Staff sales report dùng `VwStaffSalesReports`.
- [x] Inventory status report dùng `VwInventoryStatusReports`.
- [x] System revenue report dùng `VwSystemRevenueReports`.
- [x] Filter theo thời gian, tenant, store.
- [x] Export Excel nếu cần.

### 12.2. Subscription

- [x] Owner xem subscription hiện tại.
- [x] Owner xem lịch sử thanh toán SaaS.
- [x] Admin gán plan cho tenant.
- [x] Admin tạo subscription mới.
- [x] Kiểm tra giới hạn plan khi tạo store/staff/product.
- [x] Chặn hoặc cảnh báo tenant hết hạn.

### 12.3. Audit log

- [x] Tạo `IAuditLogService`.
- [x] Ghi log các action quan trọng:
  - `Login`
  - `Logout`
  - `CreateUser`
  - `LockUser`
  - `UnlockUser`
  - `CreateStore`
  - `UpdateStore`
  - `CreateProduct`
  - `UpdateProduct`
  - `DeleteProduct`
  - `ImportStock`
  - `ExportStock`
  - `AdjustStock`
  - `CreateOrder`
  - `CancelOrder`
  - `OpenShift`
  - `CloseShift`
  - `ChangeSubscription`
- [x] Ghi audit log cho `CreateUser`, `CreateTenant`, `LockUser`, `UnlockUser`, `SuspendTenant`, `ActivateTenant`, `CancelTenant`.
- [x] Ghi audit log cho `CreateStore`, `UpdateStore`, `ChangeStoreStatus`, `DeleteStore`.
- [x] Ghi audit log cho `CreateStaff`, `ResetStaffPassword`, `LockStaff`, `UnlockStaff`, `AssignStaffStore`, `EnableStaffStore`, `DisableStaffStore`.
- [x] Ghi audit log cho `CreateCategory`, `UpdateCategory`, `ActivateCategory`, `DeactivateCategory`, `DeleteCategory`.
- [x] Ghi audit log cho `CreateProduct`, `UpdateProduct`, `ActivateProduct`, `DeactivateProduct`, `DeleteProduct`.
- [x] Ghi audit log cho `AssignStoreProduct`, `UpdateStoreProduct`, `EnableStoreProduct`, `DisableStoreProduct`.
- [x] Ghi audit log cho `ImportStock`, `ExportStock`, `AdjustStock`.
- [x] Ghi audit log cho `OpenShift`, `CloseShift`, `CreateOrder`, `CancelOrder`.
- [x] Ghi audit log cho `Login`, `Logout`.
- [x] Ghi audit log cho `ChangeSubscription` sau khi triển khai Subscription UI.
- [x] Admin xem toàn bộ audit log.
- [x] Owner chỉ xem audit log trong tenant của mình.
- [x] Filter audit theo user, store, action, thời gian.

### 12.4. Realtime SignalR

- [x] Tạo `ChainPosHub` dùng cookie auth.
- [x] Join group theo admin platform, tenant owner và store staff.
- [x] Broadcast realtime khi inventory import/export/adjust.
- [x] Broadcast realtime khi POS checkout tạo order.
- [x] Broadcast realtime khi cancel order và hoàn kho.
- [x] Broadcast realtime khi open/close shift.
- [x] Broadcast realtime khi tenant subscription/system payment thay đổi.
- [x] Client nhận live toast, notification badge và notification dropdown.
- [x] POS/Inventory cập nhật stock live cho item đang hiển thị.
- [x] Orders/Shifts/Subscription/Payments hiển thị reload banner khi dữ liệu thay đổi.
- [x] Tự động prepend order/payment mới vào table mà không cần reload.
- [x] Live update dashboard metrics.

## 13. Security checklist

- [x] Bật HTTPS.
- [x] Bật antiforgery token cho form POST.
- [x] Không bind entity trực tiếp từ request.
- [x] Validate server-side đầy đủ.
- [x] Không log password hoặc `PasswordHash`.
- [x] Không log OTP; OTP reset password lưu dạng hash và bị xóa sau khi dùng thành công/hết hạn.
- [x] Không cho upload file ngoài định dạng ảnh.
- [x] Giới hạn dung lượng upload.
- [x] Chặn path traversal khi upload.
- [x] Mọi query owner/staff lọc theo `TenantId`.
- [x] Mọi thao tác staff theo store kiểm tra `UserStores`.
- [x] Action POST phải kiểm tra quyền lại ở server, không chỉ ẩn nút trên UI.

## 14. Test checklist

### 14.1. Manual test phân quyền

- [x] Anonymous truy cập `/admin` bị redirect login.
- [x] Anonymous truy cập `/owner` bị redirect login.
- [x] Anonymous truy cập `/staff` bị redirect login.
- [x] Staff truy cập `/owner/products` bị access denied.
- [x] Staff truy cập `/owner/storeproducts` bị access denied.
- [x] Owner truy cập `/admin/owners` bị access denied.
- [x] Staff đổi store id sang store chưa được gán bị chặn.
- [x] Owner không xem được tenant khác.
- [x] Tenant suspended không thao tác được module nghiệp vụ.

### 14.2. Manual test nghiệp vụ

- [x] Admin tạo owner.
- [x] Owner có tenant riêng.
- [x] Owner tạo store.
- [x] Owner tạo staff.
- [x] Owner gán staff vào store.
- [x] Staff login và chỉ thấy store được gán.
- [x] Owner tạo product.
- [x] Owner gán product vào store.
- [x] Owner chỉnh selling price và available cho store product.
- [x] Owner/staff nhập kho.
- [x] Owner/staff xuất kho.
- [x] Owner/staff điều chỉnh kho.
- [x] Owner/staff mở ca.
- [x] Owner/staff đóng ca.
- [x] User đang có ca mở không mở thêm ca thứ hai được.
- [x] Staff không bán POS khi chưa có ca mở.
- [x] POS tạo order trừ kho đúng.
- [x] POS checkout thiếu tồn kho bị chặn.
- [x] Receipt hiển thị đúng sau checkout.
- [x] Cancel order hoàn kho đúng.
- [x] Payment cập nhật trạng thái order đúng.
- [x] Admin xem Reports gồm daily sales, staff sales, inventory status và system revenue.
- [x] Owner xem Reports trong tenant của mình và không thấy System Revenue Report.
- [x] Admin xem Audit Logs và filter audit theo action.
- [x] Owner xem Audit Logs trong tenant của mình và filter audit theo action.

### 14.3. Unit/integration test nên thêm sau MVP UI

- [x] Test `StoreAccessService`.
- [x] Test tạo owner.
- [x] Test tạo staff.
- [x] Test import stock.
- [x] Test export stock.
- [x] Test adjust stock.
- [x] Test create order.
- [x] Test cancel order.
- [x] Test close shift.
- [x] Regression build/test sau khi thêm Owner forgot password OTP.

### 14.4. Manual test nên làm tiếp cho Owner forgot password

- [ ] Owner nhập email thật tại `/forgot-password` và nhận được OTP qua Gmail SMTP.
- [ ] Owner nhập OTP đúng tại `/forgot-password/verify` và đổi mật khẩu thành công.
- [ ] OTP sai quá số lần cho phép hoặc hết hạn bị chặn.
- [ ] OTP đã dùng thành công không dùng lại được.

## 15. Ưu tiên triển khai gần nhất

Thứ tự nên làm tiếp sau Subscription/Admin billing MVP:

1. Bổ sung test cho tạo owner/staff và các luồng admin billing chi tiết.
2. Export Excel cho report nếu cần.
3. Bổ sung low stock/recent orders vào Owner dashboard và live update dashboard metrics.
4. Tự động prepend order/payment mới vào table realtime nếu không muốn reload banner.
5. Manual smoke test sâu hơn cho tenant expired/suspended và owner không xem tenant khác.

## 16. Backlog phản hồi nghiệm thu ngày 2026-06-04

Các task dưới đây được lập sau khi rà soát source hiện tại theo phản hồi mới. Các mục đã triển khai và kiểm tra hợp lý được tick theo trạng thái hiện tại.

### 16.1. Search/filter realtime phía client

- [x] Rà soát toàn bộ màn có search/filter: Admin Owners/Tenants/Plans/System Payments/Audit, Owner Stores/Staff/Categories/Products/Store Products/Inventory/POS/Orders/Reports, Staff POS/Inventory/Orders/Shifts.
- [x] Chuyển các ô search chính sang trải nghiệm realtime/debounce để người dùng không bắt buộc bấm nút Search/Filter.
- [x] Giữ query string/server filter để reload, back/forward browser và phân quyền vẫn đúng.
- [x] Thêm loading state nhỏ khi debounce submit hoặc fetch partial.
- [x] Với các bảng có dữ liệu ít, cân nhắc client-side filter trực tiếp; với bảng lớn, dùng debounce GET/server-side paging.

### 16.2. Payment visibility và payment management

- [x] Làm rõ 2 nhóm payment hiện có: POS `Payments` và SaaS `SystemPayments`.
- [x] Thêm màn POS Payments cho Owner/Staff để xem payment theo store, order, method, status, transaction code, date.
- [x] Thêm link Payment dưới nhóm Sales nếu cần, thay vì chỉ thấy payment trong order detail.
- [x] Bổ sung filter payment theo store, method, status, date, search order code/transaction code.
- [x] Làm nổi bật payment trên Order list/receipt để người dùng nhìn thấy trạng thái thanh toán ngay.
- [x] Kiểm tra lại Owner subscription checkout/System Payment để payment SaaS hiển thị đủ action và trạng thái.

### 16.3. Login role quick fill và lỗi hiển thị quyền

- [x] Fix role pill ở màn login để tự điền cả email và password demo đúng role, không chỉ email.
- [x] Khi bấm lại role, clear validation summary/error cũ và cập nhật trạng thái link quên mật khẩu.
- [x] Fallback sidebar/topbar lấy role từ claim thật nếu `ViewData["AppRole"]` thiếu hoặc layout render sai.
- [x] Hardening login redirect theo primary role sau khi cookie đã sign-in thành công.
- [ ] Thêm smoke test login Admin/Owner/Staff nhiều lần liên tiếp để bắt lỗi quyền không hiển thị.

### 16.4. Bulk import/file upload nhiều bản ghi

- [x] Admin tạo Owner + Tenant hàng loạt bằng CSV/XLSX upload.
- [x] Owner tạo Staff hàng loạt bằng CSV/XLSX, có gán nhiều store trong file.
- [x] Owner tạo Store hàng loạt bằng CSV/XLSX.
- [x] Owner tạo Category/Product hàng loạt bằng CSV/XLSX.
- [x] Product import hỗ trợ image URL hoặc cơ chế upload nhiều ảnh rồi map theo SKU/barcode.
- [x] Store Product assignment import hàng loạt theo store code + SKU + selling price + availability.
- [x] Inventory import hàng loạt theo store code + SKU + quantity/min quantity/reason.
- [x] Có template tải xuống, validate từng dòng, báo lỗi từng dòng và chế độ partial import rõ ràng.
- [x] Ghi audit log tổng quan và chi tiết số dòng thành công/thất bại.

### 16.5. Chặn trùng email/số điện thoại

- [x] Chuẩn hóa email và phone trước khi validate.
- [x] Owner create: chặn trùng `NormalizedEmail`, `NormalizedUserName` và `PhoneNumber`.
- [x] Staff create: chặn trùng `NormalizedEmail`, `NormalizedUserName` và `PhoneNumber`.
- [x] Xác định scope phone unique: toàn hệ thống hay trong tenant; sau đó đồng bộ service, thông báo UI và test.
- [x] Bổ sung handling `DbUpdateException` cho unique index/race condition để không văng lỗi thô.
- [x] Bổ sung unique index có filter cho phone trong EF mapping và schema SQL.

### 16.6. Audit optimization

- [x] Tối ưu `AuditLogQueryService`: không load toàn bộ action về memory để tính warning/critical.
- [x] Tính tổng warning/critical/distinct user bằng query SQL riêng hoặc projection tối thiểu.
- [x] Tránh `Include` khi count; projection trực tiếp sang view model cho page hiện tại.
- [x] Bổ sung index audit theo `TenantId`, `StoreId`, `UserId`, `Action`, `CreatedAt`.
- [x] Giới hạn page size hợp lý, giữ server-side paging/filter.
- [x] Thêm test hoặc smoke test audit với dữ liệu lớn để kiểm tra thời gian phản hồi.

### 16.7. Block user phải revoke token/cookie

- [x] `RefreshUserClaimsCookieEvents.ValidatePrincipal` phải reject cookie nếu user bị locked/inactive, lockout còn hạn, user bị xóa, role bị gỡ hoặc tenant bị suspended/cancelled.
- [x] Refresh lại role, tenant, fullname/email claim khi validate cookie.
- [x] Khi lock/unlock/reset password/change role, cập nhật `SecurityStamp` để cookie cũ bị vô hiệu.
- [x] Kiểm tra SignalR connection của user bị khóa không tiếp tục nhận event sau khi cookie bị reject/reconnect.
- [x] Thêm regression test: user đang login bị lock thì request kế tiếp bị sign-out/forbid.

### 16.8. Product image hiển thị đầy đủ

- [x] Gán ảnh demo cho các sản phẩm seed để POS/list không còn toàn placeholder.
- [x] Hiển thị ảnh ở Store Products list, Inventory list, Order detail/receipt và cart POS.
- [x] Bổ sung fallback ảnh nhất quán nếu sản phẩm chưa có ảnh.
- [x] Kiểm tra static path `/uploads/products/...` hoạt động sau deploy/publish.
- [x] Với bulk product import, hỗ trợ map ảnh theo SKU/barcode.

### 16.9. Order queue khi thanh toán lỗi

- [x] Thêm luồng tạo order tạm khi thanh toán lỗi hoặc khách chưa thanh toán xong.
- [x] Sử dụng trạng thái `OrderStatus = New`/payment `Unpaid` hoặc bổ sung trạng thái pending/held nếu cần schema.
- [x] POS có khu vực hàng đợi để tạm giữ đơn, xử lý khách tiếp theo, sau đó quay lại retry payment/complete/cancel.
- [x] Xác định rule giữ kho: reserve tồn khi hold hay chỉ trừ kho khi paid; triển khai nhất quán.
- [x] Có action retry payment, mark paid, cancel/release queue.
- [x] Realtime update queue cho các máy POS cùng store.
- [x] Audit log cho hold order, retry payment, complete pending order, cancel pending order.

### 16.10. Dọn path `/Home/Index`

- [x] Xóa hoặc redirect `HomeController.Index` để `/Home/Index` không còn trang template ASP.NET mặc định.
- [x] Xóa hoặc redirect `HomeController.Privacy` nếu không dùng.
- [x] Giữ error handler hoạt động đúng khi production dùng `/Home/Error` hoặc chuyển sang route error mới.
- [ ] Smoke test `/`, `/Home/Index`, `/login`, dashboard theo role.

### 16.11. Receipt/print optimization

- [x] Tạo CSS `@media print` riêng cho receipt, ẩn sidebar/topbar/action button/toast.
- [x] Hỗ trợ layout in 80mm hoặc A4 theo thiết lập POS.
- [x] Tối ưu nội dung receipt: store info, order code, date, cashier, item, qty, unit price, discount, tax, total, payment method/transaction code.
- [x] Với order cancelled, in rõ trạng thái cancelled và không gây nhầm là hóa đơn paid.
- [ ] Test print preview trên receipt Owner/Staff.

### 16.12. Realtime websocket targeting và âm thanh thông báo

- [x] Rà soát `SignalRRealtimeNotifier` để tránh gửi quá rộng khiến người dùng thấy như broadcast không cần thiết.
- [x] Tách event theo platform admin, tenant owner và store staff đúng nhu cầu từng loại event.
- [x] Client chỉ toast/sound khi event liên quan màn/store hiện tại hoặc theo setting người dùng.
- [x] Thêm âm thanh notification có toggle bật/tắt; xử lý browser autoplay bằng cách enable sau tương tác đầu tiên.
- [x] Thêm cấu hình trong Admin Settings/LocalStorage cho realtime sound.
- [x] Thêm test/manual check cho inventory/order/payment event ở 2 store khác nhau để đảm bảo không nhầm broadcast.

### 16.13. Test hồi quy cho backlog này

- [x] `dotnet build .\ChainPOS.sln`.
- [x] `dotnet test .\ChainPOS.sln`.
- [ ] HTTP smoke test login Admin/Owner/Staff.
- [ ] Manual smoke test POS payment, pending queue, receipt print, realtime sound.
- [ ] Manual smoke test bulk import với file hợp lệ và file có lỗi.

## 17. Definition of Done cho mỗi chức năng

Một chức năng chỉ coi là xong khi:

- [ ] Có controller action.
- [ ] Có service xử lý nghiệp vụ.
- [ ] Có ViewModel/InputModel riêng.
- [ ] Có Razor view đủ trạng thái chính.
- [ ] Có validation server-side.
- [ ] Có authorization theo role.
- [ ] Có kiểm tra tenant.
- [ ] Có kiểm tra store access nếu liên quan store.
- [ ] Có audit log nếu là thao tác quan trọng.
- [ ] Có xử lý lỗi và thông báo UI.
- [ ] Có manual test checklist hoặc test tự động tối thiểu.
