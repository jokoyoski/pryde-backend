using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pryde.Contracts.RequestModels;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Common.Exceptions;
using Pryde.Domain.Entities;
using Pryde.Domain.Enums;
using Pryde.Persistence.Repository.Interfaces;
using Pryde.Services.Service.Interface;

namespace Pryde.Services.Service.Implementation;

public class FinancialService(IUnitOfWork unitOfWork) : IFinancialService
{
    private const string Currency = "NGN";
    private const string EscrowAccountCode = "SYSTEM:ESCROW:NGN";
    private const string PlatformRevenueAccountCode = "SYSTEM:PLATFORM_REVENUE:NGN";
    private const string DriverWithdrawalAccountCode = "SYSTEM:DRIVER_WITHDRAWALS:NGN";
    private const string TestFundingAccountCode = "SYSTEM:TEST_FUNDING:NGN";

    public async Task<EscrowResponseDto> HoldBookingPaymentAsync(
        Guid passengerId, Guid bookingId, string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var key = ValidateIdempotencyKey(idempotencyKey);
        try
        {
            var result = await unitOfWork.ExecuteInTransactionOnceAsync(
                async transactionToken =>
            {
                var booking = await unitOfWork.TripBookings
                    .GetByIdForUpdateAsync(
                        bookingId,
                        transactionToken)
                    ?? throw new NotFoundException(
                        nameof(TripBooking),
                        bookingId);
                if (booking.PassengerId != passengerId)
                {
                    throw new ForbiddenException(
                        "Only the booking owner can pay for this booking.");
                }

                var trip = await unitOfWork.Trips
                    .GetByIdWithVehicleForUpdateAsync(
                        booking.TripId,
                        transactionToken)
                    ?? throw new NotFoundException(
                        nameof(Trip),
                        booking.TripId);
                booking.Trip = trip;

                var priorTransaction = await unitOfWork.Ledger
                    .GetByIdempotencyKeyAsync(
                        key,
                        transactionToken);
                if (priorTransaction is not null)
                {
                    if (priorTransaction.BookingId != bookingId)
                    {
                        throw new ConflictException(
                            "The idempotency key has already been used for another booking.");
                    }

                    var priorEscrow = await unitOfWork.Escrows
                        .GetByBookingIdAsync(
                            bookingId,
                            transactionToken)
                        ?? throw new ConflictException(
                            "The prior payment record is incomplete.");
                    var priorResponse = MapEscrow(priorEscrow);
                    priorResponse.TripId = booking.TripId;
                    return new PaymentHoldResult(
                        priorResponse,
                        false);
                }

                if (booking.Status != BookingStatus.Approved)
                {
                    throw PaymentUnavailableConflict(booking);
                }

                if (booking.PaidAt.HasValue ||
                    await unitOfWork.Escrows.GetByBookingIdAsync(
                        bookingId,
                        transactionToken) is not null)
                {
                    throw new ConflictException(
                        "This booking has already been paid.");
                }

                var now = DateTime.UtcNow;
                if (!booking.PaymentExpiresAt.HasValue)
                {
                    throw new ConflictException(
                        "This booking does not have a valid payment deadline.");
                }

                if (booking.PaymentExpiresAt.Value <= now)
                {
                    BookingSeatReservation.CancelApprovedBooking(
                        booking);
                    unitOfWork.TripBookings.Update(booking);
                    unitOfWork.Trips.Update(trip);
                    await unitOfWork.SaveChangesAsync(
                        transactionToken);
                    return new PaymentHoldResult(null, true);
                }

                var passengerWallet = await unitOfWork.Wallets
                    .GetByUserIdAsync(
                        passengerId,
                        transactionToken)
                    ?? throw new NotFoundException(
                        nameof(Wallet),
                        passengerId);
                if (passengerWallet.Balance < booking.TotalAmount)
                {
                    throw new ConflictException(
                        "The wallet balance is insufficient for this booking.");
                }

                var walletAccount = await EnsureWalletAccountAsync(
                    passengerWallet,
                    transactionToken);
                var escrowAccount = await EnsureSystemAccountAsync(
                    EscrowAccountCode,
                    "Booking Escrow",
                    LedgerAccountType.Escrow,
                    transactionToken);
                var escrow = new Escrow
                {
                    BookingId = booking.Id,
                    Booking = booking,
                    PassengerId = passengerId,
                    DriverId = trip.DriverId,
                    Amount = booking.TotalAmount,
                    DriverAmount = booking.SeatPrice,
                    PlatformAmount = booking.ServiceCharge,
                    Currency = Currency,
                    Status = EscrowStatus.Held,
                    HeldAt = now
                };
                await unitOfWork.Escrows.CreateAsync(
                    escrow,
                    transactionToken);

                var ledgerTransaction = NewTransaction(
                    LedgerTransactionType.BookingPaymentHold,
                    booking.TotalAmount,
                    booking.Id,
                    escrow.Id,
                    key,
                    "HOLD",
                    now);
                await AddBalancedEntriesAsync(
                    ledgerTransaction,
                    [
                        NewEntry(
                            ledgerTransaction,
                            walletAccount,
                            LedgerEntryType.Debit,
                            booking.TotalAmount),
                        NewEntry(
                            ledgerTransaction,
                            escrowAccount,
                            LedgerEntryType.Credit,
                            booking.TotalAmount)
                    ],
                    transactionToken);

                passengerWallet.Balance -= booking.TotalAmount;
                passengerWallet.EscrowBalance +=
                    booking.TotalAmount;
                unitOfWork.Wallets.Update(passengerWallet);
                await unitOfWork.WalletTransactions.CreateAsync(
                    new WalletTransaction
                    {
                        WalletId = passengerWallet.Id,
                        Amount = booking.TotalAmount,
                        Type = WalletTransactionType.EscrowHold,
                        Reference = ledgerTransaction.Reference
                    },
                    transactionToken);
                booking.PaidAt = now;
                unitOfWork.TripBookings.Update(booking);
                await unitOfWork.SaveChangesAsync(
                    transactionToken);
                var response = MapEscrow(escrow);
                response.TripId = booking.TripId;
                return new PaymentHoldResult(response, false);
            }, cancellationToken);

            if (result.Expired)
            {
                throw new ConflictException(
                    "The booking payment window has expired.");
            }

            return result.Response!;
        }
        catch (Exception exception)
            when (IsConcurrencyFailure(exception))
        {
            var current = await unitOfWork.TripBookings
                .GetByIdAsync(bookingId, cancellationToken);
            if (current is not null &&
                (current.Status == BookingStatus.Cancelled ||
                 (!current.PaidAt.HasValue &&
                  current.PaymentExpiresAt.HasValue &&
                  current.PaymentExpiresAt.Value <=
                    DateTime.UtcNow)))
            {
                throw new ConflictException(
                    "The booking payment window has expired.");
            }

            throw new ConflictException(
                "The booking changed while payment was being processed. No payment was taken.");
        }
    }

    public async Task<bool> ExpireUnpaidApprovedBookingAsync(
        Guid bookingId,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await unitOfWork.ExecuteInTransactionOnceAsync(
                async transactionToken =>
                {
                    var booking = await unitOfWork.TripBookings
                        .GetByIdForUpdateAsync(
                            bookingId,
                            transactionToken);
                    if (!CanExpire(booking, utcNow))
                    {
                        return false;
                    }

                    var trip = await unitOfWork.Trips
                        .GetByIdWithVehicleForUpdateAsync(
                            booking!.TripId,
                            transactionToken)
                        ?? throw new NotFoundException(
                            nameof(Trip),
                            booking.TripId);
                    booking.Trip = trip;

                    if (!CanExpire(booking, utcNow))
                    {
                        return false;
                    }

                    BookingSeatReservation.CancelApprovedBooking(
                        booking);
                    unitOfWork.TripBookings.Update(booking);
                    unitOfWork.Trips.Update(trip);
                    await unitOfWork.SaveChangesAsync(
                        transactionToken);
                    return true;
                },
                cancellationToken);
        }
        catch (Exception exception)
            when (IsConcurrencyFailure(exception))
        {
            var current = await unitOfWork.TripBookings
                .GetByIdAsync(bookingId, cancellationToken);
            if (!CanExpire(current, DateTime.UtcNow))
            {
                return false;
            }

            throw;
        }
    }

    public async Task RefundBookingAsync(Guid bookingId, CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var escrow = await unitOfWork.Escrows.GetByBookingIdAsync(bookingId, transactionToken);
            if (escrow is null)
            {
                throw new ConflictException(
                    "The paid booking escrow was not found.");
            }

            await RefundEscrowAsync(escrow, transactionToken);
            await unitOfWork.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);
    }

    public async Task RefundTripAsync(Guid tripId, CancellationToken cancellationToken = default)
    {
        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var escrows = await unitOfWork.Escrows.GetHeldByTripIdAsync(tripId, transactionToken);
            foreach (var escrow in escrows)
                await RefundEscrowAsync(escrow, transactionToken);
            await unitOfWork.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);
    }

    public async Task CompleteTripAsync(
        Guid tripId, Guid driverId, CancellationToken cancellationToken = default)
    {
        await CompleteTripInternalAsync(
            tripId,
            driverId,
            false,
            cancellationToken);
    }

    public async Task AutoCompleteTripAsync(
        Guid tripId,
        CancellationToken cancellationToken = default)
    {
        await CompleteTripInternalAsync(
            tripId,
            null,
            true,
            cancellationToken);
    }

    private async Task CompleteTripInternalAsync(
        Guid tripId,
        Guid? driverId,
        bool isAutomaticCompletion,
        CancellationToken cancellationToken)
    {
        await unitOfWork.ExecuteInTransactionAsync(async transactionToken =>
        {
            var trip = await unitOfWork.Trips.GetByIdForUpdateAsync(tripId, transactionToken)
                ?? throw new NotFoundException(nameof(Trip), tripId);

            if (trip.Status == TripStatus.Completed)
            {
                return true;
            }

            if (trip.Status == TripStatus.Cancelled)
            {
                throw new ConflictException("A cancelled trip cannot be completed.");
            }

            if (trip.Status != TripStatus.DropoffConfirmationPending)
            {
                throw new ConflictException("The trip is not waiting for drop-off confirmations.");
            }

            var completionTime = DateTime.UtcNow;

            if (isAutomaticCompletion)
            {
                if (!trip.DriverEndedAt.HasValue ||
                    !trip.ConfirmationDeadline.HasValue ||
                    trip.ConfirmationDeadline.Value > completionTime)
                {
                    throw new ConflictException(
                        "The trip confirmation deadline has not expired.");
                }
            }
            else
            {
                if (!driverId.HasValue ||
                    trip.DriverId != driverId.Value)
                {
                    throw new ForbiddenException(
                        "Only the trip owner can complete this trip.");
                }
            }

            var activeBookings = trip.Bookings
                .Where(booking =>
                    booking.Status == BookingStatus.Approved &&
                    booking.PaidAt.HasValue)
                .ToList();

            if (activeBookings.Count == 0)
            {
                throw new ConflictException(
                    "At least one active paid booking is required to complete the trip.");
            }

            if (!isAutomaticCompletion &&
                activeBookings.Any(booking =>
                    !booking.DropoffConfirmed))
            {
                throw new ConflictException("Every active passenger must confirm drop-off before completion.");
            }

            var escrows = await unitOfWork.Escrows.GetHeldByTripIdAsync(tripId, transactionToken);

            if (escrows.Count != activeBookings.Count)
            {
                throw new ConflictException(
                    "Every active paid booking must have held escrow.");
            }

            var driverWallet = await unitOfWork.Wallets.GetByUserIdAsync(
                trip.DriverId,
                transactionToken)
                ?? throw new NotFoundException(
                    nameof(Wallet),
                    trip.DriverId);
            var driverAccount = await EnsureWalletAccountAsync(
                driverWallet,
                transactionToken);
            var escrowAccount = await EnsureSystemAccountAsync(
                EscrowAccountCode,
                "Booking Escrow",
                LedgerAccountType.Escrow,
                transactionToken);
            var platformAccount = await EnsureSystemAccountAsync(
                PlatformRevenueAccountCode,
                "Platform Revenue",
                LedgerAccountType.PlatformRevenue,
                transactionToken);

            foreach (var escrow in escrows)
            {
                var passengerWallet = await unitOfWork.Wallets.GetByUserIdAsync(
                    escrow.PassengerId,
                    transactionToken)
                    ?? throw new NotFoundException(
                        nameof(Wallet),
                        escrow.PassengerId);

                if (passengerWallet.EscrowBalance < escrow.Amount)
                {
                    throw new ConflictException(
                        "The passenger escrow balance is inconsistent.");
                }

                var ledgerTransaction = NewTransaction(
                    LedgerTransactionType.EscrowRelease,
                    escrow.Amount,
                    escrow.BookingId,
                    escrow.Id,
                    $"release:{escrow.Id:N}",
                    "RELEASE",
                    completionTime);
                await AddBalancedEntriesAsync(
                    ledgerTransaction,
                    [
                        NewEntry(
                            ledgerTransaction,
                            escrowAccount,
                            LedgerEntryType.Debit,
                            escrow.Amount),
                        NewEntry(
                            ledgerTransaction,
                            driverAccount,
                            LedgerEntryType.Credit,
                            escrow.DriverAmount),
                        NewEntry(
                            ledgerTransaction,
                            platformAccount,
                            LedgerEntryType.Credit,
                            escrow.PlatformAmount)
                    ],
                    transactionToken);

                passengerWallet.EscrowBalance -= escrow.Amount;
                driverWallet.Balance += escrow.DriverAmount;
                unitOfWork.Wallets.Update(passengerWallet);
                await unitOfWork.WalletTransactions.CreateAsync(
                    new WalletTransaction
                    {
                        WalletId = driverWallet.Id,
                        Amount = escrow.DriverAmount,
                        Type = WalletTransactionType.EscrowRelease,
                        Reference = ledgerTransaction.Reference
                    },
                    transactionToken);
                escrow.Status = EscrowStatus.Released;
                escrow.ReleasedAt = completionTime;
                unitOfWork.Escrows.Update(escrow);
            }

            foreach (var booking in activeBookings)
            {
                booking.Status = BookingStatus.Completed;
                unitOfWork.TripBookings.Update(booking);
            }

            unitOfWork.Wallets.Update(driverWallet);
            trip.Status = TripStatus.Completed;

            if (isAutomaticCompletion)
            {
                trip.WasAutoCompleted = true;
                trip.AutoCompletedAt = completionTime;
            }

            unitOfWork.Trips.Update(trip);
            await unitOfWork.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);
    }

    public async Task<FinancialSummaryResponseDto> GetSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        var escrow = await unitOfWork.Escrows.GetTotalsAsync(cancellationToken);
        var ledger = await unitOfWork.Ledger.GetFinancialTotalsAsync(cancellationToken);
        return new FinancialSummaryResponseDto
        {
            Currency = Currency,
            TotalPlatformEarnings = ledger.PlatformEarnings,
            MonthlyPlatformEarnings = ledger.MonthlyPlatformEarnings,
            TotalEscrowHeld = escrow.Held,
            TotalEscrowReleased = escrow.Released,
            TotalEscrowRefunded = escrow.Refunded,
            TotalTransactions = ledger.TotalTransactions,
            TotalCommissions = ledger.PlatformEarnings,
            TotalDriverPayouts = ledger.DriverPayouts
        };
    }

    public async Task<PagedResponseDto<EscrowResponseDto>> GetEscrowsAsync(
        AdminEscrowsRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new AdminEscrowsRequestDto();
        ValidateDateRange(request.DateFrom, request.DateTo);
        var result = await unitOfWork.Escrows.GetAsync(
            request.Status, request.BookingId, request.PassengerId, request.DriverId,
            request.DateFrom, request.DateTo, request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(MapEscrow).ToList(), result.TotalCount, request.PageNumber, request.PageSize);
    }

    public async Task<EscrowResponseDto> GetEscrowAsync(
        Guid escrowId, CancellationToken cancellationToken = default)
    {
        var escrow = await unitOfWork.Escrows.GetByIdAsync(escrowId, cancellationToken)
            ?? throw new NotFoundException(nameof(Escrow), escrowId);
        return MapEscrow(escrow);
    }

    public async Task<PagedResponseDto<LedgerTransactionResponseDto>> GetTransactionsAsync(
        AdminLedgerTransactionsRequestDto request, CancellationToken cancellationToken = default)
    {
        request ??= new AdminLedgerTransactionsRequestDto();
        ValidateDateRange(request.DateFrom, request.DateTo);
        var result = await unitOfWork.Ledger.GetTransactionsAsync(
            request.TransactionType, request.Status, request.Reference, request.BookingId,
            request.EscrowId, request.UserId, request.DateFrom, request.DateTo,
            request.PageNumber, request.PageSize, cancellationToken);
        return Page(result.Items.Select(MapTransaction).ToList(), result.TotalCount, request.PageNumber, request.PageSize);
    }

    public async Task<LedgerTransactionDetailResponseDto> GetTransactionAsync(
        Guid transactionId, CancellationToken cancellationToken = default)
    {
        var transaction = await unitOfWork.Ledger.GetTransactionByIdAsync(transactionId, cancellationToken)
            ?? throw new NotFoundException(nameof(LedgerTransaction), transactionId);
        EnsureBalanced(transaction.Entries);
        var response = new LedgerTransactionDetailResponseDto
        {
            Entries = transaction.Entries.Select(entry => new LedgerEntryResponseDto
            {
                Id = entry.Id,
                AccountName = entry.LedgerAccount.Name,
                AccountType = entry.LedgerAccount.AccountType,
                EntryType = entry.EntryType,
                Amount = entry.Amount,
                Currency = entry.Currency
            }).ToList()
        };
        CopyTransaction(transaction, response);
        return response;
    }

    public async Task<IReadOnlyList<RevenueSummaryItemResponseDto>> GetRevenueSummaryAsync(
        int days, CancellationToken cancellationToken = default)
    {
        var safeDays = Math.Clamp(days, 1, 30);
        var from = DateTime.UtcNow.Date.AddDays(-(safeDays - 1));
        var totals = await unitOfWork.Ledger.GetRevenueSummaryAsync(from, cancellationToken);
        return totals.Select(total => new RevenueSummaryItemResponseDto
        {
            Date = total.Date,
            Amount = total.Amount
        }).ToList();
    }

    public async Task<WalletTransaction> RecordDriverWithdrawalAsync(
        Guid userId,
        decimal amount,
        string providerReference,
        string bankName,
        string maskedAccountNumber,
        string accountName,
        WalletTransactionStatus status,
        CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var wallet = await unitOfWork.Wallets.GetByUserIdAsync(
                    userId,
                    transactionToken)
                    ?? throw new NotFoundException(nameof(Wallet), userId);

                if (wallet.Balance < amount)
                {
                    throw new ConflictException(
                        "The wallet balance is insufficient for this withdrawal.");
                }

                var walletAccount = await EnsureWalletAccountAsync(
                    wallet,
                    transactionToken);
                var withdrawalAccount = await EnsureSystemAccountAsync(
                    DriverWithdrawalAccountCode,
                    "Driver Withdrawals",
                    LedgerAccountType.ExternalPayout,
                    transactionToken);
                var now = DateTime.UtcNow;
                var ledgerTransaction = new LedgerTransaction
                {
                    Reference = $"WITHDRAWAL-{Guid.NewGuid():N}",
                    IdempotencyKey = providerReference,
                    TransactionType = LedgerTransactionType.DriverWithdrawal,
                    Status = LedgerTransactionStatus.Posted,
                    Amount = amount,
                    Currency = Currency,
                    ExternalProvider = "Paystack",
                    ExternalReference = providerReference,
                    CompletedAt = now
                };

                await AddBalancedEntriesAsync(
                    ledgerTransaction,
                    new List<LedgerEntry>
                    {
                        NewEntry(
                            ledgerTransaction,
                            walletAccount,
                            LedgerEntryType.Debit,
                            amount),
                        NewEntry(
                            ledgerTransaction,
                            withdrawalAccount,
                            LedgerEntryType.Credit,
                            amount)
                    },
                    transactionToken);

                wallet.Balance -= amount;
                unitOfWork.Wallets.Update(wallet);

                var walletTransaction = new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Amount = amount,
                    Type = WalletTransactionType.Withdrawal,
                    Reference = providerReference,
                    Status = status,
                    Description = "Pryde driver withdrawal",
                    Provider = "Paystack",
                    Currency = Currency,
                    BankName = bankName,
                    MaskedAccountNumber = maskedAccountNumber,
                    AccountName = accountName,
                    CompletedAt = status == WalletTransactionStatus.Successful
                        ? now
                        : null
                };

                await unitOfWork.WalletTransactions.CreateAsync(
                    walletTransaction,
                    transactionToken);
                await unitOfWork.SaveChangesAsync(transactionToken);

                return walletTransaction;
            },
            cancellationToken);
    }

    public async Task<(Wallet Wallet, WalletTransaction Transaction)>
        RecordTestWalletFundingAsync(
            Guid userId,
            decimal amount,
            string description,
            CancellationToken cancellationToken = default)
    {
        return await unitOfWork.ExecuteInTransactionAsync(
            async transactionToken =>
            {
                var wallet = await unitOfWork.Wallets.GetByUserIdAsync(
                    userId,
                    transactionToken)
                    ?? throw new NotFoundException(nameof(Wallet), userId);
                var walletAccount = await EnsureWalletAccountAsync(
                    wallet,
                    transactionToken);
                var fundingAccount = await EnsureSystemAccountAsync(
                    TestFundingAccountCode,
                    "Test Wallet Funding",
                    LedgerAccountType.SystemFunding,
                    transactionToken);
                var now = DateTime.UtcNow;
                var reference = $"TEST-FUND-{Guid.NewGuid():N}";
                var ledgerTransaction = new LedgerTransaction
                {
                    Reference = reference,
                    IdempotencyKey = reference,
                    TransactionType =
                        LedgerTransactionType.TestWalletFunding,
                    Status = LedgerTransactionStatus.Posted,
                    Amount = amount,
                    Currency = Currency,
                    ExternalProvider = "System",
                    ExternalReference = reference,
                    CompletedAt = now
                };

                await AddBalancedEntriesAsync(
                    ledgerTransaction,
                    new List<LedgerEntry>
                    {
                        NewEntry(
                            ledgerTransaction,
                            fundingAccount,
                            LedgerEntryType.Debit,
                            amount),
                        NewEntry(
                            ledgerTransaction,
                            walletAccount,
                            LedgerEntryType.Credit,
                            amount)
                    },
                    transactionToken);

                wallet.Balance += amount;
                unitOfWork.Wallets.Update(wallet);

                var walletTransaction = new WalletTransaction
                {
                    WalletId = wallet.Id,
                    Amount = amount,
                    Type = WalletTransactionType.Credit,
                    Reference = reference,
                    Status = WalletTransactionStatus.Successful,
                    Description = description,
                    Provider = "System",
                    Currency = Currency,
                    CompletedAt = now
                };

                await unitOfWork.WalletTransactions.CreateAsync(
                    walletTransaction,
                    transactionToken);
                await unitOfWork.SaveChangesAsync(transactionToken);

                return (wallet, walletTransaction);
            },
            cancellationToken);
    }

    private async Task RefundEscrowAsync(Escrow escrow, CancellationToken cancellationToken)
    {
        if (escrow.Status == EscrowStatus.Refunded)
        {
            throw new ConflictException(
                "A refunded escrow cannot be refunded again.");
        }

        if (escrow.Status == EscrowStatus.Released)
        {
            throw new ConflictException("A released escrow cannot be refunded.");
        }

        var passengerWallet = await unitOfWork.Wallets.GetByUserIdAsync(escrow.PassengerId, cancellationToken)
            ?? throw new NotFoundException(nameof(Wallet), escrow.PassengerId);
        if (passengerWallet.EscrowBalance < escrow.Amount)
            throw new ConflictException("The passenger escrow balance is inconsistent.");
        var walletAccount = await EnsureWalletAccountAsync(passengerWallet, cancellationToken);
        var escrowAccount = await EnsureSystemAccountAsync(
            EscrowAccountCode, "Booking Escrow", LedgerAccountType.Escrow, cancellationToken);
        var now = DateTime.UtcNow;
        var ledgerTransaction = NewTransaction(
            LedgerTransactionType.EscrowRefund, escrow.Amount,
            escrow.BookingId, escrow.Id, $"refund:{escrow.Id:N}", "REFUND", now);
        await AddBalancedEntriesAsync(ledgerTransaction, [
            NewEntry(ledgerTransaction, escrowAccount, LedgerEntryType.Debit, escrow.Amount),
            NewEntry(ledgerTransaction, walletAccount, LedgerEntryType.Credit, escrow.Amount)
        ], cancellationToken);

        passengerWallet.EscrowBalance -= escrow.Amount;
        passengerWallet.Balance += escrow.Amount;
        unitOfWork.Wallets.Update(passengerWallet);
        await unitOfWork.WalletTransactions.CreateAsync(new WalletTransaction
        {
            WalletId = passengerWallet.Id,
            Amount = escrow.Amount,
            Type = WalletTransactionType.Credit,
            Reference = ledgerTransaction.Reference
        }, cancellationToken);
        escrow.Status = EscrowStatus.Refunded;
        escrow.RefundedAt = now;
        unitOfWork.Escrows.Update(escrow);
    }

    private async Task<LedgerAccount> EnsureWalletAccountAsync(
        Wallet wallet, CancellationToken cancellationToken)
    {
        var code = $"WALLET:{wallet.Id:N}";
        var account = await unitOfWork.Ledger.GetAccountByCodeAsync(code, cancellationToken);
        if (account is not null) return account;
        account = new LedgerAccount
        {
            Code = code,
            Name = $"Wallet {wallet.Id:N}",
            AccountType = LedgerAccountType.Wallet,
            WalletId = wallet.Id,
            Currency = Currency
        };
        return await unitOfWork.Ledger.CreateAsync(account, cancellationToken);
    }

    private async Task<LedgerAccount> EnsureSystemAccountAsync(
        string code, string name, LedgerAccountType type, CancellationToken cancellationToken)
    {
        var account = await unitOfWork.Ledger.GetAccountByCodeAsync(code, cancellationToken);
        if (account is not null) return account;
        return await unitOfWork.Ledger.CreateAsync(new LedgerAccount
        {
            Code = code,
            Name = name,
            AccountType = type,
            Currency = Currency
        }, cancellationToken);
    }

    private async Task AddBalancedEntriesAsync(
        LedgerTransaction transaction, IReadOnlyList<LedgerEntry> entries,
        CancellationToken cancellationToken)
    {
        EnsureBalanced(entries);
        await unitOfWork.Ledger.CreateAsync(transaction, cancellationToken);
        foreach (var entry in entries)
            await unitOfWork.Ledger.CreateAsync(entry, cancellationToken);
    }

    private static LedgerTransaction NewTransaction(
        LedgerTransactionType type, decimal amount, Guid bookingId, Guid escrowId,
        string idempotencyKey, string referencePrefix, DateTime completedAt) => new()
    {
        Reference = $"{referencePrefix}-{Guid.NewGuid():N}",
        IdempotencyKey = idempotencyKey,
        TransactionType = type,
        Status = LedgerTransactionStatus.Posted,
        Amount = amount,
        Currency = Currency,
        BookingId = bookingId,
        EscrowId = escrowId,
        CompletedAt = completedAt
    };

    private static LedgerEntry NewEntry(
        LedgerTransaction transaction, LedgerAccount account,
        LedgerEntryType type, decimal amount) => new()
    {
        LedgerTransactionId = transaction.Id,
        LedgerTransaction = transaction,
        LedgerAccountId = account.Id,
        LedgerAccount = account,
        EntryType = type,
        Amount = amount,
        Currency = Currency
    };

    private static void EnsureBalanced(IEnumerable<LedgerEntry> entries)
    {
        var materialized = entries.ToList();
        if (materialized.Count < 2 || materialized.Any(entry => entry.Amount <= 0))
            throw new InvalidOperationException("A ledger transaction requires at least two positive entries.");
        var debits = materialized.Where(entry => entry.EntryType == LedgerEntryType.Debit).Sum(entry => entry.Amount);
        var credits = materialized.Where(entry => entry.EntryType == LedgerEntryType.Credit).Sum(entry => entry.Amount);
        if (debits != credits)
            throw new InvalidOperationException("Ledger debits and credits must balance.");
    }

    private static EscrowResponseDto MapEscrow(Escrow escrow) => new()
    {
        EscrowId = escrow.Id,
        BookingId = escrow.BookingId,
        PassengerId = escrow.PassengerId,
        PassengerName = $"{escrow.Booking?.Passenger?.Profile?.FirstName} {escrow.Booking?.Passenger?.Profile?.LastName}".Trim(),
        DriverId = escrow.DriverId,
        DriverName = $"{escrow.Booking?.Trip?.Driver?.Profile?.FirstName} {escrow.Booking?.Trip?.Driver?.Profile?.LastName}".Trim(),
        Amount = escrow.Amount,
        DriverAmount = escrow.DriverAmount,
        PlatformAmount = escrow.PlatformAmount,
        Currency = escrow.Currency,
        Status = escrow.Status,
        HeldAt = escrow.HeldAt,
        ReleasedAt = escrow.ReleasedAt,
        RefundedAt = escrow.RefundedAt
    };

    private static LedgerTransactionResponseDto MapTransaction(LedgerTransaction transaction)
    {
        var response = new LedgerTransactionResponseDto();
        CopyTransaction(transaction, response);
        return response;
    }

    private static void CopyTransaction(LedgerTransaction source, LedgerTransactionResponseDto target)
    {
        target.Id = source.Id;
        target.Reference = source.Reference;
        target.TransactionType = source.TransactionType;
        target.Status = source.Status;
        target.Amount = source.Amount;
        target.Currency = source.Currency;
        target.BookingId = source.BookingId;
        target.EscrowId = source.EscrowId;
        target.ExternalProvider = source.ExternalProvider;
        target.ExternalReference = source.ExternalReference;
        target.CreatedAt = source.CreatedAt;
        target.CompletedAt = source.CompletedAt;
    }

    private static PagedResponseDto<T> Page<T>(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize) => new()
    {
        Items = items,
        PageNumber = pageNumber,
        PageSize = pageSize,
        TotalCount = totalCount,
        TotalPages = (int)Math.Ceiling(totalCount / (double)pageSize)
    };

    private static string ValidateIdempotencyKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ValidationException("IdempotencyKey is required.");
        var value = key.Trim();
        if (value.Length > 150) throw new ValidationException("IdempotencyKey cannot exceed 150 characters.");
        return value;
    }

    private static bool CanExpire(
        TripBooking? booking,
        DateTime utcNow)
    {
        return booking is not null &&
               booking.Status == BookingStatus.Approved &&
               !booking.PaidAt.HasValue &&
               booking.PaymentExpiresAt.HasValue &&
               booking.PaymentExpiresAt.Value <= utcNow;
    }

    private static ConflictException PaymentUnavailableConflict(
        TripBooking booking)
    {
        if (!booking.PaidAt.HasValue &&
            booking.PaymentExpiresAt.HasValue &&
            booking.PaymentExpiresAt.Value <= DateTime.UtcNow)
        {
            return new ConflictException(
                "The booking payment window has expired.");
        }

        return new ConflictException(
            "Only an approved booking can be paid.");
    }

    private static bool IsConcurrencyFailure(
        Exception exception)
    {
        for (Exception? current = exception;
             current is not null;
             current = current.InnerException)
        {
            if (current is DbUpdateConcurrencyException)
            {
                return true;
            }

            if (current is PostgresException postgresException &&
                postgresException.SqlState is
                    PostgresErrorCodes.SerializationFailure or
                    PostgresErrorCodes.DeadlockDetected)
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidateDateRange(DateTime? from, DateTime? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
            throw new ValidationException("DateFrom cannot be later than DateTo.");
    }

    private sealed record PaymentHoldResult(
        EscrowResponseDto? Response,
        bool Expired);
}
