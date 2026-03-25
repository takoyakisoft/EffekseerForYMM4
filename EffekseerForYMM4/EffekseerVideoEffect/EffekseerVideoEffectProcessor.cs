using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using EffekseerForYMM4.Commons;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{
    partial class EffekseerVideoEffectProcessor : IVideoEffectProcessor
    {
        private const double EffekseerFps = 60.0;
        private const int MaxReplaySteps = 600;
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
        private ID2D1Image? inputImage;
        private readonly EffekseerLoadErrorNotifier loadErrorNotifier = new();

        private static object _renderLock = new object();

        public EffekseerVideoEffectProcessor(IGraphicsDevicesAndContext devices, EffekseerVideoEffect item)
        {
            this.item = item;

            _devices = devices;
            // Get D3D11 Device from IGraphicsDevicesAndContext
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
                // Initialize Native Renderer
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
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, string.Format(Translate.Error_InvalidEffectExtension, ".efk, .efkefc"));
                    }
                    else if (!System.IO.File.Exists(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, Translate.Error_EffectFileNotFound);
                    }
                    else if (nativeRenderer.LoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.Reset();
                        int tFrames = nativeRenderer.GetTotalFrame();
                        if (tFrames > 0 && tFrames < int.MaxValue)
                        {
                            _duration = TimeSpan.FromSeconds(tFrames / safeFps);
                        }
                    }
                    else
                    {
                        loadedFilePath = item.FilePath;
                        loadErrorNotifier.ShowIfNeeded(item.FilePath, nativeRenderer.LastErrorMessage ?? Translate.Error_EffectFilesMayBeInvalid);
                    }
                }
                else
                {
                    loadedFilePath = item.FilePath;
                    loadErrorNotifier.Reset();
                }
            }

            if (nativeRenderer == null)
            {
                return effectDescription.DrawDescription;
            }

            int totalFrames = nativeRenderer.GetTotalFrame();
            // 差分更新ではなく絶対時刻から毎回再構築する。
            // プレビューとサムネイルで Update の呼ばれ方が違っても同じ見た目に揃える。
            double targetFrame = Math.Max(0, effectDescription.ItemPosition.Time.TotalSeconds * EffekseerFps);

            if (item.IsLoop && totalFrames > 0 && totalFrames < int.MaxValue)
            {
                targetFrame %= totalFrames;
                if (targetFrame < 0)
                    targetFrame += totalFrames;
            }

            lock (_renderLock)
            {
                if (nativeRenderer == null)
                {
                    return effectDescription.DrawDescription;
                }

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
                float scale = scalePercent / 100.0f;
                scale = Math.Max(scale, 0.0001f);

                nativeRenderer.SetCameraLookAt(
                    camX, camY, camZ,
                    camX, camY, 0,
                    0, 1, 0
                );

                // Update Projection
                float fov = (float)item.Fov.GetValue((long)animFrame, length, safeFps);

                nativeRenderer.SetProjectionPerspective(fov, width, height, 1.0f, 2000.0f);
                nativeRenderer.SetLocation(posX, posY, posZ);
                nativeRenderer.SetRotation(rotX, rotY, rotZ);
                nativeRenderer.SetScale(scale);

                ReplayRendererToTargetFrame(targetFrame);

                // Render
                var d3dContext = d3dDevice.ImmediateContext;
                if (d3dContext == null || renderTargetView == null || depthStencilView == null)
                    return effectDescription.DrawDescription;

                if (item.IsScreenSize)
                {
                    // Center Effekseer's screen center on YMM4's screen center
                    // YMM4 item local center is (0,0). 
                    // To align Effekseer screen (0,0) to YMM4 project (0,0):
                    // We need to offset by the item's position on screen.
                    transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(-width / 2f, -height / 2f);
                }
                else
                {
                    // Center on item
                    transformEffect.TransformMatrix = Matrix3x2.CreateTranslation(-width / 2f, -height / 2f);
                }

                if (!string.IsNullOrEmpty(loadedFilePath))
                {
                    // Clear
                    d3dContext.ClearRenderTargetView(renderTargetView, new Color4(0, 0, 0, 0));
                    d3dContext.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);

                    // Save current targets
                    var oldTargets = new ID3D11RenderTargetView[1];
                    d3dContext.OMGetRenderTargets(1, oldTargets, out var oldDepth);

                    d3dContext.OMSetRenderTargets(renderTargetView, depthStencilView);
                    d3dContext.RSSetViewport(0, 0, width, height);

                    nativeRenderer.Render();

                    // Restore targets
                    d3dContext.OMSetRenderTargets(oldTargets, oldDepth);

                    // Release array refs
                    if (oldTargets != null)
                    {
                        foreach (var t in oldTargets) t?.Dispose();
                    }
                    oldDepth?.Dispose();
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

            // 厳密再生: 1フレーム刻みで積み上げ、端数のみ最後に加算する
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
            nativeRenderer.Update(0);

            if (targetFrame <= 0)
                return;

            var replayStep = Math.Max(1.0, targetFrame / MaxReplaySteps);
            var replayed = 0.0;
            while (replayed < targetFrame)
            {
                var next = Math.Min(targetFrame, replayed + replayStep);
                AdvanceRenderer((float)(next - replayed));
                replayed = next;
            }
        }

        private void CreateResources(int _width, int _height)
        {
            // Dispose old resources
            DisposeResources();

            // Create Render Target (D3D11 Texture)
            var texDesc = new Texture2DDescription
            {
                Width = _width,
                Height = _height,
                MipLevels = 1,
                ArraySize = 1,
                Format = Format.B8G8R8A8_UNorm, // Compatible with D2D
                SampleDescription = new SampleDescription(1, 0),
                Usage = ResourceUsage.Default,
                BindFlags = BindFlags.RenderTarget | BindFlags.ShaderResource,
                CPUAccessFlags = CpuAccessFlags.None,
                MiscFlags = ResourceOptionFlags.None
            };

            renderTargetTexture = d3dDevice.CreateTexture2D(texDesc);
            renderTargetView = d3dDevice.CreateRenderTargetView(renderTargetTexture);

            // Create Depth Texture
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

            // Create D2D Bitmap from DXGI Surface
            using var surface = renderTargetTexture.QueryInterface<IDXGISurface>();
            bitmap = _devices.DeviceContext.CreateBitmapFromDxgiSurface(surface, new BitmapProperties1
            {
                PixelFormat = new PixelFormat(Format.B8G8R8A8_UNorm, Vortice.DCommon.AlphaMode.Premultiplied),
                BitmapOptions = BitmapOptions.Target,
                DpiX = 96.0f,
                DpiY = 96.0f
            });

            transformEffect.SetInput(0, bitmap, true);

            // Update Native Projection
            nativeRenderer?.SetProjection(_width, _height);
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
            lock (_renderLock)
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

            nativeRenderer?.Destroy();
            nativeRenderer = null;


            GC.SuppressFinalize(this);
        }
    }
}
