using Mapster;
using Microsoft.EntityFrameworkCore;
using Pryde.Api.Extension;
using Pryde.Api.Extensions;
using Pryde.Api.Middleware;
using Pryde.Persistence.Context;
using Pryde.Services.DependencyInjection;

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

builder.Services.AddAuthenticationConfiguration(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
        policy.WithOrigins(builder.Configuration["FrontendUrl"] ?? "http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

if (app.Configuration.GetValue<bool>("RunMigrationsOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PrydeDbContext>();
    await dbContext.Database.MigrateAsync();
}
await app.SeedDatabaseAsync();

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