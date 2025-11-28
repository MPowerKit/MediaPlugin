namespace MPowerKit.MediaPlugin;

/// <summary>
/// Media file representations
/// </summary>
public sealed class MediaFile : IDisposable
{
    private bool _isDisposed;
    private Func<Stream>? _streamGetter;
    private readonly Func<Stream>? _streamGetterForExternalStorage;
    private readonly string? _originalFilename;
    private readonly string? _path;
    private string? _albumPath;

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="path"></param>
    /// <param name="streamGetter"></param>
    /// <param name="albumPath"></param>
    public MediaFile(
        string path,
        Func<Stream> streamGetter,
        Func<Stream>? streamGetterForExternalStorage = null,
        string? albumPath = null,
        string? originalFilename = null)
    {
        _streamGetter = streamGetter;
        _streamGetterForExternalStorage = streamGetterForExternalStorage;
        _path = path;
        _albumPath = albumPath;
        _originalFilename = originalFilename;
    }

    /// <summary>
    /// The original filename
    /// </summary>
    public string? OriginalFilename => _isDisposed ? throw new ObjectDisposedException(null) : _originalFilename;

    /// <summary>
    /// Path to file
    /// </summary>
    public string? Path => _isDisposed ? throw new ObjectDisposedException(null) : _path;

    /// <summary>
    /// Path to file
    /// </summary>
    public string? AlbumPath
    {
        get => _isDisposed ? throw new ObjectDisposedException(null) : _albumPath;
        set
        {
            ObjectDisposedException.ThrowIf(_isDisposed, this);

            _albumPath = value;
        }
    }

    /// <summary>
    /// Get stream if available
    /// </summary>
    /// <returns></returns>
    public Stream GetStream()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return _streamGetter!();
    }

    /// <summary>
    /// Get stream with image orientation rotated if available. If not, then just GetStream()
    /// </summary>
    /// <returns></returns>
    public Stream GetStreamWithImageRotatedForExternalStorage()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);

        return _streamGetterForExternalStorage?.Invoke() ?? GetStream();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        if (_isDisposed)
            return;

        _isDisposed = true;
        if (disposing)
            _streamGetter = null;
    }

    ~MediaFile()
    {
        Dispose(false);
    }
}
