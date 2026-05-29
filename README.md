# ChainPOS

ChainPOS là hệ thống quản lý chuỗi cửa hàng và POS dạng SaaS, xây dựng bằng ASP.NET Core MVC Razor Views trên nền database-first từ SQL Server.

Hệ thống hiện tập trung vào 3 nhóm người dùng:

- `ADMIN`: quản lý platform, owner, tenant và dữ liệu SaaS.
- `OWNER`: quản lý dữ liệu trong tenant của mình như store, staff, category, product và product theo store.
- `STAFF`: thao tác theo tenant và store được gán qua `UserStores`.

## Công nghệ sử dụng

- .NET 9
- ASP.NET Core MVC server-rendered với Razor Views
- Entity Framework Core SQL Server
- Cookie Authentication
- `PasswordHasher<AspNetUser>` để verify password trên model Identity scaffolded
- Tailwind CSS qua CDN cho giao diện dashboard
- SQL Server schema/database-first, không dùng EF migration cho schema hiện tại

## Kiến trúc dữ liệu

Project dùng model scaffolded từ SQL Server trong thư mục:

```text
ChainPOS/Models
```

DbContext chính:

```text
ChainPOS.Models.StoreFlowDbContext
```

Các nhóm model quan trọng:

- Identity: `AspNetUser`, `AspNetRole`, `AspNetUserRole`
- Tenant/store: `Tenant`, `Store`, `UserStore`
- Catalog: `Category`, `Product`, `StoreProduct`
- Inventory/POS về sau: `Inventory`, `InventoryTransaction`, `Order`, `OrderItem`, `Payment`, `Shift`
- SaaS subscription: `SubscriptionPlan`, `TenantSubscription`, `SystemPayment`
- Audit/report: `AuditLog`, các view report `VwDailySalesReport`, `VwInventoryStatusReport`, `VwStaffSalesReport`, `VwSystemRevenueReport`

## Chức năng đã triển khai

### Authentication và phân quyền

- Login/logout bằng Cookie Authentication.
- Login theo email hoặc username.
- Verify password bằng `PasswordHasher<AspNetUser>`.
- Load role từ `AspNetUser.Roles`.
- Redirect sau login theo role:
  - `ADMIN`: `/admin/dashboard`
  - `OWNER`: `/owner/dashboard`
  - `STAFF`: `/staff/dashboard`
- Chặn user inactive/locked.
- Chặn tenant suspended/cancelled cho owner và staff.
- Current user context qua `ICurrentUserService`.
- Store access nền tảng qua `IStoreAccessService`.

### Dashboard và layout

- Layout riêng cho Admin, Owner, Staff.
- Sidebar/topbar/alert/confirm modal dùng chung.
- Dashboard theo role.
- Menu hiển thị theo đúng quyền.

### Admin quản lý platform

- Quản lý owner.
- Quản lý tenant.
- Tạo owner kèm tenant.
- Gán role owner.
- Lock/unlock owner.
- Suspend/activate/cancel tenant.
- Xem chi tiết owner/tenant.
- Ghi audit log cho thao tác quan trọng.

### Owner quản lý store

- Danh sách store theo tenant.
- Search theo name/code.
- Tạo store.
- Sửa store.
- Set trạng thái `Active`, `Inactive`, `Closed`.
- Soft delete store.
- Check code unique trong tenant.
- Check giới hạn `MaxStores` theo subscription.
- Ghi audit log.

### Owner quản lý staff

- Danh sách staff theo tenant.
- Tạo staff.
- Gán role `STAFF`.
- Gán `TenantId` theo owner.
- Reset password staff.
- Lock/unlock staff.
- Gán staff vào store qua `UserStores`.
- Bật/tắt `UserStores.IsActive`.
- Check giới hạn `MaxStaff` theo subscription.
- Ghi audit log.

### Owner quản lý category

- Danh sách category theo tenant.
- Tạo category.
- Sửa category.
- Bật/tắt `IsActive`.
- Soft delete.
- Check unique `(TenantId, Name)` khi chưa xóa.
- Ghi audit log.

### Owner quản lý product

- Danh sách product theo tenant.
- Search/filter theo name, SKU, barcode, category, status.
- Create/edit/details.
- Upload ảnh vào `wwwroot/uploads/products`.
- Bật/tắt `IsActive`.
- Soft delete.
- Check SKU unique trong tenant.
- Check barcode unique trong tenant.
- Validate `Price >= 0`, `CostPrice >= 0`.
- Check giới hạn `MaxProducts` theo subscription.
- Ghi audit log.

### Owner quản lý store product

- Gán product vào store.
- Bật/tắt `IsAvailable`.
- Set `SellingPrice` riêng theo store.
- Nếu `SellingPrice` rỗng, POS sẽ dùng `Products.Price`.
- Đã có helper `GetEffectiveSellingPriceAsync` để POS ưu tiên `StoreProducts.SellingPrice`, fallback `Products.Price`.
- Ghi audit log cho assign/update/enable/disable.

## Dữ liệu demo development

Seeder nằm tại:

```text
ChainPOS/Services/Seed/DevelopmentDataSeeder.cs
```

Seeder chạy tự động khi môi trường là Development.

Tài khoản demo:

| Role | Email | Password |
| --- | --- | --- |
| Admin | `admin@chainpos.local` | `Admin@123` |
| Owner demo | `owner@demo.local` | `Owner@123` |
| Staff demo | `staff01@demo.local` | `Staff@123` |

Dữ liệu demo đã seed:

- 4 owners
- 4 tenants
- 5 stores
- 5 staff
- 6 categories
- 13 products
- 13 store-product assignments hiển thị được
- 4 active subscriptions
- Audit logs cho các thao tác seed chính

Một số sản phẩm demo:

- Apple MacBook Pro 14-inch M3 Pro 18GB/512GB
- Apple MacBook Air 13-inch M3 8GB/256GB
- Apple iPhone 15 Pro 256GB Natural Titanium
- Samsung Galaxy S24 Ultra 256GB Titanium Gray
- Sony WH-1000XM5 Wireless Noise Cancelling Headphones
- Logitech MX Master 3S Wireless Mouse Graphite
- Dell UltraSharp U2723QE 27-inch 4K USB-C Monitor
- Samsung 990 PRO 1TB NVMe PCIe 4.0 SSD
- Anker 737 Power Bank 24000mAh 140W

Plan demo:

```text
Name: Business Demo
MaxStores: 10
MaxStaff: 50
MaxProducts: 200
BillingCycle: Monthly
```

## Cách chạy project

Từ thư mục solution:

```powershell
cd D:\laptrinhweb\code_outsrc\Dam_Van_Bao\ChainPOS\ChainPOS
dotnet build .\ChainPOS.sln
dotnet run --project .\ChainPOS\ChainPOS.csproj --launch-profile http
```

URL mặc định khi chạy profile `http`:

```text
http://localhost:5292
```

Connection string hiện đọc từ:

```text
ChainPOS/appsettings.json
ConnectionStrings:DefaultConnection
```

## Cấu trúc thư mục chính

```text
ChainPOS/
  Areas/
    Admin/
    Owner/
    Staff/
  Constants/
  Controllers/
  Filters/
  Models/
  Services/
    Admin/
    Audit/
    Auth/
    Common/
    Dashboard/
    Owner/
    Security/
    Seed/
  ViewModels/
  Views/
  wwwroot/
```

## Quy tắc phát triển

Các quy tắc làm việc chính nằm trong:

```text
ChainPOS/rule.md
```

Backlog và trạng thái task nằm trong:

```text
ChainPOS/Task.md
```

Nguyên tắc quan trọng:

- Không bind trực tiếp entity scaffolded từ request. Dùng ViewModel/InputModel.
- Controller chỉ điều phối request/response, nghiệp vụ đặt trong service.
- Query của `OWNER` và `STAFF` phải lọc theo `TenantId`.
- Staff thao tác theo store phải kiểm tra `UserStores.IsActive = true`.
- Form POST phải có antiforgery token.
- Action nguy hiểm phải có confirm modal.
- Chức năng quan trọng phải ghi audit log.
- Khi code UI, ưu tiên lấy mẫu từ `D:\laptrinhweb\code_outsrc\Dam_Van_Bao\UI`.
- Làm xong phần nào phải cập nhật `Task.md`.

## Trạng thái tiếp theo

Phần nên làm tiếp theo theo `Task.md` là Phase 6: Inventory.

Các mục chính:

- Owner/Staff xem danh sách tồn kho theo tenant/store.
- Low stock dựa trên `Quantity <= MinQuantity`.
- Import stock.
- Export stock.
- Adjust stock.
- Ghi `InventoryTransactions`.
- Ghi audit log.
- Dùng database transaction cho nghiệp vụ kho.
