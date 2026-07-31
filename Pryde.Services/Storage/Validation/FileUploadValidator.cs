using Microsoft.AspNetCore.Http;
using Pryde.Domain.Common.Exceptions;

namespace Pryde.Services.Storage.Validation;

public static class FileUploadValidator
{
    public static void Validate(
        IFormFile file,
        long maximumBytes,
        IReadOnlyCollection<string> allowedContentTypes,
        string name)
    {
        if (file.Length == 0)
        {
            throw new ValidationException($"{name} is empty.");
        }

        if (file.Length > maximumBytes)
        {
            throw new ValidationException(
                $"{name} exceeds the configured upload limit.");
        }

        if (!allowedContentTypes.Contains(
                file.ContentType,
                StringComparer.OrdinalIgnoreCase))
        {
            throw new ValidationException(
                $"{name} file type is not supported.");
        }
    }
}
