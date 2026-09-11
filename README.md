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

In the **Schedule Auto Run** box, pick a date/time and click **Set Schedule**.
The app must stay open — while it's running, an internal timer counts down,
and at the scheduled time it automatically runs **Get Count** followed by
**Run ASC + DESC**, with no confirmation prompt. Click **Cancel** to abort a
pending schedule. This is a one-shot schedule, not recurring, and does not
use Windows Task Scheduler — closing the app cancels it.

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
| `Scripts/` | The actual `.sql` files executed at runtime (copied to output on build) |

## Building a release build yourself

```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish/win-x64
```

Then zip `ChurnRunner.exe` together with the `Scripts/` folder — both need to
ship together.
