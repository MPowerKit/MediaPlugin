﻿namespace MPowerKit.MediaPlugin;

/// <summary>
/// Permission exception.
/// </summary>
public class MediaPermissionException : Exception
{
    /// <summary>
    /// Permission required that is missing
    /// </summary>
    public string[] Permissions { get; }
    /// <summary>
    /// Creates a media permission exception
    /// </summary>
    /// <param name="permissions"></param>
    public MediaPermissionException(params string[] permissions)
        : base()
    {
        Permissions = permissions;
    }

    /// <summary>
    /// Gets a message that describes current exception
    /// </summary>
    /// <value>The message.</value>
    public override string Message => $"{string.Join(", ", Permissions)} permission(s) are required.";
}
