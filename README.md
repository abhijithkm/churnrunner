# ChurnRunner

WinForms tool for running the Mister Churn Populate ASC/DESC location-processing
scripts against a SQL Server database, with live progress and logging.

## What it does

1. **Get Count** — runs `Scripts/count.sql` to find how many pending locations
   there are, and splits that count in half (ASC / DESC).
2. **Run ASC + DESC (Parallel)** — runs `Scripts/usp_MisterChurnPopulate_Asc.sql`
   and `Scripts/usp_MisterChurnPopulate_Desc.sql` at the same time, each against
   its own connection, streaming progress back from a `##ChurnRunProgress`
   global temp table the scripts update as they go.

Both scripts run as **plain ad-hoc batches**, not stored procedures — this
database does not allow `CREATE PROCEDURE`. Each script has a placeholder
(`{COUNT_ASC}` / `{COUNT_DESC}`) near the top; before execution ChurnRunner
substitutes it with the real half-count as a plain integer and sends the
resulting batch straight to the server. The substituted value always comes
from `count.sql`'s integer result, never free-form text, so this stays safe
from SQL injection.

## Scheduling an auto run

The app must stay open for either kind of schedule below — an internal timer
checks every second while running, and neither uses Windows Task Scheduler.
At trigger time, both automatically run **Get Count** followed by
**Run ASC + DESC**, with no confirmation prompt.

**One-Time Schedule** — pick a specific date/time and click **Set Schedule**.
Fires once, then clears itself. Click **Cancel** to abort a pending one.

**Recurring Schedule** — check one or more days of the week, pick a time
(e.g. Fri + Sat at 01:00), and click **Save Recurring**. Fires on every
matching day at that time, every week, for as long as the app is open at
that moment. Click **Remove** to turn it off. Unlike the one-time schedule,
this is saved to `settings.json` and reloaded automatically the next time
the app is opened — you don't need to re-enter it each time.

For unattended runs that don't require the app to be open and waiting, use
`--auto` (see below) driven by an external scheduler instead.

## Requirements

- Windows
- SQL Server login (SQL auth or Windows auth) with rights to run the scripts
  in `Scripts/`

No .NET runtime install needed if you use the published self-contained build
(see Releases).

## Running it

**From a release:** download `ChurnRunner-win-x64.zip` from the
[Releases](../../releases) page, extract it, and run `ChurnRunner.exe`. Keep
the `Scripts/` folder next to the exe — the app reads the `.sql` files from
there at runtime.

**From source:**

```bash
dotnet run
```

### Headless / unattended run

```bash
ChurnRunner.exe --auto
```

Runs Get Count + Run ASC + Run DESC end-to-end with no UI, using whatever
connection was last saved via **Save Connection Settings** in the GUI.
Logs to `%AppData%/ChurnRunner/logs/`. Requires connection settings to have
been saved at least once first — there's no scheduler built in; wire this up
to whatever external trigger (Task Scheduler, cron, CI) you want.

## Update notifications

On startup the app quietly checks the GitHub releases API for a newer
version than the one it was built with. If one exists, a link appears in
the top-right of the connection card ("⬆ Update available: vX.Y.Z") — click
it to open the release page in your browser. No network access, no
internet, or GitHub being unreachable all fail silently; nothing is ever
downloaded or installed automatically.

## Connection settings

Server, database, auth mode, and split overrides are saved via **Save
Connection Settings** to `%AppData%/ChurnRunner/settings.json`. The password
is encrypted with DPAPI, scoped to the current Windows user — it won't
decrypt on a different machine or under a different account.

## Project layout

| File | Purpose |
|---|---|
| `Form1.cs` / `Form1.Designer.cs` | Main window and event handlers |
| `ChurnJobRunner.cs` | Connects to SQL Server, runs `count.sql` and the ASC/DESC scripts, polls progress |
| `Settings.cs` | Load/save connection settings (DPAPI-encrypted password) |
| `Program.cs` | Entry point; handles `--auto` headless mode |
| `UpdateChecker.cs` | Checks GitHub releases for a newer version on startup |
| `Scripts/` | The actual `.sql` files executed at runtime (copied to output on build) |

## Building a release build yourself

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

Then zip `ChurnRunner.exe` together with the `Scripts/` folder — both need to
ship together.
