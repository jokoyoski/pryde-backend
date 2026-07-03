namespace Pryde.Domain.Common.Exceptions;

public class ForbiddenException(string message)
    : Exception(message);