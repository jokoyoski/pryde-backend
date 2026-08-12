namespace Pryde.Services.Providers.Kyc;

public interface IKycProviderResolver
{
    IKycProvider ResolveActive();
    IKycProvider Resolve(string providerName);
}
