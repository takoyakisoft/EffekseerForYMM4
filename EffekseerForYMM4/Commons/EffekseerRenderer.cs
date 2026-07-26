using System.Runtime.InteropServices;
using EffekseerForYMM4.Commons;

namespace EffekseerForNative;

public sealed class EffekseerRenderer : IDisposable
{
    private IntPtr handle;

    public EffekseerRenderer()
    {
        handle = NativeMethods.RendererCreate();
        if (handle == IntPtr.Zero)
        {
            throw new InvalidOperationException("Effekseer native renderer could not be created.");
        }
    }

    public unsafe string? LastErrorMessage
    {
        get
        {
            const int bufferSize = 2048;
            byte* buffer = stackalloc byte[bufferSize];
            var required = NativeMethods.RendererGetLastError(
                GetHandle(),
                (IntPtr)buffer,
                bufferSize);
            return required <= 1
                ? null
                : Marshal.PtrToStringUTF8((IntPtr)buffer);
        }
    }

    public bool Initialize(IntPtr device, IntPtr context, int width, int height) =>
        NativeMethods.RendererInitialize(GetHandle(), device, context, width, height) != 0;

    public bool LoadEffect(string path) => NativeMethods.RendererLoadEffect(GetHandle(), path) != 0;

    public void Render(IntPtr renderTarget, IntPtr depthStencil, int width, int height) =>
        NativeMethods.RendererRender(GetHandle(), renderTarget, depthStencil, width, height);
    public void Update(float deltaFrames) => NativeMethods.RendererUpdate(GetHandle(), deltaFrames);
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
    public int GetTotalFrame() => NativeMethods.RendererGetTotalFrame(GetHandle());

    public void Dispose()
    {
        var current = handle;
        handle = IntPtr.Zero;
        if (current != IntPtr.Zero)
        {
            NativeMethods.RendererDestroy(current);
        }
        GC.SuppressFinalize(this);
    }

    ~EffekseerRenderer() => Dispose();

    private IntPtr GetHandle() =>
        handle != IntPtr.Zero ? handle : throw new ObjectDisposedException(nameof(EffekseerRenderer));
}
