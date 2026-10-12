using System.Text.Json;
using LockIn.Core;
using Microsoft.Data.Sqlite;

namespace LockIn.Infrastructure;

public sealed class LocalStore
{
    private readonly string _connectionString;

    public LocalStore(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        _connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadWriteCreate, Pooling = false }.ToString();
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL;";
        command.ExecuteNonQuery();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='schema_version'";
        var existing = Convert.ToInt32(command.ExecuteScalar()) != 0;
        var version = 0;
        if (existing)
        {
            command.CommandText = "SELECT version FROM schema_version";
            version = Convert.ToInt32(command.ExecuteScalar());
            if (version is < 1 or > 3) throw new InvalidDataException("Unsupported database schema version. Preserve the database and use a compatible build.");
            if (version < 3)
            {
                command.CommandText = "SELECT COUNT(*) FROM sessions WHERE state IN ('Activating','Active','Releasing','CleanupPending')";
                if (Convert.ToInt32(command.ExecuteScalar()) != 0)
                    throw new InvalidOperationException("Finish the existing simulation in the previous build before upgrading its database.");
                using var backup = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = path + $".v{version}.backup", Pooling = false }.ToString());
                backup.Open();
                db.BackupDatabase(backup);
            }
        }
        using var transaction = db.BeginTransaction();
        command.Transaction = transaction;
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS schema_version (version INTEGER NOT NULL);
            INSERT INTO schema_version(version) SELECT 3 WHERE NOT EXISTS (SELECT 1 FROM schema_version);
            CREATE TABLE IF NOT EXISTS profiles (id TEXT PRIMARY KEY, revision INTEGER NOT NULL, json TEXT NOT NULL);
            CREATE TABLE IF NOT EXISTS sessions (id TEXT PRIMARY KEY, state TEXT NOT NULL, json TEXT NOT NULL);
            CREATE INDEX IF NOT EXISTS idx_sessions_state ON sessions(state);
            CREATE UNIQUE INDEX IF NOT EXISTS idx_one_live_session ON sessions((1))
                WHERE state IN ('Activating','Active','Releasing','CleanupPending');
            CREATE TABLE IF NOT EXISTS browser_enrollments (id TEXT PRIMARY KEY, json TEXT NOT NULL);
            UPDATE schema_version SET version=3;
            """;
        command.ExecuteNonQuery();
        transaction.Commit();
    }

    private SqliteConnection Open()
    {
        var db = new SqliteConnection(_connectionString);
        try { db.Open(); return db; }
        catch { db.Dispose(); throw; }
    }

    public IReadOnlyList<Profile> ListProfiles()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM profiles ORDER BY id";
        using var reader = command.ExecuteReader();
        var profiles = new List<Profile>();
        while (reader.Read()) profiles.Add(ProfileRules.Freeze(JsonSerializer.Deserialize<Profile>(reader.GetString(0), JsonOptions.Default)!));
        return profiles;
    }

    public Profile? GetProfile(string id)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM profiles WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        var json = command.ExecuteScalar() as string;
        return json is null ? null : ProfileRules.Freeze(JsonSerializer.Deserialize<Profile>(json, JsonOptions.Default)!);
    }

    public Profile SaveProfile(Profile profile, int expectedRevision)
    {
        profile = ProfileRules.Freeze(profile);
        using var db = Open();
        using var tx = db.BeginTransaction();
        using var get = db.CreateCommand();
        get.Transaction = tx;
        get.CommandText = "SELECT revision FROM profiles WHERE id=$id";
        get.Parameters.AddWithValue("$id", profile.Id);
        var current = get.ExecuteScalar();
        var actual = current is null ? 0 : Convert.ToInt32(current);
        if (actual != expectedRevision) throw new InvalidOperationException($"Profile changed. Expected revision {expectedRevision}, actual {actual}.");
        var saved = profile with { Revision = actual + 1 };
        using var write = db.CreateCommand();
        write.Transaction = tx;
        write.CommandText = "INSERT INTO profiles(id,revision,json) VALUES($id,$revision,$json) ON CONFLICT(id) DO UPDATE SET revision=$revision,json=$json";
        write.Parameters.AddWithValue("$id", saved.Id);
        write.Parameters.AddWithValue("$revision", saved.Revision);
        write.Parameters.AddWithValue("$json", JsonSerializer.Serialize(saved, JsonOptions.Default));
        write.ExecuteNonQuery();
        tx.Commit();
        return saved;
    }

    public Session? GetCurrentSession()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM sessions WHERE state IN ('Activating','Active','Releasing','CleanupPending') ORDER BY rowid DESC LIMIT 1";
        var json = command.ExecuteScalar() as string;
        return json is null ? null : ReadSession(json);
    }

    public void SaveSession(Session session)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO sessions(id,state,json) VALUES($id,$state,$json) ON CONFLICT(id) DO UPDATE SET state=$state,json=$json";
        command.Parameters.AddWithValue("$id", session.Id);
        command.Parameters.AddWithValue("$state", session.State.ToString());
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(session, JsonOptions.Default));
        command.ExecuteNonQuery();
    }

    public Session? GetSession(string id)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM sessions WHERE id=$id";
        command.Parameters.AddWithValue("$id", id);
        var json = command.ExecuteScalar() as string;
        return json is null ? null : ReadSession(json);
    }

    // History tolerates old snapshots that fail current validation; live-session reads stay strict.
    public IReadOnlyList<SessionHistoryItem> ListHistory()
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT id, state, json FROM sessions ORDER BY rowid DESC LIMIT 50";
        using var reader = command.ExecuteReader();
        var rows = new List<SessionHistoryItem>();
        while (reader.Read())
        {
            var json = reader.GetString(2);
            try
            {
                var s = ReadSession(json);
                rows.Add(new(s.Id, s.Policy.Name, s.StartedAtUtc, s.EndsAtUtc, s.State, s.EffectiveElapsedSeconds, s.FinishedAtUtc, s.CompletionReason,
                    s.BrowserInstallationId is null ? "Simulation" : "Browser alpha", s.CleanupEvidence));
            }
            catch (Exception error) when (Unreadable(error))
            {
                rows.Add(DamagedHistoryItem(reader.GetString(0), reader.GetString(1), json));
            }
        }
        return rows;
    }

    private static bool Unreadable(Exception error) => error is InvalidDataException or JsonException or ArgumentException or FormatException;

    private static SessionHistoryItem DamagedHistoryItem(string id, string state, string json)
    {
        JsonElement root = default;
        try { root = JsonSerializer.Deserialize<JsonElement>(json); } catch (JsonException) { }
        string? Text(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        DateTimeOffset Time(string name) => DateTimeOffset.TryParse(Text(root, name), out var value) ? value : DateTimeOffset.MinValue;
        var policy = root.ValueKind == JsonValueKind.Object && root.TryGetProperty("policy", out var p) ? p : default;
        return new(id, Text(policy, "name") ?? "Unknown profile", Time("startedAtUtc"), Time("endsAtUtc"),
            Enum.TryParse<SessionState>(state, out var parsed) ? parsed : SessionState.Recovered, 0, null, Text(root, "completionReason"),
            Text(root, "browserInstallationId") is null ? "Simulation" : "Browser alpha", Text(root, "cleanupEvidence"), Damaged: true);
    }

    // Latest readable session for one browser, without validating unrelated rows.
    public Session? GetLatestBrowserSession(string installationId)
    {
        using var db = Open();
        using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM sessions WHERE json_extract(json, '$.browserInstallationId')=$id ORDER BY rowid DESC LIMIT 50";
        command.Parameters.AddWithValue("$id", installationId);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            try { return ReadSession(reader.GetString(0)); }
            catch (Exception error) when (Unreadable(error)) { }
        }
        return null;
    }

    public void RecordOwnerReportedRemoval(Session recovered)
    {
        if (recovered.State != SessionState.Recovered || recovered.BrowserInstallationId is null ||
            recovered.CleanupEvidence != "ownerReportedRemoval" || recovered.BrowserReleased)
            throw new ArgumentException("Invalid owner-reported recovery record.");
        using var db = Open();
        using var transaction = db.BeginTransaction();
        using var update = db.CreateCommand();
        update.Transaction = transaction;
        update.CommandText = "UPDATE sessions SET state='Recovered',json=$json WHERE id=$id AND state='CleanupPending'";
        update.Parameters.AddWithValue("$id", recovered.Id);
        update.Parameters.AddWithValue("$json", JsonSerializer.Serialize(recovered, JsonOptions.Default));
        if (update.ExecuteNonQuery() != 1) throw new InvalidOperationException("Cleanup state changed; refresh before recovering.");
        using var revoke = db.CreateCommand();
        revoke.Transaction = transaction;
        revoke.CommandText = "DELETE FROM browser_enrollments WHERE id=$id";
        revoke.Parameters.AddWithValue("$id", recovered.BrowserInstallationId);
        revoke.ExecuteNonQuery();
        transaction.Commit();
    }

    public IReadOnlyList<BrowserEnrollment> ListBrowsers()
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "SELECT json FROM browser_enrollments";
        using var reader = command.ExecuteReader();
        var rows = new List<BrowserEnrollment>();
        while (reader.Read()) rows.Add(JsonSerializer.Deserialize<BrowserEnrollment>(reader.GetString(0), JsonOptions.Default)!);
        return rows;
    }

    public void SaveBrowser(BrowserEnrollment enrollment)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "INSERT INTO browser_enrollments(id,json) VALUES($id,$json) ON CONFLICT(id) DO UPDATE SET json=$json";
        command.Parameters.AddWithValue("$id", enrollment.InstallationId);
        command.Parameters.AddWithValue("$json", JsonSerializer.Serialize(enrollment, JsonOptions.Default));
        command.ExecuteNonQuery();
    }

    public void RevokeBrowser(string installationId)
    {
        using var db = Open(); using var command = db.CreateCommand();
        command.CommandText = "DELETE FROM browser_enrollments WHERE id=$id";
        command.Parameters.AddWithValue("$id", installationId);
        command.ExecuteNonQuery();
    }

    private static Session ReadSession(string json)
    {
        var session = JsonSerializer.Deserialize<Session>(json, JsonOptions.Default) ?? throw new InvalidDataException("Missing session snapshot.");
        if (string.IsNullOrWhiteSpace(session.Id) || !Enum.IsDefined(session.State) || ProfileRules.Validate(session.Policy).Count != 0 ||
            session.ProfileId != session.Policy.Id || session.ProfileRevision != session.Policy.Revision ||
            session.EndsAtUtc - session.StartedAtUtc != TimeSpan.FromSeconds(session.Policy.DurationSeconds) ||
            session.EffectiveElapsedSeconds < 0 || session.EffectiveElapsedSeconds > session.Policy.DurationSeconds ||
            session.PolicyHash != ProfileRules.Hash(session.Policy))
            throw new InvalidDataException("The stored session snapshot failed validation. Preserve the database for recovery.");
        return session with { Policy = ProfileRules.Freeze(session.Policy) };
    }
}
