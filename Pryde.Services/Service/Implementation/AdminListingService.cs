using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class AdminListingService(IUnitOfWork unitOfWork) : IAdminListingService
{
    public async Task<PagedResponseDto<UserSummaryResponseDto>> GetUsersAsync(AdminUsersRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.AdminListings.GetUsersAsync(request.Role, request.Status, request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(user => new UserSummaryResponseDto
        {
            Id = user.Id,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            FirstName = user.Profile?.FirstName ?? string.Empty,
            LastName = user.Profile?.LastName ?? string.Empty,
            Status = user.Status.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            IsPhoneNumberVerified = user.IsPhoneNumberVerified,
            KycStatus = user.KycVerification?.Status.ToString() ?? "NotStarted",
            Roles = user.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList(),
            CreatedAt = user.CreatedAt
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<AdminKycResponseDto>> GetKycAsync(AdminKycRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.AdminListings.GetKycAsync(request.Status, request.Role, request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(kyc => new AdminKycResponseDto
        {
            Id = kyc.Id,
            UserId = kyc.UserId,
            Email = kyc.User.Email,
            FirstName = kyc.User.Profile?.FirstName ?? string.Empty,
            LastName = kyc.User.Profile?.LastName ?? string.Empty,
            Roles = kyc.User.UserRoles.Select(userRole => userRole.Role.Name).Distinct().ToList(),
            BiometricVerificationUrl = kyc.BiometricVerificationUrl,
            DriverLicenseUrl = kyc.DriverLicenseUrl,
            SecondaryIdentificationUrl = kyc.SecondaryIdentificationUrl,
            Status = kyc.Status,
            VerifiedAt = kyc.VerifiedAt
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<AdminVehicleResponseDto>> GetVehiclesAsync(AdminVehiclesRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.AdminListings.GetVehiclesAsync(request.IsActive, request.OwnerId, request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(vehicle => new AdminVehicleResponseDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            OwnerEmail = vehicle.User.Email,
            OwnerName = $"{vehicle.User.Profile?.FirstName} {vehicle.User.Profile?.LastName}".Trim(),
            LicensePlateNumber = vehicle.LicensePlateNumber,
            Capacity = vehicle.Capacity,
            IsActive = vehicle.IsActive,
            ImageUrls = vehicle.Images.Select(image => image.ImageUrl).ToList()
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<AdminVehicleDocumentResponseDto>> GetVehicleDocumentsAsync(AdminVehicleDocumentsRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.AdminListings.GetVehicleDocumentsAsync(request.VehicleId, request.OwnerId, request.DocumentType, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(document => new AdminVehicleDocumentResponseDto
        {
            Id = document.Id,
            VehicleId = document.VehicleId,
            OwnerId = document.Vehicle.UserId,
            OwnerEmail = document.Vehicle.User.Email,
            LicensePlateNumber = document.Vehicle.LicensePlateNumber,
            DocumentType = document.DocumentType,
            DocumentUrl = document.DocumentUrl,
            ExpiryDate = document.ExpiryDate
        }).ToList(), request, result.TotalCount);
    }

    private static PagedResponseDto<T> Page<T>(IReadOnlyList<T> items, PaginationRequestDto request, int totalCount)
    {
        return new PagedResponseDto<T>
        {
            Items = items,
            PageNumber = request.PageNumber,
            PageSize = request.PageSize,
            TotalCount = totalCount,
            TotalPages = (int)Math.Ceiling(totalCount / (double)request.PageSize)
        };
    }
}
