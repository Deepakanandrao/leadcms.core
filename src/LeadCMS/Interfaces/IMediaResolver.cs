// <copyright file="IMediaResolver.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

namespace LeadCMS.Interfaces;

public interface IMediaResolver
{
    string Resolve(string uri, HttpContext context, string mode);
}

