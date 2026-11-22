using Soundboard.Core.Abstractions;
using Soundboard.Core.Downloads;

namespace Soundboard.App.Tests;

/// <summary>Scriptable <see cref="IAudioDownloader"/>: each step does what its delegate says and is recorded.</summary>
internal sealed class FakeAudioDownloader : IAudioDownloader
{
    public DownloadToolsStatus Status { get; set; } = new(IsInstalled: true, IsUpdateDue: false, Version: "2026.10.26");

    public Func<IProgress<DownloadProgress>?, CancellationToken, Task>? OnInstall { get; set; }

    public Func<Uri, IProgress<DownloadProgress>?, CancellationToken, Task<DownloadedAudio>>? OnDownload { get; set; }

    public int InstallCalls { get; private set; }

    public List<Uri> DownloadedUrls { get; } = [];

    public List<DownloadedAudio> Deleted { get; } = [];

    public Task<DownloadToolsStatus> GetToolsStatusAsync(CancellationToken cancellationToken = default) => Task.FromResult(Status);

    public async Task InstallToolsAsync(IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        InstallCalls++;
        if (OnInstall is not null)
        {
            await OnInstall(progress, cancellationToken);
        }

        Status = new DownloadToolsStatus(IsInstalled: true, IsUpdateDue: false, Version: Status.Version ?? "2026.10.26");
    }

    public Task<DownloadedAudio> DownloadAudioAsync(Uri url, IProgress<DownloadProgress>? progress, CancellationToken cancellationToken = default)
    {
        DownloadedUrls.Add(url);
        return OnDownload?.Invoke(url, progress, cancellationToken)
            ?? Task.FromResult(new DownloadedAudio(@"C:\downloads\x\abc.m4a", "Title"));
    }

    public void Delete(DownloadedAudio audio) => Deleted.Add(audio);
}
