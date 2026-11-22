namespace Soundboard.Core.Downloads;

/// <summary>An expected download failure. <see cref="Exception.Message"/> is written for the user.</summary>
public sealed class AudioDownloadException : Exception
{
    public AudioDownloadException()
    {
    }

    public AudioDownloadException(string message)
        : base(message)
    {
    }

    public AudioDownloadException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
