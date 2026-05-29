# ChainPOS - Tai lieu nghiep vu va huong dan test truc quan

Cap nhat: 2026-05-29

Tai lieu nay mo ta he thong ChainPOS theo goc nhin nghiep vu: he thong co nhung chuc nang gi, moi vai tro dung de lam gi, va nen test truc quan nhu the nao de thay duoc du lieu thay doi tren man hinh.

> Luu y ve "realtime": hien tai ChainPOS la ASP.NET Core MVC server-rendered, chua co SignalR/WebSocket de day thay doi realtime sang nhieu man hinh dang mo. Nghia la sau khi dat hang, dong ca, nhap kho... du lieu duoc ghi ngay vao database va man hinh hien tai redirect/cap nhat sau POST. Cac man hinh khac muon thay doi moi thi reload lai trang hoac vao lai menu tuong ung. Khi tai lieu nay noi "test realtime/truc quan", hay hieu la test cap nhat ngay sau thao tac va test bang 2 tab/2 tai khoan kem thao tac refresh de xac nhan du lieu da dong bo.

## 1. Tong quan he thong

ChainPOS la he thong quan ly chuoi cua hang va ban hang POS theo mo hinh SaaS.

Mot platform co nhieu tenant. Moi tenant dai dien cho mot chuoi cua hang cua mot owner. Trong tenant co cac store, staff, danh muc, san pham, ton kho, ca ban hang, don hang POS, bao cao, goi subscription va audit log.

He thong co 3 vai tro chinh:

- `ADMIN`: quan ly toan bo platform SaaS.
- `OWNER`: quan ly du lieu trong tenant cua minh.
- `STAFF`: thao tac ban hang/kho trong nhung store duoc owner gan quyen.

Dia chi chay local mac dinh:

```text
http://localhost:5292
```

Tai khoan demo:

| Vai tro | Email | Mat khau |
| --- | --- | --- |
| Admin | `admin@chainpos.local` | `Admin@123` |
| Owner demo | `owner@demo.local` | `Owner@123` |
| Staff demo | `staff01@demo.local` | `Staff@123` |

Lenh chay:

```powershell
cd D:\laptrinhweb\code_outsrc\Dam_Van_Bao\ChainPOS\ChainPOS
dotnet build .\ChainPOS.sln
dotnet run --project .\ChainPOS\ChainPOS.csproj --launch-profile http
```

Lenh test tu dong:

```powershell
dotnet test .\ChainPOS.sln
```

## 2. Cac khai niem nghiep vu chinh

### 2.1. Platform, tenant va owner

`ADMIN` la nguoi van hanh platform. Admin tao owner, tao tenant, quan ly trang thai tenant va quan ly goi subscription.

`Tenant` la mot don vi kinh doanh rieng, vi du mot chuoi cua hang. Toan bo store, staff, product, inventory, order cua tenant nay phai tach biet voi tenant khac.

`OWNER` la chu tai khoan cua tenant. Owner chi xem va quan ly du lieu cua tenant minh.

Test can nhin thay:

- Admin xem duoc danh sach owner/tenant cua toan platform.
- Owner khong vao duoc khu vuc Admin.
- Owner chi thay store, staff, product, inventory, order cua tenant minh.

### 2.2. Store va store access

Store la cua hang/chi nhanh trong tenant.

Owner co quyen thao tac tat ca store active trong tenant.

Staff chi thao tac store ma owner gan trong bang `UserStores` va ban ghi do phai `IsActive = true`.

Test can nhin thay:

- Owner tao store moi.
- Owner gan staff vao store.
- Staff dang nhap chi thay store duoc gan.
- Staff khong thao tac duoc store chua duoc gan hoac da bi tat quyen.

### 2.3. Catalog: category, product, store product

`Category` la danh muc san pham.

`Product` la san pham chung trong tenant. Product co SKU, barcode, gia goc, gia von, anh, trang thai active/inactive.

`StoreProduct` la viec bat san pham ban tai tung store. Cung mot product co the duoc ban o store A nhung khong ban o store B. StoreProduct co `SellingPrice` rieng. Neu `SellingPrice` rong, POS dung `Products.Price`.

Test can nhin thay:

- Product inactive hoac deleted khong hien o POS.
- StoreProduct `IsAvailable = false` khong hien o POS.
- Khi set SellingPrice rieng, POS hien gia rieng do.
- Khi xoa SellingPrice, POS fallback ve Product Price.

### 2.4. Inventory va inventory transaction

Inventory luu ton kho theo tenant, store, product.

Moi bien dong kho phai ghi `InventoryTransactions`:

- `Import`: nhap kho.
- `Export`: xuat kho thu cong.
- `Adjust`: kiem kho/dieu chinh ton.
- `Sale`: POS ban hang tru kho.
- `Return`: huy don hoan kho.

Test can nhin thay:

- Nhap kho tang so luong.
- Xuat kho giam so luong.
- Dieu chinh kho set lai so luong thuc te.
- POS checkout tru kho.
- Cancel order hoan kho.
- Cac thao tac quan trong co audit log.

### 2.5. Shift va POS

Shift la ca ban hang. Owner/Staff phai mo ca truoc khi checkout POS.

Luon co rule:

- Mot user khong duoc mo nhieu ca `Open` cung luc.
- Checkout phai co ca `Open` tai store dang ban.
- Dong ca tinh:
  - `ExpectedCash = OpeningCash + tong payment cash trong ca`
  - `DifferenceAmount = ClosingCash - ExpectedCash`

POS la man hinh ban hang:

- Chon store.
- Search product theo ten, SKU, barcode.
- Them product vao gio.
- Tang/giam so luong.
- Chon payment method.
- Neu cash thi nhap tien khach dua.
- Checkout tao order, order items, payment, tru kho va redirect sang receipt.

### 2.6. Orders, receipt va cancel

Order la don POS da checkout.

Receipt la man chi tiet/in hoa don.

Cancel order:

- Doi `OrderStatus = Cancelled`.
- Doi `PaymentStatus = Cancelled`.
- Cap nhat payment ve `Cancelled`.
- Hoan kho bang transaction `Return`.
- Ghi audit `CancelOrder`.

### 2.7. Subscription va billing SaaS

Subscription Plan quy dinh gioi han tenant:

- `MaxStores`
- `MaxStaff`
- `MaxProducts`
- `Price`
- `BillingCycle`

TenantSubscription la lich su goi cua tenant.

SystemPayment la thanh toan SaaS cua tenant cho platform, khac voi Payment POS.

Test can nhin thay:

- Admin tao/sua/kich hoat/tat plan.
- Admin khong xoa vat ly plan da co tenant dung; nen deactivate.
- Admin gan plan cho tenant.
- Owner xem subscription hien tai.
- Owner xem lich su system payment.
- Admin mark system payment `Paid` hoac `Failed`.

### 2.8. Reports va audit log

Reports hien co:

- Daily sales report.
- Staff sales report.
- Inventory status report.
- System revenue report cho Admin.

Audit log ghi cac thao tac quan trong:

- Login/logout.
- Tao/sua/khoa user.
- Tao/sua store, staff, category, product.
- Nhap/xuat/dieu chinh kho.
- Mo/dong ca.
- Tao/huy order.
- Doi subscription.

Admin xem toan bo audit log. Owner chi xem audit log trong tenant minh.

## 3. Ban do menu va URL chinh

### 3.1. Authentication

| Chuc nang | URL |
| --- | --- |
| Login | `/login` |
| Logout | POST `/logout` |
| Access denied | `/access-denied` |

### 3.2. Admin

| Chuc nang | URL |
| --- | --- |
| Dashboard | `/admin/dashboard` |
| Owners | `/admin/owners` |
| Tenants | `/admin/tenants` |
| Subscription plans | `/admin/subscriptionplans` |
| Gan subscription cho tenant | `/admin/subscriptions/create` |
| System payments | `/admin/systempayments` |
| Reports | `/admin/reports` |
| Audit logs | `/admin/auditlogs` |

### 3.3. Owner

| Chuc nang | URL |
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

| Chuc nang | URL |
| --- | --- |
| Dashboard | `/staff/dashboard` |
| Inventory | `/staff/inventory` |
| Shifts | `/staff/shifts` |
| POS | `/staff/pos` |
| Orders | `/staff/orders` |

## 4. Luong nghiep vu theo vai tro

