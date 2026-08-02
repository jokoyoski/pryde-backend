using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminFinanceController(
    IFinancialService financialService,
    IAdminPortalService adminPortalService) : ControllerBase
{
    [HttpGet("finance/summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken) =>
        Ok(await financialService.GetSummaryAsync(cancellationToken));

    [HttpGet("wallet-transactions")]
    public async Task<IActionResult> GetWalletTransactions(
        [FromQuery] AdminWalletTransactionsRequestDto request,
        CancellationToken cancellationToken) =>
        Ok(await adminPortalService.GetWalletTransactionsAsync(request, cancellationToken));
}
