// <copyright file="AIProviderException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions;

namespace LeadCMS.Core.AIAssistance.Exceptions;

public class AIProviderException : InternalServerErrorException
{
    public AIProviderException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
        AddExtension("providerName", providerName);
    }

    public AIProviderException(string providerName, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderName = providerName;
        AddExtension("providerName", providerName);
    }

    public string ProviderName { get; }
}
