using Foundation;

using ImageIO;

using Photos;

using PhotosUI;

using UIKit;

using UniformTypeIdentifiers;

using Permissions = Microsoft.Maui.ApplicationModel.Permissions;
using PermissionStatus = Microsoft.Maui.ApplicationModel.PermissionStatus;

namespace MPowerKit.MediaPlugin;

public class State
{
    public required TaskCompletionSource<List<MediaFile>?> Tcs { get; set; }
    public UIViewController? Controller { get; set; }
}

/// <summary>
/// Implementation for Media
/// </summary>
public class MediaImplementation : IMedia
{
    protected const string CameraDescription = "NSCameraUsageDescription";
    protected const string PhotoDescription = "NSPhotoLibraryUsageDescription";
    protected const string PhotoAddDescription = "NSPhotoLibraryAddUsageDescription";
    protected const string MicrophoneDescription = "NSMicrophoneUsageDescription";

    /// <summary>
    /// image type
    /// </summary>
    public const string TypeImage = "public.image";

    /// <summary>
    /// movie type
    /// </summary>
    public const string TypeMovie = "public.movie";

    /// <summary>
    /// Color of the status bar
    /// </summary>
    protected static UIStatusBarStyle StatusBarStyle { get; set; }

    /// <inheritdoc/>
    public bool IsCameraAvailable { get; }

    /// <inheritdoc/>
    public bool IsTakePhotoSupported { get; }

    /// <inheritdoc/>
    public bool IsPickPhotoSupported { get; }

    /// <inheritdoc/>
    public bool IsTakeVideoSupported { get; }

    /// <inheritdoc/>
    public bool IsPickVideoSupported { get; }

    protected State? State;

    protected bool IsPicking => State is not null;

    /// <summary>
    /// Implementation
    /// </summary>
    public MediaImplementation()
    {
        StatusBarStyle = UIApplication.SharedApplication.StatusBarStyle;
        IsCameraAvailable = UIImagePickerController.IsCameraDeviceAvailable(UIImagePickerControllerCameraDevice.Front)
            | UIImagePickerController.IsCameraDeviceAvailable(UIImagePickerControllerCameraDevice.Rear);

        var availableCameraMedia = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.Camera) ?? [];
        var avaialbleLibraryMedia = UIImagePickerController.AvailableMediaTypes(UIImagePickerControllerSourceType.PhotoLibrary) ?? [];

        foreach (var type in availableCameraMedia.Concat(avaialbleLibraryMedia))
        {
            if (type == TypeMovie)
                IsTakeVideoSupported = IsPickVideoSupported = true;
            else if (type == TypeImage)
                IsTakePhotoSupported = IsPickPhotoSupported = true;
        }
    }

    public virtual Task<bool> Initialize() => Task.FromResult(true);

    public async Task<MediaFile?> PickPhotoAsync(PickRequest? request = null, CancellationToken token = default)
    {
        if (!IsPickPhotoSupported) throw new NotSupportedException();

        request ??= new PickRequest();

        var medias = await GetMediasAsync(true, request, null, token);

        return medias?.FirstOrDefault();
    }

    public Task<List<MediaFile>?> PickPhotosAsync(
        PickRequest? request = null,
        MultiPickerOptions? multiOptions = null,
        CancellationToken token = default)
    {
        if (!IsPickPhotoSupported) throw new NotSupportedException();

        request ??= new PickRequest();
        multiOptions ??= new MultiPickerOptions();

        return GetMediasAsync(true, request, multiOptions, token);
    }

    public async Task<MediaFile?> TakePhotoAsync(CaptureRequest? request = null, CancellationToken token = default)
    {
        if (!IsTakeVideoSupported || !IsCameraAvailable) throw new NotSupportedException();

        CheckUsageDescription(CameraDescription);

        request ??= new CaptureRequest();

        request.VerifyCaptureRequest();

        List<string> permissionsToCheck = [nameof(Permissions.Camera)];

        await CheckPermissions([.. permissionsToCheck]);

        var medias = await GetMediasAsync(true, request, null, token);

        var media = medias?.FirstOrDefault();

        return media;
    }

    public async Task<MediaFile?> PickVideoAsync(VideoPickRequest? request = null, CancellationToken token = default)
    {
        if (!IsPickVideoSupported) throw new NotSupportedException();

        request ??= new VideoPickRequest();

        var medias = await GetMediasAsync(false, request, token: token);

        return medias?.FirstOrDefault();
    }

    public async Task<MediaFile?> TakeVideoAsync(CaptureRequest? request = null, CancellationToken token = default)
    {
        if (!IsTakeVideoSupported || !IsCameraAvailable) throw new NotSupportedException();

        CheckUsageDescription(CameraDescription, MicrophoneDescription);

        request ??= new CaptureRequest();

        request.VerifyCaptureRequest();

        List<string> permissionsToCheck = [nameof(Permissions.Camera), nameof(Permissions.Microphone)];

        await CheckPermissions([.. permissionsToCheck]);

        var medias = await GetMediasAsync(false, request, null, token);

        return medias?.FirstOrDefault();
    }

    protected virtual async Task<List<MediaFile>?> GetMediasAsync(
        bool isPhoto,
        MediaRequest request,
        MultiPickerOptions? pickerOptions = null,
        CancellationToken token = default)
    {
        if (IsPicking) throw new InvalidOperationException("Only one picking operation can be produced at a time");

        var tcs = new TaskCompletionSource<List<MediaFile>?>();

        if (token.IsCancellationRequested)
        {
            tcs.SetCanceled(token);

            return await tcs.Task;
        }

        State = new State
        {
            Tcs = tcs
        };

        try
        {
            try
            {
                token.Register(() =>
                {
                    State.Controller?.DismissViewController(true, null);
                });

                var vc = Platform.GetCurrentUIViewController()!;

                if (request is PickRequest)
                {
                    PHPickerConfiguration config = new()
                    {
                        SelectionLimit = pickerOptions?.MaximumImagesCount ?? 1,
                        Filter = isPhoto ? PHPickerFilter.ImagesFilter : PHPickerFilter.VideosFilter,
                    };

                    State.Controller = new PHPickerViewController(config)
                    {
                        Delegate = new PHPickerDelegate(State),
                    };

                    if (DeviceInfo.Idiom == DeviceIdiom.Tablet)
                    {
                        State.Controller.ModalPresentationStyle =
                            request?.ModalPresentationStyle is MediaPickerModalPresentationStyle.OverFullScreen
                            ? UIModalPresentationStyle.Popover
                            : UIModalPresentationStyle.FullScreen;

                        if (State.Controller.PopoverPresentationController != null)
                        {
                            State.Controller.PopoverPresentationController.SourceView = vc.View!;
                        }
                    }
                }
                else
                {
                    var captureRequest = (request as CaptureRequest)!;

                    State.Controller = new UIImagePickerController
                    {
                        SourceType = UIImagePickerControllerSourceType.Camera,
                        AllowsEditing = !isPhoto,
                        MediaTypes = [isPhoto ? TypeImage : TypeMovie],
                        CameraDevice = GetUICameraDevice(captureRequest.DefaultCamera),
                        Delegate = new PhotoPickerDelegate(State, captureRequest),
                        CameraCaptureMode = isPhoto ? UIImagePickerControllerCameraCaptureMode.Photo : UIImagePickerControllerCameraCaptureMode.Video
                    };

                    if (State.Controller.PopoverPresentationController != null)
                    {
                        State.Controller.PopoverPresentationController.SourceView = vc.View!;
                    }
                }

                ConfigureController(State.Controller, State);

                await vc.PresentViewControllerAsync(State.Controller, true);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return await tcs.Task;
        }
        finally
        {
            State.Controller?.Dispose();
            State = null;
        }
    }

    protected virtual void ConfigureController(UIViewController controller, State state)
    {
        if (controller.PresentationController is not null)
            controller.PresentationController.Delegate = new PresentatControllerDelegate(state);
    }

    protected static UIImagePickerControllerCameraDevice GetUICameraDevice(CameraDevice device)
    {
        return device switch
        {
            CameraDevice.Front => UIImagePickerControllerCameraDevice.Front,
            CameraDevice.Rear => UIImagePickerControllerCameraDevice.Rear,
            _ => throw new NotSupportedException(),
        };
    }

    protected static UIImagePickerControllerQualityType GetQuailty(VideoQuality quality)
    {
        return quality switch
        {
            VideoQuality.Low => UIImagePickerControllerQualityType.Low,
            VideoQuality.Medium => UIImagePickerControllerQualityType.Medium,
            _ => UIImagePickerControllerQualityType.High,
        };
    }

    protected static async Task CheckPermissions(params string[] permissions)
    {
        //See which ones we need to request.
        var permissionsToRequest = new List<string>();
        foreach (var permission in permissions)
        {
            var permissionStatus = PermissionStatus.Unknown;
            switch (permission)
            {
                case nameof(Permissions.Camera):
                    permissionStatus = await Permissions.CheckStatusAsync<Permissions.Camera>();
                    break;
                case nameof(Permissions.Photos):
                    permissionStatus = await Permissions.CheckStatusAsync<Permissions.Photos>();
                    break;
                case nameof(Permissions.Microphone):
                    permissionStatus = await Permissions.CheckStatusAsync<Permissions.Microphone>();
                    break;
            }

            if (permissionStatus != PermissionStatus.Granted)
                permissionsToRequest.Add(permission);
        }

        //Nothing to request, Awesome!
        if (permissionsToRequest.Count == 0)
            return;

        var results = new Dictionary<string, PermissionStatus>();
        foreach (var permission in permissions)
        {
            switch (permission)
            {
                case nameof(Permissions.Camera):
                    results.Add(permission, await Permissions.RequestAsync<Permissions.Camera>());
                    break;
                case nameof(Permissions.Photos):
                    results.Add(permission, await Permissions.RequestAsync<Permissions.Photos>());
                    break;
                case nameof(Permissions.Microphone):
                    results.Add(permission, await Permissions.RequestAsync<Permissions.Microphone>());
                    break;
            }
        }

        //check for anything not granted, if none, Awesome!
        var notGranted = results.Where(r => r.Value is not PermissionStatus.Granted);
        if (!notGranted.Any()) return;

        //Gunna need those permissions :(
        throw new MediaPermissionException([.. notGranted.Select(r => r.Key)]);
    }

    protected virtual void CheckUsageDescription(params string[] descriptionNames)
    {
        foreach (var description in descriptionNames)
        {
            var info = NSBundle.MainBundle.InfoDictionary;

            if (!info.ContainsKey(new NSString(description)))
                throw new UnauthorizedAccessException($"On iOS 10 and higher you must set {description} in your Info.plist file to enable Authorization Requests for access!");
        }
    }
}

public class PHPickerDelegate : PHPickerViewControllerDelegate
{
    public State State { get; }

    public PHPickerDelegate(State state)
    {
        State = state;
    }

    public override async void DidFinishPicking(PHPickerViewController picker, PHPickerResult[] results)
    {
        var tcs = State.Tcs;

        if (results?.Length is null or 0)
        {
            await picker.DismissViewControllerAsync(true);
            tcs.TrySetCanceled();
            return;
        }

        List<MediaFile> files = new(results.Length);

        try
        {
            foreach (var res in results)
            {
                var provider = res.ItemProvider;

                string? type = provider.HasItemConformingTo(MediaImplementation.TypeImage)
                    ? MediaImplementation.TypeImage
                    : (provider.HasItemConformingTo(MediaImplementation.TypeMovie)
                        ? MediaImplementation.TypeMovie
                        : null);

                if (type is null) continue;

                TaskCompletionSource<string> localTcs = new();
                provider.LoadFileRepresentation(type, (url, err) =>
                {
                    if (err is not null)
                    {
                        localTcs.SetException(new NSErrorException(err));
                        return;
                    }

                    var newPath = MediaExtensions.GetUniquePath(FileSystem.Current.AppDataDirectory, url.LastPathComponent!, true);

                    using var rs = File.OpenRead(url.Path!);
                    using var ws = File.OpenWrite(newPath);
                    rs.CopyTo(ws);

                    localTcs.SetResult(newPath);
                });

                var path = await localTcs.Task;

                var url = NSUrl.FromFilename(path);

                var mediaFile = new MediaFile(path, () => File.OpenRead(path), null, url.AbsoluteString, url.LastPathComponent);
                files.Add(mediaFile);
            }

            await picker.DismissViewControllerAsync(true);

            if (files.Count == 0)
            {
                tcs.TrySetCanceled();
            }
            else tcs.TrySetResult(files);
        }
        catch (Exception ex)
        {
            await picker.DismissViewControllerAsync(true);

            tcs.TrySetException(ex);
        }
    }

    protected virtual string? GetIdentifier(string[] identifiers)
    {
        if (identifiers?.Length is null or 0) return null;
        if (identifiers.Any(i => i.StartsWith(UTTypes.LivePhoto.Identifier)) && identifiers.Contains(UTTypes.Jpeg.Identifier))
            return identifiers.FirstOrDefault(i => i == UTTypes.Jpeg.Identifier);
        if (identifiers.Contains(UTTypes.QuickTimeMovie.Identifier))
            return identifiers.FirstOrDefault(i => i == UTTypes.QuickTimeMovie.Identifier);
        return identifiers.FirstOrDefault();
    }
}

public class PresentatControllerDelegate : UIAdaptivePresentationControllerDelegate
{
    public State State { get; }

    public PresentatControllerDelegate(State state)
    {
        State = state;
    }

    public override void DidDismiss(UIPresentationController presentationController)
    {
        State.Tcs?.TrySetCanceled();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        State.Tcs?.TrySetCanceled();
    }
}

public class PhotoPickerDelegate : UIImagePickerControllerDelegate
{
    protected State State { get; }
    protected CaptureRequest Request { get; }

    public PhotoPickerDelegate(State state, CaptureRequest request)
    {
        State = state;
        Request = request;
    }

    public override async void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
    {
        await picker.DismissViewControllerAsync(true);

        if (info is null)
        {
            State.Tcs?.TrySetCanceled();
            return;
        }

        try
        {
            var result = ConvertPickerResults(info);

            if (result is null)
            {
                State.Tcs?.TrySetCanceled();
                return;
            }

            State.Tcs?.TrySetResult([result]);
        }
        catch (Exception ex)
        {
            State.Tcs?.TrySetException(ex);
        }
    }

    public override async void Canceled(UIImagePickerController picker)
    {
        await picker.DismissViewControllerAsync(true);

        State.Tcs?.SetCanceled();
    }

    protected virtual MediaFile? ConvertPickerResults(NSDictionary info)
    {
        var assetUrl = (info.ValueForKey(UIImagePickerController.ImageUrl)
            ?? info.ValueForKey(UIImagePickerController.MediaURL)) as NSUrl;

        var path = assetUrl?.Path;

        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return new MediaFile(path, () => File.OpenRead(path), null, assetUrl!.AbsoluteString, GetOriginalName(info));
        }

        assetUrl?.Dispose();

        if (info.ValueForKey(UIImagePickerController.OriginalImage) is UIImage img
            && info.ValueForKey(UIImagePickerController.MediaMetadata) is NSDictionary meta)
        {
            assetUrl = GetImageUri(img, meta, true);

            path = assetUrl!.Path!;

            return new MediaFile(path, () => File.OpenRead(path), null, assetUrl!.AbsoluteString, assetUrl.LastPathComponent);
        }

        return null;
    }

    protected virtual string? GetOriginalName(NSDictionary info)
    {
        if (PHPhotoLibrary.GetAuthorizationStatus(PHAccessLevel.ReadWrite) != PHAuthorizationStatus.Authorized
            || !info.ContainsKey(UIImagePickerController.PHAsset))
            return null;

        using var asset = info.ValueForKey(UIImagePickerController.PHAsset) as PHAsset;

        return asset is not null
            ? PHAssetResource.GetAssetResources(asset)?.FirstOrDefault()?.OriginalFilename
            : null;
    }

    protected virtual NSUrl? GetImageUri(UIImage? image, NSDictionary? metadata, bool isPhoto = true)
    {
        if (image is null || metadata is null) return null;

        using var source = CGImageSource.FromData(image.AsJPEG()!);

        var destData = new NSMutableData();

        using var destination = CGImageDestination.Create(destData, source!.TypeIdentifier!, 1, null);
        destination!.AddImage(source, 0, metadata);
        destination.Close();
        image.Dispose();
        metadata.Dispose();

        string fullPath = MediaExtensions.GetDesiredPath(Request.DesiredDirectory, Request.DesiredName, isPhoto);

        var url = NSUrl.FromFilename(fullPath);

        destData!.Save(url, true);

        return url;
    }
}