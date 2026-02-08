using System.Numerics;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;
using YukkuriMovieMaker.Commons;
using YukkuriMovieMaker.Player.Video;
using YukkuriMovieMaker.Plugin.Effects;

namespace EffekseerForYMM4
{
    partial class EffekseerVideoEffectProcessor : IVideoEffectProcessor
    {
        bool isFirst = true;
        readonly EffekseerVideoEffect item;

        public ID2D1Image Output { get; private set; }

        ID2D1Bitmap1 bitmap;
        Vortice.Direct2D1.Effects.Composite compositeEffect;
        Vortice.Direct2D1.Effects.AffineTransform2D transformEffect;

        private EffekseerForNative.EffekseerRenderer nativeRenderer;

        private ID3D11Device d3dDevice;
        private ID3D11Texture2D renderTargetTexture;
        private ID3D11RenderTargetView renderTargetView;
        private ID3D11Texture2D depthStencilTexture;
        private ID3D11DepthStencilView depthStencilView;
        private IGraphicsDevicesAndContext _devices;

        private TimeSpan _duration = TimeSpan.Zero;
        public TimeSpan Duration => _duration;
        private double currentFrame = 0;

        private int lastWidth = 0;
        private int lastHeight = 0;

        private string? loadedFilePath = null;
        private ID2D1Image? inputImage;

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
                isFirst = false;

                // Initialize Native Renderer
                nativeRenderer = new EffekseerForNative.EffekseerRenderer();
                if (!nativeRenderer.Initialize(d3dDevice.NativePointer, d3dDevice.ImmediateContext.NativePointer, width, height))
                {
                    throw new Exception("Failed to initialize Effekseer Renderer");
                }

                CreateResources(width, height);
            }

            if (loadedFilePath != item.FilePath)
            {
                if (!string.IsNullOrEmpty(item.FilePath))
                {
                    var ext = System.IO.Path.GetExtension(item.FilePath).ToLower();
                    if ((ext == ".efk" || ext == ".efkefc") && nativeRenderer.LoadEffect(item.FilePath))
                    {
                        loadedFilePath = item.FilePath;

                        int tFrames = nativeRenderer.GetTotalFrame();
                        // Calculate duration from total frames and FPS.
                        // If totalFrames is valid (non-zero and not infinite), use it.
                        if (tFrames > 0 && tFrames < int.MaxValue)
                        {
                            _duration = TimeSpan.FromSeconds(tFrames / fps);
                        }
                    }
                }
                else
                {
                    loadedFilePath = item.FilePath;
                }
            }


            int totalFrames = nativeRenderer.GetTotalFrame();
            // ItemPosition.Frameからエフェクトの再生位置を計算（巻き戻し対応）
            double targetFrame = frame;

            if (item.IsLoop && totalFrames > 0 && totalFrames < int.MaxValue)
            {
                targetFrame %= totalFrames;
            }

            lock (_renderLock)
            {
                // 巻き戻し検出：targetFrameがcurrentFrameより小さい場合はReset
                if (targetFrame < currentFrame)
                {
                    nativeRenderer.Reset();
                    currentFrame = 0;
                }

                // 差分だけUpdate
                float delta = (float)(targetFrame - currentFrame);
                if (delta > 0)
                {
                    nativeRenderer.Update(delta);
                    currentFrame = targetFrame;
                }

                double animFrame = targetFrame;

                float camX = (float)item.CamPosX.GetValue((long)animFrame, length, (int)fps);
                float camY = (float)item.CamPosY.GetValue((long)animFrame, length, (int)fps);
                float camZ = (float)item.CamPosZ.GetValue((long)animFrame, length, (int)fps);

                nativeRenderer.SetCameraLookAt(
                    camX, camY, camZ,
                    camX, camY, 0,
                    0, 1, 0
                );

                // Update Projection
                float fov = (float)item.Fov.GetValue((long)animFrame, length, (int)fps);

                nativeRenderer.SetProjectionPerspective(fov, width, height, 1.0f, 2000.0f);

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