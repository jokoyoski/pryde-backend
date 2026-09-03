using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;

namespace Pryde.Services.Service.Implementation;

public class TripService(
    IUnitOfWork unitOfWork,
    IFareCalculator fareCalculator,
    IRouteMatchingService routeMatchingService,
    IOptions<PricingSettings> pricingSettings,
    IOptions<TripSettings> tripSettings,
    IFinancialService financialService,
    INotificationService notificationService) : ITripService
{
    private readonly PricingSettings _pricingSettings = pricingSettings.Value;
    private readonly TripSettings _tripSettings = tripSettings.Value;

    public TripService(
        IUnitOfWork unitOfWork,
        IFareCalculator fareCalculator,
        IRouteMatchingService routeMatchingService,
        IOptions<PricingSettings> pricingSettings)
        : this(
            unitOfWork,
            fareCalculator,
            routeMatchingService,
            pricingSettings,
            Options.Create(new TripSettings()),
            new FinancialService(unitOfWork),
            new NotificationService(unitOfWork))
    {
    }

    public TripService(
        IUnitOfWork unitOfWork,
        IFareCalculator fareCalculator,
        IRouteMatchingService routeMatchingService,
        IOptions<PricingSettings> pricingSettings,
        IFinancialService financialService)
        : this(
            unitOfWork,
            fareCalculator,
            routeMatchingService,
            pricingSettings,
            Options.Create(new TripSettings()),
            financialService,
            new NotificationService(unitOfWork))
    {
    }

    public async Task<TripDetailsResponseDto> CreateAsync(
        Guid driverId,
        CreateTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await CreateInternalAsync(
            driverId,
            request,
            null,
            cancellationToken);
    }

    public async Task ValidateRecurringTemplateAsync(
        Guid driverId,
        CreateTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        await ValidateCreationAsync(
            driverId,
            request,
            cancellationToken);
    }

    public async Task<TripDetailsResponseDto> CreateRecurringOccurrenceAsync(
        Guid driverId,
        Guid recurringTripId,
        CreateTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        return await CreateInternalAsync(
            driverId,
            request,
            recurringTripId,
            cancellationToken);
    }

    private async Task<TripDetailsResponseDto> CreateInternalAsync(
        Guid driverId,
        CreateTripRequestDto request,
        Guid? recurringTripId,
        CancellationToken cancellationToken)
    {
        var (vehicle, bookingWindowMinutes) = await ValidateCreationAsync(
            driverId,
            request,
            cancellationToken);

        var fare = fareCalculator.Calculate(request.DistanceKm, request.EstimatedDurationMinutes, vehicle.Capacity);
        var trip = new Trip
        {
            DriverId = driverId,
            VehicleId = vehicle.Id,
            OriginLatitude = request.OriginLatitude,
            OriginLongitude = request.OriginLongitude,
            OriginAddress = request.OriginAddress.Trim(),
            DestinationLatitude = request.DestinationLatitude,
            DestinationLongitude = request.DestinationLongitude,
            DestinationAddress = request.DestinationAddress.Trim(),
            RoutePolyline = string.IsNullOrWhiteSpace(request.RoutePolyline) ? null : request.RoutePolyline.Trim(),
            DistanceKm = request.DistanceKm,
            EstimatedDurationMinutes = request.EstimatedDurationMinutes,
            DepartureTime = request.DepartureTime.ToUniversalTime(),
            AvailableSeats = request.AvailableSeats,
            AllowLuggage = request.AllowLuggage,
            BookingWindowMinutes = bookingWindowMinutes,
            RecurringTripId = recurringTripId,
            TripFare = fare.TotalTripCost,
            SeatPrice = fare.SeatPrice,
            ServiceChargePercentage = fare.ServiceChargePercentage,
            Status = TripStatus.Scheduled
        };

        await unitOfWork.Trips.CreateAsync(trip, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(trip.Id, cancellationToken);
    }

    private async Task<(Vehicle Vehicle, int BookingWindowMinutes)>
        ValidateCreationAsync(
        Guid driverId,
        CreateTripRequestDto request,
        CancellationToken cancellationToken)
    {
        var bookingWindowMinutes = ResolveBookingWindowMinutes(
            request.BookingWindowMinutes);
        ValidateTrip(request.OriginLatitude, request.OriginLongitude,
            request.DestinationLatitude, request.DestinationLongitude,
            request.OriginAddress, request.DestinationAddress,
            request.DistanceKm, request.EstimatedDurationMinutes,
            request.DepartureTime, request.AvailableSeats,
            bookingWindowMinutes);

        await EnsureDriverAsync(driverId, cancellationToken);
        await EnsureApprovedKycAsync(driverId, cancellationToken);
        var vehicle = await GetOwnedActiveVehicleAsync(
            request.VehicleId,
            driverId,
            cancellationToken);
        if (request.AvailableSeats > vehicle.Capacity)
            throw new ValidationException(
                "Available seats cannot exceed vehicle capacity.");

        return (vehicle, bookingWindowMinutes);
    }

    public async Task<IReadOnlyList<TripSummaryResponseDto>> SearchAsync(
        SearchTripsRequestDto request,
        CancellationToken cancellationToken = default)
    {
        request ??= new SearchTripsRequestDto();
        var hasAnyCoordinate = request.OriginLatitude.HasValue || request.OriginLongitude.HasValue
            || request.DestinationLatitude.HasValue || request.DestinationLongitude.HasValue;
        var hasAllCoordinates = request.OriginLatitude.HasValue && request.OriginLongitude.HasValue
            && request.DestinationLatitude.HasValue && request.DestinationLongitude.HasValue;

        if (hasAnyCoordinate && !hasAllCoordinates)
            throw new ValidationException("Origin and destination coordinates must all be supplied for route matching.");
        if (hasAllCoordinates)
            ValidateCoordinates(request.OriginLatitude!.Value, request.OriginLongitude!.Value,
                request.DestinationLatitude!.Value, request.DestinationLongitude!.Value);

        var hasNearbyCoordinates =
            request.Latitude.HasValue && request.Longitude.HasValue;
        if (hasNearbyCoordinates)
        {
            ValidatePickupCoordinates(
                request.Latitude!.Value,
                request.Longitude!.Value);
            if (_pricingSettings.PickupRadiusKm <= 0)
            {
                throw new ValidationException(
                    "Pickup radius must be greater than zero.");
            }
        }

        var requiredSeats = request.RequiredSeats ?? 1;
        if (requiredSeats <= 0)
            throw new ValidationException("Required seats must be greater than zero.");

        var radius = request.PickupRadiusKm ?? _pricingSettings.PickupRadiusKm;
        if (radius <= 0)
            throw new ValidationException("Pickup radius must be greater than zero.");

        var trips = await unitOfWork.Trips.SearchAsync(
            DateTime.UtcNow, request.DepartureDate, request.RequiresLuggage,
            requiredSeats,
            hasNearbyCoordinates ? request.Latitude : null,
            hasNearbyCoordinates ? request.Longitude : null,
            hasNearbyCoordinates ? radius : null,
            cancellationToken);

        if (hasAllCoordinates)
        {
            trips = trips.Where(t => routeMatchingService.IsPassengerOnRoute(
                t.OriginLatitude, t.OriginLongitude,
                t.DestinationLatitude, t.DestinationLongitude,
                t.RoutePolyline,
                request.OriginLatitude!.Value, request.OriginLongitude!.Value,
                request.DestinationLatitude!.Value, request.DestinationLongitude!.Value,
                radius)).ToList();
        }

        return trips.Select(MapSummary).ToList();
    }

    public async Task<CustomerTripDetailsResponseDto> GetByIdAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetByIdWithDetailsAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        var rating = await unitOfWork.TripRatings.GetSummaryAsync(
            trip.DriverId,
            cancellationToken);
        return MapCustomerDetails(trip, rating.AverageRating);
    }

    public async Task<IReadOnlyList<TripSummaryResponseDto>> GetMineAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        var trips = await unitOfWork.Trips.GetByDriverIdAsync(driverId, cancellationToken);
        return trips.Select(MapSummary).ToList();
    }

    public async Task<DriverDashboardTripSummaryResponseDto?> GetNextUpcomingAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetNextUpcomingByDriverIdAsync(
            driverId,
            utcNow,
            cancellationToken);

        return trip is null ? null : MapDashboardSummary(trip);
    }

    public async Task<IReadOnlyList<DriverDashboardTripSummaryResponseDto>>
        GetLatestCompletedAsync(
        Guid driverId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var trips = await unitOfWork.Trips.GetLatestCompletedByDriverIdAsync(
            driverId,
            count,
            cancellationToken);

        return trips.Select(MapDashboardSummary).ToList();
    }

    public async Task<TripDetailsResponseDto> UpdateAsync(
        Guid tripId,
        Guid driverId,
        UpdateTripRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var bookingWindowMinutes = ResolveBookingWindowMinutes(
            request.BookingWindowMinutes);
        ValidateTrip(request.OriginLatitude, request.OriginLongitude,
            request.DestinationLatitude, request.DestinationLongitude,
            request.OriginAddress, request.DestinationAddress,
            request.DistanceKm, request.EstimatedDurationMinutes,
            request.DepartureTime, request.AvailableSeats,
            bookingWindowMinutes);

        var trip = await unitOfWork.Trips.GetByIdForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        if (trip.DriverId != driverId)
            throw new ForbiddenException("Only the trip owner can update this trip.");
        if (trip.Status != TripStatus.Scheduled || trip.DepartureTime <= DateTime.UtcNow)
            throw new ConflictException("Only a scheduled trip that has not departed can be updated.");

        var vehicle = await GetOwnedActiveVehicleAsync(request.VehicleId, driverId, cancellationToken);
        var approvedCount = trip.Bookings.Count(b => b.Status == BookingStatus.Approved);
        if (request.AvailableSeats + approvedCount > vehicle.Capacity)
            throw new ValidationException("Available seats plus approved bookings cannot exceed vehicle capacity.");

        var fare = fareCalculator.Calculate(request.DistanceKm, request.EstimatedDurationMinutes, vehicle.Capacity);
        trip.VehicleId = vehicle.Id;
        trip.OriginLatitude = request.OriginLatitude;
        trip.OriginLongitude = request.OriginLongitude;
        trip.OriginAddress = request.OriginAddress.Trim();
        trip.DestinationLatitude = request.DestinationLatitude;
        trip.DestinationLongitude = request.DestinationLongitude;
        trip.DestinationAddress = request.DestinationAddress.Trim();
        trip.RoutePolyline = string.IsNullOrWhiteSpace(request.RoutePolyline) ? null : request.RoutePolyline.Trim();
        trip.DistanceKm = request.DistanceKm;
        trip.EstimatedDurationMinutes = request.EstimatedDurationMinutes;
        trip.DepartureTime = request.DepartureTime.ToUniversalTime();
        trip.AvailableSeats = request.AvailableSeats;
        trip.AllowLuggage = request.AllowLuggage;
        trip.BookingWindowMinutes = bookingWindowMinutes;
        trip.TripFare = fare.TotalTripCost;
        trip.SeatPrice = fare.SeatPrice;
        trip.ServiceChargePercentage = fare.ServiceChargePercentage;

        unitOfWork.Trips.Update(trip);
        await SaveWithConcurrencyHandlingAsync(cancellationToken);
        return await GetByIdAsync(trip.Id, cancellationToken);
    }

    public async Task CancelAsync(Guid tripId, Guid driverId, CancellationToken cancellationToken = default)
    {
        var trip = await unitOfWork.Trips.GetByIdForUpdateAsync(tripId, cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
        if (trip.DriverId != driverId)
            throw new ForbiddenException("Only the trip owner can cancel this trip.");
        if (trip.Status is TripStatus.InProgress or TripStatus.Completed or TripStatus.Cancelled
            || trip.DepartureTime <= DateTime.UtcNow)
            throw new ConflictException("This trip can no longer be cancelled.");

        var affectedBookings = trip.Bookings
            .Where(booking => booking.Status is BookingStatus.Pending or BookingStatus.Approved)
            .Select(booking => (booking.Id, booking.PassengerId))
            .ToList();
        var approvedCount = 0;
        foreach (var booking in trip.Bookings.Where(b => b.Status is BookingStatus.Pending or BookingStatus.Approved))
        {
            if (booking.Status == BookingStatus.Approved)
                approvedCount++;
            booking.Status = BookingStatus.Cancelled;
            unitOfWork.TripBookings.Update(booking);
        }

        trip.AvailableSeats = Math.Min(trip.Vehicle.Capacity, trip.AvailableSeats + approvedCount);
        trip.Status = TripStatus.Cancelled;
        unitOfWork.Trips.Update(trip);
        if (trip.Bookings.Any(booking => booking.PaidAt.HasValue))
            await financialService.RefundTripAsync(trip.Id, cancellationToken);
        else
            await SaveWithConcurrencyHandlingAsync(cancellationToken);

        foreach (var booking in affectedBookings)
        {
            await notificationService.TryCreateAsync(
                NewNotification(
                    booking.PassengerId,
                    NotificationType.BookingCancelled,
                    "Trip cancelled",
                    "The driver cancelled a trip connected to your booking.",
                    booking.Id,
                    nameof(TripBooking),
                    $"booking-cancelled:{booking.Id}:{booking.PassengerId}"),
                cancellationToken);
        }
    }

    public async Task<TripDetailsResponseDto> CompleteAsync(
        Guid tripId, Guid driverId, CancellationToken cancellationToken = default)
    {
        await financialService.CompleteTripAsync(tripId, driverId, cancellationToken);
        return await GetByIdAsync(tripId, cancellationToken);
    }

    public async Task<TripDetailsResponseDto> StartAsync(
        Guid tripId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var passengerIds = await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var trip = await GetTripForLifecycleAsync(
                    tripId,
                    transactionToken);

                if (trip.DriverId != driverId)
                {
                    throw new ForbiddenException(
                        "Only the trip driver can start this trip.");
                }

                if (trip.Status != TripStatus.Scheduled)
                {
                    throw new ConflictException(
                        "Only a scheduled trip can be started.");
                }

                var activeBookings = GetActiveBookings(trip);

                if (activeBookings.Count == 0)
                {
                    throw new ConflictException(
                        "At least one paid passenger is required to start the trip.");
                }

                trip.Status = TripStatus.PickupConfirmationPending;
                unitOfWork.Trips.Update(trip);
                await unitOfWork.SaveChangesAsync(transactionToken);
                return activeBookings
                    .Select(booking => booking.PassengerId)
                    .Distinct()
                    .ToList();
            },
            cancellationToken);

        var response = await GetLifecycleResponseAsync(
            tripId,
            cancellationToken);
        foreach (var passengerId in passengerIds)
        {
            await notificationService.TryCreateAsync(
                NewNotification(
                    passengerId,
                    NotificationType.PickupConfirmationRequired,
                    "Confirm your pickup",
                    "The driver started the trip. Please confirm your pickup.",
                    tripId,
                    nameof(Trip),
                    $"pickup-confirmation-required:{tripId}:{passengerId}"),
                cancellationToken);
        }

        return response;
    }

    public async Task<TripDetailsResponseDto> ConfirmPickupAsync(
        Guid tripId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var trip = await GetTripForLifecycleAsync(
                    tripId,
                    transactionToken);

                if (trip.Status !=
                    TripStatus.PickupConfirmationPending)
                {
                    throw new ConflictException(
                        "Pickup cannot be confirmed in the current trip state.");
                }

                var activeBookings = GetActiveBookings(trip);
                var booking = activeBookings.FirstOrDefault(item =>
                    item.PassengerId == passengerId);

                if (booking == null)
                {
                    throw new ForbiddenException(
                        "Only approved passengers with a paid booking can confirm pickup.");
                }

                if (booking.PickupConfirmed)
                {
                    throw new ConflictException(
                        "Pickup has already been confirmed.");
                }

                booking.PickupConfirmed = true;
                unitOfWork.TripBookings.Update(booking);

                if (activeBookings.All(item =>
                    item.PickupConfirmed))
                {
                    trip.Status = TripStatus.InProgress;
                    unitOfWork.Trips.Update(trip);
                }

                await unitOfWork.SaveChangesAsync(transactionToken);
                return true;
            },
            cancellationToken);

        return await GetLifecycleResponseAsync(
            tripId,
            cancellationToken);
    }

    public async Task<TripDetailsResponseDto> EndAsync(
        Guid tripId,
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        var passengerIds = await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var trip = await GetTripForLifecycleAsync(
                    tripId,
                    transactionToken);

                if (trip.DriverId != driverId)
                {
                    throw new ForbiddenException(
                        "Only the trip driver can end this trip.");
                }

                if (trip.Status != TripStatus.InProgress)
                {
                    throw new ConflictException(
                        "Only an in-progress trip can be ended.");
                }

                var activeBookings = GetActiveBookings(trip);
                var driverEndedAt = DateTime.UtcNow;
                trip.Status =
                    TripStatus.DropoffConfirmationPending;
                trip.DriverEndedAt = driverEndedAt;
                trip.ConfirmationDeadline =
                    driverEndedAt.AddHours(24);
                unitOfWork.Trips.Update(trip);
                await unitOfWork.SaveChangesAsync(transactionToken);
                return activeBookings
                    .Select(booking => booking.PassengerId)
                    .Distinct()
                    .ToList();
            },
            cancellationToken);

        var response = await GetLifecycleResponseAsync(
            tripId,
            cancellationToken);
        foreach (var passengerId in passengerIds)
        {
            await notificationService.TryCreateAsync(
                NewNotification(
                    passengerId,
                    NotificationType.DropoffConfirmationRequired,
                    "Confirm your drop-off",
                    "The driver ended the trip. Please confirm your drop-off.",
                    tripId,
                    nameof(Trip),
                    $"dropoff-confirmation-required:{tripId}:{passengerId}"),
                cancellationToken);
        }

        return response;
    }

    private static CreateNotificationRequest NewNotification(
        Guid userId,
        NotificationType type,
        string title,
        string message,
        Guid relatedEntityId,
        string relatedEntityType,
        string deduplicationKey)
    {
        return new CreateNotificationRequest
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            RelatedEntityId = relatedEntityId,
            RelatedEntityType = relatedEntityType,
            DeduplicationKey = deduplicationKey
        };
    }

    public async Task<TripDetailsResponseDto> ConfirmDropoffAsync(
        Guid tripId,
        Guid passengerId,
        CancellationToken cancellationToken = default)
    {
        var lifecycleResult = await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var trip = await GetTripForLifecycleAsync(
                    tripId,
                    transactionToken);

                if (trip.Status !=
                    TripStatus.DropoffConfirmationPending)
                {
                    throw new ConflictException(
                        "Drop-off cannot be confirmed in the current trip state.");
                }

                var activeBookings = GetActiveBookings(trip);
                var booking = activeBookings.FirstOrDefault(item =>
                    item.PassengerId == passengerId);

                if (booking == null)
                {
                    throw new ForbiddenException(
                        "Only passengers with an active paid booking can confirm drop-off.");
                }

                if (booking.Escrow?.Status != EscrowStatus.Held)
                {
                    throw new ConflictException(
                        "Drop-off can only be confirmed while the booking escrow is held.");
                }

                if (booking.DropoffConfirmed)
                {
                    throw new ConflictException(
                        "Drop-off has already been confirmed.");
                }

                booking.DropoffConfirmed = true;
                unitOfWork.TripBookings.Update(booking);
                var allConfirmed = activeBookings.All(item =>
                    item.DropoffConfirmed);

                if (!allConfirmed)
                {
                    await unitOfWork.SaveChangesAsync(
                        transactionToken);
                }

                return (
                    AllConfirmed: allConfirmed,
                    DriverId: trip.DriverId);
            },
            cancellationToken);

        if (lifecycleResult.AllConfirmed)
        {
            await financialService.CompleteTripAsync(
                tripId,
                lifecycleResult.DriverId,
                cancellationToken);
        }

        return await GetLifecycleResponseAsync(
            tripId,
            cancellationToken);
    }

    private async Task<TripDetailsResponseDto> GetLifecycleResponseAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        var response = await GetByIdAsync(
            tripId,
            cancellationToken);

        switch (response.Status)
        {
            case TripStatus.PickupConfirmationPending:
            {
                response.NextAction =
                    WorkflowNextAction.PassengerConfirmPickup;
                response.RequiredActor = WorkflowActor.Passenger;
                break;
            }
            case TripStatus.InProgress:
            {
                response.NextAction = WorkflowNextAction.DriverEndTrip;
                response.RequiredActor = WorkflowActor.Driver;
                break;
            }
            case TripStatus.DropoffConfirmationPending:
            {
                response.NextAction =
                    WorkflowNextAction.PassengerConfirmDropoff;
                response.RequiredActor = WorkflowActor.Passenger;
                break;
            }
            case TripStatus.Completed:
            {
                response.NextAction = WorkflowNextAction.SubmitReview;
                response.RequiredActor = WorkflowActor.Passenger;
                break;
            }
            default:
            {
                response.NextAction = WorkflowNextAction.None;
                response.RequiredActor = WorkflowActor.None;
                break;
            }
        }

        return response;
    }

    private async Task<Trip> GetTripForLifecycleAsync(
        Guid tripId,
        CancellationToken cancellationToken)
    {
        return await unitOfWork.Trips.GetByIdForUpdateAsync(
            tripId,
            cancellationToken)
            ?? throw new NotFoundException(nameof(Trip), tripId);
    }

    private static List<TripBooking> GetActiveBookings(Trip trip)
    {
        return trip.Bookings
            .Where(booking =>
                booking.Status == BookingStatus.Approved &&
                booking.PaidAt.HasValue)
            .ToList();
    }

    private async Task EnsureDriverAsync(Guid userId, CancellationToken cancellationToken)
    {
        var roles = await unitOfWork.UserRoles.GetByUserIdAsync(userId, cancellationToken);
        if (!roles.Any(r => r.Role.Name == RoleType.Driver.ToString()))
            throw new ForbiddenException("Only users with the Driver role can create trips.");
    }

    private async Task EnsureApprovedKycAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var kyc = await unitOfWork.KycVerifications.GetByUserIdAsync(
            userId,
            cancellationToken);
        if (kyc?.Status != KycStatus.Approved)
        {
            throw new ForbiddenException(
                "Approved KYC is required before creating trips.");
        }
    }

    private async Task<Vehicle> GetOwnedActiveVehicleAsync(Guid vehicleId, Guid driverId, CancellationToken cancellationToken)
    {
        var vehicle = await unitOfWork.Vehicles.GetByIdAsync(vehicleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Vehicle), vehicleId);
        if (vehicle.UserId != driverId)
            throw new ForbiddenException("The selected vehicle does not belong to the authenticated driver.");
        if (!vehicle.IsActive ||
            vehicle.OnboardingStatus != VehicleOnboardingStatus.Approved)
        {
            throw new ConflictException(
                "The selected vehicle must be approved and active.");
        }
        if (vehicle.Capacity <= 0)
            throw new ConflictException("The selected vehicle has an invalid passenger capacity.");
        return vehicle;
    }

    private async Task SaveWithConcurrencyHandlingAsync(CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The trip changed while this request was being processed. Please try again.");
        }
    }

    private static void ValidateTrip(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude,
        string originAddress, string destinationAddress,
        double distanceKm, int durationMinutes, DateTime departureTime,
        int availableSeats, int bookingWindowMinutes)
    {
        ValidateCoordinates(originLatitude, originLongitude, destinationLatitude, destinationLongitude);
        if (string.IsNullOrWhiteSpace(originAddress) || string.IsNullOrWhiteSpace(destinationAddress))
            throw new ValidationException("Origin and destination addresses are required.");
        if (distanceKm <= 0) throw new ValidationException("Distance must be greater than zero.");
        if (durationMinutes <= 0) throw new ValidationException("Estimated duration must be greater than zero.");
        if (availableSeats <= 0) throw new ValidationException("Available seats must be greater than zero.");
        if (bookingWindowMinutes <= 0)
            throw new ValidationException(
                "Booking window must be greater than zero minutes.");
        var utcNow = DateTime.UtcNow;
        var departureUtc = departureTime.ToUniversalTime();
        if (departureUtc <= utcNow)
            throw new ValidationException("Departure time must be in the future.");
        if (TripBookingWindow.GetClosesAtUtc(
                departureUtc,
                bookingWindowMinutes) <= utcNow)
        {
            throw new ValidationException(
                "The booking cutoff must be in the future.");
        }
    }

    private int ResolveBookingWindowMinutes(
        int? bookingWindowMinutes)
    {
        return bookingWindowMinutes ??
            _tripSettings.DefaultBookingWindowMinutes;
    }

    private static void ValidateCoordinates(
        double originLatitude, double originLongitude,
        double destinationLatitude, double destinationLongitude)
    {
        if (originLatitude is < -90 or > 90 || destinationLatitude is < -90 or > 90)
            throw new ValidationException("Latitude must be between -90 and 90.");
        if (originLongitude is < -180 or > 180 || destinationLongitude is < -180 or > 180)
            throw new ValidationException("Longitude must be between -180 and 180.");
    }

    private static void ValidatePickupCoordinates(
        double latitude,
        double longitude)
    {
        if (latitude is < -90 or > 90)
            throw new ValidationException("Latitude must be between -90 and 90.");
        if (longitude is < -180 or > 180)
            throw new ValidationException("Longitude must be between -180 and 180.");
    }

    private TripSummaryResponseDto MapSummary(Trip trip)
    {
        var serviceCharge = _pricingSettings.CalculatePassengerServiceCharge(
            trip.SeatPrice,
            trip.ServiceChargePercentage);
        return new TripSummaryResponseDto
        {
            TripId = trip.Id,
            IsRecurring = trip.RecurringTripId.HasValue,
            RecurringTripId = trip.RecurringTripId,
            DriverId = trip.DriverId,
            DriverName = trip.Driver?.Profile is null ? string.Empty : $"{trip.Driver.Profile.FirstName} {trip.Driver.Profile.LastName}".Trim(),
            VehicleId = trip.VehicleId,
            VehicleLicensePlateNumber = trip.Vehicle?.LicensePlateNumber ?? string.Empty,
            VehicleCapacity = trip.Vehicle?.Capacity ?? 0,
            VehicleImageUrls = trip.Vehicle?.Images.OrderByDescending(i => i.IsPrimary).Select(i => i.ImageUrl).ToList() ?? [],
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

    private static DriverDashboardTripSummaryResponseDto MapDashboardSummary(
        DriverDashboardTripSummaryData trip)
    {
        return new DriverDashboardTripSummaryResponseDto
        {
            TripId = trip.TripId,
            OriginAddress = trip.OriginAddress,
            DestinationAddress = trip.DestinationAddress,
            DepartureTime = trip.DepartureTime,
            Status = trip.Status,
            SeatPrice = trip.SeatPrice,
            AvailableSeats = trip.AvailableSeats,
            VehicleLicensePlateNumber = trip.VehicleLicensePlateNumber,
            VehicleImageUrl = trip.VehicleImageUrl
        };
    }

    private TripDetailsResponseDto MapDetails(Trip trip)
    {
        var summary = MapSummary(trip);
        return new TripDetailsResponseDto
        {
            TripId = summary.TripId,
            IsRecurring = summary.IsRecurring,
            RecurringTripId = summary.RecurringTripId,
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
            PendingBookingCount = trip.Bookings.Count(b => b.Status == BookingStatus.Pending),
            ApprovedBookingCount = trip.Bookings.Count(b => b.Status == BookingStatus.Approved)
        };
    }

    private CustomerTripDetailsResponseDto MapCustomerDetails(
        Trip trip,
        double averageRating)
    {
        var details = MapDetails(trip);
        var profile = trip.Driver?.Profile;
        var vehicle = trip.Vehicle;
        return new CustomerTripDetailsResponseDto
        {
            NextAction = details.NextAction,
            RequiredActor = details.RequiredActor,
            TripId = details.TripId,
            IsRecurring = details.IsRecurring,
            RecurringTripId = details.RecurringTripId,
            DriverId = details.DriverId,
            DriverName = details.DriverName,
            VehicleId = details.VehicleId,
            VehicleLicensePlateNumber = details.VehicleLicensePlateNumber,
            VehicleCapacity = details.VehicleCapacity,
            VehicleImageUrls = details.VehicleImageUrls,
            OriginAddress = details.OriginAddress,
            OriginLatitude = details.OriginLatitude,
            OriginLongitude = details.OriginLongitude,
            DestinationAddress = details.DestinationAddress,
            DestinationLatitude = details.DestinationLatitude,
            DestinationLongitude = details.DestinationLongitude,
            RoutePolyline = details.RoutePolyline,
            DepartureTime = details.DepartureTime,
            AvailableSeats = details.AvailableSeats,
            AllowLuggage = details.AllowLuggage,
            DistanceKm = details.DistanceKm,
            EstimatedDurationMinutes = details.EstimatedDurationMinutes,
            TripFare = details.TripFare,
            SeatPrice = details.SeatPrice,
            ServiceChargePercentage = details.ServiceChargePercentage,
            PassengerServiceCharge = details.PassengerServiceCharge,
            PassengerTotal = details.PassengerTotal,
            BookingWindowMinutes = details.BookingWindowMinutes,
            Status = details.Status,
            CreatedAt = details.CreatedAt,
            PendingBookingCount = details.PendingBookingCount,
            ApprovedBookingCount = details.ApprovedBookingCount,
            Driver = new DriverSummaryDto
            {
                Id = trip.DriverId,
                FullName = profile is null
                    ? string.Empty
                    : $"{profile.FirstName} {profile.LastName}".Trim(),
                ProfileImageUrl = profile?.ProfilePhotoUrl,
                AverageRating = Math.Round(averageRating, 2)
            },
            Vehicle = new VehicleSummaryDto
            {
                Id = trip.VehicleId,
                Make = vehicle?.Make,
                Model = vehicle?.Model,
                Year = vehicle?.ManufacturingYear,
                Color = vehicle?.Colour,
                PlateNumber = vehicle?.LicensePlateNumber ?? string.Empty,
                PrimaryImageUrl = vehicle?.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image => image.ImageType)
                    .ThenBy(image => image.Id)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault(),
                Capacity = vehicle?.Capacity ?? 0
            }
        };
    }
}
