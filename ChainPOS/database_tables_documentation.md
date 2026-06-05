# Tài liệu chức năng các bảng database

Tài liệu này mô tả chi tiết chức năng từng bảng trong schema `saas_store_pos_schema.sql`.

Hệ thống là nền tảng SaaS quản lý chuỗi cửa hàng, POS, kho, nhân sự, subscription và báo cáo. Ba vai trò chính gồm:

- `ADMIN`: quản trị toàn bộ nền tảng SaaS.
- `OWNER`: chủ tenant / chủ chuỗi cửa hàng.
- `STAFF`: nhân viên bán hàng hoặc kho, chỉ thao tác trong cửa hàng được phân quyền.

Ràng buộc nghiệp vụ quan trọng:

- `STAFF` không tự đăng ký.
- `STAFF` phải do `OWNER` tạo.
- `STAFF` phải được gán vào ít nhất một cửa hàng qua bảng `UserStores` trước khi thao tác dữ liệu cửa hàng.
- Dữ liệu nghiệp vụ luôn phải lọc theo `TenantId`.
- Dữ liệu phát sinh tại cửa hàng phải có thêm `StoreId`.

## Quy ước chung

### Tenant

`Tenant` đại diện cho một doanh nghiệp, một chủ chuỗi hoặc một owner chính trong hệ thống SaaS.

Ví dụ:

- Chuỗi cửa hàng A là một tenant.
- Chủ cửa hàng B chỉ có một cửa hàng cũng là một tenant.

Các bảng nghiệp vụ như `Stores`, `Products`, `Inventories`, `Orders`, `AuditLogs` đều gắn với `TenantId` để đảm bảo dữ liệu của tenant này không lẫn với tenant khác.

### Store

`Store` là cửa hàng vật lý hoặc điểm bán thuộc một tenant.

Các dữ liệu như tồn kho, ca làm, đơn hàng, giao dịch kho và báo cáo bán hàng phải gắn với `StoreId`.

### Soft delete

Các bảng có cột `IsDeleted` dùng xóa mềm. Khi xóa mềm, dữ liệu không bị xóa vật lý khỏi database.

Lý do:

- Giữ lịch sử bán hàng.
- Giữ lịch sử kho.
- Giữ audit.
- Tránh mất dữ liệu liên quan đến hóa đơn cũ.

Khi truy vấn nghiệp vụ bình thường, nên luôn lọc:

```sql
WHERE IsDeleted = 0
```

### Audit fields

Các cột thường gặp:

- `CreatedAt`: thời điểm tạo bản ghi.
- `CreatedBy`: user tạo bản ghi.
- `UpdatedAt`: thời điểm cập nhật cuối.
- `UpdatedBy`: user cập nhật cuối.
- `IsDeleted`: đánh dấu xóa mềm.

Các cột này giúp truy vết dữ liệu, hỗ trợ audit log và điều tra lỗi vận hành.

## Nhóm tài khoản và phân quyền

## 1. AspNetRoles

### Chức năng

`AspNetRoles` lưu danh sách vai trò trong hệ thống theo chuẩn ASP.NET Core Identity.

Trong hệ thống này có ba role lõi:

- `ADMIN`
- `OWNER`
- `STAFF`

### Ý nghĩa nghiệp vụ

Role quyết định người dùng được phép truy cập nhóm chức năng nào.

`ADMIN`:

- Quản lý toàn bộ nền tảng SaaS.
- Quản lý owner.
- Quản lý gói dịch vụ.
- Theo dõi doanh thu subscription.

`OWNER`:

- Quản lý tenant của mình.
- Tạo cửa hàng.
- Tạo staff.
- Gán staff vào cửa hàng.
- Quản lý sản phẩm, kho, POS và báo cáo trong tenant.

`STAFF`:

- Thao tác bán hàng hoặc kho trong cửa hàng được gán.
- Không được tự đăng ký.
- Không được truy cập dữ liệu ngoài cửa hàng được phân quyền.

### Cột chính

- `Id`: khóa chính của role. Trong seed hiện tại dùng trực tiếp `ADMIN`, `OWNER`, `STAFF`.
- `Name`: tên hiển thị của role.
- `NormalizedName`: tên chuẩn hóa để tìm kiếm và so sánh.
- `ConcurrencyStamp`: cột dùng bởi Identity để kiểm soát cập nhật đồng thời.

### Quan hệ

- Một role có thể được gán cho nhiều user qua `AspNetUserRoles`.
- Một role có thể có nhiều claim qua `AspNetRoleClaims`.

### Lưu ý khi code

Không nên cho user tự chọn role khi đăng ký. Role phải được gán bởi flow nghiệp vụ:

- `ADMIN` tạo hoặc duyệt `OWNER`.
- `OWNER` tạo `STAFF`.

## 2. AspNetUsers

### Chức năng

`AspNetUsers` lưu tài khoản đăng nhập và thông tin hồ sơ người dùng theo chuẩn ASP.NET Core Identity, đồng thời được mở rộng thêm các field phục vụ SaaS.

Đây là bảng trung tâm cho tất cả user trong hệ thống:

- Admin hệ thống.
- Owner của tenant.
- Staff trong tenant.

### Ý nghĩa nghiệp vụ

Mỗi người dùng đăng nhập vào hệ thống đều là một bản ghi trong `AspNetUsers`.

`ADMIN` thường không thuộc tenant nào, nên `TenantId` có thể `NULL`.

`OWNER` thường có `TenantId` trỏ đến tenant mà owner sở hữu.

`STAFF` luôn phải có `TenantId`, vì staff thuộc một tenant cụ thể.

### Cột Identity chuẩn

- `Id`: khóa chính user, kiểu `NVARCHAR(450)`.
- `UserName`: tên đăng nhập.
- `NormalizedUserName`: tên đăng nhập đã chuẩn hóa.
- `Email`: email user.
- `NormalizedEmail`: email đã chuẩn hóa.
- `EmailConfirmed`: đã xác thực email hay chưa.
- `PasswordHash`: mật khẩu đã hash.
- `SecurityStamp`: dùng để vô hiệu hóa session/token khi đổi mật khẩu hoặc thay đổi bảo mật.
- `ConcurrencyStamp`: kiểm soát cập nhật đồng thời.
- `PhoneNumber`: số điện thoại.
- `PhoneNumberConfirmed`: đã xác thực số điện thoại hay chưa.
- `TwoFactorEnabled`: bật xác thực hai lớp hay chưa.
- `LockoutEnd`: thời điểm hết khóa tài khoản.
- `LockoutEnabled`: có cho phép khóa tài khoản hay không.
- `AccessFailedCount`: số lần đăng nhập sai.

