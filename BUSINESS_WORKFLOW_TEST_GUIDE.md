# ChainPOS - Tài liệu nghiệp vụ và hướng dẫn test trực quan

Cập nhật: 2026-05-29

Tài liệu này mô tả hệ thống ChainPOS theo góc nhìn nghiệp vụ: hệ thống có những chức năng gì, mỗi vai trò dùng để làm gì, và nên test trực quan như thế nào để thấy được dữ liệu thay đổi trên màn hình.

> Lưu ý về "realtime": ChainPOS đã có SignalR cho các sự kiện nghiệp vụ chính. Khi nhập/xuất/điều chỉnh kho, POS checkout, cancel order, open/close shift, subscription/payment thay đổi, các client đang online sẽ nhận toast/thông báo live. POS và Inventory có thể cập nhật stock đang hiện trên màn hình ngay lập tức. Một số danh sách server-rendered như Orders/Payments sẽ hiện banner live và nút Reload để tải lại danh sách theo filter hiện tại.

## 1. Tổng quan hệ thống

ChainPOS là hệ thống quản lý chuỗi cửa hàng và bán hàng POS theo mô hình SaaS.

Một platform có nhiều tenant. Mỗi tenant đại diện cho một chuỗi cửa hàng của một owner. Trong tenant có các store, staff, danh mục, sản phẩm, tồn kho, ca bán hàng, đơn hàng POS, báo cáo, gói subscription và audit log.

Hệ thống có 3 vai trò chính:

- `ADMIN`: quản lý toàn bộ platform SaaS.
- `OWNER`: quản lý dữ liệu trong tenant của mình.
- `STAFF`: thao tác bán hàng/kho trong những store được owner gán quyền.

Địa chỉ chạy local mặc định:

```text
http://localhost:5292
```

Tài khoản demo:

| Vai trò | Email | Mật khẩu |
| --- | --- | --- |
| Admin | `admin@chainpos.local` | `Admin@123` |
| Owner demo | `owner@demo.local` | `Owner@123` |
| Staff demo | `staff01@demo.local` | `Staff@123` |

Lệnh chạy:

```powershell
cd D:\laptrinhweb\code_outsrc\Dam_Van_Bao\ChainPOS\ChainPOS
dotnet build .\ChainPOS.sln
dotnet run --project .\ChainPOS\ChainPOS.csproj --launch-profile http
```

Lệnh test tự động:

```powershell
dotnet test .\ChainPOS.sln
```

## 2. Các khái niệm nghiệp vụ chính

### 2.1. Platform, tenant và owner

`ADMIN` là người vận hành platform. Admin tạo owner, tạo tenant, quản lý trạng thái tenant và quản lý gói subscription.

`Tenant` là một đơn vị kinh doanh riêng, ví dụ một chuỗi cửa hàng. Toàn bộ store, staff, product, inventory, order của tenant này phải tách biệt với tenant khác.

`OWNER` là chủ tài khoản của tenant. Owner chỉ xem và quản lý dữ liệu của tenant mình.

Test cần nhìn thấy:

- Admin xem được danh sách owner/tenant của toàn platform.
- Owner không vào được khu vực Admin.
- Owner chỉ thấy store, staff, product, inventory, order của tenant mình.

### 2.2. Store và store access

Store là cửa hàng/chi nhánh trong tenant.

Owner có quyền thao tác tất cả store active trong tenant.

Staff chỉ thao tác store mà owner gán trong bảng `UserStores` và bản ghi đó phải `IsActive = true`.

Test cần nhìn thấy:

- Owner tạo store mới.
- Owner gán staff vào store.
- Staff đăng nhập chỉ thấy store được gán.
- Staff không thao tác được store chưa được gán hoặc đã bị tắt quyền.

### 2.3. Catalog: category, product, store product

`Category` là danh mục sản phẩm.

`Product` là sản phẩm chung trong tenant. Product có SKU, barcode, giá gốc, giá vốn, ảnh, trạng thái active/inactive.

`StoreProduct` là việc bật sản phẩm bán tại từng store. Cùng một product có thể được bán ở store A nhưng không bán ở store B. StoreProduct có `SellingPrice` riêng. Nếu `SellingPrice` rỗng, POS dùng `Products.Price`.

Test cần nhìn thấy:

- Product inactive hoặc deleted không hiện ở POS.
- StoreProduct `IsAvailable = false` không hiện ở POS.
- Khi set SellingPrice riêng, POS hiện giá riêng đó.
- Khi xóa SellingPrice, POS fallback về Product Price.

### 2.4. Inventory và inventory transaction

Inventory lưu tồn kho theo tenant, store, product.

Mỗi biến động kho phải ghi `InventoryTransactions`:

- `Import`: nhập kho.
- `Export`: xuất kho thủ công.
- `Adjust`: kiểm kho/điều chỉnh tồn.
- `Sale`: POS bán hàng trừ kho.
- `Return`: hủy đơn hoàn kho.

Test cần nhìn thấy:

- Nhập kho tăng số lượng.
- Xuất kho giảm số lượng.
- Điều chỉnh kho set lại số lượng thực tế.
- POS checkout trừ kho.
- Cancel order hoàn kho.
- Các thao tác quan trọng có audit log.

### 2.5. Shift và POS

Shift là ca bán hàng. Owner/Staff phải mở ca trước khi checkout POS.

Luôn có rule:

- Một user không được mở nhiều ca `Open` cùng lúc.
- Checkout phải có ca `Open` tại store đang bán.
- Đóng ca tính:
  - `ExpectedCash = OpeningCash + tổng payment cash trong ca`
  - `DifferenceAmount = ClosingCash - ExpectedCash`

POS là màn hình bán hàng:

- Chọn store.
- Search product theo tên, SKU, barcode.
- Thêm product vào giỏ.
- Tăng/giảm số lượng.
- Chọn payment method.
- Nếu cash thì nhập tiền khách đưa.
- Checkout tạo order, order items, payment, trừ kho và redirect sang receipt.

### 2.6. Orders, receipt và cancel

Order là đơn POS đã checkout.

Receipt là màn chi tiết/in hóa đơn.

Cancel order:

- Đổi `OrderStatus = Cancelled`.
- Đổi `PaymentStatus = Cancelled`.
- Cập nhật payment về `Cancelled`.
- Hoàn kho bằng transaction `Return`.
- Ghi audit `CancelOrder`.

### 2.7. Subscription và billing SaaS

Subscription Plan quy định giới hạn tenant:

- `MaxStores`
- `MaxStaff`
- `MaxProducts`
- `Price`
- `BillingCycle`

TenantSubscription là lịch sử gói của tenant.

SystemPayment là thanh toán SaaS của tenant cho platform, khác với Payment POS.

Test cần nhìn thấy:

- Admin tạo/sửa/kích hoạt/tắt plan.
- Admin không xóa vật lý plan đã có tenant dùng; nên deactivate.
- Admin gán plan cho tenant.
- Owner xem subscription hiện tại.
- Owner xem lịch sử system payment.
- Admin mark system payment `Paid` hoặc `Failed`.

### 2.8. Reports và audit log

Reports hiện có:

- Daily sales report.
- Staff sales report.
- Inventory status report.
- System revenue report cho Admin.

Audit log ghi các thao tác quan trọng:

- Login/logout.
- Tạo/sửa/khóa user.
- Tạo/sửa store, staff, category, product.
- Nhập/xuất/điều chỉnh kho.
- Mở/đóng ca.
- Tạo/hủy order.
- Đổi subscription.

Admin xem toàn bộ audit log. Owner chỉ xem audit log trong tenant mình.

## 3. Bản đồ menu và URL chính

### 3.1. Authentication

| Chức năng | URL |
| --- | --- |
| Login | `/login` |
| Logout | POST `/logout` |
| Access denied | `/access-denied` |

### 3.2. Admin

| Chức năng | URL |
| --- | --- |
| Dashboard | `/admin/dashboard` |
| Owners | `/admin/owners` |
| Tenants | `/admin/tenants` |
| Subscription plans | `/admin/subscriptionplans` |
| Gán subscription cho tenant | `/admin/subscriptions/create` |
| System payments | `/admin/systempayments` |
| Reports | `/admin/reports` |
| Audit logs | `/admin/auditlogs` |

### 3.3. Owner

| Chức năng | URL |
| --- | --- |
| Dashboard | `/owner/dashboard` |
| Stores | `/owner/stores` |
| Staff | `/owner/staff` |
| Categories | `/owner/categories` |
| Products | `/owner/products` |
| Store products | `/owner/storeproducts` |
| Inventory | `/owner/inventory` |
| Shifts | `/owner/shifts` |
| POS | `/owner/pos` |
| Orders | `/owner/orders` |
| Reports | `/owner/reports` |
| Subscription | `/owner/subscription` |
| Audit logs | `/owner/auditlogs` |

### 3.4. Staff

| Chức năng | URL |
| --- | --- |
| Dashboard | `/staff/dashboard` |
| Inventory | `/staff/inventory` |
| Shifts | `/staff/shifts` |
| POS | `/staff/pos` |
| Orders | `/staff/orders` |

## 4. Luồng nghiệp vụ theo vai trò

### 4.1. Admin vận hành platform

Mục tiêu: tạo tenant mới, quản lý trạng thái tenant, gói subscription và thanh toán SaaS.

Luồng cơ bản:

1. Đăng nhập bằng `admin@chainpos.local`.
2. Vào `/admin/owners`.
3. Tạo owner mới.
4. Hệ thống tạo owner user, gán role `OWNER`, tạo tenant và gán owner vào tenant.
5. Vào `/admin/tenants` để xem tenant mới.
6. Vào detail tenant để suspend/activate/cancel nếu cần.
7. Vào `/admin/subscriptionplans` để tạo hoặc sửa plan.
8. Vào `/admin/subscriptions/create` để gán plan cho tenant.
9. Vào `/admin/systempayments` để theo dõi thanh toán SaaS.
10. Vào `/admin/auditlogs` để kiểm tra thao tác đã ghi audit.

Kết quả mong đợi:

- Owner mới login được nếu dùng đúng password.
- Tenant mới có owner.
- Subscription mới xuất hiện ở Owner subscription.
- Payment SaaS có thể mark paid/failed.
- Audit log có action liên quan.

### 4.2. Owner chuẩn bị cửa hàng để bán POS

Mục tiêu: tạo store, tạo staff, tạo product, gán product vào store, nhập kho.

Luồng cơ bản:

1. Đăng nhập bằng `owner@demo.local`.
2. Vào `/owner/stores`, tạo store mới hoặc dùng store demo `TZ-HCM-01`.
3. Vào `/owner/staff`, tạo staff hoặc dùng `staff01@demo.local`.
4. Gán staff vào store.
5. Vào `/owner/categories`, tạo category nếu cần.
6. Vào `/owner/products`, tạo product mới.
7. Vào `/owner/storeproducts`, gán product vào store, bật `IsAvailable`, set `SellingPrice` nếu muốn.
8. Vào `/owner/inventory/import`, nhập kho cho product ở store.
9. Vào `/owner/pos`, chọn store, search product để kiểm tra sản phẩm đã hiện.

Kết quả mong đợi:

- Store hiện trong danh sách.
- Staff thuộc tenant hiện trong danh sách.
- Staff login và chỉ thấy store được gán.
- Product hiện trong POS nếu active, available và có store product.
- Tồn kho hiện đúng sau khi import.

### 4.3. Staff bán hàng tại quầy POS

Mục tiêu: mở ca, checkout, in receipt, đóng ca.

Luồng cơ bản:

1. Đăng nhập bằng `staff01@demo.local`.
2. Vào `/staff/shifts`.
3. Mở ca cho store được gán, nhập `OpeningCash`.
4. Vào `/staff/pos`.
5. Chọn store của ca đang mở.
6. Search product.
7. Thêm product vào cart.
8. Tăng/giảm số lượng.
9. Chọn payment method.
10. Nếu cash, nhập `CustomerPaidAmount` lớn hơn hoặc bằng total.
11. Checkout.
12. Hệ thống redirect sang receipt/order detail.
13. Vào `/staff/orders` để thấy order mới.
14. Vào `/staff/shifts`, đóng ca, nhập `ClosingCash`.

Kết quả mong đợi:

- Nếu chưa mở ca, checkout bị chặn.
- Checkout thành công tạo order, order item, payment.
- Inventory giảm theo số lượng đã bán.
- Receipt hiện đúng sản phẩm, số lượng, giá, total.
- Đóng ca tính đúng cash expected và difference.

## 5. Hướng dẫn test trực quan chi tiết

### 5.1. Test login và phân quyền

Mục tiêu: xác nhận 3 vai trò vào đúng khu vực, sai role bị chặn.

Bước test:

1. Mở `http://localhost:5292/login`.
2. Đăng nhập Admin.
3. Xác nhận redirect về `/admin/dashboard`.
4. Thử mở `/owner/products`.
5. Kết quả mong đợi: Admin không dùng khu vực Owner/Staff nghiệp vụ, nếu bị chặn thì đúng.
6. Logout.
7. Đăng nhập Owner.
8. Xác nhận redirect về `/owner/dashboard`.
9. Thử mở `/admin/owners`.
10. Kết quả mong đợi: access denied.
11. Logout.
12. Đăng nhập Staff.
13. Xác nhận redirect về `/staff/dashboard`.
14. Thử mở `/owner/products` và `/admin/owners`.
15. Kết quả mong đợi: access denied.

Cần nhìn trên UI:

- Sidebar thay đổi theo role.
- Staff không thấy menu Admin/Owner.
- Owner không thấy menu Admin.

### 5.2. Test Admin tạo owner và tenant

Mục tiêu: admin tạo owner mới, hệ thống tạo tenant riêng.

Bước test:

1. Đăng nhập Admin.
2. Vào `/admin/owners`.
3. Bấm Create/New Owner.
4. Nhập thông tin owner:
   - Full name: `Owner Test Manual`
   - Email: email chưa tồn tại, ví dụ `owner.manual.001@demo.local`
   - Password: dùng format hợp lệ theo form.
   - Tenant name/code nếu form yêu cầu.
5. Submit.
6. Quay lại danh sách owner.
7. Search email vừa tạo.
8. Vào detail owner/tenant.

Kết quả mong đợi:

- Owner mới xuất hiện.
- Owner có role `OWNER`.
- Tenant mới được tạo và gán owner.
- Audit log có `CreateUser`/`CreateTenant`.

Test trực quan tiếp:

1. Logout Admin.
2. Login bằng owner mới.
3. Xác nhận vào `/owner/dashboard`.
4. Owner mới chưa thấy dữ liệu của tenant demo cũ.

### 5.3. Test Admin tenant status

Mục tiêu: tenant suspended/cancelled bị chặn khi owner/staff thao tác nghiệp vụ.

Bước test:

1. Đăng nhập Admin.
2. Vào `/admin/tenants`.
3. Chọn tenant của owner test.
4. Bấm Suspend.
5. Logout Admin.
6. Login owner của tenant đó.
7. Thử vào `/owner/stores`, `/owner/products`, `/owner/pos`.

Kết quả mong đợi:

- Tenant suspended/cancelled không được thao tác module nghiệp vụ.
- Nếu đăng nhập bị chặn từ đầu thì cũng hợp lệ theo rule hiện có.
- Admin activate lại tenant thì owner thao tác lại được.

### 5.4. Test owner tạo store

Mục tiêu: owner quản lý store trong tenant.

Bước test:

1. Đăng nhập Owner.
2. Vào `/owner/stores`.
3. Bấm Create.
4. Nhập:
   - Name: `Manual Test Store`
   - Code: `MTS-001`
   - Address/Phone nếu có.
5. Submit.
6. Search `MTS-001`.
7. Edit store, đổi tên thành `Manual Test Store Updated`.
8. Toggle status inactive/active nếu có nút.

Kết quả mong đợi:

- Store mới hiện trong list.
- Code trùng trong tenant bị chặn.
- Store inactive/closed không dùng được cho POS/kho.
- Audit log có create/update/change status.

### 5.5. Test owner tạo staff và gán store

Mục tiêu: staff chỉ thao tác store được gán.

Bước test:

1. Đăng nhập Owner.
2. Vào `/owner/staff`.
3. Bấm Create.
4. Tạo staff mới:
   - Full name: `Staff Manual Test`
   - Email: `staff.manual.001@demo.local`
   - Password: password hợp lệ.
5. Sau khi tạo, vào màn gán store.
6. Gán staff vào store `Manual Test Store` hoặc `TZ-HCM-01`.
7. Logout Owner.
8. Login staff mới.
9. Vào `/staff/dashboard`, `/staff/inventory`, `/staff/pos`.

Kết quả mong đợi:

- Staff mới login được.
- Staff chỉ thấy store được gán.
- Nếu owner tắt `UserStores.IsActive`, staff không thao tác store đó nữa.
- Audit log có `CreateStaff`, `AssignStaffStore`, `EnableStaffStore`/`DisableStaffStore`.

### 5.6. Test category/product/store product

Mục tiêu: sản phẩm được tạo, gán vào store và hiện trên POS.

Bước test:

1. Đăng nhập Owner.
2. Vào `/owner/categories`.
3. Tạo category `Manual Electronics`.
4. Vào `/owner/products`.
5. Tạo product:
   - Name: `Manual POS Product`
   - SKU: `MANUAL-POS-001`
   - Barcode: `899000000001`
   - Price: `100000`
   - CostPrice: `70000`
   - Category: `Manual Electronics`
   - Upload ảnh JPG/PNG nếu muốn.
6. Vào `/owner/storeproducts`.
7. Gán product vào store `TZ-HCM-01`.
8. Set `IsAvailable = true`.
9. Set `SellingPrice = 95000`.
10. Vào `/owner/pos`, chọn store, search `MANUAL-POS-001`.

Kết quả mong đợi:

- Product hiện trong POS.
- Giá trên POS là `95000`, không phải `100000`.
- Nếu tắt `IsAvailable`, reload POS thì product không hiện.
- Nếu xóa `SellingPrice`, POS fallback về `100000`.

