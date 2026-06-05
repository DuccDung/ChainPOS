# Bug & Risk Audit - ChainPOS

Ngày audit: 2026-06-05  
Mục tiêu: Đọc toàn hệ thống, ghi lại lỗi tiềm ẩn và điểm nghi ngờ, không sửa code.  
Đặc biệt tập trung: lỗi cancel/abort (OperationCanceledException, CancellationToken, Context.Abort).

---

## 1. LOG FILES HIỆN CÓ

| File | Trạng thái | Chi tiết |
|------|-----------|----------|
| `chainpos_run.out.log` | OK | App startup bình thường, listen trên 7219 HTTPS và 5292 HTTP. Không có exception. |
| `chainpos_run.err.log` | OK | Trống, không có lỗi. |
| `chainpos_debug.err.log` | File trống | Không có data. |
| `chainpos_debug.out.log` | File trống | Không có data. |
| `chainpos_verify.err.log` | File trống | Không có data. |
| `chainpos_verify.out.log` | File trống | Không có data. |
| `chainpos_prod.err.log` | **Có lỗi PowerShell syntax** | `+ ='Development'; dotnet '..\artifacts\staff-profile-build\ChainPOS.dll ...` → lỗi parse do `=` ở đầu dòng. Không phải lỗi app. |
| `chainpos_staff_profile.err.log` | **Có lỗi PowerShell syntax** | Cùng pattern: `Production` bị nhận diện thành cmdlet. |
| `chainpos_reports-test.err.log` | **File không tồn tại** | Chưa từng tạo. |
| `chainpos_reports-test.out.log` | Có lỗi PowerShell syntax | Cùng pattern. |
| `chainpos-seed-phase7.err.log` | File trống | Không có data. |
| `chainpos-seed-phase7.out.log` | File trống | Không có data. |

**Kết luận về log:** Các log hiện có không chứa trace lỗi runtime nghiêm trọng. Các "lỗi" trong `.err.log` thực ra là lỗi shell script (PowerShell parsing), không phải exception từ ứng dụng. Điều này có nghĩa: khi có lỗi thật xảy ra ở production, ứng dụng hiện **không ghi log chi tiết vào file** (không có `appsettings.Production.json` cấu hình logging file). Chỉ có output console.

---

## 2. LỖI / RỦI RO THEO MỨC ĐỘ

### 2.1. CRITICAL

#### BUG-C1: ChainPosHub.OnConnectedAsync - Context.Abort() không có try/catch
- **File:** `ChainPOS\Realtime\ChainPosHub.cs:33`
- **Mô tả:** Khi user kết nối SignalR nhưng không đủ quyền, code gọi `Context.Abort()` rồi return. `Abort()` sẽ raise `OperationCanceledException` bên trong async method vì connection bị chủ động đóng. Method `OnConnectedAsync` không có try/catch bao bọc, nên exception này propagate lên SignalR framework. Framework sẽ log error vào console, client thấy connection đóng đột ngột mà không có thông báo rõ ràng.
- **Tác động:** Log đầy console khi user cũ hoặc user bị khóa thử kết nối lại. Không ảnh hưởng data, nhưng nhiễu log production.

#### BUG-C2: RefreshUserClaimsCookieEvents.ValidatePrincipal - không có CancellationToken
- **File:** `ChainPOS\Services\Auth\RefreshUserClaimsCookieEvents.cs:33`
- **Mô tả:** `FirstOrDefaultAsync()` được gọi **không có CancellationToken** trong cookie validation pipeline. Đây là middleware chạy trên mọi request đã authenticated. Nếu DB chậm hoặc bị network lag, request của user bị treo không giới hạn. Nếu user nhấn refresh nhiều lần, nhiều query đồng thời đè lên DB. Trường hợp xấu: user bị xóa hoặc tenant bị cancelled giữa chừng → query trả null → reject cookie đúng, nhưng query đó vẫn tốn thời gian DB.
- **Tác động:** Request bị treo khi DB lag. Nếu DB pool cạn, có thể gây cascade timeout cho tất cả users.

#### BUG-C3: SepayGatewayClient.ResolveReceiverAsync - catch tất cả exceptions, swallow cả OCE
- **File:** `ChainPOS\Services\Payments\SepayGatewayClient.cs:83`
- **Mô tả:** `catch (Exception exception)` catch cả `OperationCanceledException` và `TaskCanceledException`. Khi user cancel request trong lúc đang gọi SePay API (ví dụ: đóng tab), OCE bị swallow, trả về fallback receiver thay vì propagate cancel lên. Điều này có thể gây **checkout bị treo**: vì user đã cancel nhưng backend vẫn tiếp tục xử lý checkout với thông tin bank fallback.
- **Tác động:** User cancel → backend vẫn tiếp tục tạo order → data tồn tại không mong muốn.

### 2.2. HIGH

#### BUG-H1: SignalRRealtimeNotifier.SendBestEffortAsync - bắt OCE nhưng không log
- **File:** `ChainPOS\Services\Realtime\SignalRRealtimeNotifier.cs:71-74`
- **Mô tả:** `catch (OperationCanceledException)` bị swallow hoàn toàn, không log gì cả. Comment nói "best-effort" nhưng không có metric/log đếm bao nhiêu lần realtime fail. Khó biết có bao nhiêu client bị mất event.
- **Tác động:** Mất visibility. Khi admin hỏi "sao cập nhật tồn kho không realtime?", không có cách biết có bao nhiêu client disconnect.

#### BUG-H2: PasswordResetService - race condition giữa check token và save
- **File:** `ChainPOS\Services\Auth\PasswordResetService.cs:56-86`
- **Mô tả:** Flow: check existing token → nếu expired → tạo token mới → save → send email. Hai request gần đồng thời:
  1. Request A đọc token cũ (chưa expired).
  2. Request B cũng đọc token cũ.
  3. Cả hai đều generate OTP mới và overwrite `tokenEntity.Value`.
  4. Request A save thành công, gửi email A.
  5. Request B save thành công, gửi email B.
  6. User nhận 2 email, token cuối cùng là của B. Nếu A gửi email trước, user nhập OTP A → **sai** (token đã bị B overwrite).
- **Tác động:** User nhận nhiều OTP, token có thể bị ghi đè → đăng nhập thất bại.

#### BUG-H3: SubscriptionManagementService.CreateSubscriptionAsync - SaveChanges trước AuditLog, chưa Commit
- **File:** `ChainPOS\Services\Subscriptions\SubscriptionManagementService.cs:346-356`
- **Mô tả:** Thứ tự:
  1. `_db.SaveChangesAsync()` → subscription + payment đã được lưu vào DB (trong transaction scope).
  2. `_auditLog.LogAsync()` → ghi audit log (cũng dùng chung DbContext → insert AuditLog).
  3. `transaction.CommitAsync()` → commit tất cả (subscription + payment + audit log).
  Nếu cancel xảy ra GIỮA bước 2 và 3: subscription đã `SaveChanges` (tức đã trong transaction buffer) nhưng chưa commit. Khi transaction rollback (do cancel ném OCE), subscription biến mất → đúng. **Tuy nhiên**, nếu cancel xảy ra giữa bước 1 (SaveChanges) và bước 2 (AuditLog): subscription đã được ghi Nhật ký EF nhưng audit log chưa được tạo. Khi transaction rollback, cả 2 đều mất → OK.
  **Nhưng**: `AuditLogService` dùng chung `_db` context. Nếu transaction chưa commit mà context bị dispose (do scope kết thúc), có thể gây `InvalidOperationException: A second operation was started on this context before the previous operation completed`.
- **Tác động:** Lỗi hiếm, xảy ra khi request bị cancel đúng thời điểm nhạy cảm.

#### BUG-H4: PosService.GenerateOrderCodeAsync - vòng retry không propagate CancellationToken
- **File:** `ChainPOS\Services\Sales\PosService.cs:655-668`
- **Mô tả:** Method nhận `CancellationToken` nhưng trong vòng `for` loop, `AnyAsync()` nhận token nhưng **không kiểm tra `cancellationToken.ThrowIfCancellationRequested()`** giữa các lần retry. Nếu user cancel request trong lúc retry 10 lần (trường hợp trùng order code liên tiếp), backend vẫn chạy hết 10 lần rồi mới nhận cancel.
- **Tác động:** Tốn tài nguyên DB khi user cancel checkout.

#### BUG-H5: OrderService.CancelOrderAsync - InventoryRow tracking có thể gây DbUpdateConcurrencyException
- **File:** `ChainPOS\Services\Sales\OrderService.cs:235-256`
- **Mô tả:** `CancelOrderAsync` dùng `FirstOrDefaultAsync` (không AsNoTracking) để lấy inventory row, rồi modify trực tiếp `inventory.Quantity += item.Quantity`. Nếu inventory row đang được track bởi context (ví dụ: cùng request vừa query ở chỗ khác), có thể gây `InvalidOperationException: The instance of entity type 'Inventory' cannot be tracked because another instance with the same key is already being tracked`.
- **Tác động:** Lỗi hiếm, xảy ra khi cancel order ngay sau khi vừa import/export stock trong cùng request (ít xảy ra vì mỗi request là scope mới).

