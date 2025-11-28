namespace MPowerKit.MediaPlugin;

/// <summary>
/// Media Options
/// </summary>
public abstract class MediaRequest
{
    /// <summary>
    /// Specifies the media picker's modal presentation style.
    /// Only applies to iOS.
    /// Defaults to FullScreen, which is the equivalent of using UIKit.UIModalPresentationStyle.FullScreen.
    /// </summary>
    public MediaPickerModalPresentationStyle ModalPresentationStyle { get; set; }
}

public class PickRequest : MediaRequest
{

}

public class CaptureRequest : MediaRequest
{
    public CameraDevice DefaultCamera { get; set; } = CameraDevice.Rear;

    /// <summary>
    /// Directory name
    /// </summary>
    public string? DesiredDirectory { get; set; }

    /// <summary>
    /// File name
    /// </summary>
    public string? DesiredName { get; set; }
}

/// <summary>
/// Store Video options
/// </summary>
public class VideoPickRequest : PickRequest
{
    /// <summary>
    /// Constructor
    /// </summary>
    public VideoPickRequest()
    {
        DesiredQuality = VideoQuality.High;
        DesiredLength = TimeSpan.FromMinutes(10);
    }

    /// <summary>
    /// Desired Length
    /// </summary>
    public TimeSpan DesiredLength { get; set; }

    /// <summary>
    /// Desired Quality
    /// </summary>
    public VideoQuality DesiredQuality { get; set; }

    /// <summary>
    /// Desired Video Size
    /// Only available on Android - Set the desired file size in bytes.
    /// Eg. 1000000 = 1MB
    /// </summary>
    public long DesiredSize { get; set; }
}

/// <summary>
/// UI options for iOS multi image picker
/// </summary>
public class MultiPickerOptions
{
    /// <summary>
    /// This only affects iOS since Android uses native
    /// </summary>
    public int MaximumImagesCount { get; set; } = 10;
}

/// <summary>
/// Camera device
/// </summary>
public enum CameraDevice
{
    /// <summary>
    /// Back of device
    /// </summary>
    Rear,
    /// <summary>
    /// Front facing of device
    /// </summary>
    Front
}

/// <summary>
/// Specifies the media picker's modal presentation style.
/// Only applies to iOS.
/// </summary>
public enum MediaPickerModalPresentationStyle
{
    /// <summary>
    /// This is the equivalent of presenting the media picker with UIKit.UIModalPresentationStyle.FullScreen style.
    /// Will remove the views of the underlying view controller when presenting the media picker.
    /// Only applies to iOS.
    /// </summary>
    FullScreen,

    /// <summary>
    /// This is the equivalent of presenting the media picker with UIKit.UIModalPresentationStyle.OverFullScreen style.
    /// Will keep the views of the underlying view controller when presenting the media picker.
    /// Only applies to iOS.
    /// </summary>
    OverFullScreen
}

/// <summary>
/// Video quality
/// </summary>
public enum VideoQuality
{
    /// <summary>
    /// Low
    /// </summary>
    Low = 0,
    /// <summary>
    /// Medium
    /// </summary>
    Medium = 1,
    /// <summary>
    /// High
    /// </summary>
    High = 2,
}