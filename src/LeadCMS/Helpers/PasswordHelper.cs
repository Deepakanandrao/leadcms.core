// <copyright file="PasswordHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System.Security.Cryptography;

namespace LeadCMS.Helpers
{
    public static class PasswordHelper
    {
        public static string GenerateStrongPassword(int length = 16)
        {
            const string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ1234567890!@#$%^&*()-_=+";
            using var rng = RandomNumberGenerator.Create();
            var bytes = new byte[length];
            rng.GetBytes(bytes);
            return new string(bytes.Select(b => valid[b % valid.Length]).ToArray());
        }
    }
}
