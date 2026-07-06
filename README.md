# Probe DB Editor

Windows-native MySQL/MariaDB desktop client inspired by the older SQLPro workflow.

## Important notice

This codebase has been and will be 100% vibe coded. I have basically no experience in C# and dotnet. The software runs and it's all I need it to do. I will not be publishing releases nor built executables. I did ask codex to check against OWASP best practices and to double check the dotnet/C# best practices for a maintainable codebase, but I cannot vouch for any of it.

I personally wanted to make this software for 2 reasons:

- I miss having Sequel Ace because I am on windows now.
- I wanted an excuse to see how well (or bad) would Codex work while making an actual useful software instead of the classic to-do app.

If you want to improve this codebase, feel free to open a PR. If you experience issues, feel free to open an issue, but beware that I might not fix it, given my lack of experience in C# and the fact that this project to me is more like a "handy bash script made for my qorkflow" rather than "a product useful for the community".

Licensing is 0BSD because:

- The code is likely "learned" by the AI from someone else's project
- I literally don't care :)

## Current Direction

This repository uses WPF on .NET 10 instead of Rust for the first implementation. The reason is practical: WPF gives a mature native Windows `DataGrid` with in-place editing, virtualization, selection, sorting, and validation already solved. The database layer uses MySqlConnector and SSH.NET.

Rust remains viable for a custom-rendered UI, especially with egui or Slint, but it would require more custom table/editor work to reach the same database-client ergonomics.

## Implemented In This Baseline

- TCP/IP MySQL connection.
- Windows named pipe connection.
- SSH tunnel connection using local port forwarding.
- Configurable MySQL TLS mode.
- Optional SSH host key fingerprint pinning.
- Saved reusable connection profiles.
- Multiple active connections in separate tabs.
- Schema/table tree.
- Table browsing in a real editable grid.
- Immediate or deferred cell updates.
- Pending edit review, apply, and revert.
- Index listing plus create/drop index controls.
- Query runner and query result grid.
- Per-connection query log.
- Basic schema overview drawing from foreign keys.

## Code Organization

- `Models/`: one file per domain model or enum.
- `Services/DatabaseCommandExecutor.cs`: command execution, timing, and sanitized query logging.
- `Services/DatabaseMetadataService.cs`: schemas, tables, columns, indexes, and foreign keys.
- `Services/TableDataService.cs`: table loading and cell update execution.
- `Services/DatabaseConnectionStringFactory.cs`: connection-string construction.
- `Services/SshTunnel.cs`: SSH tunnel setup and optional host-key fingerprint validation.
- `Security/`: SQL identifier validation, query-log sanitization, and SSH fingerprint comparison.
- `Views/`: WPF screens plus view-specific rendering helpers.

## Security Notes

- App-generated SQL uses parameters for values.
- Dynamic identifiers are quoted and user-entered identifiers are allow-listed.
- Index column names are validated against live table metadata before SQL is generated.
- Query log parameter values are redacted; statements are normalized and obvious secret assignments are masked.
- Saved profiles persist database and SSH passwords only when their `Save password` flags are enabled; those passwords are protected with Windows DPAPI for the current Windows user.
- Saved profiles do not persist SSH key passphrases.
- Direct database connections default to full certificate verification. Lower TLS modes remain available for local/dev environments.
- For SSH tunnels, set the expected host key fingerprint to pin the remote host key.
- Least privilege is still an operational requirement: use DB accounts scoped to the target schema and required operations only.

## Build

```powershell
dotnet restore .\src\Probe.DbEditor\Probe.DbEditor.csproj
dotnet build .\src\Probe.DbEditor\Probe.DbEditor.csproj
dotnet run --project .\src\Probe.DbEditor\Probe.DbEditor.csproj
```

NuGet packages are restored into `.nuget/packages` inside the workspace.
