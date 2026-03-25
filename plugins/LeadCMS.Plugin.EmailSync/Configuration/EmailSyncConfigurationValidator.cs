// <copyright file="EmailSyncConfigurationValidator.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;
using Microsoft.Extensions.Configuration;

namespace LeadCMS.Plugin.EmailSync.Configuration;

public static class EmailSyncConfigurationValidator
{
    public const string EncryptionKeyPlaceholder = "$EMAILSYNC__ENCRYPTIONKEY";

    public static string GetEncryptionKey(IConfiguration configuration)
    {
        var emailSyncConfig = configuration.GetSection("EmailSync").Get<EmailSyncConfig>() ?? new EmailSyncConfig();
        if (string.IsNullOrWhiteSpace(emailSyncConfig.EncryptionKey))
        {
            throw new MissingConfigurationException("The EmailSync:EncryptionKey setting is required.");
        }

        return emailSyncConfig.EncryptionKey;
    }

    public static string? GetProductionEncryptionKeyError(IConfiguration configuration)
    {
        var encryptionKey = GetEncryptionKey(configuration);
        if (!IsProduction(configuration) || !IsPlaceholderEncryptionKey(encryptionKey))
        {
            return null;
        }

        return $"The EmailSync:EncryptionKey setting still uses the placeholder value '{EncryptionKeyPlaceholder}'. Replace it before using EmailSync in production.";
    }

    public static void EnsureProductionReady(IConfiguration configuration)
    {
        var validationError = GetProductionEncryptionKeyError(configuration);
        if (validationError != null)
        {
            throw new UnprocessableEntityException(validationError);
        }
    }

    internal static bool IsPlaceholderEncryptionKey(string encryptionKey)
    {
        return string.Equals(encryptionKey, EncryptionKeyPlaceholder, StringComparison.Ordinal);
    }

    private static bool IsProduction(IConfiguration configuration)
    {
        var environmentName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? configuration["DOTNET_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");

        return string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase);
    }
}