using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.BackgroundServices;
using Pryde.Services.Service.Implementation;
using Pryde.Services.Service.Interface;
using Pryde.Services.Settings;
using Pryde.Tests.TestInfrastructure;

namespace Pryde.Tests.Services;

public class BookingPaymentExpiryBackgroundServiceTests
{
    [Fact]
    public async Task FailedBookingDoesNotStopRemainingBookings()
    {
        var unitOfWork = new TestUnitOfWork();
        var failed = AddExpiredBooking(
            unitOfWork,
            false);
        var successful = AddExpiredBooking(
            unitOfWork,
            true);
        using var provider = Services(unitOfWork);
        using var service = BackgroundService(provider);

        await service.ProcessExpiredBookingsAsync();

        Assert.Equal(
            BookingStatus.Approved,
            failed.Status);
        Assert.Equal(
            BookingStatus.Cancelled,
            successful.Status);
        Assert.Equal(
            2,
            successful.Trip.AvailableSeats);
    }

    private static ServiceProvider Services(
        TestUnitOfWork unitOfWork)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IUnitOfWork>(unitOfWork);
        services.AddScoped<IFinancialService>(
            _ => new FinancialService(unitOfWork));
        return services.BuildServiceProvider();
    }

    private static BookingPaymentExpiryBackgroundService
        BackgroundService(ServiceProvider provider)
    {
        return new BookingPaymentExpiryBackgroundService(
            provider.GetRequiredService<
                IServiceScopeFactory>(),
            Options.Create(new BookingPaymentSettings
            {
                PaymentWindowMinutes = 15,
                ExpiryCheckIntervalMinutes = 1
            }),
            NullLogger<
                BookingPaymentExpiryBackgroundService>.Instance);
    }

    private static TripBooking AddExpiredBooking(
        TestUnitOfWork unitOfWork,
        bool addTrip)
    {
        var vehicle = new Vehicle
        {
            Capacity = 4
        };
        var trip = new Trip
        {
            VehicleId = vehicle.Id,
            Vehicle = vehicle,
            AvailableSeats = 1
        };
        var booking = new TripBooking
        {
            TripId = trip.Id,
            Trip = trip,
            PassengerId = Guid.NewGuid(),
            Status = BookingStatus.Approved,
            ApprovedAt = DateTime.UtcNow.AddMinutes(-2),
            PaymentExpiresAt =
                DateTime.UtcNow.AddMinutes(-1)
        };

        if (addTrip)
        {
            unitOfWork.TripRepository.Items.Add(trip);
        }

        unitOfWork.TripBookingRepository.Items.Add(
            booking);
        return booking;
    }
}
