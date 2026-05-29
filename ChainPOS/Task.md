# Task triển khai ChainPOS sau khi Data First

Cập nhật ngày: 2026-05-29

Tài liệu này là backlog triển khai tiếp theo cho dự án `ChainPOS` sau khi đã scaffold model bằng EF Core từ SQL Server. Trạng thái hiện tại không còn là tạo project từ đầu, mà là phát triển tiếp trên nền database-first đã có.

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
- [ ] Chưa có test.

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

- [ ] Không commit connection string máy cá nhân vào production config.
- [ ] Chuyển connection string local sang User Secrets hoặc `appsettings.Local.json`.
- [ ] Giữ `appsettings.json` chỉ chứa cấu hình an toàn hoặc placeholder.
- [ ] Thêm hướng dẫn cấu hình connection string trong README nếu cần.

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
- [ ] Ghi audit log khi seed nếu cần.

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
- [ ] Owner dashboard bổ sung low stock và recent orders khi triển khai inventory/order.
- [x] Staff dashboard hiển thị store được gán, ca hiện tại, đơn và doanh thu cá nhân hôm nay.

### 6.3. UI rules

- [x] Không để staff thấy menu owner/admin.
- [x] Không để owner thấy menu admin.
- [ ] Table phải có search/filter/pagination.
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

- [ ] Tạo `Areas/Admin/Controllers/SubscriptionPlansController`.
- [ ] CRUD plan.
- [ ] Validate `Price >= 0`.
- [ ] Validate `MaxStores`, `MaxStaff`, `MaxProducts`.
- [ ] Không xóa vật lý plan đã có tenant dùng.
- [ ] Ghi audit log.

### 7.4. System payment

- [ ] Tạo `Areas/Admin/Controllers/SystemPaymentsController`.
- [ ] Danh sách payment SaaS.
- [ ] Mark as paid.
- [ ] Mark as failed.
- [ ] Gắn `PaidAt`.
- [ ] Link invoice nếu có.

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
- [ ] Staff không thấy store chưa được gán.
- [ ] Staff bị tắt `UserStores.IsActive` không thao tác được store đó.

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

- [ ] Tạo UI gán product vào store.
- [ ] Set `IsAvailable`.
- [ ] Set `SellingPrice` riêng theo store nếu có.
- [ ] Khi bán POS, ưu tiên `StoreProducts.SellingPrice`, fallback `Products.Price`.

## 10. Phase 6: Inventory

Mục tiêu: quản lý tồn kho đúng rule, có lịch sử giao dịch kho.

### 10.1. Inventory views

- [ ] Tạo `Areas/Owner/Controllers/InventoryController`.
- [ ] Tạo `Areas/Staff/Controllers/InventoryController`.
- [ ] Danh sách tồn kho theo tenant/store.
- [ ] Low stock dựa trên `Quantity <= MinQuantity`.
- [ ] Dùng `VwInventoryStatusReports` cho report nếu phù hợp.

### 10.2. Import stock

- [ ] Check store access.
- [ ] Validate product thuộc tenant.
- [ ] Validate product available tại store nếu cần.
- [ ] Validate quantity > 0.
- [ ] Tạo `Inventory` nếu chưa có.
- [ ] Tính `BeforeQuantity`.
- [ ] Cập nhật `Quantity`.
- [ ] Tính `AfterQuantity`.
- [ ] Ghi `InventoryTransactions` type `Import`.
- [ ] Ghi audit log.
- [ ] Dùng database transaction.

### 10.3. Export stock

- [ ] Check store access.
- [ ] Validate quantity > 0.
- [ ] Validate tồn đủ.
- [ ] Cập nhật `Inventory`.
- [ ] Ghi `InventoryTransactions` type `Export`.
- [ ] Ghi audit log.
- [ ] Dùng database transaction.

### 10.4. Adjust stock

- [ ] Check store access.
- [ ] Validate actual quantity >= 0.
- [ ] Validate reason bắt buộc.
- [ ] Cập nhật `Inventory`.
- [ ] Ghi `InventoryTransactions` type `Adjust`.
- [ ] Ghi audit log.
- [ ] Dùng database transaction.

## 11. Phase 7: Shift và POS

Mục tiêu: staff mở ca, bán hàng, thanh toán, trừ kho, in hóa đơn.

### 11.1. Shift

- [ ] Tạo `Areas/Owner/Controllers/ShiftsController`.
- [ ] Tạo `Areas/Staff/Controllers/ShiftsController`.
- [ ] Open shift.
- [ ] Close shift.
- [ ] Validate `OpeningCash >= 0`.
- [ ] Tính `ExpectedCash` từ payment cash trong ca.
- [ ] Tính `DifferenceAmount`.
- [ ] Không cho user mở nhiều ca cùng lúc nếu nghiệp vụ yêu cầu.
- [ ] Ghi audit log.

### 11.2. POS

