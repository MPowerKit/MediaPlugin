﻿namespace MPowerKit.MediaPlugin;

public class MediaFileNotFoundException : Exception
{
    public MediaFileNotFoundException()
    {
    }

    public MediaFileNotFoundException(string? path)
      : base("Unable to locate media file at " + path)
    {
        Path = path;
    }

    public MediaFileNotFoundException(string? path, Exception innerException)
      : base("Unable to locate media file at " + path, innerException)
    {
        Path = path;
    }

    /// <summary>
    /// Path
    /// </summary>
    public string? Path { get; protected set; }
}