### 2.3. MEDIUM

#### BUG-M1: ChainPosHub.OnConnectedAsync - DB query không có try/catch
- **File:** `ChainPOS\Realtime\ChainPosHub.cs:60, 79, 105`
- **Mô tả:** Ba lần query DB (`ToListAsync(Context.ConnectionAborted)`) không có try/catch. Nếu DB timeout hoặc connection mất, exception propagates lên SignalR framework. Client disconnect đột ngột. Không có log chi tiết.
- **Tác động:** User mất kết nối SignalR khi DB có vấn đề.

#### BUG-M2: AuditLogService.WriteLogAsync - HttpContext có thể null
- **File:** `ChainPOS\Services\Audit\AuditLogService.cs:78-93`
- **Mô tả:** `_httpContextAccessor.HttpContext` có thể null nếu gọi từ background task hoặc từ scope không có HttpContext (ví dụ: hosted service). Code dùng null-conditional `?.` nên không crash, nhưng `IpAddress` và `UserAgent` sẽ null.
- **Tác động:** Audit log thiếu IP/UserAgent khi gọi từ background.

#### BUG-M3: OwnerStaffService.CreateStaffAsync - catch DbUpdateException thiếu scope
- **File:** `ChainPOS\Services\Owner\OwnerStaffService.cs:200`
- **Mô tả:** `catch (DbUpdateException)` catch exception từ `SaveChangesAsync`, nhưng **không có async/await trong catch block** (catch trực tiếp). Trong async method, catch block không async là đúng, nhưng `DbUpdateException` có thể là concurrency violation hoặc unique index. Hiện tại trả về thông báo chung "Email or phone already exists", không phân biệt.
- **Tác động:** Không phân biệt được duplicate email vs duplicate phone vs concurrency.

#### BUG-M4: SmtpEmailSender - không timeout riêng, phụ thuộc request CancellationToken
- **File:** `ChainPOS\Services\Email\SmtpEmailSender.cs:40`
- **Mô tả:** `smtpClient.SendMailAsync(message, cancellationToken)` sử dụng cancellationToken từ request. Nếu request timeout (mặc định 100s của Kestrel), SMTP send bị cancel. Tuy nhiên, `SmtpClient` không có timeout riêng, có thể treo vô hạn nếu SMTP server lag.
- **Tác động:** Request thread bị chiếm khi SMTP server không phản hồi.

### 2.4. LOW

#### BUG-L1: Program.cs - Main catch tất cả nhưng không log chi tiết
- **File:** `ChainPOS\Program.cs:111`
- **Mô tả:** `catch (Exception ex)` catch tất cả exception từ startup, nhưng chỉ log `LogWarning`. Nếu startup fail do config sai, log cảnh báo thay vì error, khó phân biệt.
- **Tác động:** Khó debug startup failure.

#### BUG-L2: RequireTenantFilter - không async lock, race condition tiềm ẩn
- **File:** `ChainPOS\Filters\RequireTenantFilter.cs:45-55`
- **Mô tả:** Filter query DB để kiểm tra tenant status rồi set `context.Result = new ForbidResult()`. Nếu 2 request đồng thời từ cùng user (1 request đang chạy, 1 request mới), và service chặn tenant giữa chừng, request cũ vẫn đi qua vì filter đã chạy trước.
- **Tác động:** Race condition hiếm, vì cookie validation cũng kiểm tra tenant status.

#### BUG-L3: Log files từ shell script có lỗi PowerShell syntax
- **File:** `chainpos_prod.err.log`, `chainpos_staff_profile.err.log`
- **Mô tả:** Các file `.err.log` chứa dòng lỗi do PowerShell parser: `+ ='Production'; dotnet ...`. Nguyên nhân: script ghi log dùng `$env:ASPNETCORE_ENVIRONMENT=Production; dotnet run` nhưng cách ghi error stream bị lỗi parse. Không phải lỗi của ứng dụng.
- **Tác động:** Log file không trustable. Khi có lỗi thật, khó phân biệt với noise.

#### BUG-L4: chainpos_reports-test.*.log - file không tồn tại
- **Mô tả:** Các file log cho báo cáo chưa từng được tạo. Có thể script test/run chưa chạy bao giờ, hoặc log bị xóa.
- **Tác động:** Không có data để debug reports module.

