using SmsNudge;
using Xunit;

public class UpdateDetectorTests
{
    private static BundleRow Installed(string name, string plc, string release, string version) =>
        new(Upi2: "{00000000-0000-0000-0000-00000000000" + plc.Length + "}", Version: version, UpdateVersion: null,
            UpgradeCode: "{AAAAAAAA-0000-0000-0000-000000000000}", Name: name, Type: "PRD", Plc: plc,
            Release: release, State: "INSTALLED", ConstantId: "{11111111-1111-1111-1111-111111111111}");

    private static AssetRow Asset(string displayName, string plc, string plcVersion, string buildNumber,
        string state = "None", string upgradeCode = "{BBBBBBBB-0000-0000-0000-000000000000}") =>
        new(Upi2: "{22222222-2222-2222-2222-222222222222}", UserId: "USER", State: state,
            BuildNumber: buildNumber, UpgradeCode: upgradeCode, DisplayName: displayName,
            Type: "UP", Plc: plc, PlcVersion: plcVersion);

    [Fact]
    public void UpdateAvailable_WhenAssetBuildNewerThanInstalled()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            new[] { Installed("Autodesk AutoCAD 2025 - English", "ACD", "2025", "25.0.171.0") },
            new[] { Asset("Autodesk AutoCAD 2025.1.4 Update", "ACD", "2025", "25.0.189.0") });
        var update = Assert.Single(updates);
        Assert.Equal("Autodesk AutoCAD 2025.1.4 Update", update.Name);
        Assert.Equal("25.0.189.0", update.AvailableVersion);
        Assert.Equal("25.0.171.0", update.InstalledVersion);
    }

    [Fact]
    public void NoUpdate_WhenAssetBuildEqualsOrOlder()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            new[] { Installed("Autodesk Revit 2024", "RVT", "2024", "24.3.60.12") },
            new[] { Asset("Autodesk Revit 2024.3.6", "RVT", "2024", "24.3.60.12") });
        Assert.Empty(updates);
    }

    [Fact]
    public void NoUpdate_WhenNoMatchingInstalledLine()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            Array.Empty<BundleRow>(),
            new[] { Asset("Autodesk AutoCAD 2025.1.4 Update", "ACD", "2025", "25.0.189.0") });
        Assert.Empty(updates);
    }

    [Fact]
    public void Skips_NotApplicable_Assets()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            new[] { Installed("Autodesk Shared Components 2027", "ASC", "2027", "2.0.4.1") },
            new[] { Asset("Autodesk Shared Components 2027.2", "ASC", "2027", "2.2.0.56", state: "NOT_APPLICABLE") });
        Assert.Empty(updates);
    }

    [Fact]
    public void MatchesLine_CaseAndWhitespaceInsensitive()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            new[] { Installed("Autodesk Desktop Connector", "DSKCON", "2027", "2027.2.2.2") },
            new[] { Asset("Autodesk Desktop Connector Update", "dskcon ", " 2027", "2027.2.2.4") });
        Assert.Single(updates);
    }

    [Fact]
    public void Skips_AssetWithUnparsableBuild()
    {
        var updates = new AccessStateReader().ComputeAvailableUpdates(
            new[] { Installed("Autodesk AutoCAD 2025", "ACD", "2025", "25.0.171.0") },
            new[] { Asset("Weird asset", "ACD", "2025", "not-a-version") });
        Assert.Empty(updates);
    }

    [Fact]
    public void DedupeKey_IsStableAndReflectsVersions()
    {
        var snapshot1 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "CODE-A"),
            new AvailableUpdate("B", "RVT", "2024", "3.0", "4.0", "CODE-B"),
        });
        var snapshot2 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("B", "RVT", "2024", "3.0", "4.0", "CODE-B"),
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "CODE-A"),
        });
        var snapshot3 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("B", "RVT", "2024", "3.0", "5.0", "CODE-B"),
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "CODE-A"),
        });
        Assert.Equal(snapshot1.DedupeKey, snapshot2.DedupeKey);
        Assert.NotEqual(snapshot1.DedupeKey, snapshot3.DedupeKey);
    }
}
