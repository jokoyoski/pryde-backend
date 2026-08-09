using System.Reflection;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Pryde.Persistence.Migrations;

namespace Pryde.Tests.Persistence;

public class PaystackSafetyMigrationTests
{
    [Fact]
    public void MigrationCreatesRequiredUniqueConstraintsAndBackfillsEarnings()
    {
        var builder = new MigrationBuilder(
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration = new AddPaystackFundingIntentsAndWithdrawalSafety();
        typeof(AddPaystackFundingIntentsAndWithdrawalSafety)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });
        var indexes = builder.Operations
            .OfType<CreateIndexOperation>()
            .Where(operation => operation.IsUnique)
            .Select(operation => operation.Name)
            .ToHashSet();

        Assert.Contains(
            "IX_PaystackFundingIntents_Reference",
            indexes);
        Assert.Contains(
            "IX_PaystackFundingIntents_PaystackTransactionId",
            indexes);
        Assert.Contains(
            "IX_WalletTransactions_Provider_Reference",
            indexes);
        Assert.Contains(
            "IX_LedgerTransactions_ExternalReference",
            indexes);
        var sql = string.Join(
            Environment.NewLine,
            builder.Operations
                .OfType<SqlOperation>()
                .Select(operation => operation.Sql));
        Assert.Contains("WithdrawableBalance", sql);
        Assert.Contains("VerifiedAt", sql);
        Assert.Contains("reversal:", sql);
    }

    [Fact]
    public void RenameMigrationPreservesPaymentTableAndRenamesIndexes()
    {
        var builder = new MigrationBuilder(
            "Npgsql.EntityFrameworkCore.PostgreSQL");
        var migration =
            new RenamePaystackFundingIntentsToWalletFundingRequests();
        typeof(RenamePaystackFundingIntentsToWalletFundingRequests)
            .GetMethod("Up", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(migration, new object[] { builder });

        var tableRename = Assert.Single(
            builder.Operations.OfType<RenameTableOperation>());
        Assert.Equal("PaystackFundingIntents", tableRename.Name);
        Assert.Equal("PaystackWalletFundingRequests", tableRename.NewName);
        Assert.Equal(
            3,
            builder.Operations.OfType<RenameIndexOperation>().Count());
        Assert.Empty(builder.Operations.OfType<DropTableOperation>());
        Assert.Empty(builder.Operations.OfType<CreateTableOperation>());
    }
}
