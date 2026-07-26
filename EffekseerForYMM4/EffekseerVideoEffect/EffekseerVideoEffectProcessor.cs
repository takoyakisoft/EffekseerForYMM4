using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using EffekseerForYMM4.Commons;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{
    partial class EffekseerVideoEffectProcessor : IVideoEffectProcessor
    {
        private const double EffekseerFps = 60.0;
        bool isFirst = true;
        readonly EffekseerVideoEffect item;

        public ID2D1Image Output { get; private set; }

        ID2D1Bitmap1? bitmap;
        Vortice.Direct2D1.Effects.Composite compositeEffect;
        Vortice.Direct2D1.Effects.AffineTransform2D transformEffect;

        private EffekseerForNative.EffekseerRenderer? nativeRenderer;

        private ID3D11Device d3dDevice;
        private ID3D11Texture2D? renderTargetTexture;
        private ID3D11RenderTargetView? renderTargetView;
        private ID3D11Texture2D? depthStencilTexture;
        private ID3D11DepthStencilView? depthStencilView;
        private IGraphicsDevicesAndContext _devices;

        private TimeSpan _duration = TimeSpan.Zero;
        public TimeSpan Duration => _duration;
        private int lastWidth = 0;
        private int lastHeight = 0;

        private string? loadedFilePath = null;
        private bool hasLoadedEffect;
        private double renderedFrame;
        private ID2D1Image? inputImage;
        private readonly EffekseerLoadErrorNotifier loadErrorNotifier = new();
        private static readonly object RenderLock = new();

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
        }

        /// <summary>
        /// エフェクトに入力する映像を設定する
        /// </summary>
        /// <param name="input"></param>
        public void SetInput(ID2D1Image? input)
        {
            inputImage = input;
            compositeEffect.SetInput(0, input, true);
        }

        /// <summary>
        /// エフェクトに入力する映像をクリアする
        /// </summary>
        public void ClearInput()
        {
            inputImage = null;
            compositeEffect.SetInput(0, null, true);
        }

        /// <summary>
        /// エフェクトを更新する
        /// </summary>
        /// <param name="effectDescription">エフェクトの描画に必要な各種設定項目</param>
        /// <returns>描画関連の設定項目</returns>
        public DrawDescription Update(EffectDescription effectDescription)
        {
            if (inputImage == null)
                return effectDescription.DrawDescription;

            int width = 0;
            int height = 0;
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
                if (!nativeRenderer.Initialize(d3dDevice.NativePointer, d3dDevice.ImmediateContext.NativePointer, width, height))
                {
                    nativeRenderer.Dispose();
                    nativeRenderer = null;
                    return effectDescription.DrawDescription;
                }

                isFirst = false;
                CreateResources(width, height);
            }

            if (loadedFilePath != item.FilePath)
            {
                if (nativeRenderer == null)
                {
                    return effectDescription.DrawDescription;
                }

                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    var ext = System.IO.Path.GetExtension(item.FilePath).ToLowerInvariant();
                    if (ext != ".efk" && ext != ".efkefc")
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = false;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, string.Format(Translate.Error_InvalidEffectExtension, ".efk, .efkefc"));
                    }
                    else if (!System.IO.File.Exists(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = false;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, Translate.Error_EffectFileNotFound);
                    }
                    else if (nativeRenderer.LoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        hasLoadedEffect = true;
                        renderedFrame = 0;
                        loadErrorNotifier.Reset();
                        int tFrames = nativeRenderer.GetTotalFrame();
                        if (tFrames > 0 && tFrames < int.MaxValue)
                        {
                            _duration = TimeSpan.FromSeconds((double)tFrames / EffekseerFps);
                        }
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

            int totalFrames = nativeRenderer.GetTotalFrame();
            double targetFrame = Math.Max(0, effectDescription.ItemPosition.Time.TotalSeconds * EffekseerFps);

            if (item.IsLoop && totalFrames > 0 && totalFrames < int.MaxValue)
            {
                targetFrame %= totalFrames;
                if (targetFrame < 0)
                    targetFrame += totalFrames;
            }

            lock (RenderLock)
            {
                double animFrame = frame;
                float camX = (float)item.CamPosX.GetValue((long)animFrame, length, safeFps);
                float camY = (float)item.CamPosY.GetValue((long)animFrame, length, safeFps);
                float camZ = (float)item.CamPosZ.GetValue((long)animFrame, length, safeFps);
                float posX = (float)item.PosX.GetValue((long)animFrame, length, safeFps);
                float posY = (float)item.PosY.GetValue((long)animFrame, length, safeFps);
                float posZ = (float)item.PosZ.GetValue((long)animFrame, length, safeFps);
                float rotX = (float)item.RotX.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
                float rotY = (float)item.RotY.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
                float rotZ = (float)item.RotZ.GetValue((long)animFrame, length, safeFps) * MathF.PI / 180f;
                float scalePercent = (float)item.Scale.GetValue((long)animFrame, length, safeFps);
                float scale = scalePercent <= 0f ? 0f : Math.Max(scalePercent / 100.0f, 0.0001f);

                nativeRenderer.SetCameraLookAt(
                    camX, camY, camZ,
                    camX, camY, 0,
                    0, 1, 0);

                float fov = (float)item.Fov.GetValue((long)animFrame, length, safeFps);
                if (item.ProjectionMode == ProjectionMode.Orthographic)
                {
                    var orthographicHeight = Math.Max(
                        0.001f,
                        (float)item.OrthographicSize.GetValue((long)animFrame, length, safeFps));
                    var orthographicWidth = orthographicHeight * width / Math.Max(1.0f, height);
                    nativeRenderer.SetProjectionOrthographic(orthographicWidth, orthographicHeight, 1.0f, 2000.0f);
                }
                else
                {
                    nativeRenderer.SetProjectionPerspective(fov, width, height, 1.0f, 2000.0f);
                }
                nativeRenderer.SetLocation(posX, posY, posZ);
                nativeRenderer.SetRotation(rotX, rotY, rotZ);
                nativeRenderer.SetScale(scale);

                if (targetFrame < renderedFrame)
                {
                    ReplayRendererToTargetFrame(targetFrame);
                }
                else
                {
                    AdvanceRenderer((float)(targetFrame - renderedFrame));
                    renderedFrame = targetFrame;
                }

                transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(-width / 2f, -height / 2f);
                if (renderTargetView != null && depthStencilView != null)
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
            }

            float remainder = delta - wholeSteps;
            if (remainder > 0)
            {
                nativeRenderer.Update(remainder);
            }
        }

        private void ReplayRendererToTargetFrame(double targetFrame)
        {
            if (nativeRenderer == null)
                return;

            nativeRenderer.Reset();
            renderedFrame = 0;

            if (targetFrame <= 0)
                return;

            AdvanceRenderer((float)targetFrame);
            renderedFrame = targetFrame;
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
            DisposeResources();

            compositeEffect?.SetInput(0, null, true);
            compositeEffect?.SetInput(1, null, true);

            Output?.Dispose();
            transformEffect?.Dispose();
            compositeEffect?.Dispose();

            if (nativeRenderer != null)
            {
                nativeRenderer.Dispose();
                nativeRenderer = null;
            }


            GC.SuppressFinalize(this);
        }
    }
}
