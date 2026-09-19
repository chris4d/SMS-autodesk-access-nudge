using Microsoft.Data.Sqlite;
using System.Diagnostics;

namespace SmsNudge;

public sealed record BundleRow(
    string Upi2,
    string? Version,
    string? UpdateVersion,
    string UpgradeCode,
    string Name,
    string Type,
    string Plc,
    string Release,
    string State,
    string ConstantId);

public sealed record AssetRow(
    string Upi2,
    string UserId,
    string State,
    string BuildNumber,
    string UpgradeCode,
    string DisplayName,
    string Type,
    string Plc,
    string PlcVersion);

public sealed record AvailableUpdate(
    string Name,
    string Plc,
    string Release,
    string InstalledVersion,
    string AvailableVersion,
    string UpgradeCode)
{
    public string DisplayName => $"{Name} {AvailableVersion}";
}

public sealed record AccessSnapshot(DateTime CapturedUtc, IReadOnlyList<AvailableUpdate> Updates)
{
    public string DedupeKey => string.Join("|", Updates
        .Select(u => $"{u.Plc}@{u.UpgradeCode}@{u.AvailableVersion}")
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
}

public sealed class AccessStateReader
{
    private readonly string _installDbPath;
    private readonly string _localCacheDbPath;
    private readonly string _tempDir;

    public AccessStateReader(string? installDbPath = null, string? localCacheDbPath = null, string? tempDir = null)
    {
        _installDbPath = installDbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ODIS\Install.db");
        _localCacheDbPath = localCacheDbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ODIS\LocalCache.db");
        _tempDir = tempDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMS-autodesk-access-nudge");
    }

    public AccessSnapshot ReadSnapshot()
    {
        var installCopy = CopyDbToTemp(_installDbPath, "Install");
        var cacheCopy = CopyDbToTemp(_localCacheDbPath, "LocalCache");
        try
        {
            var bundles = ReadBundles(installCopy);
            var assets = ReadAssets(cacheCopy);
            return new AccessSnapshot(DateTime.UtcNow, ComputeAvailableUpdates(bundles, assets));
        }
        finally
        {
            TryDelete(installCopy);
            TryDelete(cacheCopy);
        }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    public string DumpRaw()
    {
        var installCopy = CopyDbToTemp(_installDbPath, "Install");
        var cacheCopy = CopyDbToTemp(_localCacheDbPath, "LocalCache");
        try
        {
            var lines = new List<string> { "== Install.db Bundle ==" };
            using (var connection = new SqliteConnection($"Data Source={installCopy};Mode=ReadOnly"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT upi2, name, type, state, version, updateversion, plc, release, constantid FROM Bundle";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    lines.Add(string.Join(" | ",
                        reader.IsDBNull(0) ? "-" : reader.GetString(0),
                        reader.IsDBNull(1) ? "-" : reader.GetString(1),
                        reader.IsDBNull(2) ? "-" : reader.GetString(2),
                        reader.IsDBNull(3) ? "-" : reader.GetString(3),
                        reader.IsDBNull(4) ? "-" : reader.GetString(4),
                        reader.IsDBNull(5) ? "-" : reader.GetString(5),
                        reader.IsDBNull(6) ? "-" : reader.GetString(6),
                        reader.IsDBNull(7) ? "-" : reader.GetString(7),
                        reader.IsDBNull(8) ? "-" : reader.GetString(8)));
                }
            }
            lines.Add("== LocalCache.db AssetList ==");
            using (var connection = new SqliteConnection($"Data Source={cacheCopy};Mode=ReadOnly"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "SELECT upi2, user_id, state, is_auto_update, build_number, release_date, upgrade_code, display_name, type, plc, plc_version FROM AssetList";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    lines.Add(string.Join(" | ",
                        reader.IsDBNull(0) ? "-" : reader.GetString(0),
                        reader.IsDBNull(1) ? "-" : reader.GetString(1),
                        reader.IsDBNull(2) ? "-" : reader.GetString(2),
                        reader.IsDBNull(3) ? "-" : reader.GetString(3),
                        reader.IsDBNull(4) ? "-" : reader.GetString(4),
                        reader.IsDBNull(5) ? "-" : reader.GetString(5),
                        reader.IsDBNull(6) ? "-" : reader.GetString(6),
                        reader.IsDBNull(7) ? "-" : reader.GetString(7),
                        reader.IsDBNull(8) ? "-" : reader.GetString(8),
                        reader.IsDBNull(9) ? "-" : reader.GetString(9),
                        reader.IsDBNull(10) ? "-" : reader.GetString(10)));
                }
            }
            return string.Join(Environment.NewLine, lines);
        }
        finally
        {
            TryDelete(installCopy);
            TryDelete(cacheCopy);
        }
    }

    private string CopyDbToTemp(string sourcePath, string tag)
    {
        Directory.CreateDirectory(_tempDir);
        var temp = Path.Combine(_tempDir, $"{tag}.db.{Guid.NewGuid():N}.copy");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var target = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None);
                source.CopyTo(target);
                break;
            }
            catch (IOException) when (attempt < 4)
            {
                Thread.Sleep(500 * (attempt + 1));
            }
        }
        return temp;
    }

    private static List<BundleRow> ReadBundles(string dbPath)
    {
        var rows = new List<BundleRow>();
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT UPI2, version, updateversion, upgradecode, name, state, type, plc, release, constantid
            FROM Bundle
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new BundleRow(
                Upi2: reader.GetString(0),
                Version: reader.IsDBNull(1) ? null : reader.GetString(1),
                UpdateVersion: reader.IsDBNull(2) ? null : reader.GetString(2),
                UpgradeCode: reader.IsDBNull(3) ? "" : reader.GetString(3),
                Name: reader.IsDBNull(4) ? "" : reader.GetString(4),
                State: reader.IsDBNull(5) ? "" : reader.GetString(5),
                Type: reader.IsDBNull(6) ? "" : reader.GetString(6),
                Plc: reader.IsDBNull(7) ? "" : reader.GetString(7),
                Release: reader.IsDBNull(8) ? "" : reader.GetString(8),
                ConstantId: reader.IsDBNull(9) ? "" : reader.GetString(9)));
        }
        return rows;
    }

    private static List<AssetRow> ReadAssets(string dbPath)
    {
        var rows = new List<AssetRow>();
        using var connection = new SqliteConnection($"Data Source={dbPath};Mode=ReadOnly");
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT upi2, user_id, state, build_number, upgrade_code, display_name, type, plc, plc_version
            FROM AssetList
            """;
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            rows.Add(new AssetRow(
                Upi2: reader.GetString(0),
                UserId: reader.IsDBNull(1) ? "" : reader.GetString(1),
                State: reader.IsDBNull(2) ? "" : reader.GetString(2),
                BuildNumber: reader.IsDBNull(3) ? "" : reader.GetString(3),
                UpgradeCode: reader.IsDBNull(4) ? "" : reader.GetString(4),
                DisplayName: reader.IsDBNull(5) ? "" : reader.GetString(5),
                Type: reader.IsDBNull(6) ? "" : reader.GetString(6),
                Plc: reader.IsDBNull(7) ? "" : reader.GetString(7),
                PlcVersion: reader.IsDBNull(8) ? "" : reader.GetString(8)));
        }
        return rows;
    }

    public List<AvailableUpdate> ComputeAvailableUpdates(IEnumerable<BundleRow> bundles, IEnumerable<AssetRow> assets)
    {
        // A product line is identified by (plc, release). Access's UI-visible
        // "update available" lives in LocalCache.db AssetList: a row whose
        // build_number is newer than the max installed version of matching
        // plc/release, and whose evaluation state is not NOT_APPLICABLE.
        var updates = new List<AvailableUpdate>();
        var installedByLine = bundles
            .Where(b => b.State == "INSTALLED" && TryParseVersion(b.Version) is not null)
            .GroupBy(b => (Key: b.Plc.Trim().ToLowerInvariant(), Version: b.Release.Trim().ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Select(b => TryParseVersion(b.Version) ?? new Version(0, 0)).Max());

        foreach (var asset in assets.Where(a => !string.Equals(a.State, "NOT_APPLICABLE", StringComparison.OrdinalIgnoreCase)))
        {
            var target = TryParseVersion(asset.BuildNumber);
            if (target is null) continue;
            if (!installedByLine.TryGetValue((asset.Plc.Trim().ToLowerInvariant(), asset.PlcVersion.Trim().ToLowerInvariant()), out var installedMax)) continue;
            if (target <= installedMax) continue;

            updates.Add(new AvailableUpdate(
                Name: asset.DisplayName,
                Plc: asset.Plc,
                Release: asset.PlcVersion,
                InstalledVersion: installedMax.ToString(),
                AvailableVersion: asset.BuildNumber,
                UpgradeCode: asset.UpgradeCode));
        }
        return updates;
    }

    private static Version? TryParseVersion(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : Version.TryParse(value.Trim(), out var v) ? v : null;
}

public static class ProductPaths
{
    public static void LaunchAutodeskAccess()
    {
        const string exe = @"C:\Program Files\Autodesk\AdODIS\V1\Access\AdskAccessCore.exe";
        if (File.Exists(exe))
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        else
            MessageBox.Show($"Autodesk Access executable not found:\n{exe}", "SMS Autodesk Access Nudge",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}

