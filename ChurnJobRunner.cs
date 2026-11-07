using System.Globalization;
using Microsoft.Data.SqlClient;

namespace ChurnRunner;

public record CountResult(int LocationCount, int HalfAsc, int HalfDesc);
public record AfterRunCheck(int PendingLocations, int DuplicateLocations);

public class ChurnJobRunner
{
    private readonly Settings _settings;
    public event Action<string>? OnLog;

    public ChurnJobRunner(Settings settings)
    {
        _settings = settings;
    }

    private static string ScriptsDir => Path.Combine(AppContext.BaseDirectory, "Scripts");

    private void Log(string message) => OnLog?.Invoke(message);

    public string BuildConnectionString()
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = _settings.Server,
            InitialCatalog = _settings.Database,
            TrustServerCertificate = _settings.TrustServerCertificate,
            Encrypt = false,
            ConnectTimeout = 15
        };

        if (_settings.WindowsAuth)
        {
            builder.IntegratedSecurity = true;
        }
        else
        {
            builder.UserID = _settings.Username;
            builder.Password = _settings.GetPassword();
        }

        return builder.ConnectionString;
    }

    public async Task<CountResult> GetCount()
    {
        string path = Path.Combine(ScriptsDir, "count.sql");
        string sql = await File.ReadAllTextAsync(path);

        using var conn = new SqlConnection(BuildConnectionString());
        await conn.OpenAsync();
        using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
            throw new InvalidOperationException("count.sql returned no rows.");

        int locationCount = reader.GetInt32(reader.GetOrdinal("LocationCount"));
        int halfAsc = reader.GetInt32(reader.GetOrdinal("HalfAsc"));
        int halfDesc = reader.GetInt32(reader.GetOrdinal("HalfDesc"));

        return new CountResult(locationCount, halfAsc, halfDesc);
    }

    /// <summary>
    /// Locations that already have yesterday's churn row, written today (same query as
    /// script\check1.sql). Read-only.
    /// </summary>
    public async Task<int> GetProcessedCount()
    {
        using var conn = new SqlConnection(BuildConnectionString());
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM CustomerUnlimitedData WITH (NOLOCK) " +
            "WHERE dtRecord = DATEADD(day, -1, dbo.DateOnly(GETDATE())) " +
            "AND dtCreated > dbo.DateOnly(GETDATE())",
            conn)
        { CommandTimeout = 60 };
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    /// <summary>
    /// Read-only check after a run: locations still pending (same query as Get Count)
    /// and locations that ended up with more than one row for yesterday.
    /// </summary>
    public async Task<AfterRunCheck> CheckAfterRun()
    {
        var count = await GetCount();

        using var conn = new SqlConnection(BuildConnectionString());
        await conn.OpenAsync();
        using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM (SELECT LocationId FROM CustomerUnlimitedData WITH (NOLOCK) " +
            "WHERE dtRecord = DATEADD(day, -1, dbo.DateOnly(GETDATE())) " +
            "GROUP BY LocationId HAVING COUNT(*) > 1) d",
            conn)
        { CommandTimeout = 120 };
        int duplicates = Convert.ToInt32(await cmd.ExecuteScalarAsync());

        return new AfterRunCheck(count.LocationCount, duplicates);
    }

    /// <summary>
    /// Runs a script file as plain batches (no CREATE PROCEDURE - this DB does not
    /// allow it) after substituting <paramref name="countPlaceholder"/> (e.g.
    /// "{COUNT_ASC}") with <paramref name="half"/>. Safe as a plain string
    /// substitution ONLY because <paramref name="half"/> is a real <see cref="int"/>
    /// here, never a free-form string - it can only ever become digits when
    /// formatted. Do not change this (or GetCount's return type) to accept a
    /// string without re-checking that this stays injection-safe.
    /// </summary>
    public async Task RunScript(
        string fileName,
        string countPlaceholder,
        int half,
        string label,
        CancellationToken token = default,
        Action<int, int>? onProgress = null)
    {
        // RAISERROR ... WITH NOWAIT does not reliably reach SqlConnection.InfoMessage
        // incrementally over this connection/network path in practice (observed: all
        // messages arrive in one burst at completion, not live). Progress is instead
        // read from the ChurnRunProgress table the script upserts every location, via
        // a separate short-lived polling connection that isn't sitting behind the
        // same long-running response stream.
        using var pollCts = CancellationTokenSource.CreateLinkedTokenSource(token);
        var pollTask = PollProgress(label, pollCts.Token, onProgress);

        try
        {
            string path = Path.Combine(ScriptsDir, fileName);
            string fullText = await File.ReadAllTextAsync(path, token);
            string sql = fullText.Replace(countPlaceholder, half.ToString(CultureInfo.InvariantCulture));

            var batches = sql.Split(
                new[] { "\r\nGO\r\n", "\nGO\n", "\r\nGO\n", "\nGO\r\n" },
                StringSplitOptions.RemoveEmptyEntries);

            Log($"[{label}] starting {fileName} (Half={half})");
            using var conn = new SqlConnection(BuildConnectionString());

            conn.InfoMessage += (_, e) =>
            {
                foreach (SqlError err in e.Errors)
                    Log($"[{label}] {err.Message}");
            };

            await conn.OpenAsync(token);

            foreach (var batch in batches)
            {
                var batchSql = batch.Trim();
                if (batchSql.Length == 0) continue;

                using var cmd = new SqlCommand(batchSql, conn) { CommandTimeout = 0 };
                await cmd.ExecuteNonQueryAsync(token);
            }

            Log($"[{label}] finished OK.");

            // Completing successfully means all `half` locations were processed,
            // regardless of what the last 3-second poll tick happened to catch mid-run.
            onProgress?.Invoke(half, half);
        }
        catch (OperationCanceledException)
        {
            Log($"[{label}] STOPPED by user.");
            throw;
        }
        catch (SqlException) when (token.IsCancellationRequested)
        {
            // Cancelling a SqlCommand sends a TDS attention signal; depending on timing
            // the driver surfaces that as a SqlException rather than OperationCanceledException.
            Log($"[{label}] STOPPED by user.");
            throw new OperationCanceledException($"{label} stopped by user.", token);
        }
        catch (Exception ex)
        {
            Log($"[{label}] ERROR: {ex.Message}");
            throw;
        }
        finally
        {
            pollCts.Cancel();
            try { await pollTask; } catch (OperationCanceledException) { }
        }
    }

    private async Task PollProgress(string runLabel, CancellationToken token, Action<int, int>? onProgress)
    {
        if (onProgress is null) return;

        while (!token.IsCancellationRequested)
        {
            try
            {
                using var conn = new SqlConnection(BuildConnectionString());
                await conn.OpenAsync(token);
                using var cmd = new SqlCommand(
                    "SELECT CurrentIndex, TotalCount FROM ##ChurnRunProgress WHERE RunLabel = @RunLabel",
                    conn)
                { CommandTimeout = 10 };
                cmd.Parameters.AddWithValue("@RunLabel", runLabel);

                using var reader = await cmd.ExecuteReaderAsync(token);
                if (await reader.ReadAsync(token))
                {
                    onProgress(reader.GetInt32(0), reader.GetInt32(1));
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // transient poll failure (e.g. table not yet created, brief connection hiccup) - retry next tick
            }

            try
            {
                await Task.Delay(3000, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public async Task RunBoth(
        int halfAsc,
        int halfDesc,
        CancellationToken token = default,
        Action<int, int>? onAscProgress = null,
        Action<int, int>? onDescProgress = null)
    {
        var ascTask = RunScript("usp_MisterChurnPopulate_Asc.sql", "{COUNT_ASC}", halfAsc, "ASC", token, onAscProgress);
        var descTask = RunScript("usp_MisterChurnPopulate_Desc.sql", "{COUNT_DESC}", halfDesc, "DESC", token, onDescProgress);
        await Task.WhenAll(ascTask, descTask);
    }
}