- [ ] Tạo `Areas/Owner/Controllers/PosController`.
- [ ] Tạo `Areas/Staff/Controllers/PosController`.
- [ ] UI chọn store.
- [ ] UI danh sách product available tại store.
- [ ] Search product theo tên, SKU, barcode.
- [ ] Cart client-side bằng JavaScript.
- [ ] Backend validate lại toàn bộ cart.
- [ ] Backend tự tính giá, subtotal, discount, tax, total.
- [ ] Validate tồn kho đủ.
- [ ] Tạo `Order`.
- [ ] Tạo `OrderItems`.
- [ ] Tạo `Payments`.
- [ ] Trừ kho trong `Inventories`.
- [ ] Ghi `InventoryTransactions` type `Sale`.
- [ ] Cập nhật `Orders.PaymentStatus`.
- [ ] Ghi audit log `CreateOrder`.
- [ ] Commit bằng database transaction.

### 11.3. Order

- [ ] Tạo `Areas/Owner/Controllers/OrdersController`.
- [ ] Tạo `Areas/Staff/Controllers/OrdersController`.
- [ ] Danh sách order theo tenant/store.
- [ ] Xem chi tiết order.
- [ ] In receipt.
- [ ] Cancel order.
- [ ] Khi cancel, cập nhật `OrderStatus = Cancelled`.
- [ ] Cập nhật `CancelledAt`, `CancelledBy`.
- [ ] Hoàn kho nếu order đã trừ kho.
- [ ] Ghi `InventoryTransactions` type `Return`.
- [ ] Cập nhật payment nếu cần.
- [ ] Ghi audit log `CancelOrder`.

## 12. Phase 8: Report, subscription, audit

Mục tiêu: hoàn thiện các màn hình tổng hợp và vận hành SaaS.

### 12.1. Reports

- [ ] Daily sales report dùng `VwDailySalesReports`.
- [ ] Staff sales report dùng `VwStaffSalesReports`.
- [ ] Inventory status report dùng `VwInventoryStatusReports`.
- [ ] System revenue report dùng `VwSystemRevenueReports`.
- [ ] Filter theo thời gian, tenant, store.
- [ ] Export Excel nếu cần.

### 12.2. Subscription

- [ ] Owner xem subscription hiện tại.
- [ ] Owner xem lịch sử thanh toán SaaS.
- [ ] Admin gán plan cho tenant.
- [ ] Admin tạo subscription mới.
- [ ] Kiểm tra giới hạn plan khi tạo store/staff/product.
- [ ] Chặn hoặc cảnh báo tenant hết hạn.

### 12.3. Audit log

- [x] Tạo `IAuditLogService`.
- [ ] Ghi log các action quan trọng:
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
- [ ] Admin xem toàn bộ audit log.
- [ ] Owner chỉ xem audit log trong tenant của mình.
- [ ] Filter audit theo user, store, action, thời gian.

## 13. Security checklist

- [ ] Bật HTTPS.
- [ ] Bật antiforgery token cho form POST.
- [ ] Không bind entity trực tiếp từ request.
- [ ] Validate server-side đầy đủ.
- [ ] Không log password hoặc `PasswordHash`.
- [ ] Không cho upload file ngoài định dạng ảnh.
- [ ] Giới hạn dung lượng upload.
- [ ] Chặn path traversal khi upload.
- [ ] Mọi query owner/staff lọc theo `TenantId`.
- [ ] Mọi thao tác staff theo store kiểm tra `UserStores`.
- [ ] Action POST phải kiểm tra quyền lại ở server, không chỉ ẩn nút trên UI.

## 14. Test checklist

### 14.1. Manual test phân quyền

- [x] Anonymous truy cập `/admin` bị redirect login.
- [x] Anonymous truy cập `/owner` bị redirect login.
- [x] Anonymous truy cập `/staff` bị redirect login.
- [x] Staff truy cập `/owner/products` bị access denied.
- [x] Owner truy cập `/admin/owners` bị access denied.
- [ ] Staff đổi store id sang store chưa được gán bị chặn.
- [ ] Owner không xem được tenant khác.
- [ ] Tenant suspended không thao tác được module nghiệp vụ.

### 14.2. Manual test nghiệp vụ

- [ ] Admin tạo owner.
- [ ] Owner có tenant riêng.
- [x] Owner tạo store.
- [x] Owner tạo staff.
- [x] Owner gán staff vào store.
- [x] Staff login và chỉ thấy store được gán.
- [x] Owner tạo product.
- [ ] Owner/staff nhập kho.
- [ ] POS tạo order trừ kho đúng.
- [ ] Cancel order hoàn kho đúng.
- [ ] Payment cập nhật trạng thái order đúng.

### 14.3. Unit/integration test nên thêm sau MVP UI

- [ ] Test `StoreAccessService`.
- [ ] Test tạo owner.
- [ ] Test tạo staff.
- [ ] Test import stock.
- [ ] Test export stock.
- [ ] Test adjust stock.
- [ ] Test create order.
- [ ] Test cancel order.
- [ ] Test close shift.

## 15. Ưu tiên triển khai gần nhất

Thứ tự nên làm ngay:

1. Constants cho role/status/type.
2. Cookie Authentication và `AccountController`.
3. Current user service.
4. Store access service.
5. Seed role và admin development.
6. Area layouts và dashboard placeholder.
7. Admin tạo owner và tenant.
8. Owner tạo store.
9. Owner tạo staff và gán store.
10. Product/category.
11. Inventory import/export/adjust.
12. POS checkout.

## 16. Definition of Done cho mỗi chức năng

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
