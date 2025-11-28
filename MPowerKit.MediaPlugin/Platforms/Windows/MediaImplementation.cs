using System.Diagnostics;

using Windows.Devices.Enumeration;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace MPowerKit.MediaPlugin;

/// <summary>
/// Implementation for Media
/// </summary>
public class MediaImplementation : IMedia
{
    protected static readonly IEnumerable<string> SupportedVideoFileTypes = [".mp4", ".wmv", ".avi"];
    protected static readonly IEnumerable<string> SupportedImageFileTypes = [".jpeg", ".jpg", ".png", ".gif", ".bmp"];

    protected readonly HashSet<string> CaptureDevices = [];
    protected readonly DeviceWatcher Watcher;
    protected bool Initialized;

    private bool _isCameraAvailable;

    /// <inheritdoc/>
    public bool IsCameraAvailable
    {
        get
        {
            if (!Initialized)
                throw new InvalidOperationException("You must call Initialize() before calling any properties.");

            return _isCameraAvailable;
        }
        protected set => _isCameraAvailable = value;
    }

    /// <inheritdoc/>
    public bool IsTakePhotoSupported => true;

    /// <inheritdoc/>
    public bool IsPickPhotoSupported => true;

    /// <inheritdoc/>
    public bool IsTakeVideoSupported => true;

    /// <inheritdoc/>
    public bool IsPickVideoSupported => true;

    /// <summary>
    /// Implementation
    /// </summary>
    public MediaImplementation()
    {
        Watcher = DeviceInformation.CreateWatcher(DeviceClass.VideoCapture);
        Watcher.Added += OnDeviceAdded;
        Watcher.Updated += OnDeviceUpdated;
        Watcher.Removed += OnDeviceRemoved;
        Watcher.Start();
    }

    /// <summary>
    /// Initialize camera
    /// </summary>
    /// <returns></returns>
    public virtual async Task<bool> Initialize()
    {
        if (Initialized) return true;

        try
        {
            var info = await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture).AsTask().ConfigureAwait(false);
            lock (CaptureDevices)
            {
                foreach (var device in info)
                {
                    if (device.IsEnabled)
                        CaptureDevices.Add(device.Id);
                }

                IsCameraAvailable = CaptureDevices.Count > 0;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine("Unable to detect cameras: " + ex);
        }

        Initialized = true;
        return true;
    }

    /// <summary>
    /// Take a photo async with specified options
    /// </summary>
    /// <param name="options">Camera Media Options</param>
    /// <param name="token">Cancellation token (currently ignored)</param>
    /// <returns>Media file of photo or null if canceled</returns>
    public async Task<MediaFile?> TakePhotoAsync(CaptureRequest? request = null, CancellationToken token = default)
    {
        if (!Initialized) await Initialize();

        if (!IsCameraAvailable) throw new NotSupportedException();

        request ??= new CaptureRequest();

        request.VerifyCaptureRequest();

        var capture = new CameraCaptureUI();
        capture.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;

        var result = await capture.CaptureFileAsync(CameraCaptureUIMode.Photo);
        if (result is null)
        {
            var tcs = new TaskCompletionSource<MediaFile?>();
            tcs.SetCanceled(token);
            return await tcs.Task;
        }

        var desiredName = Path.GetFileName(MediaExtensions.GetDesiredPath(request.DesiredDirectory, request.DesiredName, true));

        await result.RenameAsync(desiredName, NameCollisionOption.GenerateUniqueName);

        return MediaFileFromFile(result);
    }

    /// <summary>
    /// Picks a photo from the default gallery
    /// </summary>
    /// <param name="token">Cancellation token (currently ignored)</param>
    /// <returns>Media file or null if canceled</returns>
    public async Task<MediaFile?> PickPhotoAsync(PickRequest? request = null, CancellationToken token = default)
    {
        FileOpenPicker picker = new()
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };

        foreach (var filter in SupportedImageFileTypes)
        {
            picker.FileTypeFilter.Add(filter);
        }

        var window = Application.Current!.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var result = await picker.PickSingleFileAsync();
        if (result is null)
        {
            var tcs = new TaskCompletionSource<MediaFile?>();
            tcs.SetCanceled(token);
            return await tcs.Task;
        }

        return MediaFileFromFile(result);
    }

    public async Task<List<MediaFile>?> PickPhotosAsync(
        PickRequest? request = null,
        MultiPickerOptions? pickerOptions = null,
        CancellationToken token = default)
    {
        var picker = new FileOpenPicker
        {
            SuggestedStartLocation = PickerLocationId.PicturesLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };

        foreach (var filter in SupportedImageFileTypes)
        {
            picker.FileTypeFilter.Add(filter);
        }

        var window = Application.Current!.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var result = await picker.PickMultipleFilesAsync();
        if (result is null)
        {
            var tcs = new TaskCompletionSource<List<MediaFile>?>();
            tcs.SetCanceled(token);
            return await tcs.Task;
        }

        List<MediaFile> res = new(result.Count);
        foreach (var file in result)
        {
            res.Add(MediaFileFromFile(file));
        }

        return res;
    }

    /// <summary>
    /// Take a video with specified options
    /// </summary>
    /// <param name="options">Video Media Options</param>
    /// <param name="token">Cancellation token (currently ignored)</param>
    /// <returns>Media file of new video or null if canceled</returns>
    public async Task<MediaFile?> TakeVideoAsync(CaptureRequest? request, CancellationToken token = default)
    {
        if (!Initialized) await Initialize();

        if (!IsCameraAvailable) throw new NotSupportedException();

        request ??= new CaptureRequest();

        request.VerifyCaptureRequest();

        var capture = new CameraCaptureUI();
        capture.VideoSettings.AllowTrimming = true;

        capture.VideoSettings.Format = CameraCaptureUIVideoFormat.Mp4;

        var result = await capture.CaptureFileAsync(CameraCaptureUIMode.Video);
        if (result is null)
        {
            var tcs = new TaskCompletionSource<MediaFile?>();
            tcs.SetCanceled(token);
            return await tcs.Task;
        }

        var desiredName = Path.GetFileName(MediaExtensions.GetDesiredPath(request.DesiredDirectory, request.DesiredName, true));

        await result.RenameAsync(desiredName, NameCollisionOption.GenerateUniqueName);

        return MediaFileFromFile(result);
    }

    /// <summary>
    /// Picks a video from the default gallery
    /// </summary>
    /// <param name="token">Cancellation token (currently ignored)</param>
    /// <returns>Media file of video or null if canceled</returns>
    public async Task<MediaFile?> PickVideoAsync(VideoPickRequest? request = null, CancellationToken token = default)
    {
        var picker = new FileOpenPicker()
        {
            SuggestedStartLocation = PickerLocationId.VideosLibrary,
            ViewMode = PickerViewMode.Thumbnail
        };

        foreach (var filter in SupportedVideoFileTypes)
        {
            picker.FileTypeFilter.Add(filter);
        }

        var window = Application.Current!.Windows[0].Handler.PlatformView as Microsoft.UI.Xaml.Window;
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        var result = await picker.PickSingleFileAsync();
        if (result is null)
        {
            var tcs = new TaskCompletionSource<MediaFile?>();
            tcs.SetCanceled(token);
            return await tcs.Task;
        }

        return MediaFileFromFile(result);
    }

    protected virtual MediaFile MediaFileFromFile(StorageFile file)
    {
        var aPath = file.Path;
        var path = file.Path;

        return new MediaFile(path, () => file.OpenStreamForReadAsync().Result, null, aPath, file.Name);
    }

    //protected virtual async Task<StorageFile> CopyToLocal(StorageFile file)
    //{
    //    try
    //    {
    //        var fileNameNoEx = Path.GetFileNameWithoutExtension(file.Path);
    //        var copy = await file.CopyAsync(ApplicationData.Current.TemporaryFolder,
    //            fileNameNoEx + file.FileType, NameCollisionOption.GenerateUniqueName);

    //        return copy;
    //    }
    //    catch (Exception ex)
    //    {
    //        Debug.WriteLine("unable to save to app directory:" + ex);
    //        throw;
    //    }
    //}

    //static CameraCaptureUIMaxVideoResolution GetResolutionFromQuality(VideoQuality quality)
    //{
    //    switch (quality)
    //    {
    //        case VideoQuality.High:
    //            return CameraCaptureUIMaxVideoResolution.HighestAvailable;
    //        case VideoQuality.Medium:
    //            return CameraCaptureUIMaxVideoResolution.StandardDefinition;
    //        case VideoQuality.Low:
    //            return CameraCaptureUIMaxVideoResolution.LowDefinition;
    //        default:
    //            return CameraCaptureUIMaxVideoResolution.HighestAvailable;
    //    }
    //}

    //static CameraCaptureUIMaxPhotoResolution GetMaxResolutionFromMaxWidthHeight(int maxWidthHeight)
    //{
    //    if (maxWidthHeight > 2560)
    //        return CameraCaptureUIMaxPhotoResolution.HighestAvailable;
    //    if (maxWidthHeight > 1920)
    //        return CameraCaptureUIMaxPhotoResolution.VeryLarge5M;
    //    else if (maxWidthHeight > 1024)
    //        return CameraCaptureUIMaxPhotoResolution.Large3M;
    //    else if (maxWidthHeight > 320)
    //        return CameraCaptureUIMaxPhotoResolution.MediumXga;

    //    return CameraCaptureUIMaxPhotoResolution.SmallVga;
    //}

    /// <summary>
    ///  Rotate an image if required and saves it back to disk.
    /// </summary>
    /// <param name="filePath">The file image path</param>
    /// <param name="mediaOptions">The options.</param>
    /// <param name="exif">original metadata</param>
    /// <returns>True if rotation or compression occurred, else false</returns>
    //Task<bool> ResizeAsync(StorageFile file, PickMediaOptions mediaOptions)
    //{
    //    return ResizeAsync(
    //        file,
    //        mediaOptions != null
    //            ? new StoreCameraMediaOptions
    //            {
    //                PhotoSize = mediaOptions.PhotoSize,
    //                CompressionQuality = mediaOptions.CompressionQuality,
    //                CustomPhotoSize = mediaOptions.CustomPhotoSize,
    //                MaxWidthHeight = mediaOptions.MaxWidthHeight,
    //                RotateImage = mediaOptions.RotateImage,
    //                SaveMetaData = mediaOptions.SaveMetaData
    //            }
    //            : new StoreCameraMediaOptions());
    //}

    /// <summary>
    /// Resize Image Async
    /// </summary>
    /// <param name="filePath">The file image path</param>
    /// <param name="photoSize">Photo size to go to.</param>
    /// <param name="quality">Image quality (1-100)</param>
    /// <param name="customPhotoSize">Custom size in percent</param>
    /// <param name="exif">original metadata</param>
    /// <returns>True if rotation or compression occurred, else false</returns>
    //    Task<bool> ResizeAsync(StorageFile file, StoreCameraMediaOptions mediaOptions)
    //    {
    //        if (file is null) throw new ArgumentNullException(nameof(file));

    //        try
    //        {
    //            var photoSize = mediaOptions.PhotoSize;
    //            if (photoSize == PhotoSize.Full)
    //                return Task.FromResult(false);

    //            var customPhotoSize = mediaOptions.CustomPhotoSize;
    //            var quality = mediaOptions.CompressionQuality;
    //            return Task.Run(async () =>
    //            {
    //                try
    //                {
    //                    var percent = 1.0f;
    //                    switch (photoSize)
    //                    {
    //                        case PhotoSize.Large:
    //                            percent = .75f;
    //                            break;
    //                        case PhotoSize.Medium:
    //                            percent = .5f;
    //                            break;
    //                        case PhotoSize.Small:
    //                            percent = .25f;
    //                            break;
    //                        case PhotoSize.Custom:
    //                            percent = customPhotoSize / 100f;
    //                            break;
    //                    }

    //                    BitmapDecoder decoder;
    //                    using (var stream = await file.OpenAsync(FileAccessMode.Read))
    //                        decoder = await BitmapDecoder.CreateAsync(stream);

    //                    using var bitmap = await decoder.GetSoftwareBitmapAsync();

    //                    if (mediaOptions.PhotoSize == PhotoSize.MaxWidthHeight && mediaOptions.MaxWidthHeight.HasValue)
    //                    {
    //                        var max = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
    //                        if (max > mediaOptions.MaxWidthHeight)
    //                        {
    //                            percent = (float)mediaOptions.MaxWidthHeight / max;
    //                        }
    //                    }

    //                    var finalWidth = Convert.ToUInt32(bitmap.PixelWidth * percent);
    //                    var finalHeight = Convert.ToUInt32(bitmap.PixelHeight * percent);

    //                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
    //                    {
    //                        var propertySet = new BitmapPropertySet();
    //                        var qualityValue = new BitmapTypedValue(mediaOptions.CompressionQuality / 100.0, PropertyType.Single);
    //                        propertySet.Add("ImageQuality", qualityValue);

    //                        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream, propertySet);
    //                        encoder.SetSoftwareBitmap(bitmap);
    //                        encoder.BitmapTransform.ScaledWidth = finalWidth;
    //                        encoder.BitmapTransform.ScaledHeight = finalHeight;
    //                        encoder.BitmapTransform.InterpolationMode = BitmapInterpolationMode.Fant;
    //                        encoder.IsThumbnailGenerated = true;

    //                        var tryAgain = false;
    //                        try
    //                        {
    //                            await encoder.FlushAsync();
    //                        }
    //                        catch (Exception err)
    //                        {
    //                            const int WINCODEC_ERR_UNSUPPORTEDOPERATION = unchecked((int)0x88982F81);
    //                            switch (err.HResult)
    //                            {
    //                                case WINCODEC_ERR_UNSUPPORTEDOPERATION:
    //                                    // If the encoder does not support writing a thumbnail, then try again
    //                                    // but disable thumbnail generation.
    //                                    encoder.IsThumbnailGenerated = false;
    //                                    tryAgain = true;
    //                                    break;
    //                                default:
    //                                    throw;
    //                            }
    //                        }

    //                        if (tryAgain)
    //                        {
    //                            await encoder.FlushAsync();
    //                        }
    //                    }

    //                    return true;
    //                }
    //                catch (Exception ex)
    //                {
    //#if DEBUG
    //                    throw ex;
    //#else
    //                    return false;
    //#endif
    //                }
    //            });
    //        }
    //        catch (Exception ex)
    //        {
    //#if DEBUG
    //            throw ex;
    //#else
    //            return Task.FromResult(false);
    //#endif
    //        }
    //    }

    protected virtual void OnDeviceUpdated(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        if (!update.Properties.TryGetValue("System.Devices.InterfaceEnabled", out var value)) return;

        lock (CaptureDevices)
        {
            if ((bool)value) CaptureDevices.Add(update.Id);
            else CaptureDevices.Remove(update.Id);

            IsCameraAvailable = CaptureDevices.Count > 0;
        }
    }

    protected virtual void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate update)
    {
        lock (CaptureDevices)
        {
            CaptureDevices.Remove(update.Id);
            if (CaptureDevices.Count == 0) IsCameraAvailable = false;
        }
    }

    protected virtual void OnDeviceAdded(DeviceWatcher sender, DeviceInformation device)
    {
        if (!device.IsEnabled) return;

        lock (CaptureDevices)
        {
            CaptureDevices.Add(device.Id);
            IsCameraAvailable = true;
        }
    }
}