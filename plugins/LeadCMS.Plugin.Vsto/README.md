# VSTO Plugin Documentation

## Overview

The VSTO (Visual Studio Tools for Office) Plugin enables LeadCMS to serve Microsoft Office add-ins and extensions with dynamic content replacement and automatic file monitoring. This plugin is specifically designed for hosting and distributing Office add-ins with version control and user-specific delivery.

## Purpose

- **Office Add-in Hosting**: Serve VSTO add-ins and Office extensions
- **Dynamic Content Injection**: Replace variables in files with dynamic content
- **Version Management**: Handle multiple versions of Office add-ins
- **File Monitoring**: Automatically detect and serve updated add-in files

## Key Features

### VSTO File Serving
- Static file serving for Office add-ins (.exe, .vsto, .dll files)
- Dynamic path resolution with version support
- Automatic file discovery and case-insensitive matching
- Configurable request paths and local storage locations

### Version Management
- **Multi-Version Support**: Serve different versions of add-ins to different users
- **Version Tracking**: Remember user-requested versions for consistent delivery
- **Automatic Version Resolution**: Resolve version paths for manifest files
- **Expiration Handling**: Time-based version preference expiration

### Dynamic Content Replacement
- **Variable Substitution**: Replace placeholders in files with dynamic content
- **Language Support**: Language-specific variable replacement
- **URL Generation**: Automatically generate URLs for downloadable files
- **Template Processing**: Process template files with dynamic content

### File Monitoring
- **Real-time Monitoring**: Watch for file system changes in VSTO directory
- **Automatic Updates**: Detect new versions and updated files
- **Cache Invalidation**: Automatically invalidate cached content when files change
- **Hot Deployment**: Deploy updates without server restart

## Configuration

### Environment Variables

VSTO plugin configuration is primarily handled through plugin settings rather than environment variables.

### Plugin Settings

The plugin configuration includes path settings and file serving options:

```json
{
  "Vsto": {
    "RequestPath": "/vsto",
    "LocalPath": "vsto",
    "SubPathPrefix": "pro"
  }
}
```

**Configuration Options:**
- **RequestPath**: URL path for accessing VSTO files (default: `/vsto`)
- **LocalPath**: Local directory containing VSTO files (default: `vsto`)
- **SubPathPrefix**: Prefix for sub-paths in VSTO URLs (default: `pro`)

## File Structure and Organization

### Expected Directory Structure

```
vsto/
├── pro/
│   ├── en/
│   │   ├── YourAddin.exe
│   │   ├── YourAddin.vsto
│   │   └── Application Files/
│   │       └── YourAddin_1_0_0_0/
│   │           ├── YourAddin.exe.manifest
│   │           ├── YourAddin.vsto
│   │           └── [other version-specific files]
│   └── [other language folders]
└── [other product folders]
```

### File Types Supported

**Executable Files (.exe):**
- Main Office add-in installer files
- Version tracking and user preference recording
- Query parameter processing for version selection

**VSTO Manifest Files (.vsto):**
- Office add-in manifest files
- Dynamic path resolution based on user preferences
- Version-specific manifest serving

**Supporting Files:**
- DLL files, configuration files, resources
- Served statically with appropriate cache headers
- Support for nested directory structures

## Use Cases

### Office Add-in Distribution
- **Corporate Add-ins**: Distribute custom Office add-ins to employees
- **Software Products**: Serve commercial Office add-ins to customers
- **Beta Testing**: Provide different versions to different user groups

### Version Management
- **Gradual Rollouts**: Deploy new versions to specific users first
- **A/B Testing**: Serve different versions for testing purposes
- **Rollback Support**: Maintain multiple versions for rollback scenarios
- **User Preferences**: Allow users to request specific versions

### Development and Testing
- **Development Builds**: Serve development versions to internal users
- **Testing Environments**: Provide test versions for QA teams
- **Preview Releases**: Offer preview versions to selected users
- **Hot Fixes**: Deploy urgent fixes without affecting all users

### Enterprise Deployment
- **Centralized Distribution**: Single point for Office add-in distribution
- **License Management**: Control access to premium add-in versions
- **Update Management**: Manage add-in updates across organization
- **Usage Tracking**: Track add-in downloads and usage patterns

---

This plugin provides a comprehensive solution for hosting and distributing Office add-ins with sophisticated version management and dynamic content capabilities, making it ideal for enterprise Office add-in deployment scenarios.
