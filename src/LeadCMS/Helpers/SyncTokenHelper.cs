// <copyright file="SyncTokenHelper.cs" company="WavePoint Co. Ltd.">
// Licensed under the MIT license. See LICENSE file in the samples root for full license information.
// </copyright>

using System;

namespace LeadCMS.Helpers
{
    public static class SyncTokenHelper
    {
        public static string EncodeSyncToken(DateTime syncTime)
        {
            var tokenBytes = BitConverter.GetBytes(syncTime.Ticks);
            return Convert.ToBase64String(tokenBytes).Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static bool TryDecodeSyncToken(string? syncToken, out DateTime lastSyncTime)
        {
            lastSyncTime = DateTime.MinValue;
            if (string.IsNullOrEmpty(syncToken))
            {
                return false;
            }
                
            try
            {
                var base64 = syncToken.Replace('-', '+').Replace('_', '/');
                // Pad with '=' if needed
                switch (base64.Length % 4)
                {
                    case 2: base64 += "=="; break;
                    case 3: base64 += "="; break;
                }

                var bytes = Convert.FromBase64String(base64);
                var ticks = BitConverter.ToInt64(bytes, 0);
                lastSyncTime = new DateTime(ticks, DateTimeKind.Utc);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
