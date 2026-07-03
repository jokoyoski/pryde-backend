namespace Pryde.Domain.Common.Exceptions;

public class UnauthorizedException(string message)
    : Exception(message);