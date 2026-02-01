# LeadCMS Deploy Plugin

Plugin for triggering and monitoring deployments via Azure DevOps pipelines.

## Features

- Trigger Azure DevOps build and release pipelines
- Monitor deployment status in real-time (stateless - fetches live from Azure DevOps)
- Support for multiple deployment targets (production, staging, etc.)
- Automatic release tracking after build completion
- Deployment statistics and history

## Configuration

### Environment Variables / User Secrets

The Personal Access Token (PAT) should be set via environment variables or user secrets:

```bash
# Environment variable
export AZUREDEVOPS__PERSONALACCESSTOKEN=your-pat-here

# Or via .NET User Secrets
dotnet user-secrets set "AzureDevOps:PersonalAccessToken" "your-pat-here"
```

### pluginsettings.json

```json
{
  "AzureDevOps": {
    "OrganizationUrl": "https://dev.azure.com/your-organization",
    "ProjectName": "YourProject",
    "PersonalAccessToken": "$AZUREDEVOPS__PERSONALACCESSTOKEN"
  },
  "DeploymentTargets": {
    "production": {
      "Name": "Production Site",
      "Description": "Deploy main site to production",
      "BuildPipelineId": 123,
      "SourceBranch": "refs/heads/main",
      "TrackRelease": true,
      "Resource": "https://yoursite.com"
    },
    "staging": {
      "Name": "Staging Site",
      "Description": "Deploy site to staging environment",
      "BuildPipelineId": 456,
      "SourceBranch": "refs/heads/develop",
      "TrackRelease": true,
      "Resource": "https://staging.yoursite.com"
    },
    "preview": {
      "Name": "Preview Build",
      "Description": "Build preview for PR review",
      "BuildPipelineId": 789,
      "SourceBranch": "refs/heads/feature/preview",
      "TrackRelease": false,
      "Resource": "https://preview.yoursite.com"
    }
  }
}
```

### Configuration Options

#### AzureDevOps Settings

| Setting               | Description                                                         | Required |
| --------------------- | ------------------------------------------------------------------- | -------- |
| `OrganizationUrl`     | Azure DevOps organization URL (e.g., `https://dev.azure.com/myorg`) | Yes      |
| `ProjectName`         | Azure DevOps project name                                           | Yes      |
| `PersonalAccessToken` | PAT with Build and Release permissions                              | Yes      |

#### Deployment Target Settings

| Setting           | Description                                                                | Required | Default           |
| ----------------- | -------------------------------------------------------------------------- | -------- | ----------------- |
| `Name`            | Display name for the target                                                | Yes      | -                 |
| `Description`     | Description of what this target deploys                                    | No       | -                 |
| `BuildPipelineId` | Azure DevOps build definition ID                                           | Yes      | -                 |
| `SourceBranch`    | Git branch to trigger build from                                           | No       | `refs/heads/main` |
| `TrackRelease`    | If true, track release after build; if false, complete when build finishes | No       | `true`            |
| `Resource`        | URL of the deployed resource                                               | No       | -                 |

## Azure DevOps PAT Permissions

The Personal Access Token needs the following scopes:

- **Build**: Read & Execute
- **Release**: Read, Write, & Execute
- **Project and Team**: Read

## How It Works

1. **Trigger**: When a deployment is triggered, the plugin queues a build in Azure DevOps
2. **Build**: The build pipeline runs (compiles, tests, packages)
3. **Release** (if `TrackRelease: true`): Azure DevOps automatically triggers a release pipeline after build succeeds
4. **Status**: The plugin fetches live status from Azure DevOps APIs - no state is stored in the CMS

### Deployment Status Mapping

| Build Status          | Release Status        | Deployment Status |
| --------------------- | --------------------- | ----------------- |
| Not Started           | -                     | Pending           |
| In Progress           | -                     | In Progress       |
| Completed (Failed)    | -                     | Failed            |
| Completed (Canceled)  | -                     | Cancelled         |
| Completed (Succeeded) | Not Found/In Progress | In Progress       |
| Completed (Succeeded) | Failed                | Failed            |
| Completed (Succeeded) | Succeeded             | Completed         |

## API Endpoints

The plugin implements `IDeploymentService` which exposes:

- `GET /api/Deployments/targets` - List configured deployment targets
- `GET /api/Deployments` - Get recent deployments
- `GET /api/Deployments/{id}` - Get deployment details with steps
- `GET /api/Deployments/stats` - Get deployment statistics
- `POST /api/Deployments/trigger` - Trigger deployment(s)

## Example Usage

### Trigger single target

```json
POST /api/Deployments/trigger
{
  "targetIds": ["production"]
}
```

### Trigger multiple targets

```json
POST /api/Deployments/trigger
{
  "targetIds": ["staging", "production"]
}
```

### Trigger all targets

```json
POST /api/Deployments/trigger
{
  "triggerAll": true
}
```

## Capabilities

This plugin adds the `Deployment` capability to the system, which can be queried via the capabilities API.
