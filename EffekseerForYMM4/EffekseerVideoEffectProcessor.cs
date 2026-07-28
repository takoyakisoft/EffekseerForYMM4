using System.Numerics;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using EffekseerForYMM4.Commons;
using EffekseerForYMM4.Diagnostics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{
    partial class EffekseerVideoEffectProcessor : IVideoEffectProcessor
    {
        private const double EffekseerFps = 60.0;
        private const float MaxSimulationAdvanceFrames = 8.0f;
        private const float DefaultCameraZ = 20.0f;

        private enum PlaybackAccessKind
        {
            Initial,
            Continuous,
            Random,
        }

        bool isFirst = true;
        readonly EffekseerVideoEffect item;

        public ID2D1Image Output { get; private set; }

        ID2D1Bitmap1? bitmap;
        readonly Vortice.Direct2D1.Effects.Composite compositeEffect;
        readonly Vortice.Direct2D1.Effects.AffineTransform2D transformEffect;

        private EffekseerForNative.EffekseerRenderer? nativeRenderer;

        private readonly ID3D11Device d3dDevice;
        private ID3D11Texture2D? renderTargetTexture;
        private ID3D11RenderTargetView? renderTargetView;
        private ID3D11Texture2D? depthStencilTexture;
        private ID3D11DepthStencilView? depthStencilView;
        private readonly IGraphicsDevicesAndContext _devices;

        private TimeSpan _duration = TimeSpan.Zero;
        public TimeSpan Duration => _duration;
        private int lastWidth;
        private int lastHeight;

        private string? loadedFilePath;
        private bool hasLoadedEffect;
        private int loadedTotalFrames;
        private bool hasPreviousItemFrame;
        private long previousItemFrame;
        private double renderedFrame;

        private bool hasAppliedCamera;
        private float appliedCamX;
        private float appliedCamY;
        private float appliedCamZ;
        private bool hasAppliedProjection;
        private ProjectionMode appliedProjectionMode;
        private float appliedProjectionValue;
        private int appliedProjectionWidth;
        private int appliedProjectionHeight;
        private bool hasAppliedTransform;
        private float appliedPosX;
        private float appliedPosY;
        private float appliedPosZ;
        private float appliedRotX;
        private float appliedRotY;
        private float appliedRotZ;
        private float appliedScale;
        private ID2D1Image? inputImage;
        private readonly EffekseerLoadErrorNotifier loadErrorNotifier = new();
        private static readonly Lock RenderLock = new();
        private static readonly ConcurrentDictionary<string, CompositeFormat> MessageFormats = new(StringComparer.Ordinal);
        private string? lastUpdateErrorKey;

        public EffekseerVideoEffectProcessor(IGraphicsDevicesAndContext devices, EffekseerVideoEffect item)
        {
            this.item = item;

            _devices = devices;
            d3dDevice = devices.D3D.Device;

            compositeEffect = new Vortice.Direct2D1.Effects.Composite(devices.DeviceContext);
            transformEffect = new Vortice.Direct2D1.Effects.AffineTransform2D(devices.DeviceContext);
            transformEffect.SetInput(0, null, true);
            compositeEffect.SetInput(1, transformEffect.Output, true);

            Output = compositeEffect.Output;
            PluginLog.Information("Video effect processor created");
        }

        /// <summary>
        /// エフェクトに入力する映像を設定します。
        /// </summary>
        /// <param name="input"></param>
        public void SetInput(ID2D1Image? input)
        {
            inputImage = input;
            compositeEffect.SetInput(0, input, true);
        }

        /// <summary>
        /// エフェクトに入力する映像をクリアします。
        /// </summary>
        public void ClearInput()
        {
            inputImage = null;
            compositeEffect.SetInput(0, null, true);
        }

        /// <summary>
        /// エフェクトを更新します。
        /// </summary>
        /// <param name="effectDescription">エフェクトの描画に必要な各種設定項目。</param>
        /// <returns>描画関連の設定項目。</returns>
        public DrawDescription Update(EffectDescription effectDescription)
        {
            try
            {
                var result = UpdateCore(effectDescription);
                lastUpdateErrorKey = null;
                return result;
            }
            catch (Exception exception)
            {
                var errorKey = $"{exception.GetType().FullName}|{exception.HResult}|{exception.Message}";
                if (!string.Equals(lastUpdateErrorKey, errorKey, StringComparison.Ordinal))
                {
                    lastUpdateErrorKey = errorKey;
                    PluginLog.Error(
                        $"Video effect update failed. path={item.FilePath}, frame={effectDescription.ItemPosition.Frame}",
                        exception);
                }

                throw;
            }
        }

        private DrawDescription UpdateCore(EffectDescription effectDescription)
        {
            if (inputImage == null)
                return effectDescription.DrawDescription;
            int width;
            int height;
            if (item.IsScreenSize)
            {
                width = (int)Math.Max(1, effectDescription.ScreenSize.Width);
                height = (int)Math.Max(1, effectDescription.ScreenSize.Height);
            }
            else
            {
                var bounds = _devices.DeviceContext.GetImageLocalBounds(inputImage);
                width = (int)Math.Max(1, bounds.Right - bounds.Left);
                height = (int)Math.Max(1, bounds.Bottom - bounds.Top);
            }

            var frame = effectDescription.ItemPosition.Frame;
            var length = effectDescription.ItemDuration.Frame;
            var fps = effectDescription.FPS;
            var safeFps = Math.Max(fps, 1);

            if (lastWidth != width || lastHeight != height)
            {
                lastWidth = width;
                lastHeight = height;
                if (!isFirst)
                {
                    Resize();
                }
            }

            if (isFirst)
            {
                nativeRenderer = new EffekseerForNative.EffekseerRenderer();
                lock (RenderLock)
                {
                    if (!nativeRenderer.Initialize(d3dDevice.NativePointer, d3dDevice.ImmediateContext.NativePointer, width, height))
                    {
                        PluginLog.Warning(
                            $"Native renderer initialization failed. width={width}, height={height}, detail={nativeRenderer.LastErrorMessage}");
                        nativeRenderer.Dispose();
                        nativeRenderer = null;
                        return effectDescription.DrawDescription;
                    }

                    CreateResources(width, height);
                }
                isFirst = false;
            }

            if (loadedFilePath != item.FilePath)
            {
                ResetPlaybackTracking();
                loadedTotalFrames = 0;
                _duration = TimeSpan.Zero;

                if (nativeRenderer == null)
                {
                    return effectDescription.DrawDescription;
                }

                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    var ext = System.IO.Path.GetExtension(item.FilePath);
                    if (!string.Equals(ext, ".efk", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(ext, ".efkefc", StringComparison.OrdinalIgnoreCase))
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = false;
                        loadErrorNotifier.ShowIfNeeded(
                            item.FilePath,
                            string.Format(
                                CultureInfo.CurrentCulture,
                                MessageFormats.GetOrAdd(
                                    Translate.Error_InvalidEffectExtension,
                                    static format => CompositeFormat.Parse(format)),
                                ".efk, .efkefc"));
                    }
                    else if (!System.IO.File.Exists(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = false;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, Translate.Error_EffectFileNotFound);
                    }
                    else if (TryLoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = true;
                        loadErrorNotifier.Reset();
                        loadedTotalFrames = nativeRenderer.GetTotalFrame();
                        if (loadedTotalFrames > 0 && loadedTotalFrames < int.MaxValue)
                        {
                            _duration = TimeSpan.FromSeconds((double)loadedTotalFrames / EffekseerFps);
                        }
                        PluginLog.Information(
                            $"Effect loaded. path={item.FilePath}, totalFrames={loadedTotalFrames}");
                    }
                    else
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = false;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, nativeRenderer.LastErrorMessage ?? Translate.Error_EffectFilesMayBeInvalid);
                    }
                }
                else
                {
                    loadedFilePath = item.FilePath;
                    hasLoadedEffect = false;
                    loadErrorNotifier.Reset();
                }
            }

            if (nativeRenderer == null || !hasLoadedEffect)
            {
                return effectDescription.DrawDescription;
            }

            double targetFrame = Math.Max(0, effectDescription.ItemPosition.Time.TotalSeconds * EffekseerFps);

            if (item.IsLoop && loadedTotalFrames > 0 && loadedTotalFrames < int.MaxValue)
            {
                targetFrame %= loadedTotalFrames;
                if (targetFrame < 0)
                    targetFrame += loadedTotalFrames;
            }

            double animFrame = frame;
            float camX = (float)item.CamPosX.GetValue((long)animFrame, length, safeFps);
            float camY = (float)item.CamPosY.GetValue((long)animFrame, length, safeFps);
            float camZ = item.ProjectionMode == ProjectionMode.Perspective
                ? (float)item.CamPosZ.GetValue((long)animFrame, length, safeFps)
                : DefaultCameraZ;
            float posX = (float)item.PosX.GetValue((long)animFrame, length, safeFps);
            float posY = (float)item.PosY.GetValue((long)animFrame, length, safeFps);
            float posZ = (float)item.PosZ.GetValue((long)animFrame, length, safeFps);
            float rotX = (float)item.RotX.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
            float rotY = (float)item.RotY.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
            float rotZ = (float)item.RotZ.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
            float scalePercent = (float)item.Scale.GetValue((long)animFrame, length, safeFps);
            float scale = scalePercent <= 0f ? 0f : Math.Max(scalePercent / 100.0f, 0.0001f);

            ApplyCamera(camX, camY, camZ);

            if (item.ProjectionMode == ProjectionMode.Orthographic)
            {
                var orthographicHeight = Math.Max(
                    0.001f,
                    (float)item.OrthographicSize.GetValue((long)animFrame, length, safeFps));
                ApplyOrthographicProjection(orthographicHeight, width, height);
            }
            else
            {
                float fov = Math.Clamp(
                    (float)item.Fov.GetValue((long)animFrame, length, safeFps),
                    1.0f,
                    179.0f);
                ApplyPerspectiveProjection(fov, width, height);
            }
            ApplyTransform(posX, posY, posZ, rotX, rotY, rotZ, scale);

            var currentItemFrame = (long)frame;
            switch (ClassifyPlaybackAccess(currentItemFrame, targetFrame))
            {
                case PlaybackAccessKind.Initial:
                    InitializePlayback(targetFrame);
                    break;
                case PlaybackAccessKind.Continuous:
                    AdvanceRenderer((float)(targetFrame - renderedFrame));
                    renderedFrame = targetFrame;
                    break;
                case PlaybackAccessKind.Random:
                    RestartPlaybackAt(targetFrame);
                    break;
            }
            previousItemFrame = currentItemFrame;
            hasPreviousItemFrame = true;

            if (renderTargetView != null && depthStencilView != null)
            {
                lock (RenderLock)
                {
                    nativeRenderer.Render(
                        renderTargetView.NativePointer,
                        depthStencilView.NativePointer,
                        width,
                        height);
                }
            }

            return effectDescription.DrawDescription;
        }

        private bool TryLoadEffect(string path)
        {
            lock (RenderLock)
            {
                return nativeRenderer?.LoadEffect(path) == true;
            }
        }

        private void ApplyCamera(float camX, float camY, float camZ)
        {
            if (hasAppliedCamera &&
                appliedCamX == camX &&
                appliedCamY == camY &&
                appliedCamZ == camZ)
            {
                return;
            }

            nativeRenderer?.SetCameraLookAt(
                camX, camY, camZ,
                camX, camY, 0,
                0, 1, 0);
            hasAppliedCamera = true;
            appliedCamX = camX;
            appliedCamY = camY;
            appliedCamZ = camZ;
        }

        private void ApplyPerspectiveProjection(float fov, int width, int height)
        {
            if (hasAppliedProjection &&
                appliedProjectionMode == ProjectionMode.Perspective &&
                appliedProjectionValue == fov &&
                appliedProjectionWidth == width &&
                appliedProjectionHeight == height)
            {
                return;
            }

            nativeRenderer?.SetProjectionPerspective(fov, width, height, 1.0f, 2000.0f);
            hasAppliedProjection = true;
            appliedProjectionMode = ProjectionMode.Perspective;
            appliedProjectionValue = fov;
            appliedProjectionWidth = width;
            appliedProjectionHeight = height;
        }

        private void ApplyOrthographicProjection(float orthographicHeight, int width, int height)
        {
            if (hasAppliedProjection &&
                appliedProjectionMode == ProjectionMode.Orthographic &&
                appliedProjectionValue == orthographicHeight &&
                appliedProjectionWidth == width &&
                appliedProjectionHeight == height)
            {
                return;
            }

            var orthographicWidth = orthographicHeight * width / Math.Max(1.0f, height);
            nativeRenderer?.SetProjectionOrthographic(orthographicWidth, orthographicHeight, 1.0f, 2000.0f);
            hasAppliedProjection = true;
            appliedProjectionMode = ProjectionMode.Orthographic;
            appliedProjectionValue = orthographicHeight;
            appliedProjectionWidth = width;
            appliedProjectionHeight = height;
        }

        private void ApplyTransform(
            float posX,
            float posY,
            float posZ,
            float rotX,
            float rotY,
            float rotZ,
            float scale)
        {
            if (hasAppliedTransform &&
                appliedPosX == posX &&
                appliedPosY == posY &&
                appliedPosZ == posZ &&
                appliedRotX == rotX &&
                appliedRotY == rotY &&
                appliedRotZ == rotZ &&
                appliedScale == scale)
            {
                return;
            }

            nativeRenderer?.SetLocation(posX, posY, posZ);
            nativeRenderer?.SetRotation(rotX, rotY, rotZ);
            nativeRenderer?.SetScale(scale);
            hasAppliedTransform = true;
            appliedPosX = posX;
            appliedPosY = posY;
            appliedPosZ = posZ;
            appliedRotX = rotX;
            appliedRotY = rotY;
            appliedRotZ = rotZ;
            appliedScale = scale;
        }

        private void AdvanceRenderer(float delta)
        {
            if (nativeRenderer == null)
                return;

            if (delta <= 0)
                return;

            int wholeSteps = (int)MathF.Floor(delta);
            for (int i = 0; i < wholeSteps; i++)
            {
                nativeRenderer.Update(1.0f);
                ComputeGpuParticles();
            }

            float remainder = delta - wholeSteps;
            if (remainder > 0)
            {
                nativeRenderer.Update(remainder);
                ComputeGpuParticles();
            }
        }

        private void ComputeGpuParticles()
        {
            if (nativeRenderer == null)
                return;

            lock (RenderLock)
            {
                nativeRenderer.Compute();
            }
        }

        private void ReplayRendererAt(double targetFrame)
        {
            if (nativeRenderer == null)
                return;

            var replayFrames = Math.Min(targetFrame, MaxSimulationAdvanceFrames);
            var replayStartFrame = targetFrame - replayFrames;

            // Match VTuberKit's random-access model: evaluate the absolute
            // timeline position in one coarse update, then replay only a small
            // trailing window with fixed steps so stateful motion settles
            // without simulating the whole item from frame zero.
            if (replayStartFrame > 0)
            {
                nativeRenderer.Update((float)replayStartFrame);
                ComputeGpuParticles();
            }

            AdvanceRenderer((float)replayFrames);
        }

        private PlaybackAccessKind ClassifyPlaybackAccess(long currentItemFrame, double targetFrame)
        {
            if (!hasPreviousItemFrame)
                return PlaybackAccessKind.Initial;

            var delta = targetFrame - renderedFrame;
            // Sequential item frames are continuous playback even when a low output FPS
            // advances Effekseer by more than the bounded random-access replay window.
            if ((currentItemFrame == previousItemFrame || currentItemFrame == previousItemFrame + 1) &&
                delta >= 0)
            {
                return PlaybackAccessKind.Continuous;
            }

            return PlaybackAccessKind.Random;
        }

        private void InitializePlayback(double targetFrame)
        {
            ReplayRendererAt(targetFrame);
            renderedFrame = targetFrame;
        }

        private void RestartPlaybackAt(double targetFrame)
        {
            nativeRenderer?.Reset();
            ReplayRendererAt(targetFrame);
            renderedFrame = targetFrame;
        }

        private void ResetPlaybackTracking()
        {
            hasPreviousItemFrame = false;
            previousItemFrame = 0;
            renderedFrame = 0;
        }

        private void CreateResources(int _width, int _height)
        {
            DisposeResources();

            var texDesc = new Texture2DDescription
            {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            renderTargetTexture = d3dDevice.CreateTexture2D(texDesc);
            renderTargetView = d3dDevice.CreateRenderTargetView(renderTargetTexture);

            var depthDesc = new Texture2DDescription
            {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.D24_UNorm_S8_UInt,
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.DepthStencil,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };
            depthStencilTexture = d3dDevice.CreateTexture2D(depthDesc);
            depthStencilView = d3dDevice.CreateDepthStencilView(depthStencilTexture);

            using var surface = renderTargetTexture.QueryInterface<IDXGISurface>();
            bitmap = _devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1
            {
                PixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                BitmapOptions = BitmapOptions.Target,
                DpiX = 96.0f,
                DpiY = 96.0f
            });

            transformEffect.SetInput(0, bitmap, true);
            transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(-_width / 2f, -_height / 2f);
        }

        private void DisposeResources()
        {
            transformEffect?.SetInput(0, null, true);

            bitmap?.Dispose();
            bitmap = null;

            renderTargetView?.Dispose();
            renderTargetView = null;

            renderTargetTexture?.Dispose();
            renderTargetTexture = null;

            depthStencilView?.Dispose();
            depthStencilView = null;

            depthStencilTexture?.Dispose();
            depthStencilTexture = null;
        }

        private void Resize()
        {
            lock (RenderLock)
            {
                CreateResources(lastWidth, lastHeight);
            }
        }

        public void Dispose()
        {
            PluginLog.Information("Video effect processor disposing");
            DisposeResources();

            compositeEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(1, null, true);

            Output?.Dispose();
            transformEffect?.Dispose();
            compositeEffect?.Dispose();
            nativeRenderer?.Dispose();
            nativeRenderer = null;


            GC.SuppressFinalize(this);
        }
    }
}
