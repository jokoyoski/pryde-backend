using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class AdminUserDeletionService(IUnitOfWork unitOfWork)
    : IAdminUserDeletionService
{
    public async Task DeleteAsync(
        Guid currentUserId,
        Guid? userId,
        string? email,
        CancellationToken cancellationToken = default)
    {
        var hasUserId = userId.HasValue;
        var hasEmail = !string.IsNullOrWhiteSpace(email);

        if (hasUserId == hasEmail)
        {
            throw new ValidationException(
                "Supply either userId or email, but not both.");
        }

        await unitOfWork.ExecuteInTransactionOnceAsync(
            async transactionCancellationToken =>
            {
                var user = hasUserId
                    ? await unitOfWork.Users.GetByIdAsync(
                        userId!.Value,
                        transactionCancellationToken)
                    : await unitOfWork.Users.GetByEmailAsync(
                        email!.Trim(),
                        transactionCancellationToken);

                if (user is null)
                {
                    throw new NotFoundException(
                        nameof(User),
                        hasUserId ? userId!.Value : email!.Trim());
                }

                if (user.Id == currentUserId)
                {
                    throw new ConflictException(
                        "A SuperAdmin cannot permanently delete their own account.");
                }

                if (await unitOfWork.Users.HasProtectedDeletionRecordsAsync(
                        user.Id,
                        transactionCancellationToken))
                {
                    throw new ConflictException(
                        "The user has financial or completed business records and cannot be permanently deleted.");
                }

                await unitOfWork.Users.DeleteWithRelatedDataAsync(
                    user.Id,
                    transactionCancellationToken);
                return true;
            },
            cancellationToken);
    }
}
