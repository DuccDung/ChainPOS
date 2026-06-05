using System.Text.Json;
using ChainPOS.Constants;
using ChainPOS.Contracts.Payments;
using ChainPOS.Services.Common;
using ChainPOS.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ChainPOS.Controllers;

[ApiController]
[Route("api/system-payments")]
public sealed class SystemPaymentsApiController : ControllerBase
{
    private readonly ISystemPaymentSePayService _sePayService;
    private readonly ICurrentUserService _currentUser;

    public SystemPaymentsApiController(
        ISystemPaymentSePayService sePayService,
        ICurrentUserService currentUser)
    {
        _sePayService = sePayService;
        _currentUser = currentUser;
    }

    [Authorize(Roles = AppRoles.Admin + "," + AppRoles.Owner)]
    [HttpGet("{id:guid}/status")]
    public async Task<ActionResult<SystemPaymentStatusResponse>> GetStatus(Guid id, CancellationToken cancellationToken)
    {
        var allowAnyTenant = User.IsInRole(AppRoles.Admin);
        var status = await _sePayService.GetStatusAsync(
            id,
            allowAnyTenant ? null : _currentUser.TenantId,
            allowAnyTenant,
            cancellationToken);

        if (status is null)
        {
            return NotFound(new { message = "System payment not found." });
        }

        return Ok(status);
    }

    [AllowAnonymous]
    [HttpPost("sepay/webhook")]
    [HttpPost("/api/payments/sepay/webhook")]
    public async Task<IActionResult> HandleSePayWebhook([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var rawPayload = payload.GetRawText();
        var webhookPayload = payload.Deserialize<SepayWebhookPayload>(new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (webhookPayload is null)
        {
            return BadRequest(new { success = false, message = "Webhook payload is invalid." });
        }

        var result = await _sePayService.HandleWebhookAsync(
            webhookPayload,
            rawPayload,
            Request.Headers.Authorization.ToString(),
            Request.Headers["X-Api-Key"].ToString(),
            cancellationToken);

        if (!result.Authorized)
        {
            return Unauthorized(new { success = false, message = result.Message });
        }

        return Ok(new
        {
            success = result.Success,
            processed = result.Processed,
            message = result.Message,
            paymentId = result.PaymentId
        });
    }
}
