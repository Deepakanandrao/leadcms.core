// <copyright file="AIProviderException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Plugin.AI.Exceptions;

public class AIProviderException : Exception
{
    public AIProviderException(string providerName, string message)
        : base(message)
    {
        ProviderName = providerName;
    }

    public AIProviderException(string providerName, string message, Exception innerException)
        : base(message, innerException)
    {
        ProviderName = providerName;
    }

    public string ProviderName { get; }
}
