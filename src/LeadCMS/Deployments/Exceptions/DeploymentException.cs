// <copyright file="DeploymentException.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using LeadCMS.Exceptions.Base;

namespace LeadCMS.Core.Deployments.Exceptions;

public class DeploymentException : Exception
{
    public DeploymentException(string message)
        : base(message)
    {
    }

    public DeploymentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class DeploymentNotConfiguredException : BaseHttpException
{
    public DeploymentNotConfiguredException()
        : base("No deployment service is configured. Please install and configure a deployment plugin.")
    {
    }

    public override int StatusCode => StatusCodes.Status424FailedDependency;
}