### 4.1. Admin van hanh platform

Muc tieu: tao tenant moi, quan ly trang thai tenant, goi subscription va thanh toan SaaS.

Luong co ban:

1. Dang nhap bang `admin@chainpos.local`.
2. Vao `/admin/owners`.
3. Tao owner moi.
4. He thong tao owner user, gan role `OWNER`, tao tenant va gan owner vao tenant.
5. Vao `/admin/tenants` de xem tenant moi.
6. Vao detail tenant de suspend/activate/cancel neu can.
7. Vao `/admin/subscriptionplans` de tao hoac sua plan.
8. Vao `/admin/subscriptions/create` de gan plan cho tenant.
9. Vao `/admin/systempayments` de theo doi thanh toan SaaS.
10. Vao `/admin/auditlogs` de kiem tra thao tac da ghi audit.

Ket qua mong doi:

- Owner moi login duoc neu dung password.
- Tenant moi co owner.
- Subscription moi xuat hien o Owner subscription.
- Payment SaaS co the mark paid/failed.
- Audit log co action lien quan.

### 4.2. Owner chuan bi cua hang de ban POS

Muc tieu: tao store, tao staff, tao product, gan product vao store, nhap kho.

Luong co ban:

1. Dang nhap bang `owner@demo.local`.
2. Vao `/owner/stores`, tao store moi hoac dung store demo `TZ-HCM-01`.
3. Vao `/owner/staff`, tao staff hoac dung `staff01@demo.local`.
4. Gan staff vao store.
5. Vao `/owner/categories`, tao category neu can.
6. Vao `/owner/products`, tao product moi.
7. Vao `/owner/storeproducts`, gan product vao store, bat `IsAvailable`, set `SellingPrice` neu muon.
8. Vao `/owner/inventory/import`, nhap kho cho product o store.
9. Vao `/owner/pos`, chon store, search product de kiem tra san pham da hien.

Ket qua mong doi:

- Store hien trong danh sach.
- Staff thuoc tenant hien trong danh sach.
- Staff login va chi thay store duoc gan.
- Product hien trong POS neu active, available va co store product.
- Ton kho hien dung sau khi import.

### 4.3. Staff ban hang tai quay POS

Muc tieu: mo ca, checkout, in receipt, dong ca.

Luong co ban:

1. Dang nhap bang `staff01@demo.local`.
2. Vao `/staff/shifts`.
3. Mo ca cho store duoc gan, nhap `OpeningCash`.
4. Vao `/staff/pos`.
5. Chon store cua ca dang mo.
6. Search product.
7. Them product vao cart.
8. Tang/giam so luong.
9. Chon payment method.
10. Neu cash, nhap `CustomerPaidAmount` lon hon hoac bang total.
11. Checkout.
12. He thong redirect sang receipt/order detail.
13. Vao `/staff/orders` de thay order moi.
14. Vao `/staff/shifts`, dong ca, nhap `ClosingCash`.

Ket qua mong doi:

- Neu chua mo ca, checkout bi chan.
- Checkout thanh cong tao order, order item, payment.
- Inventory giam theo so luong da ban.
- Receipt hien dung san pham, so luong, gia, total.
- Dong ca tinh dung cash expected va difference.

## 5. Huong dan test truc quan chi tiet

### 5.1. Test login va phan quyen

Muc tieu: xac nhan 3 vai tro vao dung khu vuc, sai role bi chan.

Buoc test:

1. Mo `http://localhost:5292/login`.
2. Dang nhap Admin.
3. Xac nhan redirect ve `/admin/dashboard`.
4. Thu mo `/owner/products`.
5. Ket qua mong doi: Admin khong dung khu vuc Owner/Staff nghiep vu, neu bi chan thi dung.
6. Logout.
7. Dang nhap Owner.
8. Xac nhan redirect ve `/owner/dashboard`.
9. Thu mo `/admin/owners`.
10. Ket qua mong doi: access denied.
11. Logout.
12. Dang nhap Staff.
13. Xac nhan redirect ve `/staff/dashboard`.
14. Thu mo `/owner/products` va `/admin/owners`.
15. Ket qua mong doi: access denied.

Can nhin tren UI:

- Sidebar thay doi theo role.
- Staff khong thay menu Admin/Owner.
- Owner khong thay menu Admin.

### 5.2. Test Admin tao owner va tenant

Muc tieu: admin tao owner moi, he thong tao tenant rieng.

Buoc test:

1. Dang nhap Admin.
2. Vao `/admin/owners`.
3. Bam Create/New Owner.
4. Nhap thong tin owner:
   - Full name: `Owner Test Manual`
   - Email: email chua ton tai, vi du `owner.manual.001@demo.local`
   - Password: dung format hop le theo form.
   - Tenant name/code neu form yeu cau.
5. Submit.
6. Quay lai danh sach owner.
7. Search email vua tao.
8. Vao detail owner/tenant.

Ket qua mong doi:

- Owner moi xuat hien.
- Owner co role `OWNER`.
- Tenant moi duoc tao va gan owner.
- Audit log co `CreateUser`/`CreateTenant`.

Test truc quan tiep:

1. Logout Admin.
2. Login bang owner moi.
3. Xac nhan vao `/owner/dashboard`.
4. Owner moi chua thay du lieu cua tenant demo cu.

### 5.3. Test Admin tenant status

Muc tieu: tenant suspended/cancelled bi chan khi owner/staff thao tac nghiep vu.

Buoc test:

1. Dang nhap Admin.
2. Vao `/admin/tenants`.
3. Chon tenant cua owner test.
4. Bam Suspend.
5. Logout Admin.
6. Login owner cua tenant do.
7. Thu vao `/owner/stores`, `/owner/products`, `/owner/pos`.

Ket qua mong doi:

- Tenant suspended/cancelled khong duoc thao tac module nghiep vu.
- Neu dang nhap bi chan tu dau thi cung hop le theo rule hien co.
- Admin activate lai tenant thi owner thao tac lai duoc.

### 5.4. Test owner tao store

Muc tieu: owner quan ly store trong tenant.

Buoc test:

1. Dang nhap Owner.
2. Vao `/owner/stores`.
3. Bam Create.
4. Nhap:
   - Name: `Manual Test Store`
   - Code: `MTS-001`
   - Address/Phone neu co.
5. Submit.
6. Search `MTS-001`.
7. Edit store, doi ten thanh `Manual Test Store Updated`.
8. Toggle status inactive/active neu co nut.

Ket qua mong doi:

- Store moi hien trong list.
- Code trung trong tenant bi chan.
- Store inactive/closed khong dung duoc cho POS/kho.
- Audit log co create/update/change status.

### 5.5. Test owner tao staff va gan store

Muc tieu: staff chi thao tac store duoc gan.

Buoc test:

1. Dang nhap Owner.
2. Vao `/owner/staff`.
3. Bam Create.
4. Tao staff moi:
   - Full name: `Staff Manual Test`
   - Email: `staff.manual.001@demo.local`
   - Password: password hop le.
5. Sau khi tao, vao man gan store.
6. Gan staff vao store `Manual Test Store` hoac `TZ-HCM-01`.
7. Logout Owner.
8. Login staff moi.
9. Vao `/staff/dashboard`, `/staff/inventory`, `/staff/pos`.

Ket qua mong doi:

- Staff moi login duoc.
- Staff chi thay store duoc gan.
- Neu owner tat `UserStores.IsActive`, staff khong thao tac store do nua.
- Audit log co `CreateStaff`, `AssignStaffStore`, `EnableStaffStore`/`DisableStaffStore`.

### 5.6. Test category/product/store product

Muc tieu: san pham duoc tao, gan vao store va hien tren POS.

Buoc test:

1. Dang nhap Owner.
2. Vao `/owner/categories`.
3. Tao category `Manual Electronics`.
4. Vao `/owner/products`.
5. Tao product:
   - Name: `Manual POS Product`
   - SKU: `MANUAL-POS-001`
   - Barcode: `899000000001`
   - Price: `100000`
   - CostPrice: `70000`
   - Category: `Manual Electronics`
   - Upload anh JPG/PNG neu muon.
6. Vao `/owner/storeproducts`.
7. Gan product vao store `TZ-HCM-01`.
8. Set `IsAvailable = true`.
9. Set `SellingPrice = 95000`.
10. Vao `/owner/pos`, chon store, search `MANUAL-POS-001`.

