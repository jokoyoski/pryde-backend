using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IAdminPortalService
{
    Task<StaffResponseDto> InviteStaffAsync(InviteStaffRequestDto request, CancellationToken cancellationToken = default);
    Task<StaffListResponseDto> GetStaffAsync(AdminStaffRequestDto request, CancellationToken cancellationToken = default);
    Task<StaffResponseDto> GetStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<StaffResponseDto> ActivateStaffAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<StaffResponseDto> DeactivateStaffAsync(Guid staffId, Guid currentUserId, CancellationToken cancellationToken = default);
    Task<AdminUserDetailResponseDto> GetUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AdminUserDetailResponseDto> ActivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<AdminUserDetailResponseDto> DeactivateUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminUserSummaryResponseDto>> GetDriversAsync(AdminDriversRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailResponseDto> GetDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailResponseDto> ActivateDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<AdminDriverDetailResponseDto> DeactivateDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
    Task<AdminKycResponseDto> GetKycAsync(Guid kycId, CancellationToken cancellationToken = default);
    Task<AdminVehicleResponseDto> GetVehicleAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminWalletTransactionResponseDto>> GetWalletTransactionsAsync(AdminWalletTransactionsRequestDto request, CancellationToken cancellationToken = default);
    Task<AdminDashboardResponseDto> GetDashboardAsync(int days = 7, CancellationToken cancellationToken = default);
}