---

## 3. PATTERN LỖI LẶP LẠI

### 3.1. SaveChanges trước AuditLog, Commit sau
Xuất hiện ở:
- `SubscriptionManagementService.CreateSubscriptionAsync`
- `ShiftService.OpenShiftAsync`, `CloseShiftAsync`
- `OrderService.CancelOrderAsync`
- `PosService.CheckoutAsync`, `CompletePendingOrderAsync`
- `InventoryService.ImportStockAsync`, `ExportStockAsync`, `AdjustStockAsync`
- `OwnerStaffService.CreateStaffAsync`

**Pattern:**
```
SaveChangesAsync (ghi business data) → AuditLog.LogAsync → Transaction.CommitAsync → RealtimeNotify
```

**Rủi ro:** Nếu cancel xảy ra giữa SaveChanges và AuditLog, business data đã trong transaction buffer. Transaction chưa commit nên rollback → data và audit log cùng mất. Điều này **đúng về ACID** (atomic), nhưng:
- Nếu AuditLogService có lỗi (ví dụ: HttpContext null → exception), transaction rollback → business data mất.
- Không có cách nào biết operation đã xảy ra (vì cả data và audit log đều không commit).

**Khuyến nghị (không sửa code, chỉ ghi chú):** Nên chuyển AuditLog sang `SaveChanges` cuối cùng, hoặc dùng `Outbox pattern` để đảm bảo audit log luôn được ghi kể cả khi business logic có vấn đề.

### 3.2. Realtime notify sau transaction commit
Tất cả service đều commit transaction trước khi gọi `_realtimeNotifier`. Điều này **đúng** (không nên notify realtime trước khi commit). Tuy nhiên, nếu realtime notify fail (ví dụ: SignalR hub bị treo), operation đã hoàn thành thành công ở DB nhưng client không nhận event. Không có retry hay dead-letter queue.

### 3.3. Không có try/catch chung cho DB operations
Hầu hết các service methods không có try/catch ngoài, chỉ có catch ở những chỗ cụ thể (DbUpdateException, OperationCanceledException). Nếu có exception không lường trước (ví dụ: SqlException, TimeoutException), exception propagate lên controller, ASP.NET Core trả 500. User không thông báo rõ ràng.

---

## 4. GHI CHÚ VỀ CANCELLATION TOKEN

### 4.1. Nơi đã xử lý tốt
- `SignalRRealtimeNotifier.SendBestEffortAsync`: catch `OperationCanceledException`, swallow (best-effort).
- `PasswordResetService`: catch `Exception when ex is not OperationCanceledException` (line 96).
- `AccountController.Logout`: dùng `HttpContext.RequestAborted` làm CancellationToken.
- `SmtpEmailSender`: dùng cancellationToken cho `SendMailAsync`.

### 4.2. Nơi có thể cải thiện
- `RefreshUserClaimsCookieEvents.ValidatePrincipal`: không có CancellationToken → có thể block.
- `ChainPosHub.OnConnectedAsync`: không có try/catch cho Abort.
- `PosService.GenerateOrderCodeAsync`: retry loop không kiểm tra cancel giữa các lần.
- `SepayGatewayClient.ResolveReceiverAsync`: catch tất cả, swallow OCE.

---

## 5. TỔNG KẾT

| Mức độ | Số lượng | Vấn đề chính |
|--------|---------|--------------|
| CRITICAL | 3 | Hub Abort không catch, Cookie validate không CT, SePay swallow OCE |
| HIGH | 5 | Realtime không log, PasswordReset race, SaveChanges-before-Audit, GenerateOrderCode retry, Inventory tracking |
| MEDIUM | 4 | Hub query không catch, AuditLog HttpContext null, StaffService catch scope, Smtp timeout |
| LOW | 4 | Program.cs catch, Filter race, Log syntax error, Reports log missing |

**Lưu ý chung:**
- Hệ thống hiện **không có cấu hình logging file** cho production → không có evidence khi lỗi xảy ra ở production.
- **Không có Circuit Breaker / Retry policy** cho DB operations hoặc external API (SePay, SMTP).
- **Không có health check endpoint** để theo dõi DB connectivity và SignalR hub status.
- CancellationToken được truyền qua hầu hết các service methods, nhưng **xử lý khi cancel xảy ra chưa nhất quán** (một số nơi swallow, một số nơi propagate, một số nơi không check).
