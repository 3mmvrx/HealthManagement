# Production deployment and initial administrator

This application currently targets .NET 10, uses EF Core with SQL Server, and applies migrations automatically only in Development or when `Database:AutoMigrate` is explicitly enabled. Do not enable automatic migrations for a production deployment by default. Choose a hosting platform first; HTTPS termination, trusted proxy configuration, secret storage, and SQL connectivity must then be configured for that host.

## Required configuration

Provide production configuration through the host's secret/configuration provider or environment variables. Do not put production credentials in `appsettings*.json`, publish profiles, source control, or deployment logs.

| Setting | Environment variable | Notes |
| --- | --- | --- |
| SQL Server connection | `ConnectionStrings__DefaultConnection` | Use the production SQL Server, database, and least-privilege application identity. Do not use LocalDB in production. |
| SMTP host | `Email__Host` | Use the provider's authenticated submission endpoint. |
| SMTP port | `Email__Port` | Use the port required by the provider. |
| SMTP TLS | `Email__EnableSsl` | Keep TLS enabled when supported/required by the provider. |
| SMTP user | `Email__UserName` | Store as a protected secret. |
| SMTP password | `Email__Password` | Store as a protected secret; never log it. |
| Sender address | `Email__From` | Configure an address authorized by the provider. |
| Allowed host names | `AllowedHosts` | Replace `*` with the actual production host name(s). |

The app reports email sending as failed when SMTP credentials are missing. A successful SMTP submission confirms provider acceptance, not final inbox delivery; test confirmation, reset, and appointment messages with a controlled mailbox and check provider delivery logs and spam handling.

### Local Gmail SMTP setup

For local development, Gmail SMTP uses `smtp.gmail.com`, port `587`, with TLS enabled. Use a Google App Password, not your normal Google account password. App Passwords require 2-Step Verification and may be unavailable for some managed Workspace accounts.

From the web project directory, store the settings in .NET User Secrets. Replace the placeholders locally; never paste a real App Password into source files, this document, or chat:

```powershell
dotnet user-secrets set "Email:Host" "smtp.gmail.com"
dotnet user-secrets set "Email:Port" "587"
dotnet user-secrets set "Email:EnableSsl" "true"
dotnet user-secrets set "Email:UserName" "your-gmail-address@gmail.com"
dotnet user-secrets set "Email:Password" "your-google-app-password"
dotnet user-secrets set "Email:From" "your-gmail-address@gmail.com"
```

Run the app with the `Development` launch profile and test registration/confirmation with a mailbox you control. SMTP errors are logged by the caller; do not log message bodies or credentials. Confirmation links generated for localhost can only be opened on that same computer. Remove a secret with `dotnet user-secrets remove "Email:Password"` and list configured key names (not values) with `dotnet user-secrets list` only if your terminal is not being recorded.

## Database backup and migration release

1. Provision the production database and a restricted application login. Keep a separate deployment identity for schema changes where practical.
2. Take a full SQL Server backup before changing schema. The backup destination below must be writable by the SQL Server service account:

   ```sql
   BACKUP DATABASE [YourProductionDatabase]
   TO DISK = N'D:\SqlBackups\YourProductionDatabase-before-release.bak'
   WITH COPY_ONLY, CHECKSUM, INIT, STATS = 10;
   ```

3. Verify the backup and periodically test restoring backups to a separate environment. A successful `BACKUP` command by itself does not prove restore readiness.
4. From the web project directory, generate an idempotent EF migration script and review it before release:

   ```powershell
   New-Item -ItemType Directory -Force .\artifacts | Out-Null
   dotnet ef migrations script --idempotent --output .\artifacts\production-migrations.sql
   ```

   Apply the reviewed script to the intended production database with your approved SQL deployment tool. For `sqlcmd`, use a secure authentication method and verify the server/database target before execution; do not put a password on the command line. Do not use `dotnet ef database update` against production as an unreviewed deployment step.
5. Verify migration history after deployment:

   ```sql
   SELECT [MigrationId], [ProductVersion]
   FROM [dbo].[__EFMigrationsHistory]
   ORDER BY [MigrationId];
   ```

   Confirm the expected latest migration is present and perform an application health check before admitting users. Current migrations include irreversible data transformations; review each generated script and confirm the backup is restorable first.

The production app needs the schema migrated before startup when automatic migration is disabled: startup still seeds/checks Identity roles and the initial administrator. Keep `Database:AutoMigrate` false unless there is an explicit, reviewed operational reason to let the web process modify production schema.

## One-time initial SuperAdmin provisioning

Do not insert an Identity user or password hash with hand-written SQL. Identity hashes depend on the configured password hasher, and a malformed/manual insert can create an unusable or insecure account. Use the existing `DbInitializer` and `UserManager` after the database schema has been applied:

1. Before the first production app start, put these values in the host's protected secret store (not a file committed to source control):
   - `SeedAdmin__UserName`: a unique, non-default administrator name.
   - `SeedAdmin__Email`: a monitored administrator mailbox.
   - `SeedAdmin__Password`: a newly generated, unique password meeting the Identity password policy.
2. Start one production instance with those secrets present. The initializer creates the SuperAdmin only if no SuperAdmin currently exists. Confirm startup logs report successful creation; they should not contain the configured password.
3. Remove `SeedAdmin__Password` from the runtime configuration/secret deployment once creation is confirmed. Keep only the username/email if operationally needed.
4. Sign in over HTTPS, change the initial password to a separately generated personal password, and configure MFA at the hosting/identity layer if available. Confirm another authorized administrator can sign in before removing any bootstrap access.
5. Verify the account-to-role assignment with a read-only SQL query (replace the value with the provisioned username):

   ```sql
   SELECT u.[UserName], r.[Name] AS [RoleName], u.[EmailConfirmed]
   FROM [dbo].[AspNetUsers] AS u
   INNER JOIN [dbo].[AspNetUserRoles] AS ur ON ur.[UserId] = u.[Id]
   INNER JOIN [dbo].[AspNetRoles] AS r ON r.[Id] = ur.[RoleId]
   WHERE u.[UserName] = N'YourProvisionedAdmin';
   ```

   The returned role should be the project's SuperAdmin role. Never select or export password hashes. If bootstrap creation must be retried, check whether a SuperAdmin already exists first; the seeder intentionally will not promote an existing user.

## HTTPS, proxy, and hosting checks

- Bind a valid certificate to the public hostname and enforce HTTPS at the host/load balancer. The application already redirects HTTP to HTTPS, uses HSTS outside Development, and marks authentication cookies Secure outside Development.
- If TLS terminates at a reverse proxy, configure forwarded headers for that platform using only its documented trusted proxy addresses/networks. Do not trust forwarded headers from arbitrary clients. Configure the proxy to pass the original scheme and client address, then test redirects, generated email links, secure cookies, and IP-based rate limiting from outside the private network.
- Configure the production health monitoring, structured log retention/redaction, database network rules, backup schedule, restore tests, and alerting before launch. Do not log authentication tokens, reset codes, email bodies containing private data, or database credentials.
- Use the selected host's documented secret-management, deployment identity, certificate renewal, and rollback procedures. This document cannot verify those host-specific settings because a platform has not yet been selected.
