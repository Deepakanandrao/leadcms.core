# Reverse Proxy Plugin Documentation

## Overview

The Reverse Proxy Plugin provides sophisticated request routing and forwarding capabilities for LeadCMS using Microsoft's YARP (Yet Another Reverse Proxy) technology. This plugin enables secure, authenticated access to external services like Elasticsearch, Kibana, Metabase and others through the LeadCMS infrastructure.

## Purpose

- **Service Integration**: Provide unified access to external services through LeadCMS
- **Authentication Gateway**: Enforce authentication for access to backend services
- **Request Routing**: Intelligent routing based on URL patterns and rules
- **Path Transformation**: Modify request paths when forwarding to backend services
- **Security Layer**: Add security controls to external service access
- **Single Sign-On**: Leverage LeadCMS authentication for external services

## Configuration

### Plugin Settings & Environment Variables

For details on configuring routes, clusters, and environment variables, refer to the official YARP documentation:

https://learn.microsoft.com/en-us/aspnet/core/fundamentals/servers/yarp/yarp-overview?view=aspnetcore-9.0

---

This plugin provides a powerful and flexible reverse proxy solution that enables secure, authenticated access to external services while maintaining centralized control and monitoring capabilities within the LeadCMS ecosystem.
