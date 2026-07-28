using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class AdminWalletService : IAdminWalletService
{
    private const string DefaultDescription =
        "Manual wallet funding for testing";
    private readonly IFinancialService _financialService;
    private readonly IUnitOfWork _unitOfWork;

    public AdminWalletService(
        IFinancialService financialService,
        IUnitOfWork unitOfWork)
    {
        _financialService = financialService;
        _unitOfWork = unitOfWork;
    }

    public async Task<AdminFundWalletResponseDto> FundWalletAsync(
        AdminFundWalletRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var userExists = await _unitOfWork.Users.ExistsByIdAsync(
            request.UserId,
            cancellationToken);

        if (!userExists)
        {
            throw new NotFoundException(
                "The user was not found.");
        }

        var description = DefaultDescription;

        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            description = request.Description.Trim();
        }

        var result = await _financialService.RecordTestWalletFundingAsync(
            request.UserId,
            request.Amount,
            description,
            cancellationToken);

        return new AdminFundWalletResponseDto
        {
            WalletId = result.Wallet.Id,
            UserId = result.Wallet.UserId,
            AmountFunded = result.Transaction.Amount,
            NewBalance = result.Wallet.Balance,
            TransactionReference = result.Transaction.Reference!,
            TransactionDate = result.Transaction.CreatedAt
        };
    }

    private static void ValidateRequest(
        AdminFundWalletRequest request)
    {
        if (request == null)
        {
            throw new ValidationException(
                "Request cannot be null.");
        }

        if (request.UserId == Guid.Empty)
        {
            throw new ValidationException(
                "User ID is required.");
        }

        if (request.Amount <= 0)
        {
            throw new ValidationException(
                "Amount must be greater than zero.");
        }

        if (request.Description != null &&
            request.Description.Trim().Length > 250)
        {
            throw new ValidationException(
                "Description cannot exceed 250 characters.");
        }
    }
}
