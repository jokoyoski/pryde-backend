using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class AdminListingService(IUnitOfWork unitOfWork) : IAdminListingService
{
    public async Task<PagedResponseDto<UserSummaryResponseDto>> GetUsersAsync(AdminUsersRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(request.CreatedFrom, request.CreatedTo, "CreatedFrom", "CreatedTo");
        ValidateUserSort(request.SortBy, request.SortDirection);
        var result = await unitOfWork.AdminListings.GetUsersAsync(
            request.Role, request.Status, request.Search, request.IsActive,
            request.IsEmailVerified, request.IsPhoneVerified, request.KycStatus,
            request.CreatedFrom, request.CreatedTo, request.SortBy, request.SortDirection,
            request.PageNumber, request.PageSize, cancellationToken);
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
        ValidateDateRange(request.DateFrom, request.DateTo, "DateFrom", "DateTo");
        var result = await unitOfWork.AdminListings.GetKycAsync(request.Status, request.Role, request.Provider, request.Search, request.DateFrom, request.DateTo, request.PageNumber, request.PageSize, cancellationToken);
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
            VerifiedAt = kyc.VerifiedAt,
            ProviderName = kyc.ProviderName,
            ProviderReference = kyc.ProviderReference,
            DojahReference = kyc.DojahReference,
            ProviderStatus = kyc.ProviderStatus,
            RejectionReason = kyc.RejectionReason,
            LastProviderUpdatedAt = kyc.LastProviderUpdatedAt
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<AdminVehicleResponseDto>> GetVehiclesAsync(AdminVehiclesRequestDto request, CancellationToken cancellationToken = default)
    {
        var result = await unitOfWork.AdminListings.GetVehiclesAsync(
            request.OnboardingStatus, request.IsActive, request.OwnerId, request.RegistrationType,
            request.Search, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(vehicle => new AdminVehicleResponseDto
        {
            Id = vehicle.Id,
            UserId = vehicle.UserId,
            OwnerEmail = vehicle.User.Email,
            OwnerName = $"{vehicle.User.Profile?.FirstName} {vehicle.User.Profile?.LastName}".Trim(),
            LicensePlateNumber = vehicle.LicensePlateNumber,
            VehicleOwnerName = vehicle.VehicleOwnerName,
            RegistrationType = vehicle.RegistrationType,
            VehicleType = vehicle.VehicleType,
            Make = vehicle.Make,
            Model = vehicle.Model,
            ManufacturingYear = vehicle.ManufacturingYear,
            Colour = vehicle.Colour,
            WalkAroundVideoUrl = vehicle.WalkAroundVideoUrl,
            PassengerSeatCount = vehicle.PassengerSeatCount,
            LuggageCapacity = vehicle.LuggageCapacity,
            Amenities = vehicle.Amenities.Select(x => x.AmenityType).Order().ToList(),
            AdditionalDetails = vehicle.AdditionalDetails,
            OnboardingStatus = vehicle.OnboardingStatus,
            RejectionReason = vehicle.RejectionReason,
            Capacity = vehicle.Capacity,
            IsActive = vehicle.IsActive,
            ImageUrls = vehicle.Images.Select(image => image.ImageUrl).ToList(),
            Images = vehicle.Images.Select(image => new VehicleImageResponseDto
            {
                Id = image.Id,
                ImageUrl = image.ImageUrl,
                ImageType = image.ImageType,
                IsPrimary = image.IsPrimary
            }).ToList(),
            Documents = vehicle.Documents.Select(document => new VehicleDocumentResponseDto
            {
                Id = document.Id,
                VehicleId = document.VehicleId,
                DocumentType = document.DocumentType,
                DocumentUrl = document.DocumentUrl,
                ExpiryDate = document.ExpiryDate,
                ReviewStatus = document.ReviewStatus,
                ReviewedBy = document.ReviewedBy,
                ReviewedAt = document.ReviewedAt,
                RejectionReason = document.RejectionReason
            }).ToList()
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<AdminVehicleDocumentResponseDto>> GetVehicleDocumentsAsync(AdminVehicleDocumentsRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(request.ExpiryFrom, request.ExpiryTo, "ExpiryFrom", "ExpiryTo");
        var result = await unitOfWork.AdminListings.GetVehicleDocumentsAsync(
            request.VehicleId, request.OwnerId, request.DocumentType, request.ReviewStatus,
            request.ExpiryFrom, request.ExpiryTo, request.PageNumber, request.PageSize,
            cancellationToken);
        return Page(result.Items.Select(document => new AdminVehicleDocumentResponseDto
        {
            Id = document.Id,
            VehicleId = document.VehicleId,
            OwnerId = document.Vehicle.UserId,
            OwnerEmail = document.Vehicle.User.Email,
            LicensePlateNumber = document.Vehicle.LicensePlateNumber,
            DocumentType = document.DocumentType,
            DocumentUrl = document.DocumentUrl,
            ExpiryDate = document.ExpiryDate,
            ReviewStatus = document.ReviewStatus,
            ReviewedBy = document.ReviewedBy,
            ReviewedAt = document.ReviewedAt,
            RejectionReason = document.RejectionReason
        }).ToList(), request, result.TotalCount);
    }

    public async Task<PagedResponseDto<TripSummaryResponseDto>> GetTripsAsync(
        AdminTripsRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(
            request.DepartureFrom, request.DepartureTo, "DepartureFrom", "DepartureTo");
        var result = await unitOfWork.AdminListings.GetTripsAsync(
            request.Search, request.DriverId, request.Status, request.DepartureFrom,
            request.DepartureTo, request.IsRecurring, request.IsActive,
            request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(MapTripSummary).ToList(), request, result.TotalCount);
    }

    public async Task<TripDetailsResponseDto> GetTripAsync(
        Guid tripId, CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.AdminListings.GetTripAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        return MapTripDetails(trip);
    }

    public async Task<PagedResponseDto<TripBookingResponseDto>> GetBookingsAsync(
        AdminBookingsRequestDto request, CancellationToken cancellationToken = default)
    {
        ValidateDateRange(request.DateFrom, request.DateTo, "DateFrom", "DateTo");
        var result = await unitOfWork.AdminListings.GetBookingsAsync(
            request.UserId, request.DriverId, request.TripId, request.Status,
            request.DateFrom, request.DateTo, request.PageNumber, request.PageSize,
            cancellationToken);
        return Page(result.Items.Select(MapBooking).ToList(), request, result.TotalCount);
    }

    public async Task<TripBookingResponseDto> GetBookingAsync(
        Guid bookingId, CancellationToken cancellationToken = default)
    {
        var booking = await unitOfWork.AdminListings.GetBookingAsync(bookingId, cancellationToken)
            ?? throw new NotFoundException(nameof(TripBooking), bookingId);
        return MapBooking(booking);
    }

    private static TripSummaryResponseDto MapTripSummary(Trip trip)
    {
        var serviceCharge = Math.Round(
            trip.SeatPrice * trip.ServiceChargePercentage / 100m, 2);
        return new TripSummaryResponseDto
        {
            TripId = trip.Id,
            DriverId = trip.DriverId,
            DriverName = trip.Driver?.Profile is null
                ? string.Empty
                : $"{trip.Driver.Profile.FirstName} {trip.Driver.Profile.LastName}".Trim(),
            VehicleId = trip.VehicleId,
            VehicleLicensePlateNumber = trip.Vehicle?.LicensePlateNumber ?? string.Empty,
            VehicleCapacity = trip.Vehicle?.Capacity ?? 0,
            VehicleImageUrls = trip.Vehicle?.Images
                .OrderByDescending(image => image.IsPrimary)
                .Select(image => image.ImageUrl).ToList() ?? [],
            OriginAddress = trip.OriginAddress,
            OriginLatitude = trip.OriginLatitude,
            OriginLongitude = trip.OriginLongitude,
            DestinationAddress = trip.DestinationAddress,
            DestinationLatitude = trip.DestinationLatitude,
            DestinationLongitude = trip.DestinationLongitude,
            RoutePolyline = trip.RoutePolyline,
            DepartureTime = trip.DepartureTime,
            AvailableSeats = trip.AvailableSeats,
            AllowLuggage = trip.AllowLuggage,
            DistanceKm = trip.DistanceKm,
            EstimatedDurationMinutes = trip.EstimatedDurationMinutes,
            TripFare = trip.TripFare,
            SeatPrice = trip.SeatPrice,
            ServiceChargePercentage = trip.ServiceChargePercentage,
            PassengerServiceCharge = serviceCharge,
            PassengerTotal = trip.SeatPrice + serviceCharge,
            BookingWindowMinutes = trip.BookingWindowMinutes,
            Status = trip.Status,
            CreatedAt = trip.CreatedAt
        };
    }

    private static TripDetailsResponseDto MapTripDetails(Trip trip)
    {
        var summary = MapTripSummary(trip);
        return new TripDetailsResponseDto
        {
            TripId = summary.TripId,
            DriverId = summary.DriverId,
            DriverName = summary.DriverName,
            VehicleId = summary.VehicleId,
            VehicleLicensePlateNumber = summary.VehicleLicensePlateNumber,
            VehicleCapacity = summary.VehicleCapacity,
            VehicleImageUrls = summary.VehicleImageUrls,
            OriginAddress = summary.OriginAddress,
            OriginLatitude = summary.OriginLatitude,
            OriginLongitude = summary.OriginLongitude,
            DestinationAddress = summary.DestinationAddress,
            DestinationLatitude = summary.DestinationLatitude,
            DestinationLongitude = summary.DestinationLongitude,
            RoutePolyline = summary.RoutePolyline,
            DepartureTime = summary.DepartureTime,
            AvailableSeats = summary.AvailableSeats,
            AllowLuggage = summary.AllowLuggage,
            DistanceKm = summary.DistanceKm,
            EstimatedDurationMinutes = summary.EstimatedDurationMinutes,
            TripFare = summary.TripFare,
            SeatPrice = summary.SeatPrice,
            ServiceChargePercentage = summary.ServiceChargePercentage,
            PassengerServiceCharge = summary.PassengerServiceCharge,
            PassengerTotal = summary.PassengerTotal,
            BookingWindowMinutes = summary.BookingWindowMinutes,
            Status = summary.Status,
            CreatedAt = summary.CreatedAt,
            PendingBookingCount = trip.Bookings.Count(
                booking => booking.Status == BookingStatus.Pending),
            ApprovedBookingCount = trip.Bookings.Count(
                booking => booking.Status == BookingStatus.Approved)
        };
    }

    private static TripBookingResponseDto MapBooking(TripBooking booking) => new()
    {
        BookingId = booking.Id,
        TripId = booking.TripId,
        PassengerId = booking.PassengerId,
        PassengerName = booking.Passenger?.Profile is null
            ? null
            : $"{booking.Passenger.Profile.FirstName} {booking.Passenger.Profile.LastName}".Trim(),
        TripOrigin = booking.Trip.OriginAddress,
        TripDestination = booking.Trip.DestinationAddress,
        DepartureTime = booking.Trip.DepartureTime,
        SeatPrice = booking.SeatPrice,
        ServiceCharge = booking.ServiceCharge,
        TotalAmount = booking.TotalAmount,
        Status = booking.Status,
        RequestedAt = booking.RequestedAt,
        ApprovedAt = booking.ApprovedAt,
        PaymentExpiresAt = booking.PaymentExpiresAt
    };

    private static void ValidateDateRange(
        DateTime? from, DateTime? to, string fromName, string toName)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ValidationException($"{fromName} cannot be later than {toName}.");
    }

    private static void ValidateUserSort(string? sortBy, string? sortDirection)
    {
        if (!string.IsNullOrWhiteSpace(sortBy) &&
            !new[] { "createdAt", "email", "status", "firstName", "lastName" }
                .Contains(sortBy.Trim(), StringComparer.OrdinalIgnoreCase))
            throw new ValidationException(
                "SortBy must be createdAt, email, status, firstName or lastName.");
        if (!string.IsNullOrWhiteSpace(sortDirection) &&
            !sortDirection.Equals("asc", StringComparison.OrdinalIgnoreCase) &&
            !sortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase))
            throw new ValidationException("SortDirection must be asc or desc.");
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