Ket qua mong doi:

- Product hien trong POS.
- Gia tren POS la `95000`, khong phai `100000`.
- Neu tat `IsAvailable`, reload POS thi product khong hien.
- Neu xoa `SellingPrice`, POS fallback ve `100000`.

Test validate:

- Tao product SKU trung: bi chan.
- Tao barcode trung: bi chan.
- Price am: bi chan.
- Upload file khong phai anh: bi chan.
- Upload anh > 5MB: bi chan.

### 5.7. Test nhap kho

Muc tieu: ton kho tang va co transaction `Import`.

Buoc test:

1. Dang nhap Owner hoac Staff co quyen store.
2. Vao `/owner/inventory` hoac `/staff/inventory`.
3. Bam Import.
4. Chon store `TZ-HCM-01`.
5. Chon product `Manual POS Product`.
6. Nhap:
   - Quantity: `10`
   - MinQuantity: `2`
   - Reason: `Manual import test`
7. Submit.
8. Quay lai inventory list, filter/search product.

Ket qua mong doi:

- Quantity tang len 10 neu truoc do chua co ton.
- Low stock chi bat khi quantity > 0 va <= min quantity.
- Audit log co `ImportStock`.
- Neu quantity <= 0 thi bi chan.

### 5.8. Test xuat kho

Muc tieu: ton kho giam va co transaction `Export`.

Buoc test:

1. Dam bao product co ton kho >= 5.
2. Vao Inventory.
3. Bam Export.
4. Chon store/product.
5. Nhap Quantity `3`, reason `Manual export test`.
6. Submit.

Ket qua mong doi:

- Quantity giam di 3.
- Neu xuat qua ton thi bi chan.
- Audit log co `ExportStock`.

### 5.9. Test dieu chinh kho

Muc tieu: cap nhat ton kho theo so thuc te va co transaction `Adjust`.

Buoc test:

1. Vao Inventory.
2. Bam Adjust.
3. Chon store/product.
4. Nhap ActualQuantity `7`.
5. Nhap MinQuantity `2`.
6. Reason: `Manual cycle count`.
7. Submit.

Ket qua mong doi:

- Quantity thanh 7.
- Movement quantity trong transaction la chenh lech giua truoc va sau.
- ActualQuantity < 0 bi chan.
- Reason rong bi chan.
- Audit log co `AdjustStock`.

### 5.10. Test mo ca

Muc tieu: user phai mo ca truoc khi ban POS.

Buoc test:

1. Dang nhap Staff.
2. Vao `/staff/shifts`.
3. Bam Open Shift.
4. Chon store duoc gan.
5. Nhap OpeningCash `500000`.
6. Submit.
7. Thu bam Open Shift lan nua.

Ket qua mong doi:

- Ca dau tien mo thanh cong, status `Open`.
- Mo ca lan hai khi ca cu con open bi chan.
- Audit log co `OpenShift`.

### 5.11. Test POS checkout thanh cong

Muc tieu: dat hang tai POS, tao order, tru kho, tao payment, hien receipt.

Buoc test:

1. Dam bao da co ca `Open`.
2. Dam bao product co ton kho.
3. Vao `/staff/pos` hoac `/owner/pos`.
4. Chon store cua ca dang mo.
5. Search product theo:
   - Ten product.
   - SKU.
   - Barcode.
6. Bam add vao cart.
7. Tang so luong len `2`.
8. Chon payment method `Cash`.
9. Nhap CustomerPaidAmount lon hon total.
10. Bam Checkout.

Ket qua mong doi:

- He thong redirect sang receipt/order detail.
- Receipt hien:
  - Order code.
  - Store.
  - Staff.
  - Product name.
  - SKU.
  - Quantity.
  - Unit price.
  - Total.
  - Payment method.
- Order status la `Completed`.
- Payment status la `Paid`.
- Inventory giam dung so luong.
- Inventory transaction co type `Sale`.
- Audit log co `CreateOrder`.

### 5.12. Test POS validation

Muc tieu: backend khong tin cart/client.

Case can test:

1. Chua mo ca ma checkout:
   - Ket qua: bi chan voi thong bao can mo ca.
2. Cart rong:
   - Ket qua: bi chan.
