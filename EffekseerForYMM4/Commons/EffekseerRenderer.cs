using System.Runtime.InteropServices;
using EffekseerForYMM4.Commons;
using EffekseerForYMM4.Diagnostics;

#pragma warning disable IDE0130 // The public interop namespace is part of the plugin's compatibility contract.
namespace EffekseerForNative;
#pragma warning restore IDE0130

public sealed class EffekseerRenderer : IDisposable
{
    private IntPtr handle;

    public EffekseerRenderer()
    {
        handle = NativeMethods.RendererCreate();
        if (handle == IntPtr.Zero)
        {
            PluginLog.Error("Native renderer creation failed");
            throw new InvalidOperationException("Effekseer native renderer could not be created.");
        }
    }

    public string? LastErrorMessage
    {
        get
        {
            var currentHandle = GetHandle();
            var required = NativeMethods.RendererGetLastError(currentHandle, IntPtr.Zero, 0);
            while (required > 1)
            {
                var buffer = Marshal.AllocHGlobal(required);
                try
                {
                    var actualRequired = NativeMethods.RendererGetLastError(currentHandle, buffer, required);
                    if (actualRequired <= required)
                    {
                        return actualRequired <= 1
                            ? null
                            : Marshal.PtrToStringUTF8(buffer);
                    }

                    required = actualRequired;
                }
                finally
                {
                    Marshal.FreeHGlobal(buffer);
                }
            }

            return null;
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
