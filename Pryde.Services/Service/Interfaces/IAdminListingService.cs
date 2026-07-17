using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;

namespace Pryde.Services.Service.Interface;

public interface IAdminListingService
{
    Task<PagedResponseDto<UserSummaryResponseDto>> GetUsersAsync(AdminUsersRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminKycResponseDto>> GetKycAsync(AdminKycRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminVehicleResponseDto>> GetVehiclesAsync(AdminVehiclesRequestDto request, CancellationToken cancellationToken = default);
    Task<PagedResponseDto<AdminVehicleDocumentResponseDto>> GetVehicleDocumentsAsync(AdminVehicleDocumentsRequestDto request, CancellationToken cancellationToken = default);
}