### Cột mở rộng

- `FullName`: họ tên đầy đủ.
- `AvatarUrl`: ảnh đại diện.
- `Status`: trạng thái tài khoản.
- `TenantId`: tenant mà user thuộc về.
- `CreatedAt`: thời điểm tạo tài khoản.
- `CreatedBy`: user tạo tài khoản.
- `UpdatedAt`: thời điểm cập nhật cuối.
- `UpdatedBy`: user cập nhật cuối.
- `LastLoginAt`: thời điểm đăng nhập gần nhất.

### Trạng thái tài khoản

`Status` hỗ trợ:

- `Active`: đang hoạt động.
- `Inactive`: tạm ngưng.
- `Locked`: bị khóa.
- `Pending`: chờ kích hoạt hoặc chờ hoàn tất thông tin.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- User được gán role qua `AspNetUserRoles`.
- User được gán cửa hàng qua `UserStores`.
- User có thể tạo đơn hàng trong `Orders`.
- User có thể mở/đóng ca trong `Shifts`.
- User có thể tạo giao dịch kho trong `InventoryTransactions`.
- User có thể phát sinh log trong `AuditLogs`.

### Lưu ý khi code

Không dùng riêng `TenantId` để xác định staff được thao tác cửa hàng nào. `TenantId` chỉ xác định staff thuộc tenant nào. Quyền vào từng cửa hàng phải kiểm tra qua `UserStores`.

Ví dụ khi staff tạo đơn hàng:

1. Kiểm tra user có role `STAFF`.
2. Kiểm tra user thuộc cùng `TenantId`.
3. Kiểm tra user có bản ghi active trong `UserStores` với `StoreId` đang thao tác.

## 3. AspNetUserRoles

### Chức năng

`AspNetUserRoles` là bảng trung gian gán user với role theo chuẩn ASP.NET Core Identity.

### Ý nghĩa nghiệp vụ

Bảng này xác định user là `ADMIN`, `OWNER` hay `STAFF`.

Một user có thể có nhiều role, nhưng với MVP nên giới hạn mỗi user chỉ dùng một role chính để giảm phức tạp.

### Cột chính

- `UserId`: user được gán role.
- `RoleId`: role được gán.

### Quan hệ

- `UserId` liên kết đến `AspNetUsers.Id`.
- `RoleId` liên kết đến `AspNetRoles.Id`.

### Lưu ý khi code

Nên kiểm soát flow gán role ở service layer:

- Chỉ `ADMIN` được tạo/gán `OWNER`.
- Chỉ `OWNER` được tạo/gán `STAFF` trong tenant của mình.
- Không cho `STAFF` tự đổi role.

## 4. AspNetUserClaims

### Chức năng

`AspNetUserClaims` lưu các claim riêng cho từng user.

Claim là quyền hoặc thông tin bổ sung được gắn trực tiếp vào user.

### Ý nghĩa nghiệp vụ

Trong MVP, có thể chưa cần dùng nhiều claim. Role đã đủ để phân quyền cơ bản.

Khi hệ thống lớn hơn, claim có thể dùng để mở quyền chi tiết hơn, ví dụ:

- `CanExportReport`
- `CanManageInventory`
- `CanDiscountOrder`
- `CanCancelOrder`

### Cột chính

- `Id`: khóa chính tự tăng.
- `UserId`: user sở hữu claim.
- `ClaimType`: loại claim.
- `ClaimValue`: giá trị claim.

### Quan hệ

- `UserId` liên kết đến `AspNetUsers.Id`.

### Lưu ý khi code

Không nên lạm dụng claim ngay từ đầu. Với MVP, dùng role và `UserStores` là đủ. Claim nên dùng khi cần phân quyền chi tiết trong cùng một role.

## 5. AspNetRoleClaims

### Chức năng

`AspNetRoleClaims` lưu các claim áp dụng cho toàn bộ user thuộc một role.

### Ý nghĩa nghiệp vụ

Nếu tất cả `OWNER` đều có quyền quản lý sản phẩm, có thể gắn claim `CanManageProducts` cho role `OWNER`.

Nếu tất cả `STAFF` đều có quyền tạo đơn hàng, có thể gắn claim `CanCreateOrder` cho role `STAFF`.

### Cột chính

- `Id`: khóa chính tự tăng.
- `RoleId`: role được gán claim.
- `ClaimType`: loại claim.
- `ClaimValue`: giá trị claim.

### Quan hệ

- `RoleId` liên kết đến `AspNetRoles.Id`.

### Lưu ý khi code

Role claim phù hợp khi quyền áp dụng đồng nhất cho toàn bộ role. Nếu quyền chỉ áp dụng cho một user cụ thể, dùng `AspNetUserClaims`.

## 6. AspNetUserLogins

### Chức năng

`AspNetUserLogins` lưu thông tin đăng nhập bên ngoài theo chuẩn ASP.NET Core Identity.

Ví dụ:

- Google login.
- Facebook login.
- Microsoft login.

### Ý nghĩa nghiệp vụ

Trong MVP có thể chưa dùng. Nếu sau này cho owner đăng nhập bằng Google hoặc Microsoft, bảng này sẽ lưu liên kết giữa tài khoản nội bộ và tài khoản bên ngoài.

### Cột chính

- `LoginProvider`: tên nhà cung cấp đăng nhập.
- `ProviderKey`: khóa định danh user từ nhà cung cấp.
- `ProviderDisplayName`: tên hiển thị của provider.
- `UserId`: user nội bộ trong hệ thống.

### Quan hệ

- `UserId` liên kết đến `AspNetUsers.Id`.

### Lưu ý khi code

Không nên cho `STAFF` tự tạo tài khoản qua external login nếu nghiệp vụ yêu cầu staff phải do owner tạo.

## 7. AspNetUserTokens

### Chức năng

`AspNetUserTokens` lưu token của user theo chuẩn ASP.NET Core Identity.

### Ý nghĩa nghiệp vụ

Bảng này thường được Identity dùng cho các tính năng như:

- Remember me.
- Reset password.
- Email confirmation.
- External authentication token.

### Cột chính

- `UserId`: user sở hữu token.
- `LoginProvider`: provider của token.
- `Name`: tên token.
- `Value`: giá trị token.

### Quan hệ

- `UserId` liên kết đến `AspNetUsers.Id`.

### Lưu ý khi code

Không nên tự thao tác trực tiếp bảng này nếu không cần. Nên dùng API của ASP.NET Core Identity.

## Nhóm tenant, cửa hàng và nhân sự

## 8. Tenants

### Chức năng

`Tenants` lưu thông tin doanh nghiệp, chủ chuỗi hoặc đơn vị thuê hệ thống SaaS.

Mỗi tenant là một vùng dữ liệu riêng biệt trong hệ thống.

### Ý nghĩa nghiệp vụ

Tenant là gốc của hầu hết dữ liệu nghiệp vụ:

- Cửa hàng.
- Nhân viên.
- Danh mục.
- Sản phẩm.
- Kho.
- Đơn hàng.
- Subscription.
- Audit log.

Khi một `OWNER` đăng ký hoặc được `ADMIN` tạo, hệ thống thường tạo kèm một tenant cho owner đó.

### Cột chính

- `Id`: khóa chính tenant.
- `Name`: tên doanh nghiệp hoặc chuỗi cửa hàng.
- `OwnerUserId`: user owner chính của tenant.
- `TaxCode`: mã số thuế.
- `Address`: địa chỉ doanh nghiệp.
- `Phone`: số điện thoại.
- `Email`: email liên hệ.
- `Status`: trạng thái tenant.
- `CreatedAt`: thời điểm tạo tenant.
- `CreatedBy`: user tạo tenant.
- `UpdatedAt`: thời điểm cập nhật cuối.
- `UpdatedBy`: user cập nhật cuối.
- `IsDeleted`: xóa mềm tenant.

### Trạng thái tenant

- `Active`: đang hoạt động.
- `Suspended`: bị tạm khóa.
- `Cancelled`: đã hủy dịch vụ.
- `Trial`: đang dùng thử.

### Quan hệ

- `OwnerUserId` liên kết đến `AspNetUsers.Id`.
- Một tenant có nhiều `Stores`.
- Một tenant có nhiều `Products`.
- Một tenant có nhiều `Orders`.
- Một tenant có nhiều `TenantSubscriptions`.
- Một tenant có nhiều `AuditLogs`.

### Lưu ý khi code

Mọi truy vấn nghiệp vụ của owner/staff phải lọc theo `TenantId`.

Ví dụ:

```sql
SELECT *
FROM Products
WHERE TenantId = @TenantId
  AND IsDeleted = 0;
```

`ADMIN` có thể xem nhiều tenant, nhưng owner và staff chỉ xem tenant của mình.

## 9. Stores

### Chức năng

`Stores` lưu danh sách cửa hàng thuộc một tenant.

### Ý nghĩa nghiệp vụ

Store là nơi phát sinh:

- Đơn hàng POS.
- Tồn kho.
- Nhập kho.
- Xuất kho.
- Điều chỉnh kho.
- Ca làm việc.
- Báo cáo doanh thu theo cửa hàng.

Một tenant có thể có một hoặc nhiều store.

### Cột chính

- `Id`: khóa chính cửa hàng.
- `TenantId`: tenant sở hữu cửa hàng.
- `Name`: tên cửa hàng.
- `Code`: mã cửa hàng.
- `Address`: địa chỉ cửa hàng.
- `Phone`: số điện thoại cửa hàng.
- `Status`: trạng thái cửa hàng.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.
- `IsDeleted`: xóa mềm.

### Trạng thái cửa hàng

- `Active`: đang hoạt động.
- `Inactive`: tạm ngưng.
- `Closed`: đã đóng.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- Một store có nhiều `UserStores`.
- Một store có nhiều `Inventories`.
- Một store có nhiều `InventoryTransactions`.
- Một store có nhiều `Shifts`.
- Một store có nhiều `Orders`.
- Một store có nhiều `AuditLogs`.

### Index quan trọng

- `IX_Stores_TenantId`: tăng tốc truy vấn cửa hàng theo tenant.
- `UX_Stores_TenantId_Code`: đảm bảo mã cửa hàng không trùng trong cùng tenant nếu chưa bị xóa mềm.

### Lưu ý khi code

`Code` chỉ cần unique trong phạm vi một tenant, không cần unique toàn hệ thống.

Khi xóa cửa hàng, nên dùng `IsDeleted = 1` thay vì xóa vật lý để không làm hỏng lịch sử đơn hàng và kho.

## 10. UserStores

### Chức năng

`UserStores` gán user vào cửa hàng.

Đây là bảng rất quan trọng để giới hạn phạm vi thao tác của `STAFF`.

### Ý nghĩa nghiệp vụ

Một staff có thể làm ở một hoặc nhiều cửa hàng. Một cửa hàng có thể có nhiều staff.

Ví dụ:

- Staff A chỉ làm ở cửa hàng 1.
- Staff B làm ở cửa hàng 1 và cửa hàng 2.
- Owner có thể xem tất cả cửa hàng trong tenant, nhưng staff chỉ xem cửa hàng có trong `UserStores`.

### Cột chính

- `Id`: khóa chính.
- `TenantId`: tenant chứa user và store.
- `UserId`: user được gán.
- `StoreId`: cửa hàng được gán.
- `IsActive`: quyền gán còn hiệu lực hay không.
- `CreatedAt`: thời điểm gán.
- `CreatedBy`: user thực hiện gán.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `UserId` liên kết đến `AspNetUsers.Id`.
- `StoreId` liên kết đến `Stores.Id`.

### Index quan trọng

- `UX_UserStores_TenantId_UserId_StoreId`: tránh gán trùng một user vào cùng một store trong cùng tenant.
- `IX_UserStores_StoreId`: tăng tốc truy vấn danh sách user theo cửa hàng.

### Lưu ý khi code

Khi staff gọi API liên quan đến store, backend phải kiểm tra:

```sql
SELECT 1
FROM UserStores
WHERE TenantId = @TenantId
  AND UserId = @CurrentUserId
  AND StoreId = @StoreId
  AND IsActive = 1;
```

Nếu không có bản ghi, từ chối thao tác.

## Nhóm sản phẩm

## 11. Categories

### Chức năng

`Categories` lưu danh mục sản phẩm của từng tenant.

### Ý nghĩa nghiệp vụ

Danh mục giúp owner tổ chức sản phẩm theo nhóm.

Ví dụ:

- Đồ uống.
- Thực phẩm.
- Mỹ phẩm.
- Phụ kiện.

### Cột chính

- `Id`: khóa chính danh mục.
- `TenantId`: tenant sở hữu danh mục.
- `Name`: tên danh mục.
- `Description`: mô tả.
- `IsActive`: danh mục còn hoạt động hay không.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.
- `IsDeleted`: xóa mềm.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- Một category có nhiều `Products`.

### Index quan trọng

- `IX_Categories_TenantId`: truy vấn danh mục theo tenant.
- `UX_Categories_TenantId_Name`: tránh trùng tên danh mục trong cùng tenant nếu chưa bị xóa mềm.

### Lưu ý khi code

Khi xóa danh mục đã có sản phẩm, không nên xóa vật lý. Nên dùng `IsDeleted = 1` hoặc không cho xóa nếu vẫn còn sản phẩm active.

## 12. Products

### Chức năng

`Products` lưu danh mục hàng hóa/sản phẩm chung của tenant.

Đây là bảng catalog chính, chưa đại diện cho tồn kho từng cửa hàng.

### Ý nghĩa nghiệp vụ

Một sản phẩm thuộc tenant có thể được bán ở nhiều cửa hàng. Giá mặc định nằm ở `Products.Price`, nhưng nếu từng cửa hàng có giá khác nhau thì dùng `StoreProducts.SellingPrice`.

### Cột chính

- `Id`: khóa chính sản phẩm.
- `TenantId`: tenant sở hữu sản phẩm.
- `CategoryId`: danh mục sản phẩm.
- `Name`: tên sản phẩm.
- `Sku`: mã nội bộ của sản phẩm.
- `Barcode`: mã vạch.
- `Description`: mô tả sản phẩm.
- `Price`: giá bán mặc định.
- `CostPrice`: giá vốn.
- `ImageUrl`: ảnh sản phẩm.
- `IsActive`: sản phẩm còn hoạt động hay không.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.
- `IsDeleted`: xóa mềm.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `CategoryId` liên kết đến `Categories.Id`.
- Một product có thể có nhiều `StoreProducts`.
- Một product có thể có nhiều `Inventories`.
- Một product có thể có nhiều `InventoryTransactions`.
- Một product có thể xuất hiện trong nhiều `OrderItems`.

### Index quan trọng

- `IX_Products_TenantId_Sku`: tìm sản phẩm theo SKU trong tenant.
- `UX_Products_TenantId_Sku`: SKU không trùng trong tenant nếu chưa bị xóa mềm.
- `IX_Products_TenantId_Barcode`: tìm sản phẩm theo barcode trong tenant.
- `UX_Products_TenantId_Barcode`: barcode không trùng trong tenant nếu chưa bị xóa mềm.
- `IX_Products_CategoryId`: lọc sản phẩm theo danh mục.

### Lưu ý khi code

Không nên lấy tồn kho từ `Products`. Tồn kho nằm ở `Inventories`.

Không nên sửa dữ liệu hóa đơn cũ khi tên sản phẩm hoặc giá sản phẩm thay đổi. Vì vậy `OrderItems` có lưu snapshot `ProductName`, `Sku`, `UnitPrice`.

## 13. StoreProducts

### Chức năng

`StoreProducts` lưu cấu hình sản phẩm theo từng cửa hàng.

### Ý nghĩa nghiệp vụ

Bảng này dùng khi mỗi cửa hàng có thể có:

- Giá bán riêng.
- Trạng thái bán riêng.
- Sản phẩm có bán ở cửa hàng này nhưng không bán ở cửa hàng khác.

Ví dụ:

- Sản phẩm A có giá mặc định 100.000.
- Cửa hàng 1 bán 95.000.
- Cửa hàng 2 bán 105.000.

Khi đó `Products.Price` là giá mặc định, còn `StoreProducts.SellingPrice` là giá riêng theo cửa hàng.

### Cột chính

- `Id`: khóa chính.
- `TenantId`: tenant sở hữu dữ liệu.
- `StoreId`: cửa hàng áp dụng cấu hình.
- `ProductId`: sản phẩm được cấu hình.
- `SellingPrice`: giá bán riêng tại cửa hàng.
- `IsAvailable`: sản phẩm có bán tại cửa hàng hay không.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `ProductId` liên kết đến `Products.Id`.

### Index quan trọng

- `UX_StoreProducts_TenantId_StoreId_ProductId`: một sản phẩm chỉ có một cấu hình trong một cửa hàng của một tenant.

### Lưu ý khi code

Khi tạo đơn hàng, giá bán nên lấy theo thứ tự:

1. Nếu có `StoreProducts.SellingPrice`, dùng giá này.
2. Nếu không có, dùng `Products.Price`.

Nếu `IsAvailable = 0`, staff không được bán sản phẩm tại cửa hàng đó.

## Nhóm kho

## 14. Inventories

### Chức năng

`Inventories` lưu số lượng tồn kho hiện tại của từng sản phẩm tại từng cửa hàng.

### Ý nghĩa nghiệp vụ

Đây là bảng đọc nhanh để biết hiện còn bao nhiêu hàng.

Mỗi cặp `TenantId + StoreId + ProductId` chỉ được có một dòng tồn kho.

### Cột chính

- `Id`: khóa chính tồn kho.
- `TenantId`: tenant sở hữu tồn kho.
- `StoreId`: cửa hàng có tồn kho.
- `ProductId`: sản phẩm.
- `Quantity`: số lượng tồn hiện tại.
- `MinQuantity`: ngưỡng cảnh báo tồn kho thấp.
- `UpdatedAt`: thời điểm cập nhật tồn kho.
- `UpdatedBy`: user cập nhật tồn kho.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `ProductId` liên kết đến `Products.Id`.

### Index quan trọng

- `UX_Inventories_TenantId_StoreId_ProductId`: đảm bảo một sản phẩm trong một cửa hàng chỉ có một dòng tồn kho.

### Lưu ý khi code

Không được chỉ cập nhật `Inventories` mà không ghi `InventoryTransactions`.

Mỗi biến động kho nên thực hiện trong một transaction database:

1. Đọc tồn kho hiện tại.
2. Tính `BeforeQuantity`.
3. Tính `AfterQuantity`.
4. Cập nhật `Inventories.Quantity`.
5. Ghi một dòng `InventoryTransactions`.
6. Commit.

Điều này giúp tồn kho hiện tại và lịch sử kho luôn khớp nhau.

## 15. InventoryTransactions

### Chức năng

`InventoryTransactions` lưu lịch sử mọi biến động kho.

### Ý nghĩa nghiệp vụ

Bảng này là sổ cái kho. Nó trả lời các câu hỏi:

- Ai nhập hàng?
- Ai xuất hàng?
- Vì sao tồn kho thay đổi?
- Trước khi thay đổi tồn bao nhiêu?
- Sau khi thay đổi tồn bao nhiêu?
- Biến động này liên quan đến đơn hàng hay phiếu nào?

### Cột chính

- `Id`: khóa chính giao dịch kho.
- `TenantId`: tenant sở hữu giao dịch.
- `StoreId`: cửa hàng phát sinh giao dịch.
- `ProductId`: sản phẩm thay đổi tồn.
- `Type`: loại giao dịch kho.
- `Quantity`: số lượng thay đổi.
- `BeforeQuantity`: tồn kho trước khi thay đổi.
- `AfterQuantity`: tồn kho sau khi thay đổi.
- `Reason`: lý do thay đổi.
- `ReferenceType`: loại chứng từ tham chiếu.
- `ReferenceId`: id chứng từ tham chiếu.
- `CreatedBy`: user tạo giao dịch.
- `CreatedAt`: thời điểm tạo giao dịch.

### Loại giao dịch

- `Import`: nhập kho.
- `Export`: xuất kho.
- `Sale`: trừ kho do bán hàng.
- `Adjust`: điều chỉnh kiểm kê.
- `Return`: hàng trả lại.
- `TransferIn`: nhập chuyển kho.
- `TransferOut`: xuất chuyển kho.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `ProductId` liên kết đến `Products.Id`.
- `CreatedBy` liên kết đến `AspNetUsers.Id`.

### Index quan trọng

- `IX_InventoryTransactions_TenantId_StoreId_ProductId_CreatedAt`: truy vấn lịch sử kho theo tenant, cửa hàng, sản phẩm và thời gian.

### Lưu ý khi code

`Quantity` luôn là số dương. Hướng tăng/giảm được xác định bởi `Type`.

Ví dụ:

- `Import` làm tăng tồn.
- `Sale` làm giảm tồn.
- `Export` làm giảm tồn.
- `Return` thường làm tăng tồn.

`BeforeQuantity` và `AfterQuantity` giúp audit và debug khi có lệch kho.

## Nhóm POS và bán hàng

## 16. Shifts

### Chức năng

`Shifts` lưu ca làm việc tại cửa hàng.

### Ý nghĩa nghiệp vụ

Ca làm giúp quản lý tiền mặt và báo cáo doanh thu theo phiên làm việc.

Một staff mở ca trước khi bán hàng và đóng ca khi kết thúc.

### Cột chính

- `Id`: khóa chính ca làm.
- `TenantId`: tenant sở hữu ca.
- `StoreId`: cửa hàng mở ca.
- `OpenedBy`: user mở ca.
- `OpenedAt`: thời điểm mở ca.
- `ClosedBy`: user đóng ca.
- `ClosedAt`: thời điểm đóng ca.
- `OpeningCash`: tiền mặt đầu ca.
- `ClosingCash`: tiền mặt thực tế cuối ca.
- `ExpectedCash`: tiền mặt hệ thống dự kiến.
- `DifferenceAmount`: chênh lệch tiền mặt.
- `Status`: trạng thái ca.

### Trạng thái ca

- `Open`: đang mở.
- `Closed`: đã đóng.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `OpenedBy` liên kết đến `AspNetUsers.Id`.
- `ClosedBy` liên kết đến `AspNetUsers.Id`.
- Một shift có thể có nhiều `Orders`.

### Index quan trọng

- `IX_Shifts_TenantId_StoreId_OpenedAt`: truy vấn ca theo tenant, cửa hàng và thời gian.

### Lưu ý khi code

Schema hiện tại chưa ép chỉ có một ca đang mở cho một user hoặc một cửa hàng. Quy tắc này nên xử lý ở backend.

Nếu muốn ràng buộc mạnh hơn, có thể thêm filtered unique index cho ca `Open` theo `TenantId`, `StoreId`, `OpenedBy`.

## 17. Orders

### Chức năng

`Orders` lưu đơn hàng POS.

### Ý nghĩa nghiệp vụ

Mỗi lần bán hàng tạo một order. Order chứa thông tin tổng tiền, trạng thái thanh toán, trạng thái đơn và liên kết với staff, cửa hàng, ca làm.

### Cột chính

- `Id`: khóa chính đơn hàng.
- `TenantId`: tenant sở hữu đơn.
- `StoreId`: cửa hàng phát sinh đơn.
- `OrderCode`: mã đơn hàng.
- `StaffUserId`: nhân viên bán hàng.
- `ShiftId`: ca làm phát sinh đơn.
- `SubTotal`: tổng tiền hàng trước giảm giá và thuế.
- `DiscountAmount`: tổng giảm giá.
- `TaxAmount`: tổng thuế.
- `TotalAmount`: tổng tiền cuối cùng.
- `PaymentStatus`: trạng thái thanh toán.
- `OrderStatus`: trạng thái đơn hàng.
- `Note`: ghi chú.
- `CreatedAt`: thời điểm tạo đơn.
- `CreatedBy`: user tạo đơn.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.
- `CancelledAt`: thời điểm hủy.
- `CancelledBy`: user hủy.

### Trạng thái thanh toán

- `Unpaid`: chưa thanh toán.
- `Partial`: thanh toán một phần.
- `Paid`: đã thanh toán đủ.
- `Refunded`: đã hoàn tiền.
- `Cancelled`: thanh toán bị hủy.

### Trạng thái đơn hàng

- `New`: đơn mới.
- `Completed`: hoàn tất.
- `Cancelled`: đã hủy.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `StaffUserId` liên kết đến `AspNetUsers.Id`.
- `ShiftId` liên kết đến `Shifts.Id`.
- Một order có nhiều `OrderItems`.
- Một order có nhiều `Payments`.

### Index quan trọng

- `IX_Orders_TenantId_StoreId_CreatedAt`: báo cáo đơn hàng theo cửa hàng và thời gian.
- `UX_Orders_TenantId_OrderCode`: mã đơn không trùng trong cùng tenant.

### Lưu ý khi code

Khi tạo đơn hàng, nên thực hiện trong một database transaction:

1. Tạo `Orders`.
2. Tạo các dòng `OrderItems`.
3. Tạo `Payments` nếu thanh toán ngay.
4. Trừ tồn kho trong `Inventories`.
5. Ghi `InventoryTransactions` với type `Sale`.
6. Ghi `AuditLogs`.
7. Commit.

Không nên cho sửa trực tiếp `TotalAmount` từ client. Backend phải tự tính từ `OrderItems`, giảm giá và thuế.

## 18. OrderItems

### Chức năng

`OrderItems` lưu chi tiết từng dòng sản phẩm trong đơn hàng.

### Ý nghĩa nghiệp vụ

Một order có thể có nhiều sản phẩm. Mỗi sản phẩm trong order là một dòng `OrderItems`.

Bảng này lưu snapshot thông tin sản phẩm tại thời điểm bán.

### Cột chính

- `Id`: khóa chính dòng đơn.
- `TenantId`: tenant sở hữu dữ liệu.
- `OrderId`: đơn hàng cha.
- `ProductId`: sản phẩm được bán.
- `ProductName`: tên sản phẩm tại thời điểm bán.
- `Sku`: SKU tại thời điểm bán.
- `Quantity`: số lượng bán.
- `UnitPrice`: đơn giá tại thời điểm bán.
- `DiscountAmount`: giảm giá trên dòng.
- `LineTotal`: thành tiền dòng.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `OrderId` liên kết đến `Orders.Id`.
- `ProductId` liên kết đến `Products.Id`.

### Index quan trọng

- `IX_OrderItems_OrderId`: truy vấn chi tiết đơn hàng.
- `IX_OrderItems_ProductId`: báo cáo sản phẩm đã bán.

### Lưu ý khi code

Phải lưu `ProductName`, `Sku`, `UnitPrice` trực tiếp trong `OrderItems`.

Lý do: sản phẩm có thể đổi tên hoặc đổi giá sau này, nhưng hóa đơn cũ phải giữ đúng dữ liệu tại thời điểm bán.

`LineTotal` nên được backend tính:

```text
LineTotal = Quantity * UnitPrice - DiscountAmount
```

## 19. Payments

### Chức năng

`Payments` lưu thanh toán của đơn hàng POS.

### Ý nghĩa nghiệp vụ

Một order có thể có một hoặc nhiều payment. Điều này hỗ trợ các tình huống:

- Thanh toán tiền mặt.
- Thanh toán chuyển khoản.
- Thanh toán thẻ.
- Thanh toán một phần bằng tiền mặt, một phần bằng chuyển khoản.

### Cột chính

- `Id`: khóa chính thanh toán.
- `TenantId`: tenant sở hữu thanh toán.
- `OrderId`: đơn hàng được thanh toán.
- `Method`: phương thức thanh toán.
- `Amount`: số tiền thanh toán.
- `TransactionCode`: mã giao dịch từ ngân hàng, ví điện tử hoặc cổng thanh toán.
- `PaidAt`: thời điểm thanh toán thành công.
- `Status`: trạng thái thanh toán.
- `CreatedAt`: thời điểm tạo bản ghi.

### Phương thức thanh toán

- `Cash`: tiền mặt.
- `BankTransfer`: chuyển khoản.
- `Card`: thẻ.
- `Momo`: ví Momo.
- `ZaloPay`: ví ZaloPay.
- `Other`: phương thức khác.

### Trạng thái thanh toán

- `Pending`: đang chờ.
- `Paid`: đã thanh toán.
- `Failed`: thất bại.
- `Refunded`: đã hoàn tiền.
- `Cancelled`: đã hủy.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `OrderId` liên kết đến `Orders.Id`.

### Index quan trọng

- `IX_Payments_OrderId`: lấy danh sách thanh toán của một đơn.

### Lưu ý khi code

`Orders.PaymentStatus` nên được tính dựa trên tổng `Payments` thành công.

Ví dụ:

- Tổng payment paid = 0: `Unpaid`.
- Tổng payment paid nhỏ hơn total: `Partial`.
- Tổng payment paid bằng hoặc lớn hơn total: `Paid`.

## Nhóm subscription SaaS

## 20. SubscriptionPlans

### Chức năng

`SubscriptionPlans` lưu các gói dịch vụ SaaS.

### Ý nghĩa nghiệp vụ

Đây là bảng do `ADMIN` quản lý. Owner chọn hoặc được gán một plan để sử dụng hệ thống.

Ví dụ plan:

- Free Trial.
- Basic.
- Pro.
- Enterprise.

### Cột chính

- `Id`: khóa chính gói.
- `Name`: tên gói.
- `Price`: giá gói.
- `BillingCycle`: chu kỳ thanh toán.
- `MaxStores`: số cửa hàng tối đa.
- `MaxStaff`: số nhân viên tối đa.
- `MaxProducts`: số sản phẩm tối đa.
- `IsActive`: gói còn bán hay không.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.
- `IsDeleted`: xóa mềm.

### Chu kỳ thanh toán

- `Monthly`: theo tháng.
- `Quarterly`: theo quý.
- `Yearly`: theo năm.

### Quan hệ

- Một plan có nhiều `TenantSubscriptions`.

### Lưu ý khi code

Không nên xóa vật lý plan đã từng được tenant sử dụng. Nên dùng `IsDeleted = 1` hoặc `IsActive = 0`.

Các giới hạn như `MaxStores`, `MaxStaff`, `MaxProducts` nên được kiểm tra ở backend khi owner tạo cửa hàng, tạo staff hoặc tạo sản phẩm.

## 21. TenantSubscriptions

### Chức năng

`TenantSubscriptions` lưu lịch sử gói dịch vụ của từng tenant.

### Ý nghĩa nghiệp vụ

Bảng này cho biết tenant đang dùng gói nào, từ ngày nào đến ngày nào, trạng thái ra sao và có tự gia hạn hay không.

Một tenant có thể có nhiều bản ghi subscription qua thời gian.

### Cột chính

- `Id`: khóa chính subscription.
- `TenantId`: tenant sử dụng gói.
- `PlanId`: gói dịch vụ.
- `StartDate`: ngày bắt đầu.
- `EndDate`: ngày kết thúc.
- `Status`: trạng thái subscription.
- `AutoRenew`: có tự gia hạn hay không.
- `CreatedAt`: thời điểm tạo.
- `CreatedBy`: user tạo.
- `UpdatedAt`: thời điểm cập nhật.
- `UpdatedBy`: user cập nhật.

### Trạng thái subscription

- `Active`: đang hoạt động.
- `Trial`: dùng thử.
- `Expired`: hết hạn.
- `Cancelled`: đã hủy.
- `Suspended`: bị tạm ngưng.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `PlanId` liên kết đến `SubscriptionPlans.Id`.
- Một tenant subscription có nhiều `SystemPayments`.

### Index quan trọng

- `IX_TenantSubscriptions_TenantId_Status`: tìm subscription hiện tại của tenant theo trạng thái.

### Lưu ý khi code

Schema hiện tại chưa ép một tenant chỉ có một subscription `Active` tại một thời điểm. Quy tắc này nên được kiểm tra ở service layer.

Khi subscription hết hạn, hệ thống có thể:

- Chặn owner tạo thêm dữ liệu.
- Giới hạn tính năng.
- Chuyển tenant sang trạng thái `Suspended`.

## 22. SystemPayments

### Chức năng

`SystemPayments` lưu các khoản thanh toán subscription từ tenant cho nền tảng SaaS.

### Ý nghĩa nghiệp vụ

Đây là dữ liệu doanh thu hệ thống, phục vụ cho `ADMIN`.

Khác với `Payments`, bảng này không liên quan đến đơn hàng POS của cửa hàng. Nó liên quan đến tiền owner trả cho nền tảng SaaS.

### Cột chính

- `Id`: khóa chính payment hệ thống.
- `TenantId`: tenant thanh toán.
- `SubscriptionId`: subscription được thanh toán.
- `Amount`: số tiền thanh toán.
- `Method`: phương thức thanh toán.
- `Status`: trạng thái thanh toán.
- `TransactionCode`: mã chuyển khoản SePay duy nhất dùng để match webhook.
- `ProviderTransactionId`: mã tham chiếu giao dịch từ SePay/ngân hàng.
- `BankCode`, `BankAccountNo`, `BankAccountName`: thông tin tài khoản nhận tiền.
- `QrContent`: URL ảnh QR SePay.
- `TransferContent`: nội dung chuyển khoản bắt buộc.
- `PaidAt`: thời điểm thanh toán thành công.
- `PaidAmount`: số tiền thực nhận từ webhook.
- `RawResponse`: JSON SePay raw response hoặc raw webhook gần nhất.
- `ExpiredAt`: thời điểm hết hạn mã QR.
- `InvoiceUrl`: đường dẫn hóa đơn subscription.
- `CreatedAt`: thời điểm tạo.
- `UpdatedAt`: thời điểm cập nhật gần nhất.

### Phương thức thanh toán

- `Cash`: tiền mặt.
- `BankTransfer`: chuyển khoản.
- `SePay`: thanh toán online bằng QR/webhook SePay.
- `Card`: thẻ.
- `Momo`: ví Momo.
- `ZaloPay`: ví ZaloPay.
- `Other`: phương thức khác.

### Trạng thái thanh toán

- `Pending`: đang chờ.
- `Paid`: đã thanh toán.
- `Failed`: thất bại.
- `Refunded`: đã hoàn tiền.
- `Cancelled`: đã hủy.

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `SubscriptionId` liên kết đến `TenantSubscriptions.Id`.

### Index quan trọng

- `IX_SystemPayments_TenantId_PaidAt`: báo cáo doanh thu hệ thống theo tenant và thời gian.
- `IX_SystemPayments_TransactionCode`: đảm bảo mã chuyển khoản SePay là duy nhất.

### Lưu ý khi code

Không nhầm `SystemPayments` với `Payments`:

- `Payments`: khách hàng thanh toán đơn hàng tại cửa hàng.
- `SystemPayments`: owner thanh toán tiền dùng SaaS.

## Nhóm audit

## 23. AuditLogs

### Chức năng

`AuditLogs` lưu lịch sử hành động quan trọng trong hệ thống.

### Ý nghĩa nghiệp vụ

Audit log giúp trả lời:

- Ai đã làm?
- Làm vào lúc nào?
- Làm trên dữ liệu nào?
- Dữ liệu trước và sau thay đổi ra sao?
- Hành động đến từ IP nào và trình duyệt nào?

### Cột chính

- `Id`: khóa chính tự tăng.
- `TenantId`: tenant liên quan đến hành động.
- `StoreId`: cửa hàng liên quan đến hành động.
- `UserId`: user thực hiện hành động.
- `Action`: tên hành động.
- `EntityName`: tên bảng hoặc entity bị tác động.
- `EntityId`: id bản ghi bị tác động.
- `OldValue`: dữ liệu cũ, thường lưu dạng JSON.
- `NewValue`: dữ liệu mới, thường lưu dạng JSON.
- `IpAddress`: địa chỉ IP.
- `UserAgent`: thông tin trình duyệt/thiết bị.
- `CreatedAt`: thời điểm ghi log.

### Hành động nên ghi log

- `Login`
- `Logout`
- `CreateUser`
- `LockUser`
- `CreateStore`
- `UpdateProduct`
- `DeleteProduct`
- `ImportStock`
- `ExportStock`
- `AdjustStock`
- `CreateOrder`
- `CancelOrder`
- `ChangeSubscription`

### Quan hệ

- `TenantId` liên kết đến `Tenants.Id`.
- `StoreId` liên kết đến `Stores.Id`.
- `UserId` liên kết đến `AspNetUsers.Id`.

### Index quan trọng

- `IX_AuditLogs_TenantId_UserId_CreatedAt`: xem lịch sử thao tác của user trong tenant.
- `IX_AuditLogs_TenantId_StoreId_CreatedAt`: xem lịch sử thao tác theo cửa hàng.

### Lưu ý khi code

Không cần ghi log cho mọi thao tác đọc dữ liệu vì sẽ rất nặng. Nên ghi các hành động làm thay đổi dữ liệu hoặc ảnh hưởng bảo mật.

`OldValue` và `NewValue` nên lưu JSON ngắn gọn, tránh lưu dữ liệu quá lớn.

Ví dụ:

```json
{
  "Name": "Cafe Sua Da",
  "Price": 25000
}
```

## Các view báo cáo hỗ trợ

Các object dưới đây không phải table, nhưng được tạo trong schema để hỗ trợ dashboard và báo cáo.

## vw_DailySalesReport

### Chức năng

Tổng hợp doanh thu theo ngày, tenant và cửa hàng.

### Dữ liệu trả về

- `TenantId`
- `StoreId`
- `ReportDate`
- `OrderCount`
- `SubTotal`
- `DiscountAmount`
- `TaxAmount`
- `TotalAmount`

### Dùng cho

- Dashboard doanh thu ngày.
- Báo cáo doanh thu theo cửa hàng.
- Biểu đồ doanh thu theo thời gian.

## vw_StaffSalesReport

### Chức năng

Tổng hợp doanh thu theo nhân viên, ngày, tenant và cửa hàng.

### Dữ liệu trả về

- `TenantId`
- `StoreId`
- `StaffUserId`
- `ReportDate`
- `OrderCount`
- `TotalSales`

### Dùng cho

- Báo cáo hiệu suất nhân viên.
- So sánh doanh thu theo staff.
- Kiểm tra doanh thu theo ca hoặc theo ngày.

## vw_InventoryStatusReport

### Chức năng

Tổng hợp trạng thái tồn kho hiện tại.

### Dữ liệu trả về

- `TenantId`
- `StoreId`
- `ProductId`
- `ProductName`
- `Sku`
- `Barcode`
- `Quantity`
- `MinQuantity`
- `IsLowStock`
- `UpdatedAt`

### Dùng cho

- Dashboard tồn kho.
- Cảnh báo hàng sắp hết.
- Danh sách sản phẩm cần nhập thêm.

## vw_SystemRevenueReport

### Chức năng

Tổng hợp doanh thu SaaS từ subscription payment.

### Dữ liệu trả về

- `TenantId`
- `PaidDate`
- `PaymentCount`
- `TotalAmount`

### Dùng cho

- Dashboard `ADMIN`.
- Báo cáo doanh thu nền tảng.
- Theo dõi tenant đã thanh toán.

## Luồng dữ liệu nghiệp vụ chính

## Luồng tạo owner và tenant

1. `ADMIN` tạo user trong `AspNetUsers`.
2. Gán role `OWNER` qua `AspNetUserRoles`.
3. Tạo tenant trong `Tenants`.
4. Gán `Tenants.OwnerUserId` bằng user owner.
5. Cập nhật `AspNetUsers.TenantId` cho owner.
6. Ghi `AuditLogs` với action `CreateUser` hoặc `CreateTenant`.

## Luồng owner tạo staff

1. `OWNER` tạo user trong `AspNetUsers`.
2. User mới có `TenantId` giống owner.
3. Gán role `STAFF` qua `AspNetUserRoles`.
4. Gán staff vào cửa hàng qua `UserStores`.
5. Ghi `AuditLogs` với action `CreateUser`.

## Luồng tạo cửa hàng

1. `OWNER` tạo bản ghi `Stores`.
2. Hệ thống kiểm tra `Code` không trùng trong tenant.
3. Nếu tenant có giới hạn `MaxStores`, kiểm tra gói subscription.
4. Ghi `AuditLogs` với action `CreateStore`.

## Luồng nhập kho

1. User chọn `StoreId` và `ProductId`.
2. Backend kiểm tra quyền truy cập store.
3. Đọc hoặc tạo dòng `Inventories`.
4. Cập nhật `Quantity`.
5. Ghi `InventoryTransactions` với type `Import`.
6. Ghi `AuditLogs` với action `ImportStock`.

## Luồng bán hàng POS

1. Staff đăng nhập.
2. Staff chọn cửa hàng được gán trong `UserStores`.
3. Staff mở ca trong `Shifts`.
4. Staff tạo order trong `Orders`.
5. Backend tạo các dòng `OrderItems`.
6. Backend tạo `Payments` nếu có thanh toán.
7. Backend trừ kho trong `Inventories`.
8. Backend ghi `InventoryTransactions` với type `Sale`.
9. Backend cập nhật trạng thái thanh toán của order.
10. Backend ghi `AuditLogs` với action `CreateOrder`.

## Luồng hủy đơn

1. User có quyền hủy đơn.
2. Backend cập nhật `Orders.OrderStatus = 'Cancelled'`.
3. Backend cập nhật `CancelledAt` và `CancelledBy`.
4. Nếu đơn đã trừ kho, backend hoàn lại kho bằng `InventoryTransactions` type `Return` hoặc `Adjust`.
5. Backend xử lý hoàn tiền trong `Payments` nếu cần.
6. Backend ghi `AuditLogs` với action `CancelOrder`.

## Luồng subscription

1. `ADMIN` tạo plan trong `SubscriptionPlans`.
2. Tenant được gán plan qua `TenantSubscriptions`.
3. Khi owner thanh toán, tạo `SystemPayments`.
4. Nếu thanh toán thành công, cập nhật `SystemPayments.Status = 'Paid'`.
5. Dashboard admin lấy doanh thu từ `vw_SystemRevenueReport`.

## Phân chia theo giai đoạn MVP

## Giai đoạn 1: Core System

Bảng cần dùng trước:

- `AspNetUsers`
- `AspNetRoles`
- `AspNetUserRoles`
- `Tenants`
- `Stores`
- `UserStores`
- `AuditLogs`

Mục tiêu:

- Đăng nhập/đăng xuất.
- Role `ADMIN`, `OWNER`, `STAFF`.
- Admin quản lý owner.
- Owner tạo staff.
- Owner tạo store.
- Owner gán staff vào store.
- Dashboard cơ bản.

## Giai đoạn 2: POS, Product, Inventory

Bảng cần dùng:

- `Categories`
- `Products`
- `StoreProducts`
- `Inventories`
- `InventoryTransactions`
- `Shifts`
- `Orders`
- `OrderItems`
- `Payments`

Mục tiêu:

- Quản lý sản phẩm.
- Quản lý tồn kho.
- Nhập/xuất/điều chỉnh kho.
- Bán hàng POS.
- Thanh toán.
- Lịch sử đơn hàng.
- Báo cáo tồn kho và doanh thu.

## Giai đoạn 3: Subscription và Reporting SaaS

Bảng cần dùng:

- `SubscriptionPlans`
- `TenantSubscriptions`
- `SystemPayments`
- Các view báo cáo.

Mục tiêu:

- Quản lý gói dịch vụ.
- Thanh toán subscription.
- Báo cáo doanh thu nền tảng.
- Giới hạn tính năng theo gói.
