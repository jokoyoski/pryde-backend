using Mapster;
using Pryde.Contracts.ResponseModels;
using Pryde.Domain.Entities;

namespace Pryde.Services.Mapping;
public class MapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<User, RegisterResponseDto>()
            .Map(dest => dest.UserId, src => src.Id);

        config.NewConfig<User, LoginResponseDto>()
            .Map(dest => dest.UserId, src => src.Id);

        config.NewConfig<DriverBankAccount, DriverBankAccountResponseDto>()
            .Map(
                destination => destination.AccountNumber,
                source => "******" + source.AccountNumber.Substring(6, 4));
    }
}
