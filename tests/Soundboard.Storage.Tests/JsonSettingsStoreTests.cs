using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging.Abstractions;
using Soundboard.Core.Models;

namespace Soundboard.Storage.Tests;

public sealed class JsonSettingsStoreTests : IDisposable
{
    private static readonly DateTimeOffset FixedNow = new(2026, 11, 22, 10, 15, 0, TimeSpan.Zero);
    private const string ExpectedStamp = "20261122T101500000Z";

    private readonly TempDirectory _dir = new();
    private readonly StoragePaths _paths;
    private readonly JsonSettingsStore _store;

    public JsonSettingsStoreTests()
    {
        _paths = new StoragePaths(_dir.Path);
        _store = CreateStore();
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        _store.Dispose();
        _dir.Dispose();
    }

    [Fact]
    public async Task Load_NoFiles_ReturnsDefaultsAsFirstRunAndWritesNothing()
    {
        var result = await _store.LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.FirstRun);
        result.Settings.Should().BeEquivalentTo(new AppSettings(), o => o.ComparingRecordsByMembers());
        result.PreservedFiles.Should().BeEmpty();
        Directory.EnumerateFileSystemEntries(_dir.Path).Should().BeEmpty();
    }

    [Fact]
    public async Task SaveThenLoad_RoundTripsEveryField()
    {
        var original = SampleSettings("Airhorn");

        await _store.SaveAsync(original, Ct);
        var result = await CreateStore().LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.Loaded);
        result.Settings.Should().BeEquivalentTo(original, o => o.ComparingRecordsByMembers());
    }

    [Fact]
    public async Task Save_WritesCamelCaseSchemaVersionAndEnumNames()
    {
        await _store.SaveAsync(SampleSettings("Airhorn"), Ct);

        var root = JsonNode.Parse(File.ReadAllText(_paths.SettingsFile))!.AsObject();
        root["schemaVersion"]!.GetValue<int>().Should().Be(AppSettings.CurrentSchemaVersion);
        root["audio"]!["micMode"]!.GetValue<string>().Should().Be(nameof(MicMode.AlwaysOn));
    }

    [Fact]
    public async Task Save_StampsCurrentSchemaVersionEvenIfCallerPassedAnother()
    {
        await _store.SaveAsync(new AppSettings { SchemaVersion = 0 }, Ct);

        var root = JsonNode.Parse(File.ReadAllText(_paths.SettingsFile))!.AsObject();
        root["schemaVersion"]!.GetValue<int>().Should().Be(AppSettings.CurrentSchemaVersion);
    }

    [Fact]
    public async Task Save_Twice_KeepsPreviousGenerationAsBackup()
    {
        await _store.SaveAsync(SampleSettings("First"), Ct);
        var firstBytes = File.ReadAllBytes(_paths.SettingsFile);

        await _store.SaveAsync(SampleSettings("Second"), Ct);

        File.ReadAllBytes(_paths.SettingsBackupFile).Should().Equal(firstBytes);
        File.ReadAllText(_paths.SettingsFile).Should().Contain("Second");
    }

    [Fact]
    public async Task Save_LeavesNoTempFileBehind()
    {
        await _store.SaveAsync(SampleSettings("First"), Ct);
        await _store.SaveAsync(SampleSettings("Second"), Ct);

        File.Exists(_paths.SettingsTempFile).Should().BeFalse();
    }

    [Fact]
    public async Task Save_StaleTempFileFromEarlierCrash_IsOverwritten()
    {
        Directory.CreateDirectory(_dir.Path);
        File.WriteAllText(_paths.SettingsTempFile, "{ half written");

        await _store.SaveAsync(SampleSettings("Fresh"), Ct);

        (await CreateStore().LoadAsync(Ct)).Settings.Sounds.Single().Name.Should().Be("Fresh");
    }

    [Fact]
    public async Task Load_CorruptMain_RecoversFromBackupAndPreservesCorruptBytes()
    {
        await _store.SaveAsync(SampleSettings("Good"), Ct);
        await _store.SaveAsync(SampleSettings("Newer"), Ct);
        var corrupt = Encoding.UTF8.GetBytes("{ \"schemaVersion\": 1, \"sounds\": [ { \"na");
        File.WriteAllBytes(_paths.SettingsFile, corrupt);

        var result = await CreateStore().LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.RecoveredFromBackup);
        result.Settings.Sounds.Single().Name.Should().Be("Good");
        var preservedPath = _paths.SettingsFile + ".corrupt-" + ExpectedStamp;
        result.PreservedFiles.Should().Equal(preservedPath);
        File.ReadAllBytes(preservedPath).Should().Equal(corrupt);
    }

    [Fact]
    public async Task Save_AfterRecovery_DoesNotTouchPreservedFile()
    {
        await _store.SaveAsync(SampleSettings("Good"), Ct);
        await _store.SaveAsync(SampleSettings("Newer"), Ct);
        File.WriteAllText(_paths.SettingsFile, "not json");
        var store = CreateStore();
        var loaded = await store.LoadAsync(Ct);

        await store.SaveAsync(loaded.Settings, Ct);

        File.ReadAllText(loaded.PreservedFiles.Single()).Should().Be("not json");
        (await CreateStore().LoadAsync(Ct)).Status.Should().Be(SettingsLoadStatus.Loaded);
    }

    [Fact]
    public async Task Load_MainDeletedButBackupPresent_RecoversFromBackup()
    {
        await _store.SaveAsync(SampleSettings("Good"), Ct);
        await _store.SaveAsync(SampleSettings("Newer"), Ct);
        File.Delete(_paths.SettingsFile);

        var result = await CreateStore().LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.RecoveredFromBackup);
        result.Settings.Sounds.Single().Name.Should().Be("Good");
        result.PreservedFiles.Should().BeEmpty();
    }

    [Fact]
    public async Task Load_MainAndBackupUnreadable_ResetsToDefaultsAndPreservesBoth()
    {
        Directory.CreateDirectory(_dir.Path);
        File.WriteAllText(_paths.SettingsFile, "main garbage");
        File.WriteAllText(_paths.SettingsBackupFile, "backup garbage");

        var result = await _store.LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.ResetToDefaults);
        result.Settings.Should().BeEquivalentTo(new AppSettings(), o => o.ComparingRecordsByMembers());
        result.PreservedFiles.Should().HaveCount(2);
        result.PreservedFiles.Select(File.ReadAllText).Should().BeEquivalentTo("main garbage", "backup garbage");
        File.Exists(_paths.SettingsFile).Should().BeFalse();
        File.Exists(_paths.SettingsBackupFile).Should().BeFalse();
    }

    [Fact]
    public async Task Save_AfterReset_LeavesPreservedFilesIntact()
    {
        Directory.CreateDirectory(_dir.Path);
        File.WriteAllText(_paths.SettingsFile, "main garbage");
        File.WriteAllText(_paths.SettingsBackupFile, "backup garbage");
        var result = await _store.LoadAsync(Ct);

        await _store.SaveAsync(result.Settings, Ct);
        await _store.SaveAsync(result.Settings, Ct);

        result.PreservedFiles.Select(File.ReadAllText).Should().BeEquivalentTo("main garbage", "backup garbage");
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{ \"schemaVersion\": 0 }")]
    [InlineData("{ \"schemaVersion\": \"1\" }")]
    [InlineData("{ \"schemaVersion\": 1, \"audio\": null }")]
    [InlineData("{ \"schemaVersion\": 1, \"audio\": { \"micMode\": \"Loud\" } }")]
    public async Task Load_InvalidContent_IsTreatedAsUnreadable(string content)
    {
        Directory.CreateDirectory(_dir.Path);
        File.WriteAllText(_paths.SettingsFile, content);

        var result = await _store.LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.ResetToDefaults);
        File.ReadAllText(result.PreservedFiles.Single()).Should().Be(content);
    }

    [Fact]
    public async Task Load_NullForNonNullableSoundName_IsTreatedAsUnreadable()
    {
        await _store.SaveAsync(SampleSettings("Airhorn"), Ct);
        var root = JsonNode.Parse(File.ReadAllText(_paths.SettingsFile))!.AsObject();
        root["sounds"]![0]!["name"] = null;
        File.WriteAllText(_paths.SettingsFile, root.ToJsonString());

        var result = await CreateStore().LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.ResetToDefaults);
    }

    [Fact]
    public async Task Load_MissingConstructorField_IsTreatedAsUnreadable()
    {
        await _store.SaveAsync(SampleSettings("Airhorn"), Ct);
        var root = JsonNode.Parse(File.ReadAllText(_paths.SettingsFile))!.AsObject();
        root["sounds"]![0]!.AsObject().Remove("edit");
        File.WriteAllText(_paths.SettingsFile, root.ToJsonString());

        var result = await CreateStore().LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.ResetToDefaults);
    }

    [Fact]
    public async Task Load_HandEditedFileWithBomCommentsAndTrailingComma_Loads()
    {
        Directory.CreateDirectory(_dir.Path);
        var json = """
            {
              // edited by hand
              "schemaVersion": 1,
              "audio": { "micVolume": 0.25, },
            }
            """;
        File.WriteAllText(_paths.SettingsFile, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));

        var result = await _store.LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.Loaded);
        result.Settings.Audio.MicVolume.Should().Be(0.25f);
        result.Settings.Audio.VolumeToGame.Should().Be(AudioSettings.DefaultVolume);
    }

    [Fact]
    public async Task Load_NewerSchema_ReadsButBlocksSavingAndLeavesFileUntouched()
    {
        Directory.CreateDirectory(_dir.Path);
        var json = """{ "schemaVersion": 99, "audio": { "micVolume": 0.5 }, "somethingNew": [1, 2, 3] }""";
        File.WriteAllText(_paths.SettingsFile, json);

        var result = await _store.LoadAsync(Ct);
        var save = () => _store.SaveAsync(result.Settings, Ct);

        result.Status.Should().Be(SettingsLoadStatus.NewerSchemaReadOnly);
        result.Settings.Audio.MicVolume.Should().Be(0.5f);
        await save.Should().ThrowAsync<InvalidOperationException>();
        File.ReadAllText(_paths.SettingsFile).Should().Be(json);
        File.Exists(_paths.SettingsBackupFile).Should().BeFalse();
    }

    [Fact]
    public async Task Load_NewerSchemaThatCannotBeRead_UsesDefaultsAndStillBlocksSaving()
    {
        Directory.CreateDirectory(_dir.Path);
        var json = """{ "schemaVersion": 99, "audio": "reshaped in v99" }""";
        File.WriteAllText(_paths.SettingsFile, json);

        var result = await _store.LoadAsync(Ct);
        var save = () => _store.SaveAsync(result.Settings, Ct);

        result.Status.Should().Be(SettingsLoadStatus.NewerSchemaReadOnly);
        result.PreservedFiles.Should().BeEmpty();
        await save.Should().ThrowAsync<InvalidOperationException>();
        File.ReadAllText(_paths.SettingsFile).Should().Be(json);
    }

    [Fact]
    public async Task Load_V1Fixture_LoadsEveryField()
    {
        Directory.CreateDirectory(_dir.Path);
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "settings.v1.json"), _paths.SettingsFile);

        var result = await _store.LoadAsync(Ct);

        result.Status.Should().Be(SettingsLoadStatus.Loaded);
        var s = result.Settings;
        s.SchemaVersion.Should().Be(AppSettings.CurrentSchemaVersion);
        s.Audio.Should().Be(new AudioSettings
        {
            MicDeviceId = "{0.0.1.00000000}.{11111111-1111-1111-1111-111111111111}",
            GameOutputDeviceId = "{0.0.0.00000000}.{22222222-2222-2222-2222-222222222222}",
            MonitorOutputDeviceId = null,
            VolumeToGame = 0.8f,
            VolumeToMonitor = 0.5f,
            MicVolume = 1.2f,
            MicMode = MicMode.AlwaysOn,
            PlaybackMode = PlaybackMode.Exclusive,
        });

        var sound = s.Sounds.Should().ContainSingle().Subject;
        sound.Id.Should().Be(Guid.Parse("6f1c2a3b-0000-4000-8000-000000000001"));
        sound.Name.Should().Be("Airhorn");
        sound.LibraryFile.Should().Be("6f1c2a3b-0000-4000-8000-000000000001.mp3");
        sound.Color.Should().Be("#FF5722");
        sound.Volume.Should().Be(0.9f);
        sound.Kind.Should().Be(PlaybackKind.Preloaded);
        sound.Edit.Should().BeEquivalentTo(
            new EditParams(120, 2500, 10, 200, -3.5f,
                [new EffectConfig("pitch", new Dictionary<string, string> { ["semitones"] = "2" })]),
            o => o.ComparingRecordsByMembers());

        s.Bindings.Should().HaveCount(2);
        s.Bindings[0].Should().Be(new Binding(
            Guid.Parse("6f1c2a3b-0000-4000-8000-000000000002"),
            new Trigger(InputDevice.Keyboard, 97, Modifiers.Ctrl | Modifiers.Shift),
            "PlaySound",
            Guid.Parse("6f1c2a3b-0000-4000-8000-000000000001"),
            TriggerMode.Restart,
            PassThrough: true));
        s.Bindings[1].Should().Be(new Binding(
            Guid.Parse("6f1c2a3b-0000-4000-8000-000000000003"),
            new Trigger(InputDevice.Mouse, 5, Modifiers.None),
            "StopAll",
            null,
            TriggerMode.Press,
            PassThrough: false));
    }

    [Fact]
    public async Task Save_ConcurrentCalls_AllCompleteAndLastFileIsValid()
    {
        var saves = Enumerable.Range(0, 20).Select(i => _store.SaveAsync(SampleSettings($"Sound {i}"), Ct));

        await Task.WhenAll(saves);

        var result = await CreateStore().LoadAsync(Ct);
        result.Status.Should().Be(SettingsLoadStatus.Loaded);
        result.Settings.Sounds.Single().Name.Should().StartWith("Sound ");
    }

    private JsonSettingsStore CreateStore() =>
        new(_paths, new FixedTimeProvider(FixedNow), NullLogger<JsonSettingsStore>.Instance);

    private static AppSettings SampleSettings(string soundName)
    {
        var soundId = Guid.NewGuid();
        return new AppSettings
        {
            Audio = new AudioSettings
            {
                MicDeviceId = "mic-endpoint",
                GameOutputDeviceId = "cable-endpoint",
                VolumeToGame = 0.7f,
                MicMode = MicMode.AlwaysOn,
                PlaybackMode = PlaybackMode.Exclusive,
            },
            Sounds =
            [
                new SoundItem(
                    soundId, soundName, $"{soundId}.wav", "#00BCD4", 0.75f, PlaybackKind.Streamed,
                    new EditParams(50, null, 5, 15, 2.5f,
                        [new EffectConfig("reverb", new Dictionary<string, string> { ["room"] = "0.4", ["wet"] = "0.2" })])),
            ],
            Bindings =
            [
                new Binding(Guid.NewGuid(), new Trigger(InputDevice.Keyboard, 0x61, Modifiers.Alt | Modifiers.Win),
                    "PlaySound", soundId, TriggerMode.Hold, PassThrough: true),
                new Binding(Guid.NewGuid(), new Trigger(InputDevice.Mouse, 4, Modifiers.None),
                    "StopAll", null, TriggerMode.Press, PassThrough: false),
            ],
        };
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
