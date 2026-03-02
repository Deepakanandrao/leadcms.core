// <copyright file="GravatarHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Cryptography;
using System.Text;

namespace LeadCMS.Helpers;

public class GravatarHelper
{
    public static string EmailToGravatarUrl(string? email)
    {
        if (string.IsNullOrEmpty(email))
        {
            return "https://www.gravatar.com/avatar/?size=48&d=mp";
        }

        var emailBytes = Encoding.ASCII.GetBytes(email);
        var emailHashCode = MD5.HashData(emailBytes);

        return "https://www.gravatar.com/avatar/" + Convert.ToHexString(emailHashCode).ToLower() + "?size=48&d=mp";
    }
}