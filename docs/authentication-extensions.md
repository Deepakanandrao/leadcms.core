# Authentication Extensions API

## Overview

This document describes the extended authentication API endpoints that support two new scenarios:

1. **Microsoft Token Exchange**: Exchange Microsoft JWT tokens for internal LeadCMS JWT tokens
2. **Device Flow Authentication**: Console-based authentication similar to GitHub CLI, Docker, etc.

## Microsoft Token Exchange

### POST /api/identity/exchange-token

Exchange a Microsoft JWT token for an internal LeadCMS JWT token.

**Request Body:**
```json
{
  "microsoftToken": "eyJ0eXAiOiJKV1QiLCJhbGc..."
}
```

**Response (200 OK):**
```json
{
  "token": "eyJ0eXAiOiJKV1QiLCJhbGc...",
  "expiration": "2025-10-31T12:00:00Z"
}
```

**Error Responses:**
- `400 Bad Request`: Invalid request format
- `401 Unauthorized`: Microsoft token is invalid or expired
- `500 Internal Server Error`: Server error during token exchange

### Benefits:
- Eliminates client-side Microsoft SDK dependency
- Reduces API calls to Microsoft's validation endpoints
- Improves client performance
- Centralized token management

## Device Flow Authentication

The device flow enables console applications to authenticate users through a web browser, similar to GitHub CLI, Docker Desktop, etc.

### 1. Initiate Device Authentication

**POST /api/identity/device/initiate**

**Response (200 OK):**
```json
{
  "deviceCode": "device_abc123...",
  "userCode": "WDJB-MJHT",
  "verificationUri": "http://localhost:45437/auth/device-verify",
  "verificationUriComplete": "http://localhost:45437/auth/device-verify?user_code=WDJB-MJHT",
  "expiresIn": 900,
  "interval": 5
}
```

### 2. User Authorization

The user opens the `verificationUri` in their browser and enters the `userCode` to authorize the device.

### 3. Poll for Authorization

**POST /api/identity/device/poll**

**Request Body:**
```json
{
  "deviceCode": "device_abc123..."
}
```

**Response (200 OK - Completed):**
```json
{
  "token": "eyJ0eXAiOiJKV1QiLCJhbGc...",
  "expiration": "2025-10-31T12:00:00Z"
}
```

**Response (202 Accepted - Pending):**
```json
{
  "status": "authorization_pending",
  "message": "User has not yet authorized the device"
}
```

**Error Responses:**
- `400 Bad Request`: Device code expired, denied, or invalid

### 4. Device Verification (Web UI)

**POST /api/identity/device/verify** (Requires authentication)

**Request Body:**
```json
{
  "userCode": "WDJB-MJHT"
}
```

**POST /api/identity/device/deny** (Requires authentication)

**Request Body:**
```json
{
  "userCode": "WDJB-MJHT"
}
```

## Console Application Example

Here's how a console application would implement the device flow:

### C# Example

```csharp
public class LeadCmsAuthenticator
{
    private readonly HttpClient httpClient;
    private readonly string baseUrl;

    public LeadCmsAuthenticator(string baseUrl)
    {
        this.baseUrl = baseUrl;
        this.httpClient = new HttpClient();
    }

    public async Task<string> AuthenticateAsync()
    {
        // 1. Initiate device authentication
        var initResponse = await httpClient.PostAsync(
            $"{baseUrl}/api/identity/device/initiate",
            new StringContent("", Encoding.UTF8, "application/json"));

        var initData = await initResponse.Content.ReadFromJsonAsync<DeviceAuthInitiateDto>();

        // 2. Display instructions to user
        Console.WriteLine($"Please visit: {initData.VerificationUri}");
        Console.WriteLine($"And enter code: {initData.UserCode}");
        Console.WriteLine("Waiting for authorization...");

        // 3. Poll for completion
        var pollRequest = new { deviceCode = initData.DeviceCode };

        while (true)
        {
            await Task.Delay(TimeSpan.FromSeconds(initData.Interval));

            var pollResponse = await httpClient.PostAsJsonAsync(
                $"{baseUrl}/api/identity/device/poll",
                pollRequest);

            if (pollResponse.StatusCode == HttpStatusCode.OK)
            {
                var tokenData = await pollResponse.Content.ReadFromJsonAsync<JWTokenDto>();
                return tokenData.Token;
            }
            else if (pollResponse.StatusCode == HttpStatusCode.Accepted)
            {
                // Continue polling
                continue;
            }
            else
            {
                var error = await pollResponse.Content.ReadAsStringAsync();
                throw new Exception($"Authentication failed: {error}");
            }
        }
    }
}
```

### CLI Usage

```bash
# In your console application
leadcms login

# Output:
# Please visit: http://localhost:45437/auth/device-verify
# And enter code: WDJB-MJHT
# Waiting for authorization...
#
# Successfully authenticated! Token saved.
```

## Security Considerations

### Microsoft Token Exchange
- Microsoft tokens are validated against Microsoft's OpenID Connect configuration
- Only valid, non-expired tokens are accepted
- User information is synchronized with the local user database
- Internal tokens follow the same security practices as regular login tokens

### Device Flow
- Device codes expire after 15 minutes
- User codes are single-use and expire with the device code
- All device authentication requires user to be logged in via web browser
- Polling is rate-limited to prevent abuse
- Device codes are cryptographically secure random strings

## Frontend Integration

The device verification page is available at `/auth/device-verify` and supports:
- Manual user code entry
- Pre-filled user codes via URL parameter (`?user_code=XXXX-XXXX`)
- Responsive design for mobile and desktop
- Clear success/error messaging
- Auto-close functionality for popup windows

## Error Handling

Both flows provide detailed error messages and appropriate HTTP status codes:

- `400 Bad Request`: Malformed requests, expired codes
- `401 Unauthorized`: Authentication required, invalid tokens
- `403 Forbidden`: Access denied
- `404 Not Found`: Invalid endpoints
- `429 Too Many Requests`: Rate limiting (if implemented)
- `500 Internal Server Error`: Server-side errors

All error responses include descriptive error messages in a consistent format:

```json
{
  "error": "invalid_request",
  "error_description": "Detailed description of the error"
}
```