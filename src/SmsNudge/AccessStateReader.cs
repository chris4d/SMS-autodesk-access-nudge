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
        .Select(u => $"{u.UpgradeCode}@{u.AvailableVersion}")
        .OrderBy(s => s, StringComparer.OrdinalIgnoreCase));
}

public sealed class AccessStateReader
{
    private readonly string _installDbPath;
    private readonly string _tempDir;

    public AccessStateReader(string? installDbPath = null, string? tempDir = null)
    {
        _installDbPath = installDbPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Autodesk\ODIS\Install.db");
        _tempDir = tempDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SMS-autodesk-access-nudge");
    }

    public AccessSnapshot ReadSnapshot()
    {
        var dbCopy = CopyDbToTemp();
        try
        {
            return new AccessSnapshot(DateTime.UtcNow, ComputeAvailableUpdates(ReadBundles(dbCopy)));
        }
        finally
        {
            try { if (File.Exists(dbCopy)) File.Delete(dbCopy); } catch { }
        }
    }

    private string CopyDbToTemp()
    {
        Directory.CreateDirectory(_tempDir);
        var temp = Path.Combine(_tempDir, $"Install.db.{Guid.NewGuid():N}.copy");
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var source = new FileStream(_installDbPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
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
            WHERE state = 'INSTALLED' AND type IN ('PRD', 'UPD')
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
                State: reader.GetString(5),
                Type: reader.GetString(6),
                Plc: reader.IsDBNull(7) ? "" : reader.GetString(7),
                Release: reader.IsDBNull(8) ? "" : reader.GetString(8),
                ConstantId: reader.IsDBNull(9) ? "" : reader.GetString(9)));
        }
        return rows;
    }

    public List<AvailableUpdate> ComputeAvailableUpdates(IEnumerable<BundleRow> rows)
    {
        var updates = new List<AvailableUpdate>();
        foreach (var group in rows
                     .Where(r => r.State == "INSTALLED")
                     .GroupBy(r => r.ConstantId, StringComparer.OrdinalIgnoreCase)
                     .Where(g => g.Any(r => r.Type == "PRD")))
        {
            var product = group.First(r => r.Type == "PRD");
            var latestInstalled = group
                .Select(r => TryParseVersion(r.Version) ?? TryParseVersion(r.UpdateVersion))
                .Max();

            if (TryParseVersion(product.UpdateVersion) is not { } targetVersion) continue;
            if (latestInstalled is { } current && targetVersion <= current) continue;

            updates.Add(new AvailableUpdate(
                Name: product.Name,
                Plc: product.Plc,
                Release: product.Release,
                InstalledVersion: product.Version ?? "",
                AvailableVersion: product.UpdateVersion!,
                UpgradeCode: product.UpgradeCode));
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


