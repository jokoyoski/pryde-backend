using Mapster;
using Microsoft.EntityFrameworkCore;
using Pryde.Api.Extension;
using Pryde.Api.Extensions;
using Pryde.Api.Middleware;
using Pryde.Persistence.Context;
using Pryde.Persistence.Settings;
using Pryde.Services.DependencyInjection;
using Pryde.Services.Notifications.Implementation;
using Pryde.Services.Notifications.Interface;
using Pryde.Services.Settings;

var builder = WebApplication.CreateBuilder(args);

TypeAdapterConfig.GlobalSettings.Scan(
    typeof(Program).Assembly,
    typeof(Pryde.Services.Mapping.MapsterConfig).Assembly);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddApiVersioningConfiguration();
builder.Services.AddSwaggerConfiguration();

builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddServices();
builder.Services.AddDojahIntegration(builder.Configuration);

builder.Services.AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(EmailSettings.SectionName))
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.ApiKey),
        "EmailSettings:ApiKey is required.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromAddress),
        "EmailSettings:FromAddress is required.")
    .Validate(settings => !string.IsNullOrWhiteSpace(settings.FromName),
        "EmailSettings:FromName is required.")
    .Validate(settings => settings.OtpExpiryMinutes > 0,
        "EmailSettings:OtpExpiryMinutes must be greater than zero.")
    .ValidateOnStart();
builder.Services.AddHttpClient<IEmailService, ResendEmailService>();

builder.Services.Configure<PricingSettings>(
    builder.Configuration.GetSection("PricingSettings"));

builder.Services.AddAuthenticationConfiguration(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

builder.Services.Configure<BootstrapUsersSettings>(
    builder.Configuration.GetSection(
        BootstrapUsersSettings.SectionName));

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PrydeDbContext>();
    await dbContext.Database.MigrateAsync();
}

if (app.Configuration.GetValue<bool>("SeedDatabaseOnStartup"))
{
    await app.SeedDatabaseAsync();
}

app.UseSwagger();
app.UseSwaggerUI();

app.UseMiddleware<ExceptionMiddleware>();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
