using System.Runtime.InteropServices;

namespace EffekseerForYMM4.Commons;

internal static partial class NativeMethods
{
    private const string LibraryName = NativeAssemblyBootstrapper.NativeLibraryName;

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_create")]
    internal static partial IntPtr RendererCreate();

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_destroy")]
    internal static partial void RendererDestroy(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_initialize")]
    internal static partial int RendererInitialize(IntPtr handle, IntPtr device, IntPtr context, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_load_effect", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int RendererLoadEffect(IntPtr handle, string pathUtf8);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_get_last_error")]
    internal static partial int RendererGetLastError(IntPtr handle, IntPtr buffer, int bufferSize);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_render")]
    internal static partial void RendererRender(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_update")]
    internal static partial void RendererUpdate(IntPtr handle, float deltaFrames);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_sound_callback")]
    internal static partial void RendererSetSoundCallback(IntPtr handle, IntPtr loadSound, IntPtr unloadSound, IntPtr playSound);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_projection")]
    internal static partial void RendererSetProjection(IntPtr handle, int width, int height);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_projection_perspective")]
    internal static partial void RendererSetProjectionPerspective(IntPtr handle, float fov, int width, int height, float nearValue, float farValue);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_projection_orthographic")]
    internal static partial void RendererSetProjectionOrthographic(IntPtr handle, float width, float height, float nearValue, float farValue);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_camera_look_at")]
    internal static partial void RendererSetCameraLookAt(
        IntPtr handle,
        float positionX,
        float positionY,
        float positionZ,
        float targetX,
        float targetY,
        float targetZ,
        float upX,
        float upY,
        float upZ);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_location")]
    internal static partial void RendererSetLocation(IntPtr handle, float x, float y, float z);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_rotation")]
    internal static partial void RendererSetRotation(IntPtr handle, float x, float y, float z);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_set_scale")]
    internal static partial void RendererSetScale(IntPtr handle, float scale);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_reset")]
    internal static partial void RendererReset(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_stop")]
    internal static partial void RendererStop(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_play_effect", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial void RendererPlayEffect(IntPtr handle, string pathUtf8, float x, float y, float z);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_shutdown")]
    internal static partial void RendererShutdown(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_renderer_get_total_frame")]
    internal static partial int RendererGetTotalFrame(IntPtr handle);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_combine", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PathCombine(string basePathUtf8, string childPathUtf8, IntPtr buffer, int bufferSize);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_ensure_directory", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PathEnsureDirectory(string pathUtf8);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_exists", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PathExists(string pathUtf8);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_write_utf8_text", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PathWriteUtf8Text(string pathUtf8, string contentUtf8);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_read_utf8_text", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial int PathReadUtf8Text(string pathUtf8, IntPtr buffer, int bufferSize);

    [LibraryImport(LibraryName, EntryPoint = "effekseer_path_get_last_error")]
    internal static partial int PathGetLastError(IntPtr buffer, int bufferSize);
}
