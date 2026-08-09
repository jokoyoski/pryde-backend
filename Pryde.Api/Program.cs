using Mapster;
using Microsoft.EntityFrameworkCore;
using Pryde.Api.Extension;
using Pryde.Api.Extensions;
using Pryde.Api.Hubs;
using Pryde.Api.Middleware;
using Pryde.Persistence.Context;
using Pryde.Persistence.Settings;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Notifications.Implementation;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Settings;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

TypeAdapterConfig.GlobalSettings.Scan(
    typeof(Program).Assembly,
    typeof(Pryde.Services.Mapping.MapsterConfig).Assembly);

builder.Services.AddControllers()
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(
            new JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSignalR().AddJsonProtocol(options =>options.PayloadSerializerOptions.Converters
.Add( new JsonStringEnumConverter()));

builder.Services.AddApiVersioningConfiguration();
builder.Services.AddSwaggerConfiguration();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddServices();
builder.Services.AddScoped<
    Pryde.Services.Service.Interface.INotificationRealtimeSender,
    SignalRNotificationRealtimeSender>();
builder.Services.AddDojahIntegration(builder.Configuration);
builder.Services.AddPaystackIntegration(builder.Configuration);

builder.Services
    .AddOptions<VehicleUploadSettings>()
    .Bind(builder.Configuration.GetSection(VehicleUploadSettings.SectionName))
    .Validate(
        settings => settings.VehicleImageMaxBytes > 0 &&
                    settings.WalkAroundVideoMaxBytes > 0 &&
                    settings.VehicleDocumentMaxBytes > 0,
        "Vehicle upload limits must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.ApiKey),
        "EmailSettings:ApiKey is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.FromAddress),
        "EmailSettings:FromAddress is required.")
    .Validate(
        settings => !string.IsNullOrWhiteSpace(settings.FromName),
        "EmailSettings:FromName is required.")
    .Validate(
        settings => settings.OtpExpiryMinutes > 0,
        "EmailSettings:OtpExpiryMinutes must be greater than zero.")
    .ValidateOnStart();

builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

builder.Services.AddAuthenticationConfiguration(
    builder.Configuration);

builder.Services.Configure<PricingSettings>(
    builder.Configuration.GetSection("PricingSettings"));

builder.Services
    .AddOptions<TripSettings>()
    .Bind(builder.Configuration.GetSection(TripSettings.SectionName))
    .Validate(
        settings => settings.DefaultBookingWindowMinutes > 0,
        "Trips:DefaultBookingWindowMinutes must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<RecurringTripSettings>()
    .Bind(builder.Configuration.GetSection(
        RecurringTripSettings.SectionName))
    .Validate(
        settings => settings.GenerationHorizonDays > 0 &&
                    settings.GenerationIntervalMinutes > 0,
        "Recurring trip settings must be greater than zero.")
    .ValidateOnStart();

builder.Services
    .AddOptions<BookingPaymentSettings>()
    .Bind(builder.Configuration.GetSection(
        BookingPaymentSettings.SectionName))
    .Validate(
        settings =>
            settings.PaymentWindowMinutes > 0 &&
            settings.ExpiryCheckIntervalMinutes > 0,
        "Booking payment settings must be greater than zero.")
    .ValidateOnStart();

builder.Services.Configure<BootstrapUsersSettings>(
    builder.Configuration.GetSection(
        BootstrapUsersSettings.SectionName));

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .AllowAnyOrigin()
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>(
        "RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();

    var dbContext = scope.ServiceProvider
        .GetRequiredService<PrydeDbContext>();

    await dbContext.Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>(
        "SeedDatabaseOnStartup"))
{
    await app.SeedDatabaseAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

if (app.Environment.IsDevelopment())
{
    app.UseMiddleware<VehicleMediaRequestTimingMiddleware>();
}

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<NotificationHub>("/hubs/notifications");

app.Run();