3. Quantity <= 0:
   - Ket qua: item khong hop le, cart rong hoac bi chan.
4. Ban qua ton:
   - Ket qua: bi chan, khong tao order.
5. Cash nhap tien khach dua nho hon total:
   - Ket qua: bi chan.
6. Product inactive/unavailable:
   - Ket qua: khong hien tren POS hoac checkout bi chan neu client gui len.

### 5.13. Test POS "realtime" bang 2 tab

Muc tieu: thay du lieu duoc ghi ngay sau checkout va cac man hinh khac cap nhat sau refresh.

Chuan bi:

- Mo Tab A: `/staff/pos`.
- Mo Tab B: `/staff/inventory`.
- Mo Tab C: `/staff/orders`.
- Cung dang nhap mot staff hoac dung 2 browser rieng neu muon tach session.

Buoc test:

1. Tab B search product sap ban, ghi lai quantity hien tai.
2. Tab A checkout product do quantity `1`.
3. Sau khi checkout, Tab A redirect sang receipt.
4. Tab C reload `/staff/orders`.
5. Tab B reload `/staff/inventory`.

Ket qua mong doi:

- Tab C thay order moi o tren danh sach.
- Tab B thay inventory giam 1.
- Receipt tren Tab A hien don vua tao.
- Neu vao Audit Logs bang Owner/Admin va reload, thay `CreateOrder`.

Ket luan ve realtime hien tai:

- He thong da cap nhat du lieu ngay sau POST checkout.
- Man hinh khac can reload moi thay thay doi.
- Neu yeu cau realtime dung nghia, can bo sung SignalR sau nay cho order list, inventory stock badge, dashboard metric va audit feed.

### 5.14. Test cancel order va hoan kho

Muc tieu: huy don hoan kho dung.

Buoc test:

1. Sau khi checkout thanh cong, vao receipt/order detail.
2. Ghi lai product va quantity.
3. Vao Inventory, ghi lai quantity sau ban.
4. Quay lai order detail.
5. Bam Cancel Order.
6. Xac nhan confirm modal.
7. Quay lai Inventory, reload product.

Ket qua mong doi:

- OrderStatus = `Cancelled`.
- PaymentStatus = `Cancelled`.
- Payment status = `Cancelled`.
- Inventory tang lai dung quantity da ban.
- Inventory transaction co type `Return`.
- Audit log co `CancelOrder`.
- Cancel order lan nua bi chan.

### 5.15. Test dong ca

Muc tieu: he thong tinh tien mat du kien va chenh lech.

Buoc test:

1. Mo ca voi OpeningCash `500000`.
2. Checkout 2 don cash:
   - Don 1 total `100000`.
   - Don 2 total `150000`.
3. Vao `/staff/shifts`.
4. Bam Close Shift.
5. Man close shift hien ExpectedCash = `750000`.
6. Nhap ClosingCash `760000`.
7. Submit.

Ket qua mong doi:

- Shift status = `Closed`.
- ExpectedCash = `500000 + 100000 + 150000 = 750000`.
- DifferenceAmount = `760000 - 750000 = 10000`.
- Audit log co `CloseShift`.
- Sau khi dong ca, user can mo ca moi neu muon checkout tiep.

### 5.16. Test order list, filter va receipt

Muc tieu: order co the tim, loc, xem chi tiet va in receipt.

Buoc test:

1. Vao `/staff/orders` hoac `/owner/orders`.
2. Search theo order code vua tao.
3. Filter theo store.
4. Filter theo status `Completed`.
5. Filter theo payment status `Paid`.
6. Filter theo ngay ban.
7. Vao detail.
8. Bam print receipt neu co.

Ket qua mong doi:

- Filter tra dung don.
- Owner thay order trong tenant.
- Staff chi thay order trong store duoc gan.
- Receipt hien du thong tin.

### 5.17. Test reports

Muc tieu: bao cao doc dung report views va phan quyen dung.

Admin:

1. Dang nhap Admin.
2. Vao `/admin/reports`.
3. Xem cac block:
   - Daily sales.
   - Staff sales.
   - Inventory status.
   - System revenue.
4. Filter thoi gian/tenant/store neu UI co.

Owner:

1. Dang nhap Owner.
2. Vao `/owner/reports`.
3. Xem daily sales, staff sales, inventory status.
4. Xac nhan khong thay System Revenue Report.

Ket qua mong doi:

- Admin thay bao cao toan platform.
- Owner chi thay du lieu tenant minh.
- Order vua checkout co the phan anh trong report sau khi reload neu report view tinh theo database.

### 5.18. Test subscription plan

Muc tieu: admin quan ly goi SaaS.

Buoc test:

1. Dang nhap Admin.
2. Vao `/admin/subscriptionplans`.
3. Bam Create.
4. Tao plan:
   - Name: `Manual Growth Plan`
   - Price: `299000`
   - BillingCycle: `Monthly`
   - MaxStores: `3`
   - MaxStaff: `10`
   - MaxProducts: `100`
5. Save.
6. Edit plan, doi price hoac limit.
7. Deactivate plan.
8. Activate lai.

Ket qua mong doi:

- Plan hien trong list.
- Price < 0 bi chan.
- MaxStores/MaxStaff/MaxProducts khong hop le bi chan.
- Audit log co create/update/activate/deactivate.
- Plan da co tenant dung khong bi xoa vat ly; deactivate thay vi xoa.

### 5.19. Test gan subscription cho tenant

Muc tieu: admin gan plan moi cho tenant va owner thay duoc.

Buoc test:

1. Dang nhap Admin.
2. Vao `/admin/subscriptions/create`.
3. Chon tenant demo.
4. Chon plan.
5. Chon StartDate/EndDate.
6. Chon Status `Active`.
7. Neu form co payment option, tao pending system payment.
8. Submit.
9. Logout Admin.
10. Login Owner cua tenant do.
11. Vao `/owner/subscription`.

Ket qua mong doi:

- Owner thay current subscription moi.
- Lich su subscription co ban ghi moi.
- Neu tao payment, payment hien trong lich su thanh toan.
- Audit log co `ChangeSubscription`.

### 5.20. Test system payment

Muc tieu: admin theo doi va cap nhat thanh toan SaaS.

Buoc test:

1. Dang nhap Admin.
2. Vao `/admin/systempayments`.
3. Filter/search neu UI co.
4. Chon payment `Pending`.
5. Bam Mark Paid.
6. Kiem tra `PaidAt`.
7. Chon payment khac, bam Mark Failed.
8. Neu co invoice URL, bam link invoice.

Ket qua mong doi:

- Payment paid co status `Paid`.
- `PaidAt` duoc gan.
- Payment failed co status `Failed`.
- Invoice URL mo duoc neu la link hop le.
- Audit log co `MarkSystemPaymentPaid`/`MarkSystemPaymentFailed`.

### 5.21. Test subscription limit

Muc tieu: gioi han plan duoc ap dung khi tao store/staff/product.

Buoc test de thay truc quan:

1. Admin tao plan gioi han nho:
   - MaxStores: `1`
   - MaxStaff: `1`
   - MaxProducts: `1`
2. Admin gan plan do cho tenant test.
3. Login Owner cua tenant test.
4. Tao store thu nhat: thanh cong.
5. Tao store thu hai: bi chan.
6. Tao staff thu nhat: thanh cong.
7. Tao staff thu hai: bi chan.
8. Tao product thu nhat: thanh cong.
9. Tao product thu hai: bi chan.

Ket qua mong doi:

- He thong hien thong bao vuot gioi han subscription.
- Du lieu khong duoc tao khi vuot limit.

### 5.22. Test audit logs

Muc tieu: thao tac quan trong co log, phan quyen log dung.

Admin:

1. Dang nhap Admin.
2. Vao `/admin/auditlogs`.
3. Filter action `Login`.
4. Filter action `CreateOrder`.
5. Filter action `ChangeSubscription`.
6. Filter theo thoi gian.

Owner:

1. Dang nhap Owner.
2. Vao `/owner/auditlogs`.
3. Filter action `ImportStock`.
4. Filter action `CreateOrder`.

Ket qua mong doi:

- Admin xem duoc log toan platform.
- Owner chi xem log tenant minh.
- Filter action/time/user/store tra ket qua dung.

### 5.23. Test owner khong xem tenant khac

