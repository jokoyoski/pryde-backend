using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Pryde.Contracts.RequestModels;
using Pryde.Domain.Constants;
using Pryde.Services.Service.Interface;

namespace Pryde.Api.Controllers.V1;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/admin/ledger/transactions")]
[Authorize(Roles = RoleNames.AdminOrSuperAdmin)]
public class AdminLedgerController(IFinancialService financialService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] AdminLedgerTransactionsRequestDto request, CancellationToken cancellationToken) =>
        Ok(await financialService.GetTransactionsAsync(request, cancellationToken));

    [HttpGet("{transactionId:guid}")]
    public async Task<IActionResult> Get(Guid transactionId, CancellationToken cancellationToken) =>
        Ok(await financialService.GetTransactionAsync(transactionId, cancellationToken));
}
