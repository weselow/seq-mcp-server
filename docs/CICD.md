# CI/CD Pipeline Documentation

## Overview

Seq MCP Server uses GitHub Actions for continuous integration and deployment. The pipeline consists of three main workflows that automatically build, test, and deploy the application.

## Workflows

### 1. CI Workflow (`.github/workflows/ci.yml`)

**Triggers:**
- Push to `master`, `main`, or `develop` branches
- Pull requests to `master`, `main`, or `develop` branches
- Manual workflow dispatch

**Jobs:**

#### Build and Test
- Runs on: `ubuntu-latest`
- .NET version: `9.0.x`
- Steps:
  1. Checkout code
  2. Setup .NET SDK
  3. Cache NuGet packages
  4. Restore dependencies
  5. Build in Release configuration
  6. Run unit tests with TRX logger
  7. Upload test results as artifacts
  8. Generate test report

#### Lint and Format Check
- Runs on: `ubuntu-latest`
- Steps:
  1. Checkout code
  2. Setup .NET SDK
  3. Restore dependencies
  4. Check code formatting with `dotnet format --verify-no-changes`
  5. Build with warnings as errors

#### Publish Artifacts
- Runs on: `ubuntu-latest`
- Depends on: Build and Test, Lint
- Condition: Only on `master` or `main` branch
- Steps:
  1. Checkout code
  2. Setup .NET SDK
  3. Publish application to `./publish`
  4. Upload artifacts (7 days retention)

**Status Badge:**
```markdown
![CI](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/CI/badge.svg)
```

---

### 2. Docker Workflow (`.github/workflows/docker.yml`)

**Triggers:**
- Push to `master` or `main` branches
- Push to tags matching `v*` pattern (e.g., `v1.0.0`)
- Pull requests to `master` or `main` branches
- Manual workflow dispatch

**Configuration:**
- Registry: GitHub Container Registry (`ghcr.io`)
- Image name: `ghcr.io/YOUR_USERNAME/seq-mcp-server`

**Job: Build and Push**
- Runs on: `ubuntu-latest`
- Permissions: `contents: read`, `packages: write`
- Steps:
  1. Checkout code
  2. Set up Docker Buildx
  3. Log in to GitHub Container Registry (skip on PR)
  4. Extract Docker metadata (tags, labels)
  5. Build and push Docker image
  6. Output image digest

**Tag Strategy:**
- `main` branch → `latest`
- `develop` branch → `develop`
- `v1.2.3` tag → `1.2.3`, `1.2`, `1`
- Git SHA → `sha-abc1234`

**Docker Image Usage:**
```bash
# Pull latest image
docker pull ghcr.io/YOUR_USERNAME/seq-mcp-server:latest

# Run container
docker run -d \
  -e SEQ_URL=http://seq-server:8080 \
  -e SEQ_API_KEY=your-api-key \
  -p 5555:5555 \
  ghcr.io/YOUR_USERNAME/seq-mcp-server:latest
```

**Status Badge:**
```markdown
![Docker](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
```

---

### 3. Security Workflow (`.github/workflows/security.yml`)

**Triggers:**
- Push to `master`, `main`, or `develop` branches
- Pull requests to `master`, `main`, or `develop` branches
- Weekly schedule (Monday 00:00 UTC)
- Manual workflow dispatch

**Jobs:**

#### CodeQL Security Analysis
- Runs on: `ubuntu-latest`
- Language: C#
- Query suite: `security-and-quality`
- Steps:
  1. Checkout code
  2. Setup .NET SDK
  3. Initialize CodeQL
  4. Restore and build
  5. Perform CodeQL analysis
  6. Upload results to GitHub Security tab

**Detected Vulnerabilities:**
- SQL injection
- Cross-site scripting (XSS)
- Code injection
- Path traversal
- And more...

#### Dependency Review
- Runs on: Pull requests only
- Fails on: Moderate or higher severity vulnerabilities
- Denied licenses: GPL-2.0, GPL-3.0
- Steps:
  1. Checkout code
  2. Run dependency review
  3. Comment on PR with findings

#### .NET Security Scan
- Runs on: `ubuntu-latest`
- Steps:
  1. Checkout code
  2. Setup .NET SDK
  3. List vulnerable packages (including transitive)
  4. Fail build if vulnerabilities found
  5. Upload vulnerability report (on failure)

**Status Badge:**
```markdown
![Security](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)
```

---

## Environment Variables

### Required for Workflows

| Variable | Description | Required By |
|----------|-------------|-------------|
| `GITHUB_TOKEN` | Auto-provided by GitHub Actions | All workflows |
| `DOTNET_VERSION` | .NET SDK version (9.0.x) | CI, Security |
| `REGISTRY` | Container registry URL | Docker |
| `IMAGE_NAME` | Docker image name | Docker |

### Runtime Environment Variables

Configure in workflow files or repository secrets:

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `SEQ_URL` | Seq server URL | http://localhost:8080 | Yes |
| `SEQ_API_KEY` | Seq API key | - | No |
| `PORT` | Server port | 5555 | No |
| `ASPNETCORE_ENVIRONMENT` | ASP.NET environment | Production | No |

---

## Setting Up CI/CD

### 1. Enable GitHub Actions

Ensure GitHub Actions are enabled in repository settings:
- Go to `Settings` → `Actions` → `General`
- Enable "Allow all actions and reusable workflows"

### 2. Configure Secrets

No secrets are required for basic CI/CD. Optional secrets:

**For Docker push to private registry:**
```
Settings → Secrets and variables → Actions → New repository secret
Name: DOCKER_USERNAME
Value: your-docker-username

Name: DOCKER_PASSWORD
Value: your-docker-password
```

### 3. Enable GitHub Container Registry

GitHub Container Registry is automatically enabled. To use it:

1. Create a Personal Access Token (PAT):
   - Go to `Settings` → `Developer settings` → `Personal access tokens` → `Tokens (classic)`
   - Generate new token with `write:packages` scope

2. Docker login:
```bash
echo YOUR_PAT | docker login ghcr.io -u YOUR_USERNAME --password-stdin
```

### 4. Branch Protection Rules

Recommended branch protection for `main`:

```
Settings → Branches → Add rule

Branch name pattern: main

Require a pull request before merging: ✓
  - Require approvals: 1
  - Dismiss stale pull request approvals: ✓

Require status checks to pass before merging: ✓
  - Require branches to be up to date: ✓
  - Status checks:
    - Build and Test
    - Lint and Format Check
    - CodeQL Security Analysis

Require conversation resolution before merging: ✓

Do not allow bypassing the above settings: ✓
```

---

## Local Testing

### Test Build and Tests Locally

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build --configuration Release

# Run tests
dotnet test --configuration Release --verbosity normal

# Check formatting
dotnet format --verify-no-changes

# Build with warnings as errors
dotnet build --configuration Release /p:TreatWarningsAsErrors=true
```

### Test Docker Build Locally

```bash
# Build Docker image
docker build -t seq-mcp-server:local .

# Run container
docker run -d \
  --name seq-mcp-test \
  -e SEQ_URL=http://host.docker.internal:8080 \
  -p 5555:5555 \
  seq-mcp-server:local

# Check logs
docker logs seq-mcp-test

# Stop and remove
docker stop seq-mcp-test
docker rm seq-mcp-test
```

### Test with Act (GitHub Actions locally)

Install Act: https://github.com/nektos/act

```bash
# Install act
# macOS: brew install act
# Windows: choco install act-cli
# Linux: curl https://raw.githubusercontent.com/nektos/act/master/install.sh | bash

# List workflows
act -l

# Run CI workflow
act push -W .github/workflows/ci.yml

# Run specific job
act push -j build-and-test

# Run with secrets
act push -s GITHUB_TOKEN=your-token
```

---

## Workflow Artifacts

### CI Workflow Artifacts

**Test Results:**
- Name: `test-results`
- Path: `**/TestResults/*.trx`
- Retention: 90 days (GitHub default)
- Available in: Actions → Workflow run → Artifacts

**Published Application:**
- Name: `seq-mcp-server`
- Path: `./publish/`
- Retention: 7 days
- Available in: Actions → Workflow run → Artifacts

### Docker Workflow Artifacts

**Docker Images:**
- Registry: GitHub Container Registry
- View: Packages → seq-mcp-server
- Tags: See "Tag Strategy" above

### Security Workflow Artifacts

**Vulnerability Reports:**
- Name: `vulnerable-packages`
- Path: `vulnerable-packages.txt`
- Available only on: Failed security scan
- Retention: 90 days

**CodeQL Results:**
- View: Security → Code scanning alerts
- Severity levels: Critical, High, Medium, Low, Warning, Note

---

## Monitoring and Notifications

### GitHub Actions Status

View workflow status:
- Repository → Actions tab
- Filter by workflow, branch, or status
- View logs and artifacts

### Email Notifications

Configure in: `Settings` → `Notifications`
- Watch repository for: Actions workflow failures

### Slack Integration

Add GitHub app to Slack workspace:
```
/github subscribe YOUR_USERNAME/seq-mcp-server
/github subscribe YOUR_USERNAME/seq-mcp-server workflows:{event:"push","pull_request"}
```

### Badge Status

Add badges to README.md:

```markdown
![CI](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/CI/badge.svg)
![Docker](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/Docker%20Build%20and%20Push/badge.svg)
![Security](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/Security%20and%20Code%20Quality/badge.svg)
![CodeQL](https://github.com/YOUR_USERNAME/seq-mcp-server/workflows/CodeQL/badge.svg)
```

---

## Troubleshooting

### Build Failures

**Issue**: NuGet restore fails
```
Solution: Clear NuGet cache
- In workflow: Delete workflow cache
- Locally: dotnet nuget locals all --clear
```

**Issue**: TypeScript compilation errors
```
Solution: Update .NET SDK version in workflow
- Check: dotnet --list-sdks
- Update: env.DOTNET_VERSION in workflow
```

### Docker Build Failures

**Issue**: Docker push unauthorized
```
Solution: Re-authenticate to registry
1. Check token permissions
2. Re-login: docker login ghcr.io
3. Ensure packages:write permission
```

**Issue**: Dockerfile COPY fails
```
Solution: Check .dockerignore
- Ensure source files not ignored
- Verify COPY paths in Dockerfile
```

### Security Scan Failures

**Issue**: CodeQL analysis timeout
```
Solution: Increase timeout or reduce scope
- Add timeout-minutes: 360 to job
- Reduce query suite if needed
```

**Issue**: False positive vulnerability
```
Solution: Suppress false positives
1. Go to Security → Code scanning alerts
2. Find the alert
3. Click "Dismiss alert" → "False positive"
```

---

## Performance Optimization

### Caching Strategy

**NuGet Packages:**
```yaml
- name: Cache NuGet packages
  uses: actions/cache@v4
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
```

**Docker Layers:**
```yaml
- name: Build and push Docker image
  uses: docker/build-push-action@v5
  with:
    cache-from: type=gha
    cache-to: type=gha,mode=max
```

### Parallel Jobs

Jobs run in parallel by default. Use `needs` for dependencies:

```yaml
publish-artifacts:
  needs: [build-and-test, lint]
```

### Matrix Builds

For multi-platform testing:

```yaml
strategy:
  matrix:
    os: [ubuntu-latest, windows-latest, macos-latest]
    dotnet: ['8.0.x', '9.0.x']
```

---

## Best Practices

1. **Keep workflows DRY**: Use reusable workflows for common tasks
2. **Security first**: Never commit secrets, use GitHub Secrets
3. **Fast feedback**: Run critical tests first, expensive tests later
4. **Clear naming**: Use descriptive job and step names
5. **Version pinning**: Pin action versions (e.g., `@v4` not `@main`)
6. **Timeout protection**: Add `timeout-minutes` to prevent hanging jobs
7. **Conditional execution**: Use `if` conditions to skip unnecessary jobs
8. **Artifact cleanup**: Set appropriate retention days
9. **Resource limits**: Be mindful of GitHub Actions quotas
10. **Monitor usage**: Check Actions usage in repository insights

---

## GitHub Actions Quotas

**Free tier (public repos):**
- Unlimited minutes
- Unlimited storage
- 20 concurrent jobs

**Free tier (private repos):**
- 2,000 minutes/month
- 500 MB storage
- Limited concurrent jobs

**Check usage:**
- Settings → Billing → Plans and usage → Actions

---

## Related Documentation

- [Integration Tests Guide](./INTEGRATION_TESTS.md)
- [Docker Deployment Guide](../README.md#docker-deployment)
- [Contributing Guide](../CONTRIBUTING.md)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)

---

**Last Updated**: 2025-01-13
**Maintainer**: Seq MCP Server Team
