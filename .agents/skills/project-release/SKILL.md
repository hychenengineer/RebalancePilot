---
name: project-release
description: >-
  Generic repository release automation skill. Given a project name, it crafts a comprehensive README.md (setup guide, demo purpose, architecture, and demo test cases), builds a standalone single-file executable (.exe), configures .gitignore, and initializes/pushes the project to GitHub as contributor "Hsinyu Chen" using stored GitHub credentials. Use whenever the user asks to prepare, package, document, or publish any project to GitHub.
---

# Generic Project Release & Publishing Skill

A reusable automation workflow for packaging, documenting, and publishing any application repository to GitHub.

## Contributor Identity
- **Contributor Name:** `Hsinyu Chen`
- **Git User Config:**
  ```powershell
  git config user.name "Hsinyu Chen"
  # Set email if configured or use GitHub noreply
  ```

## Input Parameters
- **`ProjectName`**: The user-specified product or repository name (e.g., `RebalancePilot`, `AlignAI`).
- **`GitHubRepo`** (optional): GitHub repository URL or name under user account.

---

## Workflow Steps

### Step 1: Detect Project Stack & Craft `README.md`
Generate a high-standard, professional `README.md` containing:
1. **Title & Badge Header:** Project title using `ProjectName` and a clean value proposition.
2. **What Is This Project For? (Core Value Proposition):**
   - Plain-English overview of what problem this demo solves.
   - Target personas / industry context.
3. **Software Architecture:**
   - Architectural flow diagram (ASCII or Mermaid).
   - Component responsibilities (Frontend/API, Application/Domain, Services, Storage).
   - Key separation of concerns (e.g., Semantic Intent vs. Deterministic Calculation).
4. **Setup & How to Run:**
   - Prerequisites (.NET SDK, Node, Python, Docker, etc. depending on project).
   - Build commands, database migration / seeding commands.
   - Launch instructions (local dev server URL, port).
   - Unit test execution command.
5. **Interactive Demo Cases & Examples:**
   - Tabular or bulleted scenarios showing specific inputs, expected system behavior, and outputs.
   - Sample API payloads or CLI commands to verify functionality.

### Step 2: Configure Production `.gitignore`
Inspect the project files and generate/update `.gitignore` with standard rules for the stack:
- Build artifacts (`bin/`, `obj/`, `dist/`, `build/`, `node_modules/`, `target/`)
- Local databases (`*.db`, `*.db-shm`, `*.db-wal`, `*.sqlite`)
- Environment secrets & IDE configs (`.env`, `*.user`, `*.suo`, `.vs/`, `.vscode/`)

### Step 3: Build Standalone Single-File Executable (`.exe`)
For .NET projects, package into a self-contained executable that requires no runtime installed:
```powershell
dotnet publish <ProjectPath> `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o ./dist
```
*(For Node, Python, or Go projects, adjust to `pkg`, `pyinstaller`, or `go build`).*

### Step 4: GitHub Repository Initialization & Push
Publish to GitHub with the configured contributor identity and token:

```powershell
# 1. Configure author identity
git config user.name "Hsinyu Chen"

# 2. Initialize git if not present
if (-not (Test-Path -Path ".git")) {
    git init
}

# 3. Stage and commit
git add .
git commit -m "feat: initial release of $ProjectName"

# 4. Set default branch
git branch -M main

# 5. Remote push using GitHub Access Token
# If token is stored in skill config, env variable GITHUB_TOKEN, or credential helper:
# git remote add origin https://<TOKEN>@github.com/hsinyuchen/<repo>.git
# git push -u origin main --force
```

---

## Security & Token Storage Reference
- GitHub Access Tokens should be stored in:
  `~/.gemini/antigravity/skills/project-release/credentials.json` (local machine only, never committed to any project repo).
- When pushing, the token is injected into the authenticated remote URL or passed via `Authorization` header.

