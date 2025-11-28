using Microsoft.Maui.LifecycleEvents;

namespace MPowerKit.MediaPlugin;

/// <summary>
/// Cross platform Media implementations
/// </summary>
public static class Media
{
    private static readonly Lazy<IMedia> _implementation = new(CreateMedia, LazyThreadSafetyMode.PublicationOnly);

    /// <summary>
    /// Gets if the plugin is supported on the current platform.
    /// </summary>
    public static bool IsSupported => _implementation.Value is not null;

    /// <summary>
    /// Current plugin implementation to use
    /// </summary>
    public static IMedia Current => _implementation.Value;

    private static IMedia CreateMedia()
    {
#if ANDROID || MACIOS || WINDOWS
        return new MediaImplementation();
#else
        return new MediaImplementationShared();
#endif
    }

    public static MauiAppBuilder UseMPowerKitMediaPlugin(this MauiAppBuilder builder)
    {
        return UseMPowerKitMediaPlugin(builder, false);
    }

    public static MauiAppBuilder UseMPowerKitMediaPlugin(this MauiAppBuilder builder, bool registerInterface)
    {
        if (registerInterface)
        {
            builder.Services.AddSingleton(() => Current);
        }

#if ANDROID
        builder.ConfigureLifecycleEvents(lifecycle =>
        {
            lifecycle.AddAndroid(android =>
            {
                android.OnActivityResult((activity, requestCode, resultCode, data) =>
                {
                    MediaImplementation.SendActivityResult(requestCode, resultCode, data);
                });
            });
        });
#endif

        return builder;
    }
}