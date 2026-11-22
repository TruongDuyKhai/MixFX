using System.Text.Json.Nodes;
using Soundboard.Core.Models;
using Soundboard.Storage.Migrations;

namespace Soundboard.Storage.Tests.Migrations;

public sealed class SettingsMigratorTests
{
    [Fact]
    public void CreateDefault_CoversEveryVersionUpToCurrent()
    {
        // Fails when AppSettings.CurrentSchemaVersion is bumped without registering its migration.
        var migrator = SettingsMigrator.CreateDefault();

        migrator.TargetVersion.Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public void Migrate_AlreadyAtTarget_LeavesJsonUnchanged()
    {
        var migrator = new SettingsMigrator([new Step(1, []), new Step(2, [])], targetVersion: 3);
        var root = Json("""{ "schemaVersion": 3, "x": 1 }""");
        var before = root.ToJsonString();

        migrator.Migrate(root, 3);

        root.ToJsonString().Should().Be(before);
    }

    [Fact]
    public void Migrate_FromOldest_RunsEveryStepInOrderAndStampsVersion()
    {
        var log = new List<int>();
        var migrator = new SettingsMigrator([new Step(2, log), new Step(1, log)], targetVersion: 3);
        var root = Json("""{ "schemaVersion": 1 }""");

        migrator.Migrate(root, 1);

        log.Should().Equal(1, 2);
        root["schemaVersion"]!.GetValue<int>().Should().Be(3);
        root["ranFrom1"]!.GetValue<bool>().Should().BeTrue();
        root["ranFrom2"]!.GetValue<bool>().Should().BeTrue();
    }

    [Fact]
    public void Migrate_FromMiddle_RunsOnlyRemainingSteps()
    {
        var log = new List<int>();
        var migrator = new SettingsMigrator([new Step(1, log), new Step(2, log)], targetVersion: 3);
        var root = Json("""{ "schemaVersion": 2 }""");

        migrator.Migrate(root, 2);

        log.Should().Equal(2);
        root["schemaVersion"]!.GetValue<int>().Should().Be(3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Migrate_VersionOutsideRange_Throws(int fromVersion)
    {
        var migrator = new SettingsMigrator([new Step(1, []), new Step(2, [])], targetVersion: 3);

        var act = () => migrator.Migrate(Json("{}"), fromVersion);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_MissingStep_Throws()
    {
        var act = () => new SettingsMigrator([new Step(1, [])], targetVersion: 3);

        act.Should().Throw<ArgumentException>().WithMessage("*v2*");
    }

    [Fact]
    public void Constructor_DuplicateStep_Throws()
    {
        var act = () => new SettingsMigrator([new Step(1, []), new Step(1, [])], targetVersion: 2);

        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_StepOutsideRange_Throws(int fromVersion)
    {
        var act = () => new SettingsMigrator(
            [new Step(1, []), new Step(2, []), new Step(fromVersion, [])], targetVersion: 3);

        act.Should().Throw<ArgumentException>();
    }

    private static JsonObject Json(string json) => JsonNode.Parse(json)!.AsObject();

    private sealed class Step(int fromVersion, List<int> log) : ISettingsMigration
    {
        public int FromVersion { get; } = fromVersion;

        public void Apply(JsonObject root)
        {
            log.Add(FromVersion);
            root[$"ranFrom{FromVersion}"] = true;
        }
    }
}
