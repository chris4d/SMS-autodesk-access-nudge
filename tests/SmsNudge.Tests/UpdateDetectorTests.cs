using SmsNudge;
using Xunit;

public class UpdateDetectorTests
{
    private static BundleRow Prd(string name, string plc, string release, string version, string? updateVersion, string constantId = "{11111111-1111-1111-1111-111111111111}") =>
        new(Upi2: "{00000000-0000-0000-0000-000000000001}", Version: version, UpdateVersion: updateVersion,
            UpgradeCode: "{AAAAAAAA-0000-0000-0000-000000000000}", Name: name, Type: "PRD", Plc: plc,
            Release: release, State: "INSTALLED", ConstantId: constantId);

    private static BundleRow Upd(string name, string version, string constantId) =>
        new(Upi2: "{22222222-2222-2222-2222-222222222222}", Version: version, UpdateVersion: null,
            UpgradeCode: "{AAAAAAAA-0000-0000-0000-000000000000}", Name: name, Type: "UPD", Plc: "RVT",
            Release: "2024", State: "INSTALLED", ConstantId: constantId);

    [Fact]
    public void NoUpdate_WhenTargetEqualsLatestUpdate()
    {
        var rows = new[]
        {
            Prd("Autodesk Revit 2024", "RVT", "2024", "24.1.11.26", "24.3.60.12", "RVT-CID"),
            Upd("Revit 2024.3.6 Update", "24.3.60.12", "RVT-CID"),
        };
        Assert.Empty(new AccessStateReader().ComputeAvailableUpdates(rows));
    }

    [Fact]
    public void NoUpdate_WhenTargetEqualsProductVersion()
    {
        var rows = new[]
        {
            Prd("Autodesk Desktop Connector", "DSKCON", "2027", "2027.2.2.3", "2027.2.2.3", "DSK-CID"),
        };
        Assert.Empty(new AccessStateReader().ComputeAvailableUpdates(rows));
    }

    [Fact]
    public void UpdateAvailable_WhenTargetGreaterThanLatestUpdate()
    {
        var rows = new[]
        {
            Prd("Autodesk Revit 2024", "RVT", "2024", "24.1.11.26", "24.4.90.10", "RVT-CID"),
            Upd("Revit 2024.3.6 Update", "24.3.60.12", "RVT-CID"),
        };
        var updates = new AccessStateReader().ComputeAvailableUpdates(rows).ToList();
        var update = Assert.Single(updates);
        Assert.Equal("Autodesk Revit 2024", update.Name);
        Assert.Equal("24.4.90.10", update.AvailableVersion);
        Assert.Equal("24.1.11.26", update.InstalledVersion);
    }

    [Fact]
    public void UpdateAvailable_WhenTargetGreaterAndNoUpdateBundles()
    {
        var rows = new[]
        {
            Prd("Autodesk AutoCAD 2025", "ACD", "2025", "25.0.50.0", "25.0.60.0", "ACD-CID"),
        };
        Assert.Single(new AccessStateReader().ComputeAvailableUpdates(rows));
    }

    [Fact]
    public void NoUpdate_WhenNoTargetVersion()
    {
        var rows = new[]
        {
            Prd("Some Legacy Product", "LEG", "2020", "1.0.0.0", null, "LEG-CID"),
        };
        Assert.Empty(new AccessStateReader().ComputeAvailableUpdates(rows));
    }

    [Fact]
    public void ProductWithoutUpdateBundle_UndershootingTargetCounted()
    {
        var rows = new[]
        {
            Prd("Autodesk AutoCAD 2025", "ACD", "2025", "25.0.58.0", "25.0.171.0", "ACD-CID"),
            Upd("AutoCAD 2025.1.3 Update", "25.0.171.0", "ACD-CID"),
        };
        Assert.Empty(new AccessStateReader().ComputeAvailableUpdates(rows));
    }

    [Fact]
    public void DedupeKey_IsStableAndReflectsVersions()
    {
        var snapshot1 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "ACD"),
            new AvailableUpdate("B", "RVT", "2024", "3.0", "4.0", "RVT"),
        });
        var snapshot2 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("B", "RVT", "2024", "3.0", "4.0", "RVT"),
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "ACD"),
        });
        var snapshot3 = new AccessSnapshot(DateTime.UtcNow, new[]
        {
            new AvailableUpdate("B", "RVT", "2024", "3.0", "5.0", "RVT"),
            new AvailableUpdate("A", "ACD", "2025", "1.0", "2.0", "ACD"),
        });
        Assert.Equal(snapshot1.DedupeKey, snapshot2.DedupeKey);
        Assert.NotEqual(snapshot1.DedupeKey, snapshot3.DedupeKey);
    }
}
