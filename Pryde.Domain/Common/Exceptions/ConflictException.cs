namespace Pryde.Domain.Common.Exceptions;

public class ConflictException(string message)
    : Exception(message);