# Task triển khai hệ thống SaaS Store Management + POS + Inventory MVC

Tài liệu này là kế hoạch triển khai đầy đủ cho hệ thống ASP.NET Core MVC dựa trên nghiệp vụ đã chốt và schema SQL Server trong file `saas_store_pos_schema.sql`.

Phạm vi hệ thống:

- SaaS quản lý chuỗi cửa hàng.
- POS bán hàng.
- Quản lý kho.
- Quản lý nhân sự theo tenant và store.
- Subscription SaaS.
- Báo cáo.
- Audit log.

Kiến trúc UI:

- Server-rendered ASP.NET Core MVC.
- Razor Views.
- Area theo vai trò: `Admin`, `Owner`, `Staff`.
- Layout riêng theo vai trò.
- UI light theme, ưu tiên màu cam/vàng nhạt, giao diện quản trị rõ ràng, dễ dùng.

Kiến trúc logic:

- ASP.NET Core Identity cho tài khoản, đăng nhập, đăng xuất, mật khẩu, lockout.
- SQL Server làm database chính.
- EF Core làm ORM.
- Service layer xử lý nghiệp vụ.
- Middleware/filter kiểm tra tenant và store access.
- Audit log cho thao tác quan trọng.

## 1. Nguyên tắc triển khai bắt buộc

### 1.1. Role hệ thống

Hệ thống có 3 role chính:

- `ADMIN`: quản trị nền tảng SaaS.
- `OWNER`: chủ tenant / chủ chuỗi cửa hàng.
- `STAFF`: nhân viên bán hàng/kho.

### 1.2. Ràng buộc STAFF

`STAFF` không được tự đăng ký.

Flow đúng:

1. `OWNER` tạo tài khoản `STAFF`.
2. Hệ thống gán role `STAFF`.
3. Hệ thống gán `TenantId` của owner cho staff.
4. `OWNER` gán staff vào một hoặc nhiều store qua bảng `UserStores`.
5. Staff chỉ được thao tác trong các store được gán active.

### 1.3. Ràng buộc tenant

Tất cả dữ liệu nghiệp vụ của `OWNER` và `STAFF` phải lọc theo `TenantId`.

Không được truy vấn kiểu:

```csharp
var products = await _db.Products.ToListAsync();
```

Phải truy vấn kiểu:

```csharp
var products = await _db.Products
    .Where(x => x.TenantId == currentTenantId && !x.IsDeleted)
    .ToListAsync();
```

### 1.4. Ràng buộc store

Dữ liệu phát sinh tại cửa hàng phải có `StoreId`, gồm:

- `Inventories`
- `InventoryTransactions`
- `Shifts`
- `Orders`
- `AuditLogs` nếu hành động gắn với cửa hàng

Với `STAFF`, trước khi thao tác store phải kiểm tra `UserStores`.

### 1.5. Ràng buộc kho

Không được chỉ cập nhật bảng `Inventories` mà không ghi lịch sử.

Mọi biến động kho phải:

1. Đọc tồn hiện tại.
2. Tính `BeforeQuantity`.
3. Tính `AfterQuantity`.
4. Cập nhật `Inventories`.
5. Ghi `InventoryTransactions`.
6. Ghi `AuditLogs` nếu là thao tác quan trọng.

### 1.6. Ràng buộc bán hàng POS

Khi tạo đơn POS phải xử lý trong database transaction:

1. Tạo `Orders`.
2. Tạo `OrderItems`.
3. Tạo `Payments`.
4. Trừ kho trong `Inventories`.
5. Ghi `InventoryTransactions` type `Sale`.
6. Ghi `AuditLogs`.
7. Commit.

Nếu một bước lỗi, rollback toàn bộ.

## 2. Tech stack đề xuất

### 2.1. Backend

- ASP.NET Core MVC
- ASP.NET Core Identity
- Entity Framework Core
- SQL Server
- Razor Views
- Session hoặc Cookie Authentication

### 2.2. Frontend

- Razor View + ViewModel
- Bootstrap 5 hoặc Tailwind CSS
- Custom CSS theme màu cam/vàng nhạt
- JavaScript thuần cho POS cart, search, filter, modal
- Chart.js cho dashboard/report
- SheetJS hoặc thư viện server-side export Excel

Khuyến nghị thực tế cho MVC:

- Dùng Bootstrap 5 để build nhanh admin UI.
- Dùng CSS custom để tạo theme riêng.
- Dùng partial view cho table, modal, form, alert, pagination.

### 2.3. Database

- SQL Server
- Schema chuẩn: `saas_store_pos_schema.sql`
- Tài liệu bảng: `database_tables_documentation.md`

### 2.4. Export và file

- Upload ảnh sản phẩm: lưu file trong `wwwroot/uploads/products`.
- Invoice PDF: làm sau MVP POS cơ bản.
- Export Excel: dùng thư viện server-side như ClosedXML hoặc EPPlus.

## 3. Cấu trúc solution

Tạo solution theo hướng MVC có phân lớp rõ ràng.

```text
StoreSaas/
  StoreSaas.sln
  src/
    StoreSaas.Web/
      Areas/
        Admin/
          Controllers/
          Views/
        Owner/
          Controllers/
          Views/
        Staff/
          Controllers/
          Views/
      Controllers/
      Views/
      ViewModels/
      Filters/
      Middleware/
      TagHelpers/
      wwwroot/
        css/
        js/
        uploads/
          products/
    StoreSaas.Application/
      Services/
      DTOs/
      Interfaces/
      Validators/
    StoreSaas.Domain/
      Entities/
      Enums/
      Constants/
    StoreSaas.Infrastructure/
      Data/
      Identity/
      Repositories/
      Seeders/
      FileStorage/
      Reports/
  tests/
    StoreSaas.Tests/
```

Nếu muốn đơn giản hơn cho đồ án/MVP, có thể gộp tất cả vào `StoreSaas.Web`, nhưng vẫn nên giữ các thư mục:

- `Data`
- `Entities`
- `Services`
- `ViewModels`
- `Areas`

## 4. Mapping database sang entity

### 4.1. Identity entities

Tạo class:

- `ApplicationUser : IdentityUser`
- `ApplicationRole : IdentityRole`

`ApplicationUser` cần thêm:

- `FullName`
- `AvatarUrl`
- `Status`
- `TenantId`
- `CreatedAt`
- `CreatedBy`
- `UpdatedAt`
- `UpdatedBy`
- `LastLoginAt`

### 4.2. Business entities

Tạo entity tương ứng các bảng:

- `Tenant`
- `Store`
- `UserStore`
- `Category`
- `Product`
- `StoreProduct`
- `Inventory`
- `InventoryTransaction`
- `Shift`
- `Order`
- `OrderItem`
- `Payment`
- `SubscriptionPlan`
- `TenantSubscription`
- `SystemPayment`
- `AuditLog`

### 4.3. DbContext

Tạo:

- `ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>`

DbSet cần có:

```csharp
public DbSet<Tenant> Tenants { get; set; }
public DbSet<Store> Stores { get; set; }
public DbSet<UserStore> UserStores { get; set; }
public DbSet<Category> Categories { get; set; }
public DbSet<Product> Products { get; set; }
public DbSet<StoreProduct> StoreProducts { get; set; }
public DbSet<Inventory> Inventories { get; set; }
public DbSet<InventoryTransaction> InventoryTransactions { get; set; }
public DbSet<Shift> Shifts { get; set; }
public DbSet<Order> Orders { get; set; }
public DbSet<OrderItem> OrderItems { get; set; }
public DbSet<Payment> Payments { get; set; }
public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }
public DbSet<TenantSubscription> TenantSubscriptions { get; set; }
public DbSet<SystemPayment> SystemPayments { get; set; }
public DbSet<AuditLog> AuditLogs { get; set; }
```

### 4.4. Enum/constant cần tạo

Role:

- `ADMIN`
- `OWNER`
- `STAFF`

User status:

- `Active`
- `Inactive`
- `Locked`
- `Pending`

Tenant status:

- `Active`
- `Suspended`
- `Cancelled`
- `Trial`

Store status:

- `Active`
- `Inactive`
- `Closed`

Inventory transaction type:

- `Import`
- `Export`
- `Sale`
- `Adjust`
- `Return`
- `TransferIn`
- `TransferOut`

Payment method:

- `Cash`
- `BankTransfer`
- `Card`
- `Momo`
- `ZaloPay`
- `Other`

Payment status:

- `Pending`
- `Paid`
- `Failed`
- `Refunded`
- `Cancelled`

Order status:

- `New`
- `Completed`
- `Cancelled`

Order payment status:

- `Unpaid`
- `Partial`
- `Paid`
- `Refunded`
- `Cancelled`

Shift status:

- `Open`
- `Closed`

## 5. Service layer cần có

### 5.1. Core service

- `ICurrentUserService`
- `ITenantProvider`
- `IStoreAccessService`
- `IAuditLogService`
- `IFileStorageService`
- `IPaginationService`

### 5.2. Admin service

- `IAdminDashboardService`
- `IOwnerManagementService`
- `ISubscriptionPlanService`
- `ISystemPaymentService`
- `IPlatformReportService`

### 5.3. Owner service

- `IOwnerDashboardService`
- `IStoreService`
- `IStaffService`
- `ICategoryService`
- `IProductService`
- `IStoreProductService`
- `IInventoryService`
- `IShiftService`
- `IPosService`
- `IOrderService`
- `IReportService`
- `ITenantSubscriptionService`

### 5.4. Staff service

- `IStaffDashboardService`
- `IStaffPosService`
- `IStaffInventoryService`
- `IStaffProfileService`

## 6. Middleware, filter và authorization

### 6.1. Authorization policy

Tạo policy:

- `RequireAdmin`
- `RequireOwner`
- `RequireStaff`
- `RequireOwnerOrStaff`
- `RequireAuthenticatedUser`

### 6.2. Tenant middleware

Tạo `TenantMiddleware` hoặc `TenantFilter` để:

- Lấy `TenantId` từ current user.
- Kiểm tra user có tenant hợp lệ nếu role là `OWNER` hoặc `STAFF`.
- Chặn user bị khóa.
- Chặn tenant bị `Suspended` hoặc `Cancelled` nếu truy cập module nghiệp vụ.

Không áp dụng tenant bắt buộc cho `ADMIN`.

### 6.3. Store access filter

Tạo filter hoặc service để kiểm tra:

- `OWNER`: được truy cập tất cả store trong tenant của mình.
- `STAFF`: chỉ được truy cập store có bản ghi active trong `UserStores`.

Áp dụng cho:

- POS
- Inventory
- Shift
- Store report
- Order detail

### 6.4. Audit action filter

Có thể tạo helper/service ghi log tại service layer trước.

Các action bắt buộc ghi log:

- Login
- Logout
- CreateUser
- LockUser
- UnlockUser
- CreateStore
- UpdateStore
- CreateProduct
- UpdateProduct
- DeleteProduct
- ImportStock
- ExportStock
- AdjustStock
- CreateOrder
- CancelOrder
- OpenShift
- CloseShift
- ChangeSubscription

## 7. Cấu trúc Areas và route

### 7.1. Public/Auth

Không nằm trong area hoặc dùng area `Account`.

Controllers:

- `AccountController`

Routes:

- `/login`
- `/logout`
- `/forgot-password`
- `/reset-password`
- `/profile`
- `/change-password`

Lưu ý:

- Cho phép đăng ký `OWNER` nếu nghiệp vụ cần public signup.
- Không có route đăng ký `STAFF`.

### 7.2. Admin area

Base route:

```text
/admin
```

Controllers:

- `DashboardController`
- `OwnersController`
- `TenantsController`
- `SubscriptionPlansController`
- `SystemPaymentsController`
- `AuditLogsController`
- `ReportsController`
- `SettingsController`

### 7.3. Owner area

Base route:

```text
/owner
```

Controllers:

- `DashboardController`
- `StoresController`
- `StaffController`
- `CategoriesController`
- `ProductsController`
- `InventoryController`
- `ShiftsController`
- `PosController`
- `OrdersController`
- `ReportsController`
- `SubscriptionController`
- `AuditLogsController`

### 7.4. Staff area

Base route:

```text
/staff
```

Controllers:

- `DashboardController`
- `PosController`
- `InventoryController`
- `ShiftsController`
- `OrdersController`
- `ProfileController`

## 8. UI layout tổng thể

### 8.1. Layout chung

Tạo các layout:

- `Views/Shared/_AuthLayout.cshtml`
- `Views/Shared/_AdminLayout.cshtml`
- `Views/Shared/_OwnerLayout.cshtml`
- `Views/Shared/_StaffLayout.cshtml`

Mỗi layout cần có:

- Sidebar.
- Topbar.
- User menu.
- Breadcrumb.
- Toast/alert message.
- Main content area.
- Responsive mobile menu.

### 8.2. Theme

Style direction:

- Light theme.
- Nền trắng/xám rất nhạt.
- Primary màu cam.
- Accent màu vàng nhạt.
- Text màu xám đậm.
- Card radius nhỏ, khoảng 8px.
- Bảng dữ liệu rõ ràng, dễ scan.

CSS variables đề xuất:

```css
:root {
  --color-primary: #f97316;
  --color-primary-dark: #ea580c;
  --color-accent: #facc15;
  --color-bg: #f8fafc;
  --color-surface: #ffffff;
  --color-border: #e5e7eb;
  --color-text: #111827;
  --color-muted: #6b7280;
}
```

### 8.3. Component dùng lại

Tạo partial view:

- `_Sidebar.cshtml`
- `_Topbar.cshtml`
- `_Breadcrumb.cshtml`
- `_Alert.cshtml`
- `_Pagination.cshtml`
- `_ConfirmDeleteModal.cshtml`
- `_StatusBadge.cshtml`
- `_EmptyState.cshtml`
- `_ValidationSummary.cshtml`

### 8.4. UI rules

- Không để staff thấy menu không có quyền.
- Không để owner thấy menu admin platform.
- Table phải có search/filter/pagination.
- Form phải validate phía server và hiển thị lỗi rõ ràng.
- Các action nguy hiểm như khóa, xóa mềm, hủy đơn phải có confirm modal.

## 9. Tuần 1: Setup nền tảng

### 9.1. Tạo solution và project

- [ ] Tạo solution `StoreSaas`.
- [ ] Tạo project ASP.NET Core MVC `StoreSaas.Web`.
- [ ] Tạo project class library `StoreSaas.Domain`.
- [ ] Tạo project class library `StoreSaas.Application`.
- [ ] Tạo project class library `StoreSaas.Infrastructure`.
- [ ] Tạo project test `StoreSaas.Tests`.
- [ ] Add reference giữa các project.
- [ ] Cấu hình `appsettings.json`.
- [ ] Cấu hình `appsettings.Development.json`.

### 9.2. Cấu hình package

- [ ] Cài EF Core SQL Server.
- [ ] Cài EF Core Tools.
- [ ] Cài ASP.NET Core Identity EntityFrameworkCore.
- [ ] Cài package export Excel.
- [ ] Cài package logging nếu cần.
- [ ] Cấu hình static file.
- [ ] Cấu hình session nếu dùng session.

### 9.3. Cấu hình SQL Server

- [ ] Tạo database `StoreSaasDb`.
- [ ] Chạy script `saas_store_pos_schema.sql`.
- [ ] Kiểm tra đủ bảng Identity.
- [ ] Kiểm tra đủ role `ADMIN`, `OWNER`, `STAFF`.
- [ ] Kiểm tra đủ index chính.
- [ ] Kiểm tra đủ view báo cáo.

### 9.4. Cấu hình EF Core

- [ ] Tạo `ApplicationDbContext`.
- [ ] Tạo entity Identity mở rộng.
- [ ] Tạo entity nghiệp vụ.
- [ ] Cấu hình relationship bằng Fluent API.
- [ ] Cấu hình decimal precision.
- [ ] Cấu hình unique index theo schema.
- [ ] Cấu hình default value nếu dùng migration.
- [ ] Kiểm tra query kết nối database.

### 9.5. Cấu hình Identity

- [ ] Cấu hình `ApplicationUser`.
- [ ] Cấu hình `ApplicationRole`.
- [ ] Cấu hình password policy.
- [ ] Cấu hình lockout.
- [ ] Cấu hình cookie login path.
- [ ] Cấu hình access denied path.
- [ ] Cấu hình role manager.
- [ ] Cấu hình user manager.
- [ ] Cấu hình sign in manager.

### 9.6. Seed dữ liệu nền tảng

- [ ] Seed role `ADMIN`.
- [ ] Seed role `OWNER`.
- [ ] Seed role `STAFF`.
- [ ] Seed tài khoản admin mặc định.
- [ ] Bắt buộc admin đổi mật khẩu sau lần đầu nếu muốn tăng bảo mật.
- [ ] Ghi audit log cho seed admin nếu cần.

### 9.7. Auth UI

- [ ] Tạo trang login.
- [ ] Tạo action login.
- [ ] Tạo logout.
- [ ] Tạo forgot password UI.
- [ ] Tạo reset password UI.
- [ ] Tạo change password UI.
- [ ] Tạo profile UI.
- [ ] Chặn route register staff.

### 9.8. Layout admin cơ bản

- [ ] Tạo `_AdminLayout`.
- [ ] Tạo sidebar admin.
- [ ] Tạo topbar admin.
- [ ] Tạo dashboard admin placeholder.
- [ ] Điều hướng sau login theo role.

### 9.9. Acceptance criteria tuần 1

- [ ] Database chạy được.
- [ ] App start không lỗi.
- [ ] Admin đăng nhập được.
- [ ] Admin logout được.
- [ ] Role seed chính xác.
- [ ] User không đăng nhập bị redirect về login.
- [ ] User không có quyền bị redirect access denied.

## 10. Tuần 2: Tenant, Owner, Store

### 10.1. Admin quản lý OWNER

Controller:

- `Areas/Admin/Controllers/OwnersController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`
- `Lock`
- `Unlock`

Tasks:

- [ ] Tạo danh sách owner.
- [ ] Tìm kiếm owner theo tên/email.
- [ ] Filter theo trạng thái.
- [ ] Tạo owner mới.
- [ ] Khi tạo owner, tạo user Identity.
- [ ] Gán role `OWNER`.
- [ ] Tạo tenant tương ứng.
- [ ] Cập nhật `TenantId` cho owner.
- [ ] Gán `OwnerUserId` cho tenant.
- [ ] Khóa owner.
- [ ] Mở khóa owner.
- [ ] Xem chi tiết owner và tenant.
- [ ] Ghi audit log.

### 10.2. Tenant management

Controller:

- `Areas/Admin/Controllers/TenantsController`

Tasks:

- [ ] Xem danh sách tenant.
- [ ] Xem trạng thái tenant.
- [ ] Tạm khóa tenant.
- [ ] Mở lại tenant.
- [ ] Xem owner của tenant.
- [ ] Xem số store, staff, product của tenant.
- [ ] Ghi audit log khi thay đổi trạng thái.

### 10.3. OWNER đăng nhập

Tasks:

- [ ] Owner login thành công redirect về `/owner/dashboard`.
- [ ] Owner bị khóa không đăng nhập được.
- [ ] Owner có tenant suspended không truy cập được module nghiệp vụ.
- [ ] Owner chỉ thấy dữ liệu tenant của mình.

### 10.4. OWNER quản lý Store

Controller:

- `Areas/Owner/Controllers/StoresController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`

Tasks:

- [ ] Tạo danh sách store.
- [ ] Tìm kiếm store theo tên/code.
- [ ] Tạo store.
- [ ] Kiểm tra code không trùng trong tenant.
- [ ] Sửa thông tin store.
- [ ] Khóa store.
- [ ] Mở store.
- [ ] Xóa mềm store nếu cần.
- [ ] Không xóa vật lý store đã có order/inventory.
- [ ] Ghi audit log.

### 10.5. Tenant middleware

Tasks:

- [ ] Tạo `ICurrentUserService`.
- [ ] Tạo `ITenantProvider`.
- [ ] Lấy current user id.
- [ ] Lấy current role.
- [ ] Lấy current tenant id.
- [ ] Chặn owner/staff không có tenant.
- [ ] Chặn tenant suspended/cancelled.

### 10.6. Acceptance criteria tuần 2

- [ ] Admin tạo được owner.
- [ ] Owner có tenant riêng.
- [ ] Owner login vào đúng dashboard owner.
- [ ] Owner tạo/sửa/khóa/mở store được.
- [ ] Owner không thấy dữ liệu tenant khác.
- [ ] Admin thấy danh sách owner/tenant.

## 11. Tuần 3: Staff Management

### 11.1. OWNER tạo STAFF

Controller:

- `Areas/Owner/Controllers/StaffController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`
- `AssignStores`
- `ResetPassword`

Tasks:

- [ ] Tạo danh sách staff trong tenant.
- [ ] Tìm kiếm staff theo tên/email/số điện thoại.
- [ ] Tạo staff mới.
- [ ] Gán role `STAFF`.
- [ ] Gán `TenantId` của owner.
- [ ] Không cho staff tự đăng ký.
- [ ] Không cho owner tạo staff cho tenant khác.
- [ ] Ghi audit log `CreateUser`.

### 11.2. Gán STAFF vào Store

Tasks:

- [ ] Hiển thị danh sách store của tenant.
- [ ] Cho owner chọn một hoặc nhiều store.
- [ ] Tạo bản ghi `UserStores`.
- [ ] Tránh gán trùng.
- [ ] Cho phép bật/tắt `IsActive`.
- [ ] Hiển thị store staff đang được gán.
- [ ] Ghi audit log khi gán hoặc hủy gán.

### 11.3. Khóa / mở STAFF

Tasks:

- [ ] Khóa tài khoản staff.
- [ ] Mở khóa tài khoản staff.
- [ ] Khi staff bị khóa, không đăng nhập được.
- [ ] Owner chỉ khóa/mở staff trong tenant mình.
- [ ] Ghi audit log.

### 11.4. Reset mật khẩu STAFF

Tasks:

- [ ] Tạo màn hình reset password cho owner.
- [ ] Generate password tạm hoặc cho owner nhập password mới.
- [ ] Bắt staff đổi mật khẩu sau lần đăng nhập sau nếu cần.
- [ ] Ghi audit log.

### 11.5. STAFF đăng nhập

Tasks:

- [ ] Staff login redirect về `/staff/dashboard`.
- [ ] Staff thấy danh sách store được gán.
- [ ] Staff chọn store đang làm việc.
- [ ] Store được chọn lưu vào session hoặc claim tạm.
- [ ] Staff không truy cập được store không được gán.
- [ ] Staff không truy cập được area owner/admin.

### 11.6. Acceptance criteria tuần 3

- [ ] Owner tạo staff được.
- [ ] Owner gán staff vào store được.
- [ ] Staff login được.
- [ ] Staff chỉ thấy store được gán.
- [ ] Staff bị khóa không login được.
- [ ] Staff không vào được URL của owner/admin.

## 12. Tuần 4: Product

### 12.1. CRUD danh mục

Controller:

- `Areas/Owner/Controllers/CategoriesController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`

Tasks:

- [ ] Danh sách category theo tenant.
- [ ] Tạo category.
- [ ] Sửa category.
- [ ] Ẩn/hiện category.
- [ ] Xóa mềm category.
- [ ] Validate tên không trùng trong tenant.
- [ ] Ghi audit log.

### 12.2. CRUD sản phẩm

Controller:

- `Areas/Owner/Controllers/ProductsController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`
- `UploadImage`

Tasks:

- [ ] Danh sách product theo tenant.
- [ ] Search theo tên, SKU, barcode.
- [ ] Filter theo category.
- [ ] Filter theo trạng thái active/inactive.
- [ ] Tạo sản phẩm.
- [ ] Sửa sản phẩm.
- [ ] Xóa mềm sản phẩm.
- [ ] Ẩn/hiện sản phẩm.
- [ ] Validate SKU không trùng trong tenant.
- [ ] Validate barcode không trùng trong tenant.
- [ ] Validate giá bán >= 0.
- [ ] Validate giá vốn >= 0.
- [ ] Ghi audit log.

### 12.3. Upload ảnh sản phẩm

Tasks:

- [ ] Tạo `IFileStorageService`.
- [ ] Validate extension ảnh.
- [ ] Validate dung lượng ảnh.
- [ ] Lưu ảnh vào `wwwroot/uploads/products`.
- [ ] Lưu `ImageUrl` vào `Products`.
- [ ] Hiển thị preview ảnh.
- [ ] Xóa ảnh cũ nếu upload ảnh mới.

### 12.4. StoreProducts

Tasks:

- [ ] Tạo màn hình cấu hình sản phẩm theo store.
- [ ] Chọn store.
- [ ] Chọn sản phẩm.
- [ ] Cấu hình `SellingPrice`.
- [ ] Cấu hình `IsAvailable`.
- [ ] Nếu không có selling price, dùng `Products.Price`.
- [ ] Ghi audit log.

### 12.5. Staff xem sản phẩm

Controller:

- `Areas/Staff/Controllers/ProductsController` hoặc tích hợp trong POS.

Tasks:

- [ ] Staff chỉ xem sản phẩm active.
- [ ] Staff chỉ xem sản phẩm available tại store đang chọn.
- [ ] Staff không được tạo/sửa/xóa sản phẩm.

### 12.6. Acceptance criteria tuần 4

- [ ] Owner CRUD category được.
- [ ] Owner CRUD product được.
- [ ] Upload ảnh sản phẩm được.
- [ ] Search/filter product hoạt động.
- [ ] SKU/barcode không trùng trong tenant.
- [ ] Staff chỉ xem sản phẩm được bán tại store của mình.

## 13. Tuần 5: Inventory

### 13.1. Tạo tồn kho theo Store + Product

Controller:

- `Areas/Owner/Controllers/InventoryController`

Views:

- `Index`
- `Details`
- `Import`
- `Export`
- `Adjust`
- `History`

Tasks:

- [ ] Danh sách tồn kho theo store.
- [ ] Search theo product name/SKU/barcode.
- [ ] Filter tồn kho thấp.
- [ ] Tạo dòng inventory khi store/product chưa có.
- [ ] Unique theo `TenantId + StoreId + ProductId`.
- [ ] Không cho quantity âm.

### 13.2. Nhập kho

Tasks:

- [ ] Form nhập kho.
- [ ] Chọn store.
- [ ] Chọn product.
- [ ] Nhập quantity > 0.
- [ ] Nhập reason.
- [ ] Cập nhật `Inventories`.
- [ ] Ghi `InventoryTransactions` type `Import`.
- [ ] Ghi audit log `ImportStock`.
- [ ] Transaction database đầy đủ.

### 13.3. Xuất kho

Tasks:

- [ ] Form xuất kho.
- [ ] Chọn store.
- [ ] Chọn product.
- [ ] Nhập quantity > 0.
- [ ] Kiểm tra tồn đủ.
- [ ] Cập nhật `Inventories`.
- [ ] Ghi `InventoryTransactions` type `Export`.
- [ ] Ghi audit log `ExportStock`.

### 13.4. Kiểm kê / điều chỉnh kho

Tasks:

- [ ] Form điều chỉnh tồn thực tế.
- [ ] Nhập số lượng thực tế.
- [ ] Tính chênh lệch.
- [ ] Cập nhật `Inventories`.
- [ ] Ghi `InventoryTransactions` type `Adjust`.
- [ ] Ghi reason bắt buộc.
- [ ] Ghi audit log `AdjustStock`.

### 13.5. Cảnh báo tồn kho thấp

Tasks:

- [ ] Hiển thị badge low stock khi `Quantity <= MinQuantity`.
- [ ] Tạo filter low stock.
- [ ] Dashboard owner hiển thị số sản phẩm low stock.
- [ ] Staff được xem low stock nếu có quyền kho.

### 13.6. Lịch sử kho

Tasks:

- [ ] Danh sách `InventoryTransactions`.
- [ ] Filter theo store.
- [ ] Filter theo product.
- [ ] Filter theo type.
- [ ] Filter theo thời gian.
- [ ] Xem before/after quantity.
- [ ] Xem user tạo giao dịch.

### 13.7. Staff hỗ trợ kho

Controller:

- `Areas/Staff/Controllers/InventoryController`

Tasks:

- [ ] Staff xem tồn kho store được gán.
- [ ] Staff không xem store khác.
- [ ] Staff có thể nhập/xuất/adjust nếu được bật quyền ở service layer.
- [ ] Nếu chưa làm phân quyền chi tiết, chỉ cho staff xem tồn kho.

### 13.8. Acceptance criteria tuần 5

- [ ] Owner xem tồn kho theo store.
- [ ] Nhập kho cập nhật đúng tồn và lịch sử.
- [ ] Xuất kho không cho âm tồn.
- [ ] Kiểm kê ghi lịch sử rõ ràng.
- [ ] Low stock hiển thị đúng.
- [ ] Staff không xem tồn kho store không được gán.

## 14. Tuần 6: POS

### 14.1. UI bán hàng

Controller:

- `Areas/Owner/Controllers/PosController`
- `Areas/Staff/Controllers/PosController`

Views:

- `Index`
- `Checkout`
- `Receipt`

JavaScript:

- `wwwroot/js/pos-cart.js`
- `wwwroot/js/pos-search.js`

Tasks:

- [ ] Chọn store đang bán.
- [ ] Hiển thị danh sách sản phẩm available.
- [ ] Search theo tên/SKU/barcode.
- [ ] Hiển thị tồn kho hiện tại.
- [ ] Thêm sản phẩm vào giỏ.
- [ ] Tăng/giảm số lượng.
- [ ] Xóa item khỏi giỏ.
- [ ] Tính subtotal.
- [ ] Nhập discount.
- [ ] Tính tax nếu có.
- [ ] Tính total.
- [ ] Validate không bán vượt tồn.

### 14.2. Tạo đơn hàng

Service:

- `IPosService`

Tasks:

- [ ] Tạo mã đơn `OrderCode`.
- [ ] Tạo `Orders`.
- [ ] Tạo `OrderItems`.
- [ ] Lưu snapshot `ProductName`, `Sku`, `UnitPrice`.
- [ ] Backend tự tính tiền, không tin total từ client.
- [ ] Gán `StaffUserId`.
- [ ] Gán `ShiftId` nếu có ca đang mở.
- [ ] Gán `StoreId`.
- [ ] Gán `TenantId`.

### 14.3. Thanh toán

Tasks:

- [ ] Chọn phương thức thanh toán.
- [ ] Hỗ trợ `Cash`.
- [ ] Hỗ trợ `BankTransfer`.
- [ ] Hỗ trợ `Card`.
- [ ] Hỗ trợ `Momo`.
- [ ] Hỗ trợ `ZaloPay`.
- [ ] Hỗ trợ `Other`.
- [ ] Tạo `Payments`.
- [ ] Cập nhật `Orders.PaymentStatus`.
- [ ] Cập nhật `Orders.OrderStatus`.

### 14.4. Trừ kho khi bán

Tasks:

- [ ] Kiểm tra tồn từng item.
- [ ] Trừ `Inventories.Quantity`.
- [ ] Ghi `InventoryTransactions` type `Sale`.
- [ ] `ReferenceType = Order`.
- [ ] `ReferenceId = Order.Id`.
- [ ] Transaction database đầy đủ.

### 14.5. In hóa đơn

Views:

- `Receipt.cshtml`

Tasks:

- [ ] Hiển thị thông tin cửa hàng.
- [ ] Hiển thị mã đơn.
- [ ] Hiển thị nhân viên.
- [ ] Hiển thị thời gian.
- [ ] Hiển thị danh sách sản phẩm.
- [ ] Hiển thị subtotal/discount/tax/total.
- [ ] Hiển thị payment method.
- [ ] Tạo nút print dùng `window.print()`.
- [ ] CSS print riêng cho receipt.

### 14.6. Lịch sử đơn hàng

Controller:

- `OrdersController`

Views:

- `Index`
- `Details`

Tasks:

- [ ] Danh sách order theo store.
- [ ] Filter theo ngày.
- [ ] Filter theo payment status.
- [ ] Filter theo order status.
- [ ] Search theo order code.
- [ ] Xem chi tiết order.
- [ ] Xem payments.
- [ ] Xem order items.

### 14.7. Hủy đơn và hoàn kho

Tasks:

- [ ] Chỉ cho hủy đơn chưa bị hủy.
- [ ] Cập nhật `OrderStatus = Cancelled`.
- [ ] Cập nhật `CancelledAt`.
- [ ] Cập nhật `CancelledBy`.
- [ ] Nếu đã trừ kho, hoàn lại kho.
- [ ] Ghi `InventoryTransactions` type `Return`.
- [ ] Nếu đã thanh toán, cập nhật payment/refund theo nghiệp vụ.
- [ ] Ghi audit log `CancelOrder`.
- [ ] Transaction database đầy đủ.

### 14.8. Acceptance criteria tuần 6

- [ ] Staff/Owner bán hàng được.
- [ ] Tạo giỏ hàng được.
- [ ] Tạo order đúng.
- [ ] Thanh toán đúng.
- [ ] Tồn kho bị trừ đúng.
- [ ] Hóa đơn in được.
- [ ] Hủy đơn hoàn kho đúng.
- [ ] Không bán được vượt tồn.

## 15. Tuần 7: Shift + Report

### 15.1. Mở ca / đóng ca

Controller:

- `Areas/Owner/Controllers/ShiftsController`
- `Areas/Staff/Controllers/ShiftsController`

Views:

- `Index`
- `Open`
- `Close`
- `Details`

Tasks:

- [ ] Mở ca với `OpeningCash`.
- [ ] Gán `OpenedBy`.
- [ ] Gán `StoreId`.
- [ ] Gán `TenantId`.
- [ ] Không cho mở nhiều ca active nếu nghiệp vụ yêu cầu.
- [ ] Đóng ca với `ClosingCash`.
- [ ] Tính `ExpectedCash`.
- [ ] Tính `DifferenceAmount`.
- [ ] Ghi audit log `OpenShift`.
- [ ] Ghi audit log `CloseShift`.

### 15.2. Báo cáo theo ca

Tasks:

- [ ] Tổng số đơn trong ca.
- [ ] Tổng doanh thu trong ca.
- [ ] Tổng cash payment.
- [ ] Tổng bank/card/e-wallet payment.
- [ ] Chênh lệch tiền mặt.
- [ ] Danh sách đơn thuộc ca.

### 15.3. Báo cáo doanh thu

Controller:

- `Areas/Owner/Controllers/ReportsController`

Views:

- `Sales`
- `Inventory`
- `Staff`
- `Products`

Tasks:

- [ ] Báo cáo doanh thu toàn chuỗi.
- [ ] Báo cáo doanh thu theo store.
- [ ] Báo cáo doanh thu theo ngày.
- [ ] Báo cáo doanh thu theo tháng.
- [ ] Báo cáo doanh thu theo năm.
- [ ] Biểu đồ doanh thu bằng Chart.js.
- [ ] Dữ liệu lấy từ `Orders` hoặc view `vw_DailySalesReport`.

### 15.4. Sản phẩm bán chạy

Tasks:

- [ ] Query từ `OrderItems`.
- [ ] Group theo product.
- [ ] Tính tổng quantity.
- [ ] Tính tổng revenue.
- [ ] Filter theo thời gian.
- [ ] Filter theo store.
- [ ] Hiển thị top 10/top 20.

### 15.5. Tồn kho thấp

Tasks:

- [ ] Dùng `Inventories`.
- [ ] Hoặc dùng view `vw_InventoryStatusReport`.
- [ ] Filter theo store.
- [ ] Hiển thị product, quantity, min quantity.
- [ ] Link nhanh đến nhập kho.

### 15.6. Báo cáo nhân viên

Tasks:

- [ ] Dùng `Orders.StaffUserId`.
- [ ] Hoặc dùng view `vw_StaffSalesReport`.
- [ ] Tổng đơn theo staff.
- [ ] Tổng doanh thu theo staff.
- [ ] Filter theo store.
- [ ] Filter theo thời gian.

### 15.7. Xuất Excel

Service:

- `IExcelExportService`

Tasks:

- [ ] Export báo cáo doanh thu.
- [ ] Export báo cáo tồn kho.
- [ ] Export báo cáo nhân viên.
- [ ] Export lịch sử kho.
- [ ] File đặt tên theo report và thời gian.

### 15.8. Dashboard

Owner dashboard:

- [ ] Tổng doanh thu hôm nay.
- [ ] Số đơn hôm nay.
- [ ] Số sản phẩm low stock.
- [ ] Doanh thu 7 ngày gần nhất.
- [ ] Top sản phẩm bán chạy.
- [ ] Danh sách order mới nhất.

Staff dashboard:

- [ ] Store đang làm việc.
- [ ] Ca đang mở.
- [ ] Doanh thu cá nhân hôm nay.
- [ ] Số đơn cá nhân hôm nay.
- [ ] Shortcut vào POS.

Admin dashboard:

- [ ] Tổng tenant.
- [ ] Tổng store.
- [ ] Tổng owner.
- [ ] Doanh thu subscription.
- [ ] Tenant mới gần đây.
- [ ] Gói dịch vụ đang dùng nhiều.

### 15.9. Acceptance criteria tuần 7

- [ ] Mở/đóng ca được.
- [ ] Báo cáo theo ca đúng.
- [ ] Dashboard owner có số liệu thật.
- [ ] Báo cáo doanh thu đúng.
- [ ] Báo cáo tồn kho thấp đúng.
- [ ] Export Excel được.

## 16. Tuần 8: Subscription + Hoàn thiện

### 16.1. Gói dịch vụ

Controller:

- `Areas/Admin/Controllers/SubscriptionPlansController`

Views:

- `Index`
- `Create`
- `Edit`
- `Details`

Tasks:

- [ ] CRUD plan.
- [ ] Cấu hình price.
- [ ] Cấu hình billing cycle.
- [ ] Cấu hình `MaxStores`.
- [ ] Cấu hình `MaxStaff`.
- [ ] Cấu hình `MaxProducts`.
- [ ] Ẩn/hiện plan.
- [ ] Xóa mềm plan.
- [ ] Ghi audit log.

### 16.2. Tenant subscription

Admin tasks:

- [ ] Gán plan cho tenant.
- [ ] Gia hạn subscription.
- [ ] Hủy subscription.
- [ ] Tạm khóa subscription.
- [ ] Xem lịch sử subscription.

Owner tasks:

- [ ] Xem gói hiện tại.
- [ ] Xem ngày hết hạn.
- [ ] Xem giới hạn store/staff/product.
- [ ] Xem lịch sử thanh toán.
- [ ] Tải hóa đơn nếu có `InvoiceUrl`.

### 16.3. Giới hạn theo gói

Tasks:

- [ ] Khi tạo store, kiểm tra `MaxStores`.
- [ ] Khi tạo staff, kiểm tra `MaxStaff`.
- [ ] Khi tạo product, kiểm tra `MaxProducts`.
- [ ] Nếu vượt giới hạn, hiển thị thông báo rõ ràng.
- [ ] Admin có thể bypass nếu cần.

### 16.4. Lịch sử thanh toán SaaS

Controller:

- `Areas/Admin/Controllers/SystemPaymentsController`
- `Areas/Owner/Controllers/SubscriptionController`

Tasks:

- [ ] Admin xem toàn bộ system payments.
- [ ] Owner chỉ xem payment của tenant mình.
- [ ] Tạo payment record.
- [ ] Cập nhật status `Paid`.
- [ ] Cập nhật status `Failed`.
- [ ] Cập nhật `PaidAt`.
- [ ] Upload/link invoice.
- [ ] Ghi audit log.

### 16.5. Audit log đầy đủ

Controller:

- `Areas/Admin/Controllers/AuditLogsController`
- `Areas/Owner/Controllers/AuditLogsController`

Tasks:

- [ ] Admin xem toàn bộ audit log.
- [ ] Owner xem audit log trong tenant.
- [ ] Filter theo user.
- [ ] Filter theo store.
- [ ] Filter theo action.
- [ ] Filter theo thời gian.
- [ ] Xem old value/new value.
- [ ] Export audit log nếu cần.

### 16.6. Tối ưu UI

Tasks:

- [ ] Chuẩn hóa spacing.
- [ ] Chuẩn hóa button.
- [ ] Chuẩn hóa table.
- [ ] Chuẩn hóa badge status.
- [ ] Chuẩn hóa form validation.
- [ ] Tối ưu mobile sidebar.
- [ ] Tối ưu print receipt.
- [ ] Thêm loading state cho action lâu.
- [ ] Thêm empty state cho table không có dữ liệu.

### 16.7. Test phân quyền

Test matrix:

- [ ] Anonymous không vào được admin/owner/staff.
- [ ] Admin không bị tenant filter.
- [ ] Owner không xem tenant khác.
- [ ] Owner không vào admin nếu không có role admin.
- [ ] Staff không vào owner.
- [ ] Staff không vào admin.
- [ ] Staff không xem store chưa được gán.
- [ ] Staff bị khóa không login được.
- [ ] Tenant suspended bị chặn module nghiệp vụ.

### 16.8. Deploy

Tasks:

- [ ] Cấu hình production connection string.
- [ ] Cấu hình environment variables.
- [ ] Build release.
- [ ] Publish app.
- [ ] Chạy script database production.
- [ ] Seed admin production an toàn.
- [ ] Cấu hình HTTPS.
- [ ] Cấu hình backup database.
- [ ] Cấu hình logging.
- [ ] Kiểm tra upload folder permission.

### 16.9. Acceptance criteria tuần 8

- [ ] Subscription hoạt động.
- [ ] Giới hạn theo gói hoạt động.
- [ ] Audit log đủ thao tác quan trọng.
- [ ] UI hoàn thiện đủ dùng demo.
- [ ] Test phân quyền pass.
- [ ] Deploy được.

## 17. Chi tiết controller và view cần tạo

### 17.1. AccountController

Actions:

- [ ] `Login`
- [ ] `Logout`
- [ ] `ForgotPassword`
- [ ] `ResetPassword`
- [ ] `ChangePassword`
- [ ] `Profile`
- [ ] `AccessDenied`

Views:

- [ ] `Login.cshtml`
- [ ] `ForgotPassword.cshtml`
- [ ] `ResetPassword.cshtml`
- [ ] `ChangePassword.cshtml`
- [ ] `Profile.cshtml`
- [ ] `AccessDenied.cshtml`

### 17.2. Admin controllers

Dashboard:

- [ ] `DashboardController.Index`

Owners:

- [ ] `OwnersController.Index`
- [ ] `OwnersController.Create`
- [ ] `OwnersController.Edit`
- [ ] `OwnersController.Details`
- [ ] `OwnersController.Lock`
- [ ] `OwnersController.Unlock`
- [ ] `OwnersController.Delete`

Tenants:

- [ ] `TenantsController.Index`
- [ ] `TenantsController.Details`
- [ ] `TenantsController.Suspend`
- [ ] `TenantsController.Activate`
- [ ] `TenantsController.Cancel`

Subscription plans:

- [ ] `SubscriptionPlansController.Index`
- [ ] `SubscriptionPlansController.Create`
- [ ] `SubscriptionPlansController.Edit`
- [ ] `SubscriptionPlansController.Details`
- [ ] `SubscriptionPlansController.ToggleActive`
- [ ] `SubscriptionPlansController.Delete`

System payments:

- [ ] `SystemPaymentsController.Index`
- [ ] `SystemPaymentsController.Details`
- [ ] `SystemPaymentsController.MarkAsPaid`
- [ ] `SystemPaymentsController.MarkAsFailed`

Reports:

- [ ] `ReportsController.PlatformOverview`
- [ ] `ReportsController.SystemRevenue`
- [ ] `ReportsController.ExportSystemRevenue`

Audit logs:

- [ ] `AuditLogsController.Index`
- [ ] `AuditLogsController.Details`
- [ ] `AuditLogsController.Export`

### 17.3. Owner controllers

Dashboard:

- [ ] `DashboardController.Index`

Stores:

- [ ] `StoresController.Index`
- [ ] `StoresController.Create`
- [ ] `StoresController.Edit`
- [ ] `StoresController.Details`
- [ ] `StoresController.Lock`
- [ ] `StoresController.Unlock`
- [ ] `StoresController.Delete`

Staff:

- [ ] `StaffController.Index`
- [ ] `StaffController.Create`
- [ ] `StaffController.Edit`
- [ ] `StaffController.Details`
- [ ] `StaffController.AssignStores`
- [ ] `StaffController.Lock`
- [ ] `StaffController.Unlock`
- [ ] `StaffController.ResetPassword`

Categories:

- [ ] `CategoriesController.Index`
- [ ] `CategoriesController.Create`
- [ ] `CategoriesController.Edit`
- [ ] `CategoriesController.Delete`
- [ ] `CategoriesController.ToggleActive`

Products:

- [ ] `ProductsController.Index`
- [ ] `ProductsController.Create`
- [ ] `ProductsController.Edit`
- [ ] `ProductsController.Details`
- [ ] `ProductsController.Delete`
- [ ] `ProductsController.ToggleActive`
- [ ] `ProductsController.UploadImage`
- [ ] `ProductsController.StoreSettings`

Inventory:

- [ ] `InventoryController.Index`
- [ ] `InventoryController.Import`
- [ ] `InventoryController.Export`
- [ ] `InventoryController.Adjust`
- [ ] `InventoryController.History`
- [ ] `InventoryController.LowStock`

Shifts:

- [ ] `ShiftsController.Index`
- [ ] `ShiftsController.Open`
- [ ] `ShiftsController.Close`
- [ ] `ShiftsController.Details`

POS:

- [ ] `PosController.Index`
- [ ] `PosController.Checkout`
- [ ] `PosController.Receipt`

Orders:

- [ ] `OrdersController.Index`
- [ ] `OrdersController.Details`
- [ ] `OrdersController.Cancel`
- [ ] `OrdersController.Print`

Reports:

- [ ] `ReportsController.Sales`
- [ ] `ReportsController.Staff`
- [ ] `ReportsController.Products`
- [ ] `ReportsController.Inventory`
- [ ] `ReportsController.ExportSales`
- [ ] `ReportsController.ExportInventory`
- [ ] `ReportsController.ExportStaff`

Subscription:

- [ ] `SubscriptionController.Index`
- [ ] `SubscriptionController.PaymentHistory`
- [ ] `SubscriptionController.Invoice`

Audit logs:

- [ ] `AuditLogsController.Index`
- [ ] `AuditLogsController.Details`

### 17.4. Staff controllers

Dashboard:

- [ ] `DashboardController.Index`

POS:

- [ ] `PosController.Index`
- [ ] `PosController.Checkout`
- [ ] `PosController.Receipt`

Inventory:

- [ ] `InventoryController.Index`
- [ ] `InventoryController.History`

Shifts:

- [ ] `ShiftsController.Open`
- [ ] `ShiftsController.Close`
- [ ] `ShiftsController.Current`

Orders:

- [ ] `OrdersController.Index`
- [ ] `OrdersController.Details`
- [ ] `OrdersController.Print`

Profile:

- [ ] `ProfileController.Index`
- [ ] `ProfileController.Edit`
- [ ] `ProfileController.ChangePassword`

## 18. ViewModel cần tạo

### 18.1. Auth ViewModel

- [ ] `LoginViewModel`
- [ ] `ForgotPasswordViewModel`
- [ ] `ResetPasswordViewModel`
- [ ] `ChangePasswordViewModel`
- [ ] `ProfileViewModel`

### 18.2. Admin ViewModel

- [ ] `AdminDashboardViewModel`
- [ ] `OwnerListItemViewModel`
- [ ] `OwnerCreateViewModel`
- [ ] `OwnerEditViewModel`
- [ ] `TenantListItemViewModel`
- [ ] `SubscriptionPlanViewModel`
- [ ] `SystemPaymentViewModel`
- [ ] `PlatformReportViewModel`
- [ ] `AuditLogListItemViewModel`

### 18.3. Owner ViewModel

- [ ] `OwnerDashboardViewModel`
- [ ] `StoreViewModel`
- [ ] `StaffCreateViewModel`
- [ ] `StaffEditViewModel`
- [ ] `AssignStoresViewModel`
- [ ] `CategoryViewModel`
- [ ] `ProductViewModel`
- [ ] `ProductListItemViewModel`
- [ ] `StoreProductViewModel`
- [ ] `InventoryViewModel`
- [ ] `InventoryTransactionViewModel`
- [ ] `InventoryImportViewModel`
- [ ] `InventoryExportViewModel`
- [ ] `InventoryAdjustViewModel`
- [ ] `ShiftViewModel`
- [ ] `OrderViewModel`
- [ ] `OrderDetailViewModel`
- [ ] `ReportFilterViewModel`
- [ ] `SalesReportViewModel`
- [ ] `InventoryReportViewModel`
- [ ] `StaffReportViewModel`

### 18.4. POS ViewModel

- [ ] `PosProductViewModel`
- [ ] `PosCartViewModel`
- [ ] `PosCartItemViewModel`
- [ ] `CheckoutViewModel`
- [ ] `PaymentInputViewModel`
- [ ] `ReceiptViewModel`

### 18.5. Staff ViewModel

- [ ] `StaffDashboardViewModel`
- [ ] `StaffStoreSelectorViewModel`
- [ ] `StaffInventoryViewModel`
- [ ] `StaffOrderHistoryViewModel`

## 19. Logic nghiệp vụ quan trọng cần implement

### 19.1. Điều hướng sau login

Logic:

- Nếu role `ADMIN`: redirect `/admin/dashboard`.
- Nếu role `OWNER`: redirect `/owner/dashboard`.
- Nếu role `STAFF`: redirect `/staff/dashboard`.
- Nếu user bị khóa: sign out và báo lỗi.
- Nếu role không hợp lệ: access denied.

### 19.2. Tạo owner

Service method:

```text
CreateOwnerAsync(CreateOwnerRequest request)
```

Steps:

- [ ] Validate email chưa tồn tại.
- [ ] Tạo user Identity.
- [ ] Gán role `OWNER`.
- [ ] Tạo tenant.
- [ ] Gán tenant cho owner.
- [ ] Ghi audit log.
- [ ] Transaction nếu có thể.

### 19.3. Tạo staff

Service method:

```text
CreateStaffAsync(CreateStaffRequest request)
```

Steps:

- [ ] Current user phải là owner.
- [ ] Validate tenant.
- [ ] Kiểm tra giới hạn `MaxStaff`.
- [ ] Validate email chưa tồn tại.
- [ ] Tạo user Identity.
- [ ] Gán role `STAFF`.
- [ ] Gán `TenantId`.
- [ ] Gán store nếu request có store ids.
- [ ] Ghi audit log.

### 19.4. Kiểm tra quyền store

Service method:

```text
CanAccessStoreAsync(userId, tenantId, storeId)
```

Rules:

- [ ] Admin không dùng logic này cho nghiệp vụ store.
- [ ] Owner được truy cập nếu store thuộc tenant.
- [ ] Staff được truy cập nếu có `UserStores.IsActive = true`.

### 19.5. Tạo sản phẩm

Steps:

- [ ] Kiểm tra tenant.
- [ ] Kiểm tra giới hạn `MaxProducts`.
- [ ] Validate SKU unique trong tenant.
- [ ] Validate barcode unique trong tenant.
- [ ] Validate price/cost price.
- [ ] Lưu product.
- [ ] Ghi audit log.

### 19.6. Nhập kho

Service method:

```text
ImportStockAsync(ImportStockRequest request)
```

Steps:

- [ ] Check store access.
- [ ] Validate product thuộc tenant.
- [ ] Validate quantity > 0.
- [ ] Tạo inventory nếu chưa có.
- [ ] Update quantity.
- [ ] Insert inventory transaction.
- [ ] Insert audit log.
- [ ] Commit.

### 19.7. Xuất kho

Service method:

```text
ExportStockAsync(ExportStockRequest request)
```

Steps:

- [ ] Check store access.
- [ ] Validate product thuộc tenant.
- [ ] Validate quantity > 0.
- [ ] Validate tồn đủ.
- [ ] Update quantity.
- [ ] Insert inventory transaction.
- [ ] Insert audit log.
- [ ] Commit.

### 19.8. Điều chỉnh kho

Service method:

```text
AdjustStockAsync(AdjustStockRequest request)
```

Steps:

- [ ] Check store access.
- [ ] Validate actual quantity >= 0.
- [ ] Validate reason bắt buộc.
- [ ] Update inventory quantity.
- [ ] Insert transaction type `Adjust`.
- [ ] Insert audit log.

### 19.9. Tạo order POS

Service method:

```text
CreateOrderAsync(CreateOrderRequest request)
```

Steps:

- [ ] Check store access.
- [ ] Validate cart không rỗng.
- [ ] Validate từng product thuộc tenant.
- [ ] Validate product active.
- [ ] Validate product available tại store.
- [ ] Validate tồn đủ.
- [ ] Backend tự tính giá.
- [ ] Tạo order code.
- [ ] Insert order.
- [ ] Insert order items.
- [ ] Insert payment.
- [ ] Update inventory.
- [ ] Insert inventory transaction type `Sale`.
- [ ] Insert audit log.
- [ ] Commit.

### 19.10. Hủy order

Service method:

```text
CancelOrderAsync(orderId, reason)
```

Steps:

- [ ] Check order thuộc tenant.
- [ ] Check store access.
- [ ] Check order chưa bị hủy.
- [ ] Update order status.
- [ ] Update cancelled fields.
- [ ] Hoàn kho từng item nếu đã trừ kho.
- [ ] Insert inventory transaction type `Return`.
- [ ] Update payment status nếu cần.
- [ ] Insert audit log.
- [ ] Commit.

### 19.11. Mở ca

Service method:

```text
OpenShiftAsync(OpenShiftRequest request)
```

Steps:

- [ ] Check store access.
- [ ] Validate opening cash >= 0.
- [ ] Kiểm tra ca đang mở nếu nghiệp vụ chỉ cho một ca.
- [ ] Insert shift status `Open`.
- [ ] Insert audit log.

### 19.12. Đóng ca

Service method:

```text
CloseShiftAsync(CloseShiftRequest request)
```

Steps:

- [ ] Check shift thuộc tenant/store.
- [ ] Check user có quyền đóng.
- [ ] Tính expected cash từ payments cash trong ca.
- [ ] Tính difference amount.
- [ ] Update shift status `Closed`.
- [ ] Insert audit log.

## 20. Database query/report cần chuẩn bị

### 20.1. Dashboard owner

Queries:

- [ ] Tổng doanh thu hôm nay từ `Orders`.
- [ ] Tổng order hôm nay từ `Orders`.
- [ ] Low stock từ `Inventories`.
- [ ] Top product từ `OrderItems`.
- [ ] Recent orders từ `Orders`.

### 20.2. Dashboard admin

Queries:

- [ ] Tổng tenant.
- [ ] Tổng store.
- [ ] Tổng owner.
- [ ] Tổng system revenue từ `SystemPayments`.
- [ ] Subscription active từ `TenantSubscriptions`.

### 20.3. Dashboard staff

Queries:

- [ ] Store được gán từ `UserStores`.
- [ ] Current open shift từ `Shifts`.
- [ ] Doanh thu cá nhân hôm nay từ `Orders`.
- [ ] Order cá nhân gần đây.

### 20.4. Reports

- [ ] Daily sales dùng `vw_DailySalesReport`.
- [ ] Staff sales dùng `vw_StaffSalesReport`.
- [ ] Inventory status dùng `vw_InventoryStatusReport`.
- [ ] System revenue dùng `vw_SystemRevenueReport`.

## 21. Validation rules

### 21.1. User

- [ ] Email bắt buộc.
- [ ] Email đúng format.
- [ ] Email không trùng.
- [ ] Full name bắt buộc.
- [ ] Password đạt policy.
- [ ] Status hợp lệ.

### 21.2. Store

- [ ] Name bắt buộc.
- [ ] Code bắt buộc.
- [ ] Code không trùng trong tenant.
- [ ] Status hợp lệ.

### 21.3. Product

- [ ] Name bắt buộc.
- [ ] Price >= 0.
- [ ] CostPrice >= 0.
- [ ] SKU không trùng trong tenant nếu có.
- [ ] Barcode không trùng trong tenant nếu có.
- [ ] Category phải thuộc tenant.

### 21.4. Inventory

- [ ] Quantity thao tác > 0.
- [ ] Tồn sau thao tác không âm.
- [ ] Product thuộc tenant.
- [ ] Store thuộc tenant.
- [ ] Staff có quyền store.

### 21.5. Order

- [ ] Cart không rỗng.
- [ ] Quantity từng item > 0.
- [ ] Product active.
- [ ] Product available tại store.
- [ ] Tồn đủ.
- [ ] Payment amount > 0.
- [ ] Payment method hợp lệ.

## 22. Security checklist

- [ ] Bật HTTPS.
- [ ] Bật antiforgery token cho form POST.
- [ ] Không bind trực tiếp entity từ request.
- [ ] Dùng ViewModel/InputModel.
- [ ] Validate server-side đầy đủ.
- [ ] Không tin `TenantId`, `StoreId`, `UserId` gửi từ client nếu có thể lấy từ current user.
- [ ] Mọi query owner/staff phải lọc tenant.
- [ ] Mọi query staff theo store phải kiểm tra `UserStores`.
- [ ] Không lộ password tạm trong log.
- [ ] Không log `PasswordHash`.
- [ ] Không cho upload file ngoài định dạng ảnh.
- [ ] Giới hạn dung lượng upload.
- [ ] Chặn path traversal khi upload.

## 23. Test plan

### 23.1. Unit test service

- [ ] Tạo owner.
- [ ] Tạo staff.
- [ ] Gán staff vào store.
- [ ] Check store access.
- [ ] Tạo product.
- [ ] Import stock.
- [ ] Export stock.
- [ ] Adjust stock.
- [ ] Create order.
- [ ] Cancel order.
- [ ] Open shift.
- [ ] Close shift.

### 23.2. Integration test

- [ ] Login admin.
- [ ] Login owner.
- [ ] Login staff.
- [ ] Owner tạo store.
- [ ] Owner tạo staff.
- [ ] Staff bị chặn khi vào store không được gán.
- [ ] POS order trừ kho đúng.
- [ ] Cancel order hoàn kho đúng.

### 23.3. Manual test phân quyền

- [ ] Anonymous truy cập `/admin` bị redirect login.
- [ ] Staff truy cập `/owner/products` bị access denied.
- [ ] Owner truy cập `/admin/owners` bị access denied.
- [ ] Staff đổi URL store id sang store khác bị chặn.
- [ ] Owner đổi tenant id trên query string không xem được tenant khác.

## 24. Seed dữ liệu demo

Tạo seeder demo cho môi trường development.

### 24.1. Admin

- [ ] Email: `admin@storesaas.local`
- [ ] Role: `ADMIN`

### 24.2. Owner demo

- [ ] Email: `owner@demo.local`
- [ ] Role: `OWNER`
- [ ] Tenant: `Demo Retail Chain`

### 24.3. Stores demo

- [ ] Store 1: `Demo Store 01`
- [ ] Store 2: `Demo Store 02`

### 24.4. Staff demo

- [ ] Email: `staff01@demo.local`
- [ ] Role: `STAFF`
- [ ] Store: `Demo Store 01`

### 24.5. Product demo

- [ ] Category: `Beverage`
- [ ] Product: `Coffee`
- [ ] Product: `Milk Tea`
- [ ] Product: `Snack`

### 24.6. Inventory demo

- [ ] Tạo tồn kho cho từng product/store.
- [ ] Tạo vài transaction nhập kho.

## 25. Definition of Done

Một chức năng chỉ coi là xong khi:

- [ ] Có controller action.
- [ ] Có service xử lý nghiệp vụ.
- [ ] Có ViewModel riêng.
- [ ] Có Razor view đủ form/table/state.
- [ ] Có validation server-side.
- [ ] Có phân quyền role.
- [ ] Có kiểm tra tenant.
- [ ] Có kiểm tra store access nếu liên quan store.
- [ ] Có audit log nếu là thao tác quan trọng.
- [ ] Có xử lý lỗi và thông báo UI.
- [ ] Có test cơ bản hoặc manual test checklist.

## 26. Thứ tự ưu tiên nếu thiếu thời gian

Ưu tiên cao nhất:

1. Login/logout.
2. Role admin/owner/staff.
3. Tenant isolation.
4. Owner tạo store.
5. Owner tạo staff.
6. Staff store access.
7. Product.
8. Inventory.
9. POS order.
10. Payment.
11. Stock transaction.
12. Order history.

Ưu tiên sau:

1. Subscription.
2. Export Excel.
3. PDF invoice.
4. Advanced audit search.
5. Advanced dashboard chart.
6. Email confirmation.
7. External login.

## 27. Ghi chú triển khai quan trọng

- Schema SQL hiện tại đã có đủ bảng nền tảng cho MVP và giai đoạn nâng cao.
- Không nên làm subscription trước POS, vì giá trị chính của hệ thống là quản lý bán hàng và kho.
- Không nên bỏ `UserStores`, vì đây là bảng quyết định staff được thao tác cửa hàng nào.
- Không nên bỏ `InventoryTransactions`, vì nếu chỉ lưu tồn kho hiện tại sẽ không audit được sai lệch kho.
- Không nên xóa vật lý dữ liệu nghiệp vụ đã phát sinh order/kho/payment.
- Không nên để client gửi total amount rồi lưu thẳng vào order. Backend phải tự tính.
- Không nên dùng một layout cho cả 3 role nếu menu khác nhau nhiều. Nên tách layout để tránh lộ menu sai quyền.

