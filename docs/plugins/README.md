# LeadCMS Plugins Documentation Index

This directory contains comprehensive documentation for all LeadCMS plugins. Each plugin has its own detailed documentation file with configuration instructions, use cases, and best practices.

## Available Plugin Documentation

### [🤖 AI Plugin](../../plugins/LeadCMS.Core.AIAssistance/README.md)

**Purpose**: Integrates OpenAI services for AI-powered content generation, translation, and image creation.

**Key Features**:

- Content generation using GPT models
- Image generation with DALL-E
- Multi-language content translation
- AI-powered content editing and improvements

**Configuration**: Requires OpenAI API key
**Use Cases**: Content marketing, multilingual websites, automated content creation

---

### [📱 SMS Plugin](../../plugins/LeadCMS.Plugin.Sms/README.md)

**Purpose**: Multi-gateway SMS messaging with intelligent country-specific routing.

**Key Features**:

- Support for 6 SMS gateways (Amazon SNS, SMSC, Twilio, etc.)
- Country-specific routing optimization
- Delivery tracking and status monitoring
- Fallback gateway support

**Configuration**: Multiple gateway API keys and routing rules
**Use Cases**: Lead notifications, marketing campaigns, 2FA, customer service

---

### [📧 SendGrid Plugin](../../plugins/LeadCMS.Plugin.SendGrid/README.md)

**Purpose**: Enterprise-grade email delivery with comprehensive tracking and analytics.

**Key Features**:

- High-volume email sending
- Real-time delivery tracking
- Email engagement analytics
- Suppression list management

**Configuration**: SendGrid API key and webhook settings
**Use Cases**: Transactional emails, marketing campaigns, lead nurturing

---

### [📬 Email Sync Plugin](../../plugins/LeadCMS.Plugin.EmailSync/README.md)

**Purpose**: Synchronizes emails from IMAP accounts for lead management and communication tracking.

**Key Features**:

- Multi-IMAP account support
- Automatic lead creation from emails
- Email classification (internal/external)
- Communication history tracking

**Configuration**: IMAP credentials and domain classification rules
**Use Cases**: Customer service, sales communication tracking, lead enhancement

---

### [🌐 Site Plugin](../../plugins/LeadCMS.Plugin.Site/README.md)

**Purpose**: Essential website functionality including contact forms and newsletter management.

**Key Features**:

- Contact form processing with file attachments
- Automatic lead creation from website visitors
- Newsletter subscription management
- Multi-language support

**Configuration**: Contact form recipients and site settings
**Use Cases**: Lead generation, customer inquiries, newsletter signups

---

### [📊 VSTO Plugin](../../plugins/LeadCMS.Plugin.Vsto/README.md)

**Purpose**: Hosts and serves Visual Studio Tools for Office add-ins with version management.

**Key Features**:

- Office add-in hosting and distribution
- Version management and user tracking
- Dynamic content injection
- File monitoring and automatic updates

**Configuration**: File paths and serving options
**Use Cases**: Enterprise Office add-in distribution, version control, beta testing

---

### [🔄 Reverse Proxy Plugin](../../plugins/LeadCMS.Plugin.ReverseProxy/README.md)

**Purpose**: Provides secure, authenticated access to external services like Elasticsearch and Kibana.

**Key Features**:

- Request routing with authentication
- Path transformation capabilities
- Load balancing across backend services
- Integration with Elasticsearch and Kibana

**Configuration**: Route definitions and backend service endpoints
**Use Cases**: Service integration, monitoring dashboards, unified access control

---

## Plugin Management

### Enabling Plugins

Plugins are controlled through the main application configuration:

```bash
# Enable specific plugins
PLUGINS__0=LeadCMS.Core.AIAssistance
PLUGINS__1=LeadCMS.Plugin.Sms
PLUGINS__2=LeadCMS.Plugin.SendGrid
PLUGINS__3=LeadCMS.Plugin.EmailSync
PLUGINS__4=LeadCMS.Plugin.Site
PLUGINS__5=LeadCMS.Plugin.Vsto
PLUGINS__6=LeadCMS.Plugin.ReverseProxy
```

### Configuration Files

Each plugin includes:

- **Source Code**: Plugin implementation in `plugins/LeadCMS.Plugin.{Name}/`
- **Settings File**: `pluginsettings.json` with default configuration
- **Documentation**: `README.md` file in each plugin's root directory

## Getting Started

1. **Choose Required Plugins**: Select plugins based on your business needs
2. **Review Documentation**: Read the specific documentation for each plugin
3. **Configure Environment Variables**: Set up all required API keys and settings
4. **Enable Plugins**: Add plugins to the PLUGINS configuration array
5. **Test Integration**: Verify each plugin works correctly
6. **Monitor Performance**: Set up logging and monitoring for plugin activities

## Security Best Practices

- **Environment Variables**: Always use environment variables for sensitive configuration
- **API Key Rotation**: Regularly rotate API keys and access tokens
- **Updates**: Keep plugins and their dependencies updated

## Plugin Development

For information on developing custom plugins:

- Review the `IPlugin` and `IPluginApplication` interfaces
- Study existing plugin implementations
- Follow the plugin architecture patterns
- Implement proper dependency injection and configuration management

---

Each plugin documentation file contains detailed information about:

- **Purpose and Overview**: What the plugin does and why you'd use it
- **Configuration**: Complete setup instructions with examples
- **API Endpoints**: Available endpoints and their functionality
- **Security Considerations**: Important security guidelines
- **Troubleshooting**: Common issues and solutions
- **Best Practices**: Recommended approaches for optimal usage
