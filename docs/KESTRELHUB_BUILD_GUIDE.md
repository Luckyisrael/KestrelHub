# KestrelHub — AI Build Guide
> **For AI assistants:** Read this entire document before writing a single line of code. Follow each phase sequentially. Do not proceed to the next step until all tests in the current step pass. When in doubt, ask before assuming.

---

## Ground Rules

- **One step at a time.** Complete, test, and confirm before moving forward.
- **Tests are not optional.** Every step has automated tests (you write them) and manual tests (the developer runs them). Both must pass.
- **No skipping.** Do not stub, mock, or defer core logic to "implement later." Build it properly now.
- **Security is not an afterthought.** Every step that touches user data, secrets, tokens, or network communication must be implemented securely from the start. No "we'll harden this later."
- **Confirm before big decisions.** If a step requires an architectural choice not covered here, stop and ask.
- **Commit after every passing step.** Each step = one Git commit with a meaningful message.
- **Stack:** .NET 9, C#, PostgreSQL, YARP, Blazor WASM, MudBlazor, Docker.DotNet, gRPC, SignalR, MinIO, OpenTelemetry, Certes, ASP.NET Core Identity, Polar (billing).

---

## Repository Structure

```
KestrelHub/
├── src/
│   ├── KestrelHub.Controller/        # ASP.NET Core 9 Web API
│   ├── KestrelHub.Agent/             # Native AOT Console App
│   ├── KestrelHub.Dashboard/         # Blazor WASM
│   ├── KestrelHub.Shared/            # Shared models, DTOs, gRPC contracts
│   └── KestrelHub.Proxy/             # YARP reverse proxy host
├── tests/
│   ├── KestrelHub.Controller.Tests/
│   ├── KestrelHub.Agent.Tests/
│   └── KestrelHub.Integration.Tests/
├── docker/
│   └── agent.Dockerfile
├── docs/
│   └── KESTRELHUB_BUILD_GUIDE.md    # This file
├── KestrelHub.sln
└── docker-compose.dev.yml
```

---

## Phase 1 — The Core (MVP)

> **Goal:** A working end-to-end pipeline. Git repo → Docker image built → Container running → YARP routing traffic to it. No dashboard yet. CLI + API only.

---

### Step 1.1 — Solution Scaffold & Shared Models

**What to build:**
- Create the `.sln` file and all project stubs listed in the repo structure above
- `KestrelHub.Shared` class library with initial domain models:
  - `AppDeployment` — Id, Name, GitUrl, Branch, Status (enum: Pending, Building, Running, Failed, Stopped), CreatedAt, UpdatedAt
  - `ContainerInfo` — ContainerId, AppDeploymentId, ImageTag, Port, Status
  - `AgentHeartbeat` — AgentId, Timestamp, IsHealthy
- Add project references: Controller → Shared, Agent → Shared, Dashboard → Shared

**Automated Tests:**
```
- Models can be instantiated and serialized to JSON without errors
- Status enum values are correct (Pending=0, Building=1, Running=2, Failed=3, Stopped=4)
- AppDeployment.UpdatedAt is always >= CreatedAt
```

**Manual Test:**
```
1. dotnet build KestrelHub.sln → 0 errors, 0 warnings
2. Confirm all projects appear in the solution
3. Confirm KestrelHub.Shared has no dependencies on other KestrelHub projects
```

**Commit message:** `feat: scaffold solution structure and shared domain models`

---

### Step 1.2 — Database Setup (PostgreSQL + EF Core)

**What to build:**
- Add EF Core + Npgsql to `KestrelHub.Controller`
- `ApplicationDbContext` with DbSets for `AppDeployment` and `ContainerInfo`
- Initial migration: `InitialCreate`
- `appsettings.Development.json` with connection string pointing to local PostgreSQL
- `docker-compose.dev.yml` — PostgreSQL service on port 5432, volume for persistence
- Repository pattern: `IDeploymentRepository` + `DeploymentRepository`:
  - `CreateAsync`, `GetByIdAsync`, `GetAllAsync`, `UpdateStatusAsync`

**Automated Tests:**
```
- Use TestContainers (Testcontainers.PostgreSql) to spin up real PostgreSQL
- CreateAsync → persists record, returns with non-empty Id
- GetByIdAsync → returns null for missing Id, correct record for valid Id
- UpdateStatusAsync → status change is persisted correctly
- Migration applies cleanly to a fresh database
```

**Manual Test:**
```
1. docker compose -f docker-compose.dev.yml up -d
2. dotnet ef database update --project src/KestrelHub.Controller
3. Connect with a DB client → confirm AppDeployments, ContainerInfos tables exist
4. dotnet test → all green
```

**Commit message:** `feat: postgresql setup with ef core, migrations, and repository pattern`

---

### Step 1.3 — Git Clone & Repository Scanner

**What to build:**
- `GitService` using `LibGit2Sharp`:
  - `CloneAsync(string gitUrl, string branch, string targetPath)`
  - `CleanupAsync(string path)`
- `ProjectScanner`:
  - `ScanAsync(string repoPath)` → `ScanResult` with ProjectType, PrimaryProjectPath, DotNetVersion
- Temp directory: `/tmp/kestrelhub-builds/{deploymentId}/`

**Automated Tests:**
```
- ProjectScanner identifies .sln repo correctly
- ProjectScanner identifies single .csproj repo correctly
- ProjectScanner returns Unknown for repo with no .NET files
- ProjectScanner reads TargetFramework from sample .csproj
- CleanupAsync removes directory completely
```

**Manual Test:**
```
1. Call CloneAsync with a real GitHub repo
2. Confirm repo appears in /tmp/kestrelhub-builds/
3. Confirm ProjectScanner returns correct ProjectType and DotNetVersion
4. Confirm CleanupAsync removes it cleanly
```

**Commit message:** `feat: git clone service and dotnet project scanner`

---

### Step 1.4 — Dockerfile Generator

**What to build:**
- `DockerfileGenerator`:
  - `Generate(ScanResult scan)` → multi-stage Dockerfile string, or null if Dockerfile already exists
- Template (multi-stage, .NET 9 optimized):

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "{PROJECT_PATH}"
RUN dotnet publish "{PROJECT_PATH}" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080
ENTRYPOINT ["dotnet", "{ASSEMBLY_NAME}.dll"]
```

**Automated Tests:**
```
- Generate() contains "FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build"
- Generate() correctly substitutes project path and assembly name
- Generate() returns null when Dockerfile already exists in repo
- Generated string contains WORKDIR, COPY, ENTRYPOINT
- Handles both .sln and .csproj paths correctly
```

**Manual Test:**
```
1. Run generator against test repo from Step 1.3
2. Copy output to test directory → docker build -t test-kestrelhub .
3. Confirm image builds
4. docker run -p 8080:8080 test-kestrelhub → hit http://localhost:8080 → app responds
```

**Commit message:** `feat: multi-stage dockerfile generator for .net 9 projects`

---

### Step 1.5 — Docker Build & Run via Docker.DotNet

**What to build:**
- `DockerService`:
  - `BuildImageAsync(contextPath, dockerfilePath, imageTag, IProgress<string>)` — streams build logs
  - `RunContainerAsync(imageTag, hostPort, containerPort, Dictionary<string,string> envVars)` → `ContainerInfo`
  - `StopContainerAsync(string containerId)`
  - `GetContainerStatusAsync(string containerId)`
- `Docker.DotNet` NuGet, connecting to `/var/run/docker.sock`
- `PortAllocator` — tracks used ports in DB, assigns next available from 8100

**Automated Tests:**
```
- PortAllocator returns 8100 for first allocation
- PortAllocator returns 8101 for second (no overlap)
- PortAllocator does not return a port already in-use in DB
- GetContainerStatusAsync returns "missing" for non-existent container ID
```

**Manual Test:**
```
1. Call BuildImageAsync → watch logs stream to console via IProgress
2. Call RunContainerAsync → confirm in docker ps
3. Hit http://localhost:{assignedPort} → app responds
4. Call StopContainerAsync → confirm gone from docker ps -a
```

**Commit message:** `feat: docker build and run service via docker.dotnet`

---

### Step 1.6 — Deployment Pipeline Orchestrator

**What to build:**
- `DeploymentOrchestrator`:
  ```
  1. Create AppDeployment (Pending)
  2. Clone repo → Building
  3. Scan project → generate Dockerfile if needed
  4. Build image → stream + save logs
  5. Run container → create ContainerInfo
  6. Update status: Running
  7. On failure: status = Failed, log error
  8. Always: cleanup temp directory
  ```
- `DeploymentLog` model — DeploymentId, Timestamp, Message, IsError
- `DeploymentQueue` — `Channel<Guid>`, background `IHostedService`, processes one at a time

**Automated Tests:**
```
- Status = Building before docker build (mock DockerService)
- Status = Failed when GitService throws
- Status = Failed when BuildImageAsync throws
- Status = Running on full success
- Queue processes sequentially, not concurrently
- Cleanup always called even on failure
```

**Manual Test:**
```
1. Call DeployAsync with test repo → watch each stage logged
2. PostgreSQL: AppDeployments shows Status = Running
3. PostgreSQL: DeploymentLogs shows build output
4. docker ps → container running
5. Break GitUrl → confirm Status = Failed
```

**Commit message:** `feat: deployment pipeline orchestrator with queuing and status tracking`

---

### Step 1.7 — Deployment REST API

**What to build:**
- `DeploymentsController`:
  - `POST /api/deployments` → `202 Accepted` with Id
  - `GET /api/deployments`
  - `GET /api/deployments/{id}` (with logs)
  - `POST /api/deployments/{id}/stop`
  - `DELETE /api/deployments/{id}`
- RFC 7807 `ProblemDetails` global error handler
- Swagger/OpenAPI in Development

**Automated Tests:**
```
- POST with valid body returns 202 with valid Guid
- POST with missing gitUrl returns 400 ProblemDetails
- GET returns empty array when no deployments
- GET /{id} returns 404 for unknown Id
- GET /{id} returns correct status and log count
- Stop returns 404 for unknown Id
- Use WebApplicationFactory for all tests
```

**Manual Test:**
```
1. dotnet run → open /swagger
2. POST deployment → note Id
3. GET /{id} → watch Pending → Building → Running
4. GET / → confirm appears
5. POST /{id}/stop → docker ps confirms stopped
6. GET /{id} → Status = Stopped
```

**Commit message:** `feat: deployments rest api with swagger and error handling`

---

### Step 1.8 — YARP Routing Layer

**What to build:**
- `KestrelHub.Proxy` — ASP.NET Core host with YARP
- `RouteStore` (PostgreSQL-backed):
  - `RouteEntry`: Id, Domain, TargetPort, DeploymentId, IsActive
  - `AddRouteAsync`, `RemoveRouteAsync`, `GetAllActiveRoutes`
- YARP `InMemoryConfigProvider` — live reload, no restarts
- Controller API: `POST /api/routes`, `DELETE /api/routes/{deploymentId}`
- Proxy: port 80 (HTTP), 443 (HTTPS in Phase 5)

**Automated Tests:**
```
- AddRouteAsync persists to DB
- GetAllActiveRoutes returns only IsActive=true
- RemoveRouteAsync soft deletes (IsActive=false)
- InMemoryConfigProvider returns correct YARP clusters
- New route triggers config reload (version change)
- POST /api/routes with unknown deploymentId returns 404
```

**Manual Test:**
```
1. Start Proxy on 8080, test container on 8100
2. POST /api/routes { deploymentId, domain: "test.localhost" }
3. Add to /etc/hosts: 127.0.0.1 test.localhost
4. curl -H "Host: test.localhost" http://localhost:8080 → response from container
5. DELETE /api/routes/{id} → curl → 404
6. Proxy never restarted
```

**Commit message:** `feat: yarp dynamic routing layer with live route management`

---

### Step 1.9 — Deployment → Auto-Route Wiring

**What to build:**
- On deployment reaching `Running`: assign subdomain, create YARP route
- Format: `{name}.apps.localhost` (dev), `{name}.{baseDomain}` (prod)
- `BaseDomain` from `appsettings.json`
- `AppDeployment` gains: `AssignedDomain`, `AssignedPort`
- On stop or delete: remove YARP route

**Automated Tests:**
```
- AssignedDomain set after successful deployment
- Follows pattern {name}.{baseDomain}
- Route removed when deployment stopped
- Route removed when deployment deleted
- Two same-name deployments get different subdomains
```

**Manual Test:**
```
1. POST /api/deployments → GET /{id} → confirm assignedDomain
2. Add to /etc/hosts → curl http://myapp.apps.localhost:8080 → app responds via YARP
3. Stop deployment → curl → no response
```

**Commit message:** `feat: auto-route assignment on deployment success`

---

### ✅ Phase 1 Complete Checkpoint

```
□ dotnet test → 100% green
□ Fresh clone → docker compose up → dotnet run works with no manual setup
□ Full cycle: POST deploy → curl subdomain → app responds
□ Stop removes YARP route
□ No hardcoded secrets or connection strings in committed code
□ README updated: prerequisites, local setup, deploy test app
```

---

## Phase 2 — Authentication & Security

> **Goal:** KestrelHub is fully secured before any UI is built. Every API endpoint is protected. The setup wizard creates the first admin account. Security is the foundation, not a feature added later.

---

### Step 2.1 — ASP.NET Core Identity + Database Setup

**What to build:**
- `KestrelHubUser` extending `IdentityUser`: DisplayName, CreatedAt, LastLoginAt, IsActive
- `KestrelHubRole` extending `IdentityRole`
- Seeded roles: `Admin`, `Developer`, `Viewer`
- `ApplicationDbContext` extends `IdentityDbContext<KestrelHubUser, KestrelHubRole, string>`
- Migration: `AddIdentity`
- `SystemSettings` table (single row): IsSetupComplete, InstanceId (Guid), InstalledAt

**Security requirements:**
- Passwords: minimum 12 characters, require uppercase, lowercase, digit, special character — configure explicitly, never rely on defaults
- `IsActive = false` users cannot log in
- No default admin seeded in code — created only via setup wizard

**Automated Tests:**
```
- Password validator rejects < 12 characters
- Password validator rejects missing uppercase, digit, special character
- Three roles exist after migration: Admin, Developer, Viewer
- SystemSettings has 1 row, IsSetupComplete = false after migration
- Deactivated user cannot authenticate
- CreatedAt set automatically on user creation
```

**Manual Test:**
```
1. dotnet ef database update
2. Confirm Identity tables exist in DB
3. Confirm AspNetRoles has 3 rows
4. Confirm SystemSettings: 1 row, IsSetupComplete = false
5. Confirm no user rows exist
```

**Commit message:** `feat: aspnet core identity with roles, system settings, and strict password policy`

---

### Step 2.2 — JWT Authentication + Refresh Tokens

**What to build:**
- `JwtService`:
  - `GenerateAccessToken(user, roles)` → 15-minute JWT
  - `GenerateRefreshToken()` → 64-byte cryptographically random base64 string
  - `ValidateAccessToken(token)` → `ClaimsPrincipal` or throws
- `RefreshToken` model: Id, UserId, **TokenHash** (SHA-256 — never raw), ExpiresAt (7 days), CreatedAt, RevokedAt, ReplacedByTokenId, CreatedByIp, RevokedByIp
- Auth API:
  - `POST /api/auth/login` → `{ accessToken }` + HttpOnly refresh cookie
  - `POST /api/auth/refresh` → rotates token, new access token + new cookie
  - `POST /api/auth/logout` → revokes refresh token
  - `GET /api/auth/me` → current user info
- All endpoints except `/api/auth/*` and `/setup/*` require `[Authorize]`

**Security requirements:**
- JWT secret minimum 32 characters — throw on startup if shorter
- Refresh token stored as SHA-256 hash ONLY
- 5 failed logins → lock account 15 minutes (Identity lockout)
- Login error: identical generic message for wrong email AND wrong password (no user enumeration)
- Auth endpoints rate limited: 10 requests/minute/IP
- HttpOnly, Secure, SameSite=Strict on refresh token cookie
- Access token in response body only — never in a cookie

**Automated Tests:**
```
- GenerateAccessToken expires in 15 minutes
- GenerateAccessToken includes correct role claims
- ValidateAccessToken throws on expired token
- ValidateAccessToken throws on tampered token
- GenerateRefreshToken produces 64-byte base64 (non-sequential)
- Refresh token stored as SHA-256 hash, raw value never in DB
- POST /api/auth/login correct credentials → access token + HttpOnly cookie
- POST /api/auth/login wrong password → 401 generic message
- POST /api/auth/login wrong email → same 401 identical generic message
- POST /api/auth/refresh valid token → new access token, old token revoked
- POST /api/auth/refresh revoked token → 401 + ALL user tokens revoked (theft detection)
- POST /api/auth/refresh expired token → 401
- POST /api/auth/logout → token revoked
- After logout, old refresh token → 401
- GET /api/auth/me no token → 401
- GET /api/auth/me valid token → user info
- 5 failed logins → account locked
- Locked account correct password → still 401
```

**Manual Test:**
```
1. Login with non-existent user → 401 generic message
2. Login correct credentials → access token in body, Set-Cookie HttpOnly
3. GET /api/auth/me with token → user info
4. GET /api/auth/me no token → 401
5. GET /api/auth/me tampered token → 401
6. Wait for access token to expire (set to 1min for test) → 401
7. POST /api/auth/refresh → new access token issued
8. POST /api/auth/logout → cookie cleared
9. Old refresh token → 401
10. Fail login 5 times → 6th attempt rejected even with correct password
```

**Commit message:** `feat: jwt auth with rotating refresh tokens, rate limiting, and account lockout`

---

### Step 2.3 — Setup Wizard API

**What to build:**
- Only accessible when `IsSetupComplete = false` — all setup endpoints return 404 permanently after
- `GET /api/setup/status` → `{ isSetupComplete }` — always accessible
- `POST /api/setup/complete` — `{ adminEmail, adminPassword, adminDisplayName, instanceName }`:
  1. Validate all fields (same password rules as Step 2.1)
  2. Create admin user with Admin role
  3. Set `IsSetupComplete = true` atomically
  4. Return `{ accessToken }` — logs admin in immediately
- `SetupGuardMiddleware`: if not complete → redirect non-setup requests to `/setup`; if complete → setup endpoints return 404

**Security requirements:**
- `POST /api/setup/complete` executable exactly once — 404 thereafter
- Completion is atomic — if user creation fails, `IsSetupComplete` stays false
- Rate limit to 3 attempts/IP during setup window

**Automated Tests:**
```
- GET /api/setup/status returns isSetupComplete=false before setup
- POST /api/setup/complete creates admin with Admin role
- POST /api/setup/complete sets IsSetupComplete=true
- POST /api/setup/complete returns access token
- Second call to POST /api/setup/complete returns 404
- Weak password returns 400 with validation errors
- Missing fields return 400
- After setup, GET /api/setup/status returns isSetupComplete=true
- SetupGuardMiddleware redirects when not complete
- SetupGuardMiddleware does NOT redirect after setup complete
- Atomicity: simulate user creation failure → IsSetupComplete stays false
```

**Manual Test:**
```
1. Fresh DB (IsSetupComplete=false)
2. GET /api/setup/status → { isSetupComplete: false }
3. GET /api/deployments → redirected to /setup
4. POST /api/setup/complete with valid data
5. Admin user in DB with Admin role
6. SystemSettings.IsSetupComplete = true
7. Access token returned → GET /api/auth/me → admin returned
8. POST /api/setup/complete again → 404
9. GET /api/deployments with token → 200
```

**Commit message:** `feat: setup wizard api with guard middleware and atomic admin creation`

---

### Step 2.4 — Role-Based Authorization

**What to build:**
- Authorization policies:
  - `AdminOnly` — Admin role
  - `DeveloperOrAbove` — Admin or Developer
  - `AnyAuthenticatedUser` — any role + IsActive=true
- Apply to all existing endpoints:
  - `POST /api/deployments` → DeveloperOrAbove
  - `DELETE /api/deployments/{id}` → AdminOnly
  - `POST /api/deployments/{id}/stop` → DeveloperOrAbove
  - `GET /api/deployments*` → AnyAuthenticatedUser
  - All route endpoints → AdminOnly
  - All secret endpoints → DeveloperOrAbove
- `ICurrentUserService` — UserId, Email, Roles, IsAdmin, IsDeveloper
- Custom middleware: checks `IsActive` on every request — deactivated users rejected even with valid JWT

**Automated Tests:**
```
- Viewer cannot POST /api/deployments → 403
- Viewer can GET /api/deployments → 200
- Developer can POST /api/deployments → 202
- Developer cannot DELETE /api/deployments/{id} → 403
- Admin can DELETE /api/deployments/{id} → 200
- Deactivated user with valid JWT → 401
- Unauthenticated request → 401 (not 403)
```

**Manual Test:**
```
1. Create Admin, Developer, Viewer test users
2. Viewer: POST /api/deployments → 403, GET → 200
3. Developer: POST → 202, DELETE → 403
4. Admin: DELETE → 200
5. Deactivate Developer in DB (IsActive=false)
6. Developer login → token issued
7. Developer token GET /api/deployments → 401
```

**Commit message:** `feat: role-based authorization with active user enforcement`

---

### Step 2.5 — Blazor Login & Setup Screens

**What to build:**
- `JwtAuthStateProvider` implementing `AuthenticationStateProvider`:
  - Access token in **memory only** — never localStorage
  - On app load: attempt silent refresh via `POST /api/auth/refresh` (HttpOnly cookie sent automatically)
- Pages:
  - `/setup` — Setup Wizard: Instance Name, Admin Email, Display Name, Password + confirm, password strength indicator
  - `/login` — Email + password, generic error on failure, no "remember me"
  - `/logout` — clears memory token, calls logout API, redirects to `/login`
- `AuthorizeRouteView` wrapping all dashboard routes
- App startup flow:
  1. GET /api/setup/status
  2. Not complete → redirect /setup
  3. Complete → attempt silent refresh
  4. Refresh success → dashboard
  5. Refresh fail → /login

**Security requirements:**
- Access token in memory ONLY — never localStorage, never sessionStorage
- HttpOnly cookie managed by server — Blazor never touches it
- All dashboard routes in `<AuthorizeView>` — no route accidentally public
- Setup page inaccessible after completion

**Automated Tests (bUnit):**
```
- JwtAuthStateProvider returns unauthenticated state with no token
- JwtAuthStateProvider returns authenticated state after login
- JwtAuthStateProvider attempts silent refresh on init
- Login page shows generic error on 401 (not "wrong password" or "user not found")
- Login redirects to / on success
- Setup page renders all required fields
- Setup page shows password strength indicator
- Unauthenticated user visiting /deployments → redirected to /login
- After logout → /deployments redirects to /login
```

**Manual Test:**
```
1. Fresh install → navigate to / → redirected to /setup
2. Complete setup → redirected to dashboard
3. Refresh page → still logged in (silent refresh via cookie)
4. DevTools → Application → LocalStorage → NO tokens stored
5. DevTools → Cookies → HttpOnly refresh token cookie exists
6. Navigate to /setup → redirected to /login (setup complete)
7. Logout → /login
8. Visit /deployments without login → /login
9. Login → redirected back to /deployments
10. Close browser entirely → reopen → silent refresh logs you back in
11. Logout → close → reopen → must log in manually
```

**Commit message:** `feat: blazor login and setup screens with secure in-memory token storage`

---

### Step 2.6 — Security Headers & HTTPS Enforcement

**What to build:**
- Security headers middleware on all responses:
  ```
  Strict-Transport-Security: max-age=31536000; includeSubDomains
  X-Content-Type-Options: nosniff
  X-Frame-Options: DENY
  X-XSS-Protection: 1; mode=block
  Referrer-Policy: strict-origin-when-cross-origin
  Content-Security-Policy: default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline' fonts.googleapis.com; font-src fonts.gstatic.com; img-src 'self' data:; connect-src 'self' wss:
  Permissions-Policy: camera=(), microphone=(), geolocation=()
  ```
- HTTPS redirection in production, HSTS configured
- CORS locked to configured dashboard domain only — no wildcards
- `Server` header removed from all responses
- `X-Powered-By` removed

**Automated Tests:**
```
- Response includes all required security headers
- Response does NOT include Server header
- Response does NOT include X-Powered-By
- CORS rejects requests from unlisted origins
- CORS accepts configured dashboard domain
```

**Manual Test:**
```
1. Run in production mode
2. curl -I https://localhost:{port}/api/health → confirm all headers present
3. Confirm Server header absent
4. Cross-origin request from different port → CORS rejected
5. Scan with https://securityheaders.com → aim for A or A+
```

**Commit message:** `feat: security headers, hsts, cors lockdown, and server header removal`

---

### ✅ Phase 2 Complete Checkpoint

```
□ dotnet test → 100% green
□ Fresh install → setup → login → dashboard all work end-to-end
□ Access token NEVER in localStorage (verify DevTools)
□ Refresh token cookie: HttpOnly, Secure, SameSite=Strict
□ All three roles enforced correctly
□ Deactivated users rejected with valid JWT
□ Account lockout after 5 failed attempts
□ Login errors are generic (no user enumeration)
□ All security headers present in production mode
□ Setup wizard inaccessible after first completion
□ No secrets or JWT keys in committed code
□ README updated: first-run setup instructions
```

---

## Phase 3 — The Dashboard

> **Goal:** Functional Blazor WASM dashboard. All routes protected. Deployments, logs, secrets, users managed from the browser.

---

### Step 3.1 — App Shell + MudBlazor

**What to build:**
- Configure Dashboard as Blazor WASM hosted by Controller
- MudBlazor dark theme: Primary `#00d4ff`, Secondary `#7c3aed`, Background `#0a0c0f`
- `MainLayout.razor`: left sidebar (Deployments, Secrets, Storage, Observability, Settings [Admin only]), top bar (logo, display name, logout)
- `<AuthorizeRouteView>` on all routes
- `HttpClient` with `AuthorizingMessageHandler` — injects Bearer token, triggers silent refresh on expiry

**Automated Tests (bUnit):**
```
- All nav links render for Admin
- Settings link hidden for Developer and Viewer
- Top bar shows display name
- Logout calls POST /api/auth/logout and redirects to /login
- AuthorizingMessageHandler attaches Bearer token
- AuthorizingMessageHandler triggers silent refresh on expired token
```

**Manual Test:**
```
1. Login as Admin → Settings visible in sidebar
2. Login as Developer → Settings hidden
3. Top bar shows display name
4. Logout → /login
5. DevTools Network → any API call → Authorization: Bearer {token} header
6. No console errors
```

**Commit message:** `feat: blazor dashboard shell with mudblazor, auth-aware nav, and token injection`

---

### Step 3.2 — Deployments List Page

**What to build:**
- `/deployments` — MudDataGrid: Name, Git URL, Branch, Status chip, Domain, Created, Actions
- Status chips: Pending=grey, Building=amber, Running=green, Failed=red, Stopped=grey
- New Deployment button (Developer+) → MudDialog form
- Actions: Stop (Developer+), Delete (Admin only)
- Real-time updates via SignalR (no polling)
- `DeploymentApiClient` typed HttpClient

**Automated Tests (bUnit):**
```
- Empty state when no deployments
- Correct row count for mocked data
- Running=green, Failed=red chips
- New Deployment hidden for Viewer
- Delete hidden for Developer
- Empty GitUrl shows validation error
```

**Manual Test:**
```
1. Admin: all buttons visible. Viewer: New + Delete hidden
2. Create deployment → status updates in real-time
3. Stop → updates live
4. Delete → row disappears
5. Empty form submit → validation errors
```

**Commit message:** `feat: deployments list with signalr real-time updates and role-aware actions`

---

### Step 3.3 — Real-time Log Streaming via SignalR

**What to build:**
- `DeploymentHub` at `/hubs/deployments` — requires authentication
  - `JoinDeployment`, `LeaveDeployment`
  - Server pushes: `ReceiveLog(deploymentId, message, isError)`, `StatusChanged(deploymentId, newStatus)`
- Orchestrator pushes logs and status to hub in real-time
- `DeploymentDetail.razor` at `/deployments/{id}`:
  - Info header, real-time log terminal (dark, monospace, auto-scroll), status badge
  - Tabs: Logs, Settings, Secrets, Storage, Observability

**Automated Tests:**
```
- Unauthenticated SignalR connection rejected
- JoinDeployment adds caller to correct group
- Orchestrator log → hub → client receives (integration test)
- Status change → broadcast to correct group
- bUnit: lines in order, error lines red, auto-scroll on new message
```

**Manual Test:**
```
1. Open /deployments/{id} during active deployment
2. Logs stream live — no page refresh
3. Status chip updates live in list page
4. Disconnect network → reconnect → SignalR reconnects
5. Error lines appear in red, auto-scroll works
```

**Commit message:** `feat: signalr real-time log streaming with authenticated hub`

---

### Step 3.4 — Secret Vault

**What to build:**
- `Secret` model: Id, DeploymentId (nullable), Key, EncryptedValue, Environment enum, CreatedAt, LastAccessedAt
- `SecretAuditLog`: Id, SecretId, Action enum (Created/Read/Updated/Deleted), ActorUserId, ActorEmail, Timestamp
- `SecretVaultService`:
  - `SetSecretAsync` — AES-256-GCM encryption via `System.Security.Cryptography`
  - `GetSecretAsync` — decrypt + write audit log with actor from `ICurrentUserService`
  - `GetAllSecretsAsync` — keys only, never values
  - `DeleteSecretAsync` — soft delete
- Master key from `KESTRELHUB_MASTER_KEY` env var — throw on startup if missing or < 32 bytes
- Inject secrets as env vars at container start
- Dashboard: `/secrets` — environment tabs, masked values, audit log

**Automated Tests:**
```
- DB stored value != plaintext
- GetSecretAsync returns correct decrypted value
- GetSecretAsync writes audit log with correct actor
- GetAllSecretsAsync never returns plaintext
- Soft delete: not returned after deletion
- AES-GCM nonce differs per encryption (same key+plaintext → different ciphertext)
- Missing KESTRELHUB_MASTER_KEY → startup exception
- Short KESTRELHUB_MASTER_KEY (< 32 bytes) → startup exception
- bUnit: values shown as ••••••••, audit log shows entries
```

**Manual Test:**
```
1. Add secret: MY_API_KEY=super-secret-123, Env=Production
2. UI shows ••••••••
3. DB: EncryptedValue != "super-secret-123"
4. Click reveal → correct value shown
5. Audit log: Created entry
6. Deploy app reading MY_API_KEY → correct value in container
7. Delete → gone from list, Deleted in audit log
```

**Commit message:** `feat: aes-256-gcm secret vault with actor audit logging`

---

### Step 3.5 — Live Appsettings Editor

**What to build:**
- `AppSettings` model: Id, DeploymentId, Key, Value, AppliedAt
- `AppSettingsService`: SetSettingAsync, GetSettingsAsync, ApplyToContainerAsync (new container starts before old stops)
- Dashboard: Settings tab on `/deployments/{id}` — key-value editor, Apply & Restart button

**Automated Tests:**
```
- SetSettingAsync persists to DB
- GetSettingsAsync returns only correct deploymentId settings
- ApplyToContainerAsync passes env vars to RunContainerAsync
- Old container stopped only after new one starts
- bUnit: Apply & Restart disabled during restart
```

**Manual Test:**
```
1. Add appsetting: MyFeatureFlag=true → Apply & Restart
2. Watch restart logs via SignalR
3. App running after restart
4. docker inspect {containerId} → MyFeatureFlag=true in env vars
```

**Commit message:** `feat: live appsettings editor with zero-downtime restart`

---

### Step 3.6 — User Management (Admin)

**What to build:**
- Admin-only `/settings/users`: list users, invite, deactivate, change role, delete
- `UserInvite` model: Id, Email, TokenHash (SHA-256), Role, CreatedAt, ExpiresAt (48h), UsedAt
- Invite flow: Admin generates link → recipient sets password at `/accept-invite?token={token}` → user created
- API: `POST /api/users/invite`, `GET /api/users`, `PUT /api/users/{id}`, `DELETE /api/users/{id}`

**Security requirements:**
- Invite token stored as SHA-256 hash only — raw token never persisted
- Tokens expire after 48 hours, single-use
- User cannot deactivate or delete their own account — enforced server-side
- Only Admin role can access user management API

**Automated Tests:**
```
- GET /api/users returns 403 for Developer and Viewer
- POST /api/users/invite stores hashed token
- Invite token expires after 48 hours
- Used token cannot create another user
- PUT /{id} (deactivate self) returns 400
- DELETE /{id} (delete self) returns 400
- bUnit: deactivate and delete buttons disabled for own row
```

**Manual Test:**
```
1. Admin: /settings/users → invite new user → open invite link in incognito
2. Set password → account created → log in → correct role enforced
3. Try deactivating own account → blocked
4. Deactivate new user → they cannot log in
5. Developer: navigate to /settings/users → 403
```

**Commit message:** `feat: admin user management with invite system and role assignment`

---

### ✅ Phase 3 Complete Checkpoint

```
□ dotnet test → 100% green
□ Full dashboard workflow: deploy → logs → secrets → settings → users
□ Role enforcement verified for all three roles across all pages
□ Secret values never in API responses as plaintext
□ SignalR reconnects gracefully
□ Invite flow works end-to-end
□ User cannot deactivate or delete themselves
□ README updated with dashboard usage guide
```

---

## Phase 4 — Media, Storage & Observability

> **Goal:** MinIO as first-class infrastructure. OpenTelemetry observability. Health checks.

---

### Step 4.1 — MinIO Container Provisioning

**What to build:**
- `MinioInstance` model: Id, DeploymentId, ContainerId, ApiPort, ConsolePort, AccessKey, SecretKey (AES-256-GCM encrypted), BucketName, Status
- `MinioProvisioningService`: ProvisionAsync, DeprovisionAsync, GetStatusAsync
- Volume: `/data/kestrelhub/minio/{deploymentId}`
- Default bucket: `{appname}-assets`
- API: `POST /api/storage/{deploymentId}/provision`, `DELETE`, `GET`
- Inject into app containers: `STORAGE_ENDPOINT`, `STORAGE_ACCESS_KEY`, `STORAGE_SECRET_KEY`, `STORAGE_BUCKET`

**Automated Tests:**
```
- ProvisionAsync creates MinioInstance in DB
- Generated credentials: ≥16 chars, cryptographically random
- SecretKey stored encrypted (not plaintext)
- Second provision for same deploymentId → 409
- DeprovisionAsync sets status to Deprovisioned
```

**Manual Test:**
```
1. POST /api/storage/{deploymentId}/provision
2. docker ps → minio container running
3. Open MinIO console → log in with generated credentials
4. Bucket {appname}-assets exists
5. Upload file → persists in /data/kestrelhub/minio/{deploymentId}
```

**Commit message:** `feat: minio container provisioning with encrypted credential storage`

---

### Step 4.2 — Pre-signed URLs & Asset API

**What to build:**
- `MinioAssetService` (AWSSDK.S3):
  - `GenerateUploadUrlAsync` → pre-signed PUT URL
  - `GenerateDownloadUrlAsync` → pre-signed GET URL
  - `ListAssetsAsync` → `AssetInfo` list
  - `DeleteAssetAsync`
- API: `POST /api/storage/{id}/upload-url`, `GET /api/storage/{id}/assets`, `DELETE /api/storage/{id}/assets/{key}`

**Automated Tests:**
```
- Upload URL contains object key
- Download URL for correct bucket and key
- ListAssetsAsync returns correct AssetInfo structure
- DeleteAssetAsync removes object (TestContainers MinIO integration test)
- Credentials never in asset list response
```

**Manual Test:**
```
1. POST upload-url → curl -X PUT --upload-file test.png "{url}"
2. GET /assets → test.png appears
3. Download URL → image loads in browser
4. DELETE → disappears from list
```

**Commit message:** `feat: presigned urls and asset management api`

---

### Step 4.3 — Asset Browser Dashboard

**What to build:**
- `/storage` page and tab on `/deployments/{id}`:
  - Grid with thumbnails (images) and file icons (others)
  - Upload → file picker → pre-signed URL → direct browser-to-MinIO upload
  - Download and Delete per asset (confirm dialog)
  - Storage usage display

**Automated Tests (bUnit):**
```
- Empty state when no assets
- Thumbnail for image/*, file icon for others
- Delete shows confirm dialog
- Upload triggers pre-signed URL request
```

**Manual Test:**
```
1. Upload image → thumbnail appears
2. Upload PDF → file icon (not broken image)
3. Download works
4. Delete → confirm dialog → asset gone
5. Check MinIO console → actually deleted
```

**Commit message:** `feat: asset browser with direct-to-minio upload`

---

### Step 4.4 — OpenTelemetry Observability

**What to build:**
- OTel Collector container per project
- KestrelHub Controller instrumented with OTel (traces, metrics, logs)
- `TelemetryStore` — last N log entries and metric snapshots per deployment
- API: `GET /api/observability/{id}/logs`, `GET /api/observability/{id}/metrics`
- Dashboard: Observability tab — searchable log viewer (Info/Warning/Error), metrics charts, health check status per container (`/health` endpoint polling)

**Automated Tests:**
```
- SaveLogAsync persists with correct deploymentId
- GetLogsAsync descending by timestamp
- GetLogsAsync filters by log level
- Health check reader returns correct status from mock /health
- bUnit: Info plain, Warning amber, Error red
- bUnit: Health check green/red correctly
```

**Manual Test:**
```
1. Deploy OTel-instrumented .NET app
2. Hit endpoints → navigate to Observability tab → logs appear
3. Metrics charts show request rate
4. Stop app's DB → health check turns red within 30 seconds
5. Search logs by keyword → filtering works
```

**Commit message:** `feat: opentelemetry observability with log aggregation and metrics`

---

### ✅ Phase 4 Complete Checkpoint

```
□ dotnet test → 100% green
□ MinIO provisions, deprovisions, persists across restarts
□ Pre-signed URLs work for upload and download
□ App containers receive storage env vars
□ Asset browser works end-to-end
□ No MinIO credentials in API list responses
□ OTel data flowing from deployed app to dashboard
□ Health checks reflect real container state
```

---

## Phase 5 — SSL, Billing & Production Hardening

> **Goal:** Let's Encrypt SSL, Polar-powered Pro licensing with phone-home validation, agent extraction, and full production readiness.

---

### Step 5.1 — Let's Encrypt SSL via Certes

**What to build:**
- `SslService` (Certes NuGet): ProvisionCertificateAsync, RenewCertificateAsync, GetCertificateStatusAsync
- `CertificateStore` model: Id, Domain, ExpiresAt, PfxData (AES-256-GCM encrypted), Status
- YARP updated for HTTPS with dynamic cert loading
- Background renewal `IHostedService` — every 12 hours, renew if expiry < 30 days
- `DnsVerificationService` (DnsClient) — verify A record before ACME attempt
- API: `POST /api/ssl/{deploymentId}`, `GET /api/ssl/{deploymentId}`

**Automated Tests:**
```
- DnsVerificationService returns false for unpointed domain
- DnsVerificationService returns true for correctly pointed domain
- SslService calls DNS verification before ACME
- SslService stores encrypted PFX
- Renewal service only renews certs expiring within 30 days
```

**Manual Test:**
```
1. Point real domain to VPS
2. POST /api/ssl/{deploymentId} → ACME completes in logs
3. https://{domain} in browser → valid cert, no warning
4. GET /api/ssl/{deploymentId} → expiry ~90 days out
5. Unpointed domain → DNS verification fails with clear error
```

**Commit message:** `feat: lets encrypt ssl with dns verification and auto-renewal`

---

### Step 5.2 — Polar Billing & License Key Validation

**What to build:**

**Overview of licensing model:**
- User purchases Pro on Polar → Polar issues a license key → user enters key in KestrelHub → KestrelHub validates against Polar API every 24 hours → Pro features unlock
- Grace period: if Polar is unreachable, Pro features remain active for 7 days before expiring
- Raw license key is NEVER stored — only SHA-256 hash

**Polar setup (document in README, outside codebase):**
- Create "KestrelHub Pro" product on Polar
- Configure license key generation per purchase
- Create webhooks for `order.created` and `subscription.revoked`
- Store Polar API key and webhook secret in environment variables

**`LicenseKey` model (single DB row):**
- KeyHash (SHA-256), ActivatedAt, LastValidatedAt, ValidUntil, Status (enum: Active, GracePeriod, Expired, Invalid), PolarSubscriptionId, PlanName

**`LicensingService`:**
- `ActivateLicenseAsync(string rawKey)`:
  1. Hash key (SHA-256)
  2. Call `POST https://api.polar.sh/v1/licenses/validate` with hash
  3. If valid: store hash + metadata, Status = Active
  4. If invalid: return error, store nothing
- `ValidateLicenseAsync()` — called by background service every 24 hours:
  1. Call Polar API
  2. Valid → update LastValidatedAt, extend ValidUntil
  3. Polar unreachable + LastValidatedAt < 7 days ago → Status = GracePeriod (features stay on, banner shown)
  4. Polar unreachable + LastValidatedAt > 7 days ago → Status = Expired
  5. Polar returns cancelled/invalid → Status = Expired immediately
- `IsProFeatureEnabled()` → true if Status = Active or GracePeriod (within 7 days)

**`LicenseValidationService`** (`IHostedService`) — runs ValidateLicenseAsync every 24 hours

**Polar webhook receiver:**
- `POST /webhooks/polar` — validates HMAC-SHA256 against `KESTRELHUB_POLAR_WEBHOOK_SECRET`
- `subscription.revoked` → Status = Expired immediately

**`IProFeatureGate`:**
- `RequireProAsync()` — throws `ProFeatureRequiredException` if not Active or GracePeriod
- Applied to:
  - `POST /api/agents` (register additional agents)
  - `POST /api/users/invite` when existing user count > 1
  - SSO configuration endpoints

**API:**
- `POST /api/license/activate` — `{ licenseKey }` (Admin only)
- `GET /api/license/status` — `{ status, planName, validUntil, lastValidated }` (Admin only)
- `DELETE /api/license` — deactivate (Admin only)

**Dashboard: `/settings/license` (Admin only):**
- Current plan: Free or Pro with expiry
- License key input (type="password" — shown once, never displayed again after activation)
- Status: Active (green), GracePeriod (amber + days remaining), Expired (red)
- Site-wide grace period banner when Status = GracePeriod
- "Upgrade to Pro" link → Polar product page

**Security requirements:**
- Raw license key NEVER stored — SHA-256 hash only
- License key NEVER appears in application logs (mask it before any logging)
- Polar webhook signature validated on every request — reject without valid HMAC
- `POST /api/license/activate` rate limited to 5 attempts/hour/IP
- License key input field: `type="password"`

**Automated Tests:**
```
- ActivateLicenseAsync stores SHA-256 hash, not raw key
- Raw key does not appear in DB after activation
- ActivateLicenseAsync calls Polar API correctly
- ValidateLicenseAsync sets GracePeriod when Polar unreachable + LastValidatedAt < 7 days
- ValidateLicenseAsync sets Expired when Polar unreachable + LastValidatedAt > 7 days
- ValidateLicenseAsync sets Expired immediately when Polar returns cancelled
- IsProFeatureEnabled returns true for Active
- IsProFeatureEnabled returns true for GracePeriod (within 7 days)
- IsProFeatureEnabled returns false for Expired
- POST /api/agents returns 402 when Free
- POST /api/users/invite returns 402 when user count > 1 and Free
- POST /webhooks/polar invalid HMAC → 401
- POST /webhooks/polar valid HMAC + subscription.revoked → Status = Expired
- POST /api/license/activate invalid key → 400 with clear error
- bUnit: License page shows "Free Plan" with no license
- bUnit: License page shows "Pro Plan" with expiry when active
- bUnit: Grace period banner shown when GracePeriod
- bUnit: License key input is type="password"
```

**Manual Test:**
```
1. /settings/license → "Free Plan" shown
2. Try invite second user → 402 with upgrade message
3. Enter invalid license key → clear error, no status change
4. Enter valid Polar license key (real test purchase)
5. Status = Active, plan name, expiry date shown
6. Pro feature (invite second user) → now works
7. Check DB → only hash stored, not raw key
8. Check app logs → raw key does NOT appear
9. POST /webhooks/polar with subscription.revoked event
   → Status changes to Expired immediately
10. Try Pro feature → 402
11. Simulate 6-day grace (set LastValidatedAt 6 days ago, mock Polar unreachable)
    → GracePeriod: Pro features on, banner shown
12. Simulate 8-day grace → Expired, Pro features blocked
```

**Commit message:** `feat: polar license key activation with 24h phone-home validation and 7-day grace period`

---

### Step 5.3 — Agent Extraction & gRPC Communication

**What to build:**
- Move `DockerService` into `KestrelHub.Agent` (Native AOT, gRPC server port 50051)
- `.proto` in `KestrelHub.Shared`: `BuildImage` (streaming), `RunContainer`, `StopContainer`, `GetContainerStatus`, `Heartbeat`
- Controller updated to communicate via gRPC
- `AgentRegistry`: tracks agents, IP, last heartbeat, status
- Agent registration: `POST /api/agents` — Pro feature, gated by `IProFeatureGate`
- Dashboard: agent status on `/settings/infrastructure`

**Automated Tests:**
```
- Proto compiles without errors
- Heartbeat updates LastHeartbeat in AgentRegistry
- Agent marked Degraded after 90 seconds no heartbeat
- BuildImage streams at least one log message
- RunContainer returns non-empty ContainerId
- POST /api/agents returns 402 when Free
- Controller errors cleanly if Agent unreachable
```

**Manual Test:**
```
1. Build Agent: dotnet publish -r linux-x64 -c Release
2. Run on second machine (Pro license active)
3. Register agent → deploy test app → docker ps on WORKER confirms running there
4. Kill Agent → Degraded in dashboard within 90 seconds
5. Restart Agent → reconnects, status Healthy
6. Try register agent without Pro → 402
```

**Commit message:** `feat: agent extraction with grpc, heartbeat monitoring, and pro gate`

---

### Step 5.4 — GitHub Webhook & Production Hardening

**What to build:**
- `POST /webhooks/github` — validates HMAC-SHA256 (`X-Hub-Signature-256`), triggers deployment async
- Rate limiting on all public endpoints via `AspNetCoreRateLimit`
- `docker-compose.production.yml` — all services, env var references only, no inline secrets
- `install.sh` — pulls images, runs compose, initialises DB, prints post-install summary
- `README.md` — complete: prerequisites, install, first-run, configuration reference, Polar setup guide

**Automated Tests:**
```
- Webhook invalid HMAC → 401
- Webhook valid HMAC → deployment triggered (mock orchestrator)
- Rate limiter returns 429 after threshold
- GET /health → 200 when all checks pass
- GET /health → 503 when DB unavailable
```

**Manual Test:**
```
1. Fresh Ubuntu 22.04 VPS → run install.sh → everything starts
2. Navigate to http://{vps-ip} → setup wizard appears
3. Configure GitHub webhook → push commit → auto-deployment triggers
4. GET /health → all green
5. Stop PostgreSQL container → GET /health → 503
6. Restart PostgreSQL → /health returns green
```

**Commit message:** `feat: github webhooks, rate limiting, production compose, and install script`

---

### ✅ Phase 5 Complete — Platform Ready for Release

```
□ dotnet test → 100% green across all test projects
□ Fresh VPS install works via install.sh with no manual steps
□ HTTPS working with valid Let's Encrypt cert on real domain
□ GitHub push → auto deployment end-to-end
□ Polar license activation and validation working with real purchase
□ Pro features blocked without valid license
□ Grace period behaviour verified (7-day window)
□ Agent runs on separate machine, Controller orchestrates remotely (Pro)
□ All security headers present — securityheaders.com A or A+
□ Raw license key never in DB or logs
□ No secrets, keys, or credentials in source code or Docker images
□ Security checklist (Appendix D) completed in full
□ CHANGELOG.md complete, VERSION = 1.0.0
□ GitHub repo: README, LICENSE (MIT), CONTRIBUTING.md present
□ GitHub release tagged: v1.0.0
□ Polar product page live with correct pricing
```

---

## Appendix A — Tech Stack Reference

| Layer | Technology | NuGet / Package |
|---|---|---|
| API | ASP.NET Core 9 | `Microsoft.AspNetCore` |
| Auth | ASP.NET Core Identity | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` |
| ORM | Entity Framework Core 9 | `Microsoft.EntityFrameworkCore` |
| Database | PostgreSQL | `Npgsql.EntityFrameworkCore.PostgreSQL` |
| Reverse Proxy | YARP | `Yarp.ReverseProxy` |
| Frontend | Blazor WASM | `Microsoft.AspNetCore.Components.WebAssembly` |
| UI Components | MudBlazor | `MudBlazor` |
| Real-time | SignalR | `Microsoft.AspNetCore.SignalR` |
| Docker SDK | Docker.DotNet | `Docker.DotNet` |
| gRPC | Grpc.AspNetCore | `Grpc.AspNetCore` |
| Git | LibGit2Sharp | `LibGit2Sharp` |
| SSL | Certes | `Certes` |
| DNS | DnsClient.NET | `DnsClient` |
| Storage SDK | AWS S3 Compatible | `AWSSDK.S3` |
| Observability | OpenTelemetry | `OpenTelemetry.Extensions.Hosting` |
| Billing | Polar | REST API via HttpClient |
| Rate Limiting | AspNetCoreRateLimit | `AspNetCoreRateLimit` |
| Testing | xUnit + TestContainers | `Testcontainers.PostgreSql`, `Testcontainers.Minio` |
| Component Testing | bUnit | `bunit` |

---

## Appendix B — Environment Variables Reference

```bash
# Controller — ALL required unless marked optional
KESTRELHUB_DB_CONNECTION=Host=localhost;Database=kestrelhub;Username=kh;Password=...
KESTRELHUB_MASTER_KEY=<32-byte-minimum-base64>        # Secret/cert encryption — REQUIRED
KESTRELHUB_JWT_SECRET=<32-byte-minimum-random>        # JWT signing — REQUIRED
KESTRELHUB_JWT_ISSUER=https://yourdomain.com
KESTRELHUB_JWT_AUDIENCE=kestrelhub-dashboard
KESTRELHUB_BASE_DOMAIN=apps.yourdomain.com
KESTRELHUB_GITHUB_WEBHOOK_SECRET=<random-string>      # Optional — only if using GitHub webhooks
KESTRELHUB_POLAR_WEBHOOK_SECRET=<from-polar>          # Required for billing
KESTRELHUB_POLAR_API_KEY=<from-polar>                 # Required for billing
KESTRELHUB_AGENT_ENDPOINT=http://agent-host:50051     # Pro — multi-server only

# Agent
KESTRELHUB_CONTROLLER_URL=http://controller-host:5000
KESTRELHUB_AGENT_ID=<uuid>

# Auto-injected into deployed app containers (generated per deployment, never set manually)
STORAGE_ENDPOINT=http://minio-host:{port}
STORAGE_ACCESS_KEY=<generated>
STORAGE_SECRET_KEY=<generated>
STORAGE_BUCKET={appname}-assets
```

---

## Appendix C — Pro Feature Gate Reference

| Feature | Free | Pro |
|---|---|---|
| Single server / agent | ✅ | ✅ |
| All deployment features | ✅ | ✅ |
| MinIO storage | ✅ | ✅ |
| Secret Vault | ✅ | ✅ |
| OpenTelemetry | ✅ | ✅ |
| SSL automation | ✅ | ✅ |
| Single admin user | ✅ | ✅ |
| Team members + RBAC | ❌ | ✅ |
| Multiple agents / servers | ❌ | ✅ |
| SSO (SAML/OIDC) | ❌ | ✅ |
| Priority support | ❌ | ✅ |

---

## Appendix D — Security Checklist

**Run before every release:**

```
□ No secrets in Git history (run git-secrets or truffleHog)
□ Access tokens expire in 15 minutes
□ Refresh tokens expire in 7 days and are single-use (rotation enforced)
□ Refresh token reuse detected and triggers full session revocation
□ Passwords hashed by Identity (bcrypt) — never stored plain
□ Secret vault values encrypted with AES-256-GCM
□ License key stored as SHA-256 hash only — raw key never in DB or logs
□ All webhook signatures validated (GitHub HMAC, Polar HMAC)
□ Rate limiting active on auth, webhook, and license endpoints
□ All security headers present on all responses
□ CORS locked to dashboard domain only — no wildcards
□ Server header removed from all responses
□ Account lockout after 5 consecutive failed logins
□ Setup wizard returns 404 after first completion
□ Users cannot deactivate or delete their own accounts (server-side check)
□ Invite tokens: SHA-256 hash stored, expire 48 hours, single-use
□ Refresh token cookie: HttpOnly, Secure, SameSite=Strict
□ Access token in memory only — never localStorage or sessionStorage
□ KESTRELHUB_MASTER_KEY and KESTRELHUB_JWT_SECRET are ≥ 32 bytes
□ docker-compose.production.yml has no inline secrets
□ No credentials in Docker image layers (verify with docker history)
```

---

## Appendix E — Testing Strategy Summary

| Test Type | Tool | When |
|---|---|---|
| Unit tests | xUnit | Every step |
| Integration tests (DB) | xUnit + TestContainers (PostgreSQL) | Steps touching DB |
| Integration tests (Docker) | xUnit + TestContainers | Steps touching Docker |
| Integration tests (MinIO) | xUnit + TestContainers (MinIO) | Phase 4 |
| Component tests (Blazor) | bUnit | Every dashboard step |
| Manual smoke tests | Developer | End of every step |
| End-to-end | Manual on real VPS | Phase checkpoints |
| Security review | Appendix D checklist | Before every release |

---

*KestrelHub Build Guide — v2.0 — Security and billing are not afterthoughts. Build it right. Ship something real.*
