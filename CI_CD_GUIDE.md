# CI/CD Pipeline Guide

Complete guide for the GitHub Actions CI/CD pipeline configured for SpinMonitor Desktop.

---

## 📋 Pipeline Overview

The CI/CD pipeline consists of 3 main workflows:

### 1. **Main CI/CD Pipeline** (`ci-cd.yml`)
Runs on: Push to main/develop, Pull Requests, Releases

**Jobs:**
- ✅ Code Quality Check
- ✅ Build & Test (Debug + Release)
- ✅ Security Vulnerability Scan
- ✅ Publish Release Build
- ✅ Create GitHub Release
- ✅ Notify on Failure

### 2. **Pull Request Check** (`pr-check.yml`)
Runs on: Pull Request opened/updated

**Jobs:**
- ✅ Build validation
- ✅ Test execution
- ✅ Merge conflict detection
- ✅ Automatic PR comments

### 3. **Code Quality** (`code-quality.yml`)
Runs on: Push, PR, Weekly schedule

**Jobs:**
- ✅ Code style & formatting
- ✅ Dependency vulnerability check
- ✅ Static code analysis
- ✅ License compliance check

---

## 🚀 How to Use

### **Automatic Triggers**

**1. Push to main/develop:**
```bash
git push origin main
```
→ Triggers full CI/CD pipeline
→ Creates release build
→ Publishes artifacts

**2. Create Pull Request:**
```bash
git checkout -b feature/my-feature
# Make changes
git push origin feature/my-feature
# Create PR on GitHub
```
→ Runs PR validation
→ Checks for conflicts
→ Comments on PR with results

**3. Create Release:**
```bash
git tag v1.0.0
git push origin v1.0.0
```
→ Creates GitHub Release
→ Uploads build artifacts
→ Generates release notes

---

## 📦 Build Artifacts

### **What Gets Built:**

**1. Self-Contained Executable**
- **File:** `SpinMonitor.Desktop.exe`
- **Size:** ~80 MB
- **Platform:** Windows x64
- **Dependencies:** All included (.NET 9.0 runtime bundled)

**2. ZIP Archive**
- **File:** `SpinMonitor-Desktop-v1.0.0.X-win-x64.zip`
- **Contents:**
  - SpinMonitor.Desktop.exe
  - version.txt
  - checksums.txt

**3. Checksums**
- **File:** `checksums.txt`
- **Contains:** SHA256 hash of executable
- **Purpose:** Verify download integrity

### **Where to Find Artifacts:**

**Option 1: GitHub Actions Tab**
1. Go to repository → Actions
2. Click on latest workflow run
3. Scroll to "Artifacts" section
4. Download `SpinMonitor-Desktop-win-x64`

**Option 2: GitHub Releases**
1. Go to repository → Releases
2. Latest release shows automatically
3. Download from "Assets" section

---

## 🔧 Configuration

### **Required Secrets**

No secrets required for basic operation. The pipeline uses:
- `${{ secrets.GITHUB_TOKEN }}` - Automatically provided by GitHub

### **Optional Secrets (for advanced features)**

If you want to add email notifications or other integrations:

**1. Email Notifications:**
```yaml
# Add to repository secrets
SMTP_SERVER: smtp.gmail.com
SMTP_PORT: 587
SMTP_USERNAME: your-email@gmail.com
SMTP_PASSWORD: your-app-password
NOTIFICATION_EMAIL: team@example.com
```

**2. Slack Notifications:**
```yaml
SLACK_WEBHOOK_URL: https://hooks.slack.com/services/YOUR/WEBHOOK/URL
```

**3. Code Signing (for production):**
```yaml
SIGNING_CERTIFICATE: <base64-encoded-pfx>
SIGNING_PASSWORD: <certificate-password>
```

### **How to Add Secrets:**
1. Go to repository → Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Add name and value
4. Click "Add secret"

---

## 📊 Pipeline Stages Explained

### **Stage 1: Code Quality Check**

**What it does:**
- Checks code formatting (dotnet format)
- Runs code analysis (Roslyn analyzers)
- Detects code style violations

**Duration:** ~2 minutes

**If it fails:**
- Check formatting with: `dotnet format`
- Fix warnings in Visual Studio

---

### **Stage 2: Build & Test**

**What it does:**
- Restores NuGet packages (with caching)
- Builds in Debug and Release configurations
- Runs unit tests (when added)
- Generates code coverage report

**Duration:** ~5 minutes

**If it fails:**
- Check build errors in Actions log
- Run locally: `dotnet build`
- Fix compilation errors

---

### **Stage 3: Security Scan**

**What it does:**
- Scans for vulnerable NuGet packages
- Checks transitive dependencies
- Creates security report

**Duration:** ~2 minutes

**If it fails:**
- Update vulnerable packages
- Check `security-scan-results` artifact
- Run locally: `dotnet list package --vulnerable`

---

### **Stage 4: Publish**

**What it does:**
- Publishes self-contained executable
- Creates version file with build info
- Calculates SHA256 checksums
- Creates ZIP archive
- Uploads artifacts

**Duration:** ~8 minutes

**Output:**
- `SpinMonitor-Desktop-vX.X.X.XX-win-x64.zip`
- `checksums.txt`
- `version.txt`

---

### **Stage 5: Create Release**

**What it does:**
- Downloads published artifacts
- Generates release notes
- Creates GitHub Release
- Attaches build artifacts

**Duration:** ~2 minutes

**Only runs when:**
- Pushed to `main` branch
- Tag matches `v*` pattern

---

## 🔄 Development Workflow

### **Feature Development:**

```bash
# 1. Create feature branch
git checkout -b feature/search-filter

# 2. Make changes
# ... edit files ...

# 3. Commit changes
git add .
git commit -m "Add search filter feature"

# 4. Push to GitHub
git push origin feature/search-filter

# 5. Create Pull Request on GitHub
# → PR Check workflow runs automatically

# 6. Review PR results
# → Check Actions tab for build status

# 7. Merge PR
# → Full CI/CD pipeline runs on main
```

### **Release Workflow:**

```bash
# 1. Ensure main branch is stable
git checkout main
git pull origin main

# 2. Update version in .csproj
# <Version>1.1.0</Version>

# 3. Commit version change
git add .
git commit -m "Bump version to 1.1.0"
git push origin main

# 4. Create and push tag
git tag v1.1.0
git push origin v1.1.0

# 5. Wait for CI/CD to complete
# → Check Actions tab

# 6. Release created automatically
# → Go to Releases page
```

---

## 🐛 Troubleshooting

### **Build Fails: "NuGet restore failed"**

**Cause:** Network issue or missing package

**Solution:**
```bash
# Clear NuGet cache locally
dotnet nuget locals all --clear

# Restore again
dotnet restore
```

---

### **Build Fails: "Code formatting violations"**

**Cause:** Code doesn't follow formatting rules

**Solution:**
```bash
# Auto-format all code
dotnet format

# Commit formatting changes
git add .
git commit -m "Apply code formatting"
git push
```

---

### **Security Scan Fails: "Vulnerable packages found"**

**Cause:** Outdated packages with known vulnerabilities

**Solution:**
```bash
# Check vulnerable packages
dotnet list package --vulnerable

# Update specific package
dotnet add package PackageName --version X.X.X

# Or update all packages
dotnet outdated --upgrade
```

---

### **Publish Fails: "Out of memory"**

**Cause:** Large dependencies, insufficient GitHub runner resources

**Solution:**
```yaml
# In ci-cd.yml, add:
- name: Increase heap size
  run: |
    $env:DOTNET_CLI_HEAP_SIZE = "4096"
```

---

### **Release Not Created**

**Cause:** Tag format incorrect or not pushed

**Solution:**
```bash
# Ensure tag format is correct
git tag v1.0.0  # Must start with 'v'

# Push tag
git push origin v1.0.0

# Or push all tags
git push --tags
```

---

## 📈 Performance Optimization

### **Cache NuGet Packages:**
Already configured in `ci-cd.yml`:
```yaml
- uses: actions/cache@v3
  with:
    path: ~/.nuget/packages
    key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj') }}
```

**Benefit:** Reduces restore time from ~2min to ~30sec

### **Parallel Jobs:**
Multiple jobs run in parallel:
- Code Quality + Build + Security Scan (parallel)
- Publish (after above complete)
- Create Release (after publish)

**Total Pipeline Time:** ~15 minutes (sequential would be ~25 minutes)

---

## 🔒 Security Best Practices

### **1. Keep Dependencies Updated**
Run weekly dependency check:
```bash
dotnet list package --outdated
```

### **2. Enable Dependabot**
Create `.github/dependabot.yml`:
```yaml
version: 2
updates:
  - package-ecosystem: "nuget"
    directory: "/"
    schedule:
      interval: "weekly"
```

### **3. Code Signing (Production)**
Add to `ci-cd.yml`:
```yaml
- name: Sign executable
  run: |
    signtool sign /f ${{ secrets.SIGNING_CERTIFICATE }} `
      /p ${{ secrets.SIGNING_PASSWORD }} `
      /t http://timestamp.digicert.com `
      SpinMonitor.Desktop.exe
```

---

## 📊 Monitoring & Analytics

### **Build Status Badge**

Add to README.md:
```markdown
![CI/CD](https://github.com/adnanPBI/SpinMonitor_local/actions/workflows/ci-cd.yml/badge.svg)
```

### **View Build History**
1. Go to Actions tab
2. Select workflow
3. View all runs with status

### **Download Build Logs**
1. Click on workflow run
2. Click on job name
3. Click "..." → "Download log archive"

---

## 🚀 Advanced Features

### **Add Unit Testing:**

```csharp
// Create Tests/SpinMonitor.Tests.csproj
dotnet new xunit -n SpinMonitor.Tests

// Add test
[Fact]
public void StreamMonitor_ShouldDetectTrack()
{
    // Arrange
    var monitor = new StreamMonitor(...);

    // Act
    var result = monitor.DetectTrack(audioSample);

    // Assert
    Assert.NotNull(result);
    Assert.True(result.Confidence > 0.2);
}
```

Tests will run automatically in CI/CD pipeline.

---

### **Add Code Coverage:**

Already configured in `ci-cd.yml`:
```yaml
- name: Run unit tests
  run: dotnet test --collect:"XPlat Code Coverage"
```

**View coverage report:**
1. Download test-results artifact
2. Open coverage.cobertura.xml

---

### **Add Email Notifications:**

Add to `ci-cd.yml` in `notify-failure` job:
```yaml
- name: Send email notification
  uses: dawidd6/action-send-mail@v3
  with:
    server_address: ${{ secrets.SMTP_SERVER }}
    server_port: ${{ secrets.SMTP_PORT }}
    username: ${{ secrets.SMTP_USERNAME }}
    password: ${{ secrets.SMTP_PASSWORD }}
    to: ${{ secrets.NOTIFICATION_EMAIL }}
    from: GitHub Actions
    subject: Build Failed - ${{ github.repository }}
    body: |
      Build failed for commit ${{ github.sha }}
      Branch: ${{ github.ref }}
      View details: ${{ github.server_url }}/${{ github.repository }}/actions/runs/${{ github.run_id }}
```

---

## 📝 Customization

### **Change Build Configuration:**

Edit `.github/workflows/ci-cd.yml`:

```yaml
env:
  DOTNET_VERSION: '9.0.x'  # Change .NET version
  PROJECT_PATH: '...'       # Change project path
```

### **Add New Job:**

```yaml
my-custom-job:
  name: My Custom Job
  runs-on: windows-latest
  needs: [build-and-test]  # Run after build

  steps:
  - name: Checkout
    uses: actions/checkout@v4

  - name: Do something
    run: echo "Hello World"
```

### **Modify Release Notes:**

Edit `create-release` job in `ci-cd.yml`:

```powershell
@"
## Custom Release Notes
...
"@ | Out-File -FilePath release-notes.md
```

---

## 📞 Support

**Pipeline Issues:**
- Check Actions tab for error logs
- Review this guide
- Check GitHub Actions documentation

**Build Issues:**
- Ensure code builds locally first
- Check .NET version compatibility
- Review error messages in Actions log

---

## ✅ Checklist

Before pushing:
- [ ] Code builds locally (`dotnet build`)
- [ ] Tests pass locally (`dotnet test`)
- [ ] Code formatted (`dotnet format`)
- [ ] No warnings (`dotnet build /warnaserror`)
- [ ] Dependencies updated (`dotnet outdated`)
- [ ] Commit message follows convention
- [ ] PR description filled out

---

**Last Updated:** 2025-11-13
**Pipeline Version:** 1.0
**Status:** ✅ Production Ready
