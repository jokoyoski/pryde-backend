using Pryde.Domain.Entities;
namespace Pryde.Persistence.Repository.Interfaces;
public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByIdWithDocumentsAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByLicensePlateAsync(string licensePlateNumber, CancellationToken cancellationToken = default);
    Task<bool> ExistsAsync(string licensePlateNumber, CancellationToken cancellationToken = default);
    Task<Vehicle> CreateAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    void Update(Vehicle vehicle);
    void Delete(Vehicle vehicle);
}