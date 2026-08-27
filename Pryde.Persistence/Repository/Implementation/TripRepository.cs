using Microsoft.EntityFrameworkCore;
using Pryde.Domain.Entities;
using Pryde.Domain.Common;
using Pryde.Domain.Enums;
using Pryde.Persistence.Context;
using Pryde.Persistence.Repository.Interfaces;

namespace Pryde.Persistence.Repository.Implementations;

public class TripRepository(PrydeDbContext context) : ITripRepository
{
    private const double EarthRadiusKm = 6371d;
    private const double DegreesToRadians = Math.PI / 180d;

    public async Task<Trip?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithBookingsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Include(t => t.Bookings)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .Include(t => t.Vehicle)
            .Include(t => t.Bookings)
                .ThenInclude(booking => booking.Escrow)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<Trip?> GetByIdWithVehicleForUpdateAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var trip = await context.Trips
            .FromSqlInterpolated(
                $"""
                SELECT *
                FROM "Trips"
                WHERE "Id" = {id}
                FOR UPDATE
                """)
            .SingleOrDefaultAsync(cancellationToken);

        if (trip is not null)
        {
            await context.Entry(trip)
                .Reference(item => item.Vehicle)
                .LoadAsync(cancellationToken);
        }

        return trip;
    }

    public async Task<IReadOnlyList<Trip>> GetByDriverIdAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Where(t => t.DriverId == driverId)
            .OrderByDescending(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> SearchAsync(
        DateTime utcNow,
        DateTime? departureDate,
        bool? requiresLuggage,
        int requiredSeats,
        double? pickupLatitude,
        double? pickupLongitude,
        double? pickupRadiusKm,
        CancellationToken cancellationToken = default)
    {
        var query = context.Trips
            .AsNoTracking()
            .Include(t => t.Driver)
                .ThenInclude(d => d.Profile)
            .Include(t => t.Vehicle)
                .ThenInclude(v => v.Images)
            .Where(t => t.Status == TripStatus.Scheduled
                && t.DepartureTime > utcNow
                && t.AvailableSeats >= requiredSeats)
            .Where(TripBookingWindow.IsOpenAtUtc(utcNow));

        if (departureDate.HasValue)
        {
            var start = DateTime.SpecifyKind(departureDate.Value.Date, DateTimeKind.Utc);
            var end = start.AddDays(1);
            query = query.Where(t => t.DepartureTime >= start && t.DepartureTime < end);
        }

        if (requiresLuggage == true)
            query = query.Where(t => t.AllowLuggage);

        if (pickupLatitude.HasValue &&
            pickupLongitude.HasValue &&
            pickupRadiusKm.HasValue)
        {
            var latitude = pickupLatitude.Value;
            var longitude = pickupLongitude.Value;
            var radiusKm = pickupRadiusKm.Value;
            query = query.Where(trip =>
                2d * EarthRadiusKm * Math.Asin(Math.Sqrt(
                    Math.Pow(Math.Sin(
                        (trip.OriginLatitude - latitude) *
                        DegreesToRadians / 2d), 2d) +
                    Math.Cos(latitude * DegreesToRadians) *
                    Math.Cos(trip.OriginLatitude * DegreesToRadians) *
                    Math.Pow(Math.Sin(
                        (trip.OriginLongitude - longitude) *
                        DegreesToRadians / 2d), 2d))) <= radiusKm);
        }

        return await query
            .OrderBy(t => t.DepartureTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(t => t.Status == TripStatus.Scheduled && t.AvailableSeats > 0)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> RecurringOccurrenceExistsAsync(
        Guid recurringTripId,
        DateTime departureTime,
        CancellationToken cancellationToken = default)
    {
        return context.Trips.AsNoTracking().AnyAsync(
            trip => trip.RecurringTripId == recurringTripId &&
                trip.DepartureTime == departureTime,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Trip>> GetOpenRecurringOccurrencesAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        utcNow = utcNow.ToUniversalTime();
        return await context.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.RecurringTripId.HasValue &&
                trip.RecurringTrip != null &&
                trip.RecurringTrip.CancelledAt == null &&
                trip.Status == TripStatus.Scheduled &&
                trip.DepartureTime > utcNow)
            .Where(TripBookingWindow.IsOpenAtUtc(utcNow))
            .OrderBy(trip => trip.DepartureTime)
            .ThenBy(trip => trip.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountCompletedByDriverIdAsync(
        Guid driverId,
        CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .CountAsync(
                trip =>
                    trip.DriverId == driverId &&
                    trip.Status == TripStatus.Completed,
                cancellationToken);
    }

    public async Task<DriverDashboardTripSummaryData?>
        GetNextUpcomingByDriverIdAsync(
        Guid driverId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        return await DashboardTrips(driverId)
            .Where(trip =>
                trip.DepartureTime > utcNow &&
                trip.Status != TripStatus.Completed &&
                trip.Status != TripStatus.Cancelled)
            .OrderBy(trip => trip.DepartureTime)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DriverDashboardTripSummaryData>>
        GetLatestCompletedByDriverIdAsync(
        Guid driverId,
        int count,
        CancellationToken cancellationToken = default)
    {
        return await DashboardTrips(driverId)
            .Where(trip => trip.Status == TripStatus.Completed)
            .OrderByDescending(trip => trip.DepartureTime)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>>
        GetExpiredConfirmationTripIdsAsync(
            DateTime utcNow,
            CancellationToken cancellationToken = default)
    {
        return await context.Trips
            .AsNoTracking()
            .Where(trip =>
                trip.Status ==
                    TripStatus.DropoffConfirmationPending &&
                trip.DriverEndedAt.HasValue &&
                trip.ConfirmationDeadline.HasValue &&
                trip.ConfirmationDeadline.Value <= utcNow)
            .OrderBy(trip => trip.ConfirmationDeadline)
            .Select(trip => trip.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<Trip> CreateAsync(Trip trip, CancellationToken cancellationToken = default)
    {
        await context.Trips.AddAsync(trip, cancellationToken);
        return trip;
    }

    public void Update(Trip trip) => context.Trips.Update(trip);
    public void Delete(Trip trip) => context.Trips.Remove(trip);

    private IQueryable<DriverDashboardTripSummaryData> DashboardTrips(
        Guid driverId)
    {
        return context.Trips
            .AsNoTracking()
            .Where(trip => trip.DriverId == driverId)
            .Select(trip => new DriverDashboardTripSummaryData
            {
                TripId = trip.Id,
                OriginAddress = trip.OriginAddress,
                DestinationAddress = trip.DestinationAddress,
                DepartureTime = trip.DepartureTime,
                Status = trip.Status,
                SeatPrice = trip.SeatPrice,
                AvailableSeats = trip.AvailableSeats,
                VehicleLicensePlateNumber =
                    trip.Vehicle.LicensePlateNumber,
                VehicleImageUrl = trip.Vehicle.Images
                    .OrderByDescending(image => image.IsPrimary)
                    .ThenBy(image =>
                        image.ImageType == VehicleImageType.FrontView
                            ? 0
                            : 1)
                    .ThenBy(image => image.ImageType)
                    .ThenBy(image => image.Id)
                    .Select(image => image.ImageUrl)
                    .FirstOrDefault()
            });
    }
}
