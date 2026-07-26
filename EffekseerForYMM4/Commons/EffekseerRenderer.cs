using System.Runtime.InteropServices;
using EffekseerForYMM4.Commons;

namespace EffekseerForNative;

public sealed class EffekseerRenderer : IDisposable
{
    private IntPtr handle;

    public EffekseerRenderer()
    {
        NativeAssemblyBootstrapper.EnsureInitialized();
        handle = NativeMethods.RendererCreate();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Effekseer native renderer could not be created.");
        }
    }

    public string? LastErrorMessage => ReadUtf8Buffer((buffer, size) => NativeMethods.RendererGetLastError(handle, buffer, size));

    public bool Initialize(IntPtr device, IntPtr context, int width, int height) =>
        NativeMethods.RendererInitialize(GetHandle(), device, context, width, height) != 0;

    public bool LoadEffect(string path) => NativeMethods.RendererLoadEffect(GetHandle(), path) != 0;

    public void Render() => NativeMethods.RendererRender(GetHandle());
    public void Update(float deltaFrames) => NativeMethods.RendererUpdate(GetHandle(), deltaFrames);
    public void SetSoundCallback(IntPtr loadSound, IntPtr unloadSound, IntPtr playSound) =>
        NativeMethods.RendererSetSoundCallback(GetHandle(), loadSound, unloadSound, playSound);
    public void SetProjection(int width, int height) => NativeMethods.RendererSetProjection(GetHandle(), width, height);
    public void SetProjectionPerspective(float fov, int width, int height, float nearValue, float farValue) =>
        NativeMethods.RendererSetProjectionPerspective(GetHandle(), fov, width, height, nearValue, farValue);
    public void SetProjectionOrthographic(float width, float height, float nearValue, float farValue) =>
        NativeMethods.RendererSetProjectionOrthographic(GetHandle(), width, height, nearValue, farValue);
    public void SetCameraLookAt(
        float positionX,
        float positionY,
        float positionZ,
        float targetX,
        float targetY,
        float targetZ,
        float upX,
        float upY,
        float upZ) =>
        NativeMethods.RendererSetCameraLookAt(
            GetHandle(),
            positionX,
            positionY,
            positionZ,
            targetX,
            targetY,
            targetZ,
            upX,
            upY,
            upZ);
    public void SetLocation(float x, float y, float z) => NativeMethods.RendererSetLocation(GetHandle(), x, y, z);
    public void SetRotation(float x, float y, float z) => NativeMethods.RendererSetRotation(GetHandle(), x, y, z);
    public void SetScale(float scale) => NativeMethods.RendererSetScale(GetHandle(), scale);
    public void Reset() => NativeMethods.RendererReset(GetHandle());
    public void StopRoot() => NativeMethods.RendererStop(GetHandle());
    public void PlayEffect(string path, float x, float y, float z) =>
        NativeMethods.RendererPlayEffect(GetHandle(), path, x, y, z);
    public void Destroy()
    {
        if (handle != IntPtr.Zero)
        {
            NativeMethods.RendererShutdown(handle);
        }
    }
    public int GetTotalFrame() => NativeMethods.RendererGetTotalFrame(GetHandle());

    public void Dispose()
    {
        var current = Interlocked.Exchange(ref handle, IntPtr.Zero);
        if (current != IntPtr.Zero)
        {
            NativeMethods.RendererDestroy(current);
        }
        GC.SuppressFinalize(this);
    }

    ~EffekseerRenderer() => Dispose();

    private IntPtr GetHandle() =>
        handle != IntPtr.Zero ? handle : throw new ObjectDisposedException(nameof(EffekseerRenderer));

    internal static string? ReadUtf8Buffer(Func<IntPtr, int, int> nativeCall)
    {
        var required = nativeCall(IntPtr.Zero, 0);
        if (required <= 1)
        {
            return null;
        }

        var buffer = Marshal.AllocHGlobal(required);
        try
        {
            nativeCall(buffer, required);
            return Marshal.PtrToStringUTF8(buffer);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }
}
