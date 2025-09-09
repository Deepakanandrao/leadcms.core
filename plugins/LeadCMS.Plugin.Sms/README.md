# SMS Plugin Documentation

## Overview

The SMS Plugin provides comprehensive SMS messaging capabilities for LeadCMS through multiple gateway providers with intelligent country-specific routing. This plugin enables automated SMS communications, notifications, and marketing campaigns with high deliverability and global reach.

## Purpose

- **Multi-Gateway SMS Delivery**: Send SMS messages through various providers for optimal delivery
- **Country-Specific Routing**: Automatically route SMS messages through the best gateway for each country
- **Delivery Tracking**: Monitor SMS delivery status and maintain detailed logs

## Key Features

### Multi-Gateway Support
- **Amazon SNS**: Global AWS SMS service with worldwide coverage
- **Twilio**: International SMS service with global reach
- **Getshoutout**: API-based SMS service with competitive rates
- **NotifyLK**: Specialized provider for Sri Lankan market
- **SMSC (Russia)**: Specialized provider for Russian market (smsc.ru)
- **SMSC KZ (Kazakhstan)**: Dedicated provider for Kazakhstan market (smsc.kz)

### Intelligent Routing
- Country code-based gateway selection
- Default gateway fallback for unspecified countries
- Configurable routing rules per country/region

### Delivery Management
- Real-time delivery status tracking
- Comprehensive SMS logging and audit trails
- Automated status synchronization
- Failed delivery handling and retry logic

## Configuration

### Environment Variables

> **Warning:** `SMSACCESSKEY` is a secret used for server-to-server authentication to send SMS via the LeadCMS REST API. Never expose this key publicly (e.g., on websites or in client-side code). Only trusted backend systems should have access.

```bash
# SMS Access Control (server-to-server only)
SMSACCESSKEY=your-sms-access-key


# Amazon SNS Configuration
SMSGATEWAYS__AMAZONSNS__SENDERID=your-sender-id
SMSGATEWAYS__AMAZONSNS__ACCESSKEYID=your-aws-access-key-id
SMSGATEWAYS__AMAZONSNS__SECRETACCESSKEY=your-aws-secret-access-key
SMSGATEWAYS__AMAZONSNS__DEFAULTREGION=us-east-1

# Twilio Configuration
SMSGATEWAYS__TWILIO__ACCOUNTSID=your-twilio-account-sid
SMSGATEWAYS__TWILIO__AUTHTOKEN=your-twilio-auth-token

# NotifyLK Configuration (Sri Lankan market)
SMSGATEWAYS__NOTIFYLK__SENDERID=your-notify-lk-sender-id
SMSGATEWAYS__NOTIFYLK__APIKEY=your-notify-lk-api-key
SMSGATEWAYS__NOTIFYLK__USERID=your-notify-lk-user-id

# Getshoutout Configuration
SMSGATEWAYS__GETSHOUTOUT__SENDERID=your-getshoutout-sender-id
SMSGATEWAYS__GETSHOUTOUT__APIKEY=your-getshoutout-api-key
```

### Plugin Settings

#### Configuring Country Routing via Environment Variables

You can configure `SmsCountryGateways` using environment variables for international routing. For example:

```bash
# Default gateway for all countries not specified
SMSCOUNTRYGATEWAYS__0__CODE=default
SMSCOUNTRYGATEWAYS__0__GATEWAY=AmazonSns

# United States
SMSCOUNTRYGATEWAYS__1__CODE=+1
SMSCOUNTRYGATEWAYS__1__GATEWAY=Twilio

# Germany
SMSCOUNTRYGATEWAYS__2__CODE=+49
SMSCOUNTRYGATEWAYS__2__GATEWAY=AmazonSns

# Sri Lanka
SMSCOUNTRYGATEWAYS__3__CODE=+94
SMSCOUNTRYGATEWAYS__3__GATEWAY=NotifyLk
```

You may add more country codes and gateways as needed for your use case.

```json
{
  "SmsAccessKey": "$SMSACCESSKEY",
  "SmsGateways": {
    "AmazonSns": {
      "SenderId": "$SMSGATEWAYS__AMAZONSNS__SENDERID",
      "AccessKeyId": "$SMSGATEWAYS__AMAZONSNS__ACCESSKEYID",
      "SecretAccessKey": "$SMSGATEWAYS__AMAZONSNS__SECRETACCESSKEY",
      "DefaultRegion": "$SMSGATEWAYS__AMAZONSNS__DEFAULTREGION"
    },
    "Smsc": {
      "ApiUrl": "https://smsc.ru/rest/send/",
      "SenderId": "$SMSGATEWAYS__SMSC__SENDERID",
      "Login": "$SMSGATEWAYS__SMSC__LOGIN",
      "Password": "$SMSGATEWAYS__SMSC__PASSWORD"
    },
    "Twilio": {
      "AccountSid": "$SMSGATEWAYS__TWILIO__ACCOUNTSID",
      "AuthToken": "$SMSGATEWAYS__TWILIO__AUTHTOKEN"
    }
  },
  "SmsCountryGateways": [
    {
      "Code": "default",
      "Gateway": "AmazonSns"
    },
    {
      "Code": "+7",
      "Gateway": "Smsc"
    },
    {
      "Code": "+94",
      "Gateway": "NotifyLk"
    }
  ],
  "Tasks": {
    "SyncSmsLogTask": {
      "Enable": false,
      "CronSchedule": "0/30 * * * * ? *",
      "RetryCount": "2",
      "RetryInterval": "5",
      "BatchSize": "100"
    }
  }
}
```

## Country Routing Configuration


The plugin uses country codes to automatically select the most appropriate SMS gateway. Example configuration:

- **`default`**: Default gateway for countries not specifically configured (Amazon SNS)
- **`+1`**: United States (Twilio)
- **`+49`**: Germany (Amazon SNS)
- **`+94`**: Sri Lanka (NotifyLK)

You can customize this routing by modifying the `SmsCountryGateways` array or using environment variables as shown above.

## Contributing

Feel free to add support for more SMS providers! Raise a pull request (PR) with your implementation and it will be considered for inclusion in the core plugin.

## API Endpoints

### SMS Management

- **POST `/api/messages/send`** - Send SMS message
  - Input: Recipient phone number, message content, sender ID
  - Output: Message ID, delivery status, gateway used

- **GET `/api/messages/{id}/status`** - Get SMS delivery status
  - Input: Message ID
  - Output: Current delivery status, timestamps, gateway information

- **GET `/api/messages/logs`** - Retrieve SMS logs with filtering
  - Input: Date range, recipient, status filters
  - Output: Paginated list of SMS logs with delivery details

## Best Practices

### Gateway Selection
- **Cost Optimization**: Route messages through most cost-effective gateways by region
- **Regional Expertise**: Use local gateways for better delivery rates in specific countries

### Message Optimization
- **Character Limits**: Keep messages under 160 characters to avoid segmentation charges
- **Unicode Handling**: Be aware of character encoding for international messages

### Compliance and Regulations
- **Opt-in Requirements**: Ensure proper consent before sending marketing messages
- **Unsubscribe Mechanism**: Provide clear opt-out instructions
- **Regulatory Compliance**: Follow local telecommunications regulations
- **Data Protection**: Implement appropriate data handling and retention policies

---

This plugin provides a robust, scalable SMS messaging solution that can handle everything from individual notifications to high-volume marketing campaigns while ensuring optimal delivery through intelligent gateway routing.