Test validate:

- Tạo product SKU trùng: bị chặn.
- Tạo barcode trùng: bị chặn.
- Price âm: bị chặn.
- Upload file không phải ảnh: bị chặn.
- Upload ảnh > 5MB: bị chặn.

### 5.7. Test nhập kho

Mục tiêu: tồn kho tăng và có transaction `Import`.

Bước test:

1. Đăng nhập Owner hoặc Staff có quyền store.
2. Vào `/owner/inventory` hoặc `/staff/inventory`.
3. Bấm Import.
4. Chọn store `TZ-HCM-01`.
5. Chọn product `Manual POS Product`.
6. Nhập:
   - Quantity: `10`
   - MinQuantity: `2`
   - Reason: `Manual import test`
7. Submit.
8. Quay lại inventory list, filter/search product.

Kết quả mong đợi:

- Quantity tăng lên 10 nếu trước đó chưa có tồn.
- Low stock chỉ bật khi quantity > 0 và <= min quantity.
- Audit log có `ImportStock`.
- Nếu quantity <= 0 thì bị chặn.

### 5.8. Test xuất kho

Mục tiêu: tồn kho giảm và có transaction `Export`.

Bước test:

1. Đảm bảo product có tồn kho >= 5.
2. Vào Inventory.
3. Bấm Export.
4. Chọn store/product.
5. Nhập Quantity `3`, reason `Manual export test`.
6. Submit.

Kết quả mong đợi:

- Quantity giảm đi 3.
- Nếu xuất quá tồn thì bị chặn.
- Audit log có `ExportStock`.

### 5.9. Test điều chỉnh kho

Mục tiêu: cập nhật tồn kho theo số thực tế và có transaction `Adjust`.

Bước test:

1. Vào Inventory.
2. Bấm Adjust.
3. Chọn store/product.
4. Nhập ActualQuantity `7`.
5. Nhập MinQuantity `2`.
6. Reason: `Manual cycle count`.
7. Submit.

Kết quả mong đợi:

- Quantity thành 7.
- Movement quantity trong transaction là chênh lệch giữa trước và sau.
- ActualQuantity < 0 bị chặn.
- Reason rỗng bị chặn.
- Audit log có `AdjustStock`.

### 5.10. Test mở ca

Mục tiêu: user phải mở ca trước khi bán POS.

Bước test:

1. Đăng nhập Staff.
2. Vào `/staff/shifts`.
3. Bấm Open Shift.
4. Chọn store được gán.
5. Nhập OpeningCash `500000`.
6. Submit.
7. Thử bấm Open Shift lần nữa.

Kết quả mong đợi:

- Ca đầu tiên mở thành công, status `Open`.
- Mở ca lần hai khi ca cũ còn open bị chặn.
- Audit log có `OpenShift`.

### 5.11. Test POS checkout thành công

Mục tiêu: đặt hàng tại POS, tạo order, trừ kho, tạo payment, hiện receipt.

Bước test:

1. Đảm bảo đã có ca `Open`.
2. Đảm bảo product có tồn kho.
3. Vào `/staff/pos` hoặc `/owner/pos`.
4. Chọn store của ca đang mở.
5. Search product theo:
   - Tên product.
   - SKU.
   - Barcode.
6. Bấm add vào cart.
7. Tăng số lượng lên `2`.
8. Chọn payment method `Cash`.
9. Nhập CustomerPaidAmount lớn hơn total.
10. Bấm Checkout.

Kết quả mong đợi:

- Hệ thống redirect sang receipt/order detail.
- Receipt hiện:
  - Order code.
  - Store.
  - Staff.
  - Product name.
  - SKU.
  - Quantity.
  - Unit price.
  - Total.
  - Payment method.
- Order status là `Completed`.
- Payment status là `Paid`.
- Inventory giảm đúng số lượng.
- Inventory transaction có type `Sale`.
- Audit log có `CreateOrder`.

### 5.12. Test POS validation

Mục tiêu: backend không tin cart/client.

Case cần test:

1. Chưa mở ca mà checkout:
   - Kết quả: bị chặn với thông báo cần mở ca.
2. Cart rỗng:
   - Kết quả: bị chặn.
3. Quantity <= 0:
   - Kết quả: item không hợp lệ, cart rỗng hoặc bị chặn.
4. Bán quá tồn:
   - Kết quả: bị chặn, không tạo order.
5. Cash nhập tiền khách đưa nhỏ hơn total:
   - Kết quả: bị chặn.
6. Product inactive/unavailable:
   - Kết quả: không hiện trên POS hoặc checkout bị chặn nếu client gửi lên.

### 5.13. Test POS "realtime" bằng 2 tab

Mục tiêu: thấy SignalR đẩy thông báo và cập nhật stock ngay trên các tab đang mở.

Chuẩn bị:

- Mở Tab A: `/staff/pos`.
- Mở Tab B: `/staff/inventory`.
- Mở Tab C: `/staff/orders`.
- Cùng đăng nhập một staff hoặc dùng 2 browser riêng nếu muốn tách session.
- Trên mỗi tab, icon chuông trên topbar là nơi xem các live update đã nhận.

Bước test:

1. Tab B search product sắp bán, ghi lại quantity hiện tại.
2. Tab A checkout product đó quantity `1`.
3. Sau khi checkout, Tab A redirect sang receipt.
4. Quan sát Tab B mà không reload.
5. Quan sát Tab C mà không reload.

Kết quả mong đợi:

- Tab B hiện toast live và dòng inventory đang hiện giảm 1, row được highlight.
- Tab A/POS product card cập nhật stock nếu vẫn đang ở POS.
- Tab C nhận toast `Order created` và hiện banner yêu cầu Reload để tải order mới vào danh sách theo filter hiện tại.
- Receipt trên Tab A hiện đơn vừa tạo.
- Nếu vào Audit Logs bằng Owner/Admin và reload, thấy `CreateOrder`.

Kết luận về realtime hiện tại:

- Hệ thống đã có SignalR hub `/hubs/chainpos`.
- Stock trên POS/Inventory cập nhật live khi item đang nằm trong màn hình hiện tại.
- Orders/Payments/Subscription/Shifts có live toast và reload banner; một số row có sẵn sẽ được cập nhật status trực tiếp.
- Nếu muốn order mới tự động chèn vào table không cần reload, cần bổ sung partial row rendering/API HTML row ở bước sau.

### 5.14. Test cancel order và hoàn kho

Mục tiêu: hủy đơn hoàn kho đúng.

Bước test:

1. Sau khi checkout thành công, vào receipt/order detail.
2. Ghi lại product và quantity.
3. Vào Inventory, ghi lại quantity sau bán.
4. Quay lại order detail.
5. Bấm Cancel Order.
6. Xác nhận confirm modal.
7. Quay lại Inventory, reload product.

Kết quả mong đợi:

- OrderStatus = `Cancelled`.
- PaymentStatus = `Cancelled`.
- Payment status = `Cancelled`.
- Inventory tăng lại đúng quantity đã bán.
- Inventory transaction có type `Return`.
- Audit log có `CancelOrder`.
- Cancel order lần nữa bị chặn.

### 5.15. Test đóng ca

Mục tiêu: hệ thống tính tiền mặt dự kiến và chênh lệch.

Bước test:

1. Mở ca với OpeningCash `500000`.
2. Checkout 2 đơn cash:
   - Đơn 1 total `100000`.
   - Đơn 2 total `150000`.
3. Vào `/staff/shifts`.
4. Bấm Close Shift.
5. Màn close shift hiện ExpectedCash = `750000`.
6. Nhập ClosingCash `760000`.
7. Submit.

Kết quả mong đợi:

- Shift status = `Closed`.
- ExpectedCash = `500000 + 100000 + 150000 = 750000`.
- DifferenceAmount = `760000 - 750000 = 10000`.
- Audit log có `CloseShift`.
- Sau khi đóng ca, user cần mở ca mới nếu muốn checkout tiếp.

### 5.16. Test order list, filter và receipt

Mục tiêu: order có thể tìm, lọc, xem chi tiết và in receipt.

Bước test:

1. Vào `/staff/orders` hoặc `/owner/orders`.
2. Search theo order code vừa tạo.
3. Filter theo store.
4. Filter theo status `Completed`.
5. Filter theo payment status `Paid`.
6. Filter theo ngày bán.
7. Vào detail.
8. Bấm print receipt nếu có.

Kết quả mong đợi:

- Filter trả đúng đơn.
- Owner thấy order trong tenant.
- Staff chỉ thấy order trong store được gán.
- Receipt hiện đủ thông tin.

### 5.17. Test reports

Mục tiêu: báo cáo đọc đúng report views và phân quyền đúng.

Admin:

1. Đăng nhập Admin.
2. Vào `/admin/reports`.
3. Xem các block:
   - Daily sales.
   - Staff sales.
   - Inventory status.
   - System revenue.
4. Filter thời gian/tenant/store nếu UI có.

Owner:

1. Đăng nhập Owner.
2. Vào `/owner/reports`.
3. Xem daily sales, staff sales, inventory status.
4. Xác nhận không thấy System Revenue Report.

Kết quả mong đợi:

- Admin thấy báo cáo toàn platform.
- Owner chỉ thấy dữ liệu tenant mình.
- Order vừa checkout có thể phản ánh trong report sau khi reload nếu report view tính theo database.

### 5.18. Test subscription plan

Mục tiêu: admin quản lý gói SaaS.

Bước test:

1. Đăng nhập Admin.
2. Vào `/admin/subscriptionplans`.
3. Bấm Create.
4. Tạo plan:
   - Name: `Manual Growth Plan`
   - Price: `299000`
   - BillingCycle: `Monthly`
   - MaxStores: `3`
   - MaxStaff: `10`
   - MaxProducts: `100`
5. Save.
6. Edit plan, đổi price hoặc limit.
7. Deactivate plan.
8. Activate lại.

Kết quả mong đợi:

- Plan hiện trong list.
- Price < 0 bị chặn.
- MaxStores/MaxStaff/MaxProducts không hợp lệ bị chặn.
- Audit log có create/update/activate/deactivate.
- Plan đã có tenant dùng không bị xóa vật lý; deactivate thay vì xóa.

### 5.19. Test gán subscription cho tenant

Mục tiêu: admin gán plan mới cho tenant và owner thấy được.

Bước test:

1. Đăng nhập Admin.
2. Vào `/admin/subscriptions/create`.
3. Chọn tenant demo.
4. Chọn plan.
5. Chọn StartDate/EndDate.
6. Chọn Status `Active`.
7. Nếu form có payment option, tạo pending system payment.
8. Submit.
9. Logout Admin.
10. Login Owner của tenant đó.
11. Vào `/owner/subscription`.

Kết quả mong đợi:

- Owner thấy current subscription mới.
- Lịch sử subscription có bản ghi mới.
- Nếu tạo payment, payment hiện trong lịch sử thanh toán.
- Audit log có `ChangeSubscription`.

### 5.20. Test system payment

Mục tiêu: admin theo dõi và cập nhật thanh toán SaaS.

Bước test:

1. Đăng nhập Admin.
2. Vào `/admin/systempayments`.
3. Filter/search nếu UI có.
4. Chọn payment `Pending`.
5. Bấm Mark Paid.
6. Kiểm tra `PaidAt`.
7. Chọn payment khác, bấm Mark Failed.
8. Nếu có invoice URL, bấm link invoice.

Kết quả mong đợi:

- Payment paid có status `Paid`.
- `PaidAt` được gán.
- Payment failed có status `Failed`.
- Invoice URL mở được nếu là link hợp lệ.
- Audit log có `MarkSystemPaymentPaid`/`MarkSystemPaymentFailed`.

### 5.21. Test subscription limit

Mục tiêu: giới hạn plan được áp dụng khi tạo store/staff/product.

Bước test để thấy trực quan:

1. Admin tạo plan giới hạn nhỏ:
   - MaxStores: `1`
   - MaxStaff: `1`
   - MaxProducts: `1`
2. Admin gán plan đó cho tenant test.
3. Login Owner của tenant test.
4. Tạo store thứ nhất: thành công.
5. Tạo store thứ hai: bị chặn.
6. Tạo staff thứ nhất: thành công.
7. Tạo staff thứ hai: bị chặn.
8. Tạo product thứ nhất: thành công.
9. Tạo product thứ hai: bị chặn.

Kết quả mong đợi:

- Hệ thống hiện thông báo vượt giới hạn subscription.
- Dữ liệu không được tạo khi vượt limit.

### 5.22. Test audit logs

Mục tiêu: thao tác quan trọng có log, phân quyền log đúng.

Admin:

1. Đăng nhập Admin.
2. Vào `/admin/auditlogs`.
3. Filter action `Login`.
4. Filter action `CreateOrder`.
5. Filter action `ChangeSubscription`.
6. Filter theo thời gian.

Owner:

1. Đăng nhập Owner.
2. Vào `/owner/auditlogs`.
3. Filter action `ImportStock`.
4. Filter action `CreateOrder`.

Kết quả mong đợi:

- Admin xem được log toàn platform.
- Owner chỉ xem log tenant mình.
- Filter action/time/user/store trả kết quả đúng.

### 5.23. Test owner không xem tenant khác

Mục tiêu: tenant isolation đúng.

Cách test trực quan:

1. Dùng Admin tạo owner A và owner B, mỗi owner có tenant riêng.
2. Login owner A.
3. Tạo store/product có tên để nhận biết, ví dụ `ONLY OWNER A STORE`.
4. Logout.
5. Login owner B.
6. Vào Stores, Products, Inventory, Orders, Reports.

Kết quả mong đợi:

- Owner B không thấy dữ liệu có tên `ONLY OWNER A STORE`.
- Nếu thử sửa URL id của owner A trong các màn detail, kết quả phải bị chặn/not found/access denied.

## 6. Luồng demo nên dùng khi quay video hoặc demo cho khách

Nếu cần demo nhanh toàn bộ hệ thống, nên đi theo thứ tự sau:

1. Login Admin.
2. Mở dashboard để giới thiệu platform.
3. Mở tenants/owners để giới thiệu SaaS multi-tenant.
4. Mở subscription plans và system payments.
5. Login Owner demo.
6. Mở stores/staff/products/store products.
7. Mở inventory, nhập kho nhanh một product.
8. Mở shifts, mở ca.
9. Mở POS, bán 1 đơn.
10. Hiện receipt.
11. Mở inventory để thấy tồn giảm.
12. Mở orders, cancel đơn.
13. Mở inventory để thấy tồn hoàn lại.
14. Đóng ca.
15. Mở reports.
16. Mở audit logs để thấy toàn bộ hành động vừa làm.

## 7. Các dữ liệu demo có sẵn nên tận dụng

Store demo nên dùng:

- `TZ-HCM-01`
- `TZ-HCM-02`

Tài khoản demo:

- Admin: `admin@chainpos.local`
- Owner: `owner@demo.local`
- Staff: `staff01@demo.local`

Sản phẩm demo có sẵn:

- Apple MacBook Pro 14-inch M3 Pro 18GB/512GB
- Apple MacBook Air 13-inch M3 8GB/256GB
- Apple iPhone 15 Pro 256GB Natural Titanium
- Samsung Galaxy S24 Ultra 256GB Titanium Gray
- Sony WH-1000XM5 Wireless Noise Cancelling Headphones
- Logitech MX Master 3S Wireless Mouse Graphite
- Dell UltraSharp U2723QE 27-inch 4K USB-C Monitor
- Samsung 990 PRO 1TB NVMe PCIe 4.0 SSD
- Anker 737 Power Bank 24000mAh 140W

Dữ liệu POS demo có sẵn:

- Shift demo cho `TZ-HCM-01` và `TZ-HCM-02`.
- Order demo mã `POS-DEMO-*`.
- Payment demo bằng `Cash`, `Card`, `BankTransfer`, `Momo`.
- Inventory transaction demo `Sale` và `Return`.

Dữ liệu billing demo có sẵn:

- Plan `Business Demo`.
- Tenant subscription active.
- System payment có trạng thái `Paid`, `Pending`, `Failed`.

## 8. Điều hệ thống đã có và chưa có

Đã có:

- Authentication 3 role.
- Tenant isolation.
- Store access cho staff.
- Admin owner/tenant/subscription/payment.
- Owner store/staff/category/product/store product.
- Inventory import/export/adjust.
- Shift/POS/checkout/order/receipt/cancel.
- Reports.
- Audit log viewer.
- Demo data.
- Unit/integration test bước đầu.
- Realtime SignalR cho inventory, POS order, cancel order, shift, subscription và system payment.

Chưa có hoặc nên bổ sung sau:

- Export Excel cho reports.
- Owner dashboard low stock/recent orders.
- Test tự động cho tạo owner/staff và admin billing.
- Test manual sâu hơn cho tenant suspended và owner không xem tenant khác.
- Tự động prepend order/payment mới vào table không cần reload.

## 9. Realtime hiện tại và hướng mở rộng

Realtime hiện tại đã có:

1. SignalR hub `/hubs/chainpos`.
2. Group theo admin platform, tenant owner và store staff.
3. Server broadcast khi:
   - Order created.
   - Inventory changed.
   - Order cancelled.
   - Shift opened/closed.
   - Subscription changed.
   - System payment changed.
4. Client hiện toast, badge notification và notification dropdown.
5. POS/Inventory cập nhật stock đang hiện trên màn hình.
6. Orders/Shifts/Subscription/Payments hiện banner reload khi cần nạp lại danh sách.

Nếu muốn nâng cấp tiếp:

1. Thêm endpoint render partial row để order/payment mới tự động chèn vào table.
2. Cập nhật dashboard metric live.
3. Cập nhật report card live hoặc thông báo report cần reload.
4. Thêm live audit feed cho Admin/Owner.
5. Thêm reconnect indicator rõ hơn nếu mất kết nối SignalR.