Muc tieu: tenant isolation dung.

Cach test truc quan:

1. Dung Admin tao owner A va owner B, moi owner co tenant rieng.
2. Login owner A.
3. Tao store/product co ten de nhan biet, vi du `ONLY OWNER A STORE`.
4. Logout.
5. Login owner B.
6. Vao Stores, Products, Inventory, Orders, Reports.

Ket qua mong doi:

- Owner B khong thay du lieu co ten `ONLY OWNER A STORE`.
- Neu thu sua URL id cua owner A trong cac man detail, ket qua phai bi chan/not found/access denied.

## 6. Luong demo nen dung khi quay video hoac demo cho khach

Neu can demo nhanh toan bo he thong, nen di theo thu tu sau:

1. Login Admin.
2. Mo dashboard de gioi thieu platform.
3. Mo tenants/owners de gioi thieu SaaS multi-tenant.
4. Mo subscription plans va system payments.
5. Login Owner demo.
6. Mo stores/staff/products/store products.
7. Mo inventory, nhap kho nhanh mot product.
8. Mo shifts, mo ca.
9. Mo POS, ban 1 don.
10. Hien receipt.
11. Mo inventory de thay ton giam.
12. Mo orders, cancel don.
13. Mo inventory de thay ton hoan lai.
14. Dong ca.
15. Mo reports.
16. Mo audit logs de thay toan bo hanh dong vua lam.

## 7. Cac du lieu demo co san nen tan dung

Store demo nen dung:

- `TZ-HCM-01`
- `TZ-HCM-02`

Tai khoan demo:

- Admin: `admin@chainpos.local`
- Owner: `owner@demo.local`
- Staff: `staff01@demo.local`

San pham demo co san:

- Apple MacBook Pro 14-inch M3 Pro 18GB/512GB
- Apple MacBook Air 13-inch M3 8GB/256GB
- Apple iPhone 15 Pro 256GB Natural Titanium
- Samsung Galaxy S24 Ultra 256GB Titanium Gray
- Sony WH-1000XM5 Wireless Noise Cancelling Headphones
- Logitech MX Master 3S Wireless Mouse Graphite
- Dell UltraSharp U2723QE 27-inch 4K USB-C Monitor
- Samsung 990 PRO 1TB NVMe PCIe 4.0 SSD
- Anker 737 Power Bank 24000mAh 140W

Du lieu POS demo co san:

- Shift demo cho `TZ-HCM-01` va `TZ-HCM-02`.
- Order demo ma `POS-DEMO-*`.
- Payment demo bang `Cash`, `Card`, `BankTransfer`, `Momo`.
- Inventory transaction demo `Sale` va `Return`.

Du lieu billing demo co san:

- Plan `Business Demo`.
- Tenant subscription active.
- System payment co trang thai `Paid`, `Pending`, `Failed`.

## 8. Dieu he thong da co va chua co

Da co:

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
- Unit/integration test buoc dau.

Chua co hoac nen bo sung sau:

- Realtime push bang SignalR/WebSocket.
- Export Excel cho reports.
- Owner dashboard low stock/recent orders.
- Test tu dong cho tao owner/staff va admin billing.
- Test manual sau hon cho tenant suspended va owner khong xem tenant khac.

## 9. Goi y neu muon them realtime dung nghia

Neu sau nay muon "dat hang realtime" dung nghia, nen bo sung:

1. SignalR hub cho POS/order/inventory.
2. Khi checkout thanh cong, server broadcast:
   - Order created.
   - Inventory changed.
   - Dashboard metrics changed.
3. Man order list tu dong prepend order moi.
4. Man inventory tu dong cap nhat quantity.
5. Man dashboard tu dong cap nhat revenue/order count.
6. Audit log viewer co the co live feed tuy nhu cau.

Acceptance criteria cho realtime sau nay:

- Mo Tab A POS va Tab B Orders.
- Checkout o Tab A.
- Tab B tu hien order moi ma khong refresh.
- Mo Tab C Inventory.
- Ton kho tren Tab C tu giam ma khong refresh.
- Neu cancel order, Tab C tu tang ton lai.

Hien tai he thong chua lam phan nay, nen khi test dung he thong hien tai thi can refresh tab khac de thay du lieu moi.
