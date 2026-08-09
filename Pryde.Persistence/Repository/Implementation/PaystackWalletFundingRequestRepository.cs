using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class PaystackWalletFundingRequestRepository(PrydeDbContext context)
    : IPaystackWalletFundingRequestRepository
{
    public async Task<PaystackWalletFundingRequest> CreateAsync(
        PaystackWalletFundingRequest fundingRequest,
        CancellationToken cancellationToken = default)
    {
        await context.PaystackWalletFundingRequests.AddAsync(
            fundingRequest,
            cancellationToken);
        return fundingRequest;
    }

    public Task<PaystackWalletFundingRequest?> GetByReferenceAsync(
        string reference,
        CancellationToken cancellationToken = default)
    {
        return context.PaystackWalletFundingRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                fundingRequest => fundingRequest.Reference == reference,
                cancellationToken);
    }

    public Task<PaystackWalletFundingRequest?> GetByIdForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.PaystackWalletFundingRequests.FirstOrDefaultAsync(
            fundingRequest => fundingRequest.Id == id,
            cancellationToken);
    }

    public Task<PaystackWalletFundingRequest?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return context.PaystackWalletFundingRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                fundingRequest => fundingRequest.Id == id,
                cancellationToken);
    }
}
