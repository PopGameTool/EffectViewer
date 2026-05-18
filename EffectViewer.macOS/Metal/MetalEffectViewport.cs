using System;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Rendering.Composition;
using Avalonia.Styling;
using Avalonia.Threading;
using EffectViewer.Controls;
using EffectViewer.Rendering;
using EffectViewer.Rendering.TextureUpload;
using Metal;
using IOSurfaceObject = IOSurface.IOSurface;
using IOSurfaceOptions = IOSurface.IOSurfaceOptions;

namespace EffectViewer.macOS.Metal
{
    internal sealed class MetalEffectViewport : Control, IEffectViewport
    {
        public static readonly StyledProperty<RenderFrame> FrameProperty =
            AvaloniaProperty.Register<MetalEffectViewport, RenderFrame>(nameof(Frame), new RenderFrame());

        public static readonly StyledProperty<ITextureSource?> TextureSourceProperty =
            AvaloniaProperty.Register<MetalEffectViewport, ITextureSource?>(nameof(TextureSource));

        public static readonly StyledProperty<IRenderFrameProvider?> FrameProviderProperty =
            AvaloniaProperty.Register<MetalEffectViewport, IRenderFrameProvider?>(nameof(FrameProvider));

        public static readonly StyledProperty<ViewportBackgroundMode> BackgroundModeProperty =
            AvaloniaProperty.Register<MetalEffectViewport, ViewportBackgroundMode>(nameof(BackgroundMode));

        private const int SurfaceBufferCount = 4;
        private const int MinimumIOSurfaceBytesPerRowAlignment = 256;
        private const double CheckerboardCellSize = 12d;
        private const uint IOSurfacePixelFormatBgra = ((uint)'B' << 24) | ((uint)'G' << 16) | ((uint)'R' << 8) | 'A';
        private static readonly Vector4 DarkClearColor = new(0.08f, 0.09f, 0.1f, 1f);
        private static readonly Vector4 LightCheckerboardBaseColor = new(0.965f, 0.973f, 0.984f, 1f);
        private static readonly Vector4 LightCheckerboardAlternateColor = new(0.84f, 0.86f, 0.89f, 1f);

        private readonly GeneratedTextureSource _fallbackTextureSource = new();
        private readonly MetalFrameBuffer?[] _frameBuffers = new MetalFrameBuffer?[SurfaceBufferCount];
        private IMTLDevice? _device;
        private MetalRenderer? _renderer;
        private ICompositionGpuInterop? _gpuInterop;
        private CompositionDrawingSurface? _drawingSurface;
        private CompositionSurfaceVisual? _surfaceVisual;
        private CompositionSyncMode _syncMode;
        private DateTime _lastRenderUtc = DateTime.UtcNow;
        private PixelSize _bufferPixelSize;
        private Vector2 _panPixels;
        private float _zoom = 1f;
        private int _frameBufferIndex = -1;
        private int _compositionGeneration;
        private bool _isAttached;
        private bool _isAnimationFrameQueued;
        private bool _isInitializingComposition;
        private bool _isCompositionAvailable;
        private bool _isDisposed;

        public MetalEffectViewport()
        {
            ClipToBounds = true;
        }

        public static bool IsSupported => MTLDevice.SystemDefault is not null;

        public RenderFrame Frame
        {
            get => GetValue(FrameProperty);
            set => SetValue(FrameProperty, value);
        }

        public ITextureSource? TextureSource
        {
            get => GetValue(TextureSourceProperty);
            set => SetValue(TextureSourceProperty, value);
        }

        public IRenderFrameProvider? FrameProvider
        {
            get => GetValue(FrameProviderProperty);
            set => SetValue(FrameProviderProperty, value);
        }

        public ViewportBackgroundMode BackgroundMode
        {
            get => GetValue(BackgroundModeProperty);
            set => SetValue(BackgroundModeProperty, value);
        }

        public void SetViewTransform(float zoom, Vector2 panPixels)
        {
            _zoom = Math.Clamp(zoom, 0.05f, 32f);
            _panPixels = panPixels;
            RenderAndPublishFrame();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _isAttached = true;
            _isDisposed = false;
            _lastRenderUtc = DateTime.UtcNow;
            EnsureCompositionResourcesAsync();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _isAttached = false;
            ElementComposition.SetElementChildVisual(this, null);
            DisposeCompositionResources();
            base.OnDetachedFromVisualTree(e);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Size arranged = base.ArrangeOverride(finalSize);
            UpdateSurfaceVisualSize(arranged);
            RenderAndPublishFrame();
            return arranged;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextureSourceProperty)
            {
                if (_renderer is not null)
                {
                    _renderer.TextureSource = TextureSource ?? _fallbackTextureSource;
                }

                RenderAndPublishFrame();
            }
            else if (change.Property == FrameProperty ||
                     change.Property == FrameProviderProperty ||
                     change.Property == BackgroundModeProperty ||
                     string.Equals(change.Property.Name, nameof(ActualThemeVariant), StringComparison.Ordinal))
            {
                RenderAndPublishFrame();
            }
        }

        private async void EnsureCompositionResourcesAsync()
        {
            if (_isInitializingComposition || _isCompositionAvailable || !_isAttached)
            {
                return;
            }

            _isInitializingComposition = true;
            try
            {
                CompositionVisual? elementVisual = ElementComposition.GetElementVisual(this);
                if (elementVisual is null)
                {
                    Dispatcher.UIThread.Post(EnsureCompositionResourcesAsync, DispatcherPriority.Render);
                    return;
                }

                ICompositionGpuInterop? gpuInterop = await elementVisual.Compositor.TryGetCompositionGpuInterop();
                if (!_isAttached || _isDisposed || gpuInterop is null ||
                    !SupportsHandleType(gpuInterop.SupportedImageHandleTypes, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef))
                {
                    return;
                }

                CompositionSyncMode syncMode = GetCompositionSyncMode(gpuInterop);
                if (syncMode == CompositionSyncMode.None)
                {
                    Debug.WriteLine("Metal viewport unavailable: Avalonia composition GPU interop does not support IOSurface synchronization.");
                    return;
                }

                IMTLDevice? device = MTLDevice.SystemDefault;
                if (device is null)
                {
                    return;
                }

                _device = device;
                _gpuInterop = gpuInterop;
                _syncMode = syncMode;
                _renderer = new MetalRenderer(device)
                {
                    TextureSource = TextureSource ?? _fallbackTextureSource
                };

                _drawingSurface = elementVisual.Compositor.CreateDrawingSurface();
                _surfaceVisual = elementVisual.Compositor.CreateSurfaceVisual();
                _surfaceVisual.Surface = _drawingSurface;
                _surfaceVisual.Visible = true;
                _surfaceVisual.ClipToBounds = true;
                UpdateSurfaceVisualSize(Bounds.Size);
                ElementComposition.SetElementChildVisual(this, _surfaceVisual);
                InvalidateVisual();

                _isCompositionAvailable = true;
                RenderAndPublishFrame();
                RequestNextAnimationFrame();
            }
            finally
            {
                _isInitializingComposition = false;
            }
        }

        private void RequestNextAnimationFrame()
        {
            if (!_isAttached || !_isCompositionAvailable || _isAnimationFrameQueued)
            {
                return;
            }

            TopLevel? topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null)
            {
                Dispatcher.UIThread.Post(RequestNextAnimationFrame, DispatcherPriority.Render);
                return;
            }

            _isAnimationFrameQueued = true;
            topLevel.RequestAnimationFrame(_ =>
            {
                _isAnimationFrameQueued = false;
                if (!_isAttached || !_isCompositionAvailable || _isDisposed)
                {
                    return;
                }

                RenderAndPublishFrame();
                RequestNextAnimationFrame();
            });
        }

        private void RenderAndPublishFrame()
        {
            if (!_isAttached || !_isCompositionAvailable || _renderer is null || _drawingSurface is null || _gpuInterop is null || _device is null)
            {
                return;
            }

            PixelSize pixelSize = GetRenderPixelSize();
            if (pixelSize.Width <= 0 || pixelSize.Height <= 0)
            {
                return;
            }

            MetalFrameBuffer? frameBuffer = AcquireFrameBuffer(pixelSize);
            if (frameBuffer is null)
            {
                return;
            }

            DateTime now = DateTime.UtcNow;
            double deltaSeconds = Math.Clamp((now - _lastRenderUtc).TotalSeconds, 0, 0.1);
            _lastRenderUtc = now;

            RenderFrame frame = FrameProvider?.GetFrame(deltaSeconds) ?? Frame ?? new RenderFrame();
            _renderer.ViewZoom = _zoom;
            _renderer.ViewPan = _panPixels;
            frameBuffer.IsBusy = true;
            int generation = _compositionGeneration;
            FrameSynchronization synchronization = frameBuffer.CreateFrameSynchronization();

            bool submitted;
            try
            {
                submitted = _renderer.Render(
                    frameBuffer.Texture,
                    pixelSize.Width,
                    pixelSize.Height,
                    frame,
                    GetBackgroundClearColor(),
                    GetCheckerboardColor(),
                    GetCheckerboardCellSize(),
                    commandBuffer => commandBuffer.AddCompletedHandler(_ =>
                    {
                        frameBuffer.SignalReady(synchronization);
                        if (!synchronization.UsesTimelineSemaphore)
                        {
                            BeginDrawingSurfaceUpdate(frameBuffer, generation, synchronization);
                        }
                    }));
            }
            catch
            {
                frameBuffer.IsBusy = false;
                throw;
            }

            if (!submitted)
            {
                frameBuffer.IsBusy = false;
                return;
            }

            if (synchronization.UsesTimelineSemaphore)
            {
                BeginDrawingSurfaceUpdate(frameBuffer, generation, synchronization);
            }
        }

        private void BeginDrawingSurfaceUpdate(MetalFrameBuffer frameBuffer, int generation, FrameSynchronization synchronization)
        {
            Dispatcher.UIThread.Post(async () =>
            {
                try
                {
                    if (_isAttached &&
                        !_isDisposed &&
                        generation == _compositionGeneration &&
                        !frameBuffer.IsDisposed &&
                        _drawingSurface is not null)
                    {
                        await frameBuffer.UpdateDrawingSurfaceAsync(_drawingSurface, synchronization);
                        InvalidateVisual();
                    }
                    else
                    {
                        frameBuffer.SignalConsumed(synchronization);
                    }
                }
                catch (Exception ex)
                {
                    // Keep the render loop alive if the compositor rejects a stale surface during resize/detach.
                    frameBuffer.SignalConsumed(synchronization);
                    Debug.WriteLine($"Metal viewport surface update failed: {ex}");
                }
                finally
                {
                    frameBuffer.IsBusy = false;
                }
            }, DispatcherPriority.Render);
        }

        private MetalFrameBuffer? AcquireFrameBuffer(PixelSize pixelSize)
        {
            if (_bufferPixelSize != pixelSize)
            {
                if (HasBusyFrameBuffer())
                {
                    return null;
                }

                DisposeFrameBuffers();
                _bufferPixelSize = pixelSize;
            }

            EnsureFrameBuffers(pixelSize);
            for (int i = 0; i < _frameBuffers.Length; i++)
            {
                int index = (_frameBufferIndex + 1 + i) % _frameBuffers.Length;
                MetalFrameBuffer? frameBuffer = _frameBuffers[index];
                if (frameBuffer is not null && frameBuffer.IsAvailable)
                {
                    _frameBufferIndex = index;
                    return frameBuffer;
                }
            }

            return null;
        }

        private void EnsureFrameBuffers(PixelSize pixelSize)
        {
            if (_device is null || _gpuInterop is null)
            {
                return;
            }

            for (int i = 0; i < _frameBuffers.Length; i++)
            {
                _frameBuffers[i] ??= MetalFrameBuffer.Create(_device, _gpuInterop, _syncMode, pixelSize.Width, pixelSize.Height);
            }
        }

        private bool HasBusyFrameBuffer()
        {
            for (int i = 0; i < _frameBuffers.Length; i++)
            {
                if (_frameBuffers[i]?.IsBusy == true)
                {
                    return true;
                }

                if (_frameBuffers[i]?.IsWaitingForComposition == true)
                {
                    return true;
                }
            }

            return false;
        }

        private void DisposeCompositionResources()
        {
            _isDisposed = true;
            _isCompositionAvailable = false;
            _isAnimationFrameQueued = false;
            _compositionGeneration++;
            DisposeFrameBuffers();
            _bufferPixelSize = default;
            _drawingSurface?.Dispose();
            _drawingSurface = null;
            _surfaceVisual = null;
            _syncMode = CompositionSyncMode.None;
            _renderer?.Dispose();
            _renderer = null;
            _gpuInterop = null;
            _device = null;
        }

        private void DisposeFrameBuffers()
        {
            for (int i = 0; i < _frameBuffers.Length; i++)
            {
                _frameBuffers[i]?.Dispose();
                _frameBuffers[i] = null;
            }

            _frameBufferIndex = -1;
            _compositionGeneration++;
        }

        private PixelSize GetRenderPixelSize()
        {
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            return PixelSize.FromSize(Bounds.Size, scaling);
        }

        private void UpdateSurfaceVisualSize(Size size)
        {
            if (_surfaceVisual is not null)
            {
                _surfaceVisual.Size = new Avalonia.Vector(size.Width, size.Height);
            }
        }

        private Vector4 GetBackgroundClearColor()
        {
            return ShouldUseLightBackground() ? LightCheckerboardBaseColor : DarkClearColor;
        }

        private Vector4? GetCheckerboardColor()
        {
            return ShouldUseLightBackground() ? LightCheckerboardAlternateColor : null;
        }

        private bool ShouldUseLightBackground()
        {
            if (BackgroundMode == ViewportBackgroundMode.Light)
            {
                return true;
            }

            if (BackgroundMode == ViewportBackgroundMode.Dark)
            {
                return false;
            }

            return ActualThemeVariant == ThemeVariant.Light;
        }

        private int GetCheckerboardCellSize()
        {
            double scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1d;
            return Math.Max(4, (int)Math.Round(CheckerboardCellSize * scaling));
        }

        private static bool SupportsHandleType(System.Collections.Generic.IReadOnlyList<string> handleTypes, string handleType)
        {
            for (int i = 0; i < handleTypes.Count; i++)
            {
                if (string.Equals(handleTypes[i], handleType, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static CompositionSyncMode GetCompositionSyncMode(ICompositionGpuInterop gpuInterop)
        {
            CompositionGpuImportedImageSynchronizationCapabilities capabilities;
            try
            {
                capabilities = gpuInterop.GetSynchronizationCapabilities(KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Metal viewport unable to query composition synchronization capabilities: {ex}");
                return CompositionSyncMode.None;
            }

            if ((capabilities & CompositionGpuImportedImageSynchronizationCapabilities.TimelineSemaphores) != 0 &&
                SupportsHandleType(gpuInterop.SupportedSemaphoreTypes, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent))
            {
                return CompositionSyncMode.TimelineSemaphore;
            }

            if ((capabilities & CompositionGpuImportedImageSynchronizationCapabilities.Automatic) != 0)
            {
                return CompositionSyncMode.Automatic;
            }

            return CompositionSyncMode.None;
        }

        private enum CompositionSyncMode
        {
            None,
            Automatic,
            TimelineSemaphore
        }

        private readonly record struct FrameSynchronization(ulong ReadyValue, ulong ConsumedValue)
        {
            public bool UsesTimelineSemaphore => ReadyValue != 0 && ConsumedValue != 0;
        }

        private sealed class MetalFrameBuffer : IDisposable
        {
            private MetalFrameBuffer(
                IOSurfaceObject surface,
                IMTLTexture texture,
                ICompositionImportedGpuImage importedImage,
                IMTLSharedEvent? readyEvent,
                ICompositionImportedGpuSemaphore? readySemaphore,
                IMTLSharedEvent? consumedEvent,
                ICompositionImportedGpuSemaphore? consumedSemaphore)
            {
                Surface = surface;
                Texture = texture;
                ImportedImage = importedImage;
                ReadyEvent = readyEvent;
                ReadySemaphore = readySemaphore;
                ConsumedEvent = consumedEvent;
                ConsumedSemaphore = consumedSemaphore;
            }

            public IOSurfaceObject Surface { get; }
            public IMTLTexture Texture { get; }
            public ICompositionImportedGpuImage ImportedImage { get; }
            public IMTLSharedEvent? ReadyEvent { get; }
            public ICompositionImportedGpuSemaphore? ReadySemaphore { get; }
            public IMTLSharedEvent? ConsumedEvent { get; }
            public ICompositionImportedGpuSemaphore? ConsumedSemaphore { get; }
            public bool IsBusy { get; set; }
            public bool IsDisposed { get; private set; }
            public bool IsWaitingForComposition =>
                ConsumedEvent is not null &&
                _consumedValue != 0 &&
                ConsumedEvent.SignaledValue < _consumedValue;

            public bool IsAvailable => !IsBusy && !IsWaitingForComposition;

            private ulong _readyValue;
            private ulong _consumedValue;

            public static MetalFrameBuffer Create(
                IMTLDevice device,
                ICompositionGpuInterop gpuInterop,
                CompositionSyncMode syncMode,
                int width,
                int height)
            {
                int bytesPerPixel = 4;
                int bytesPerRow = AlignUp(
                    checked(width * bytesPerPixel),
                    Math.Max((int)device.GetMinimumLinearTextureAlignment(MTLPixelFormat.BGRA8Unorm), MinimumIOSurfaceBytesPerRowAlignment));
                IOSurfaceObject surface = new(new IOSurfaceOptions
                {
                    Width = width,
                    Height = height,
                    BytesPerElement = bytesPerPixel,
                    BytesPerRow = bytesPerRow,
                    PixelFormat = IOSurfacePixelFormatBgra
                });

                using MTLTextureDescriptor descriptor = MTLTextureDescriptor.CreateTexture2DDescriptor(
                    MTLPixelFormat.BGRA8Unorm,
                    (nuint)width,
                    (nuint)height,
                    false);
                descriptor.Usage = MTLTextureUsage.RenderTarget | MTLTextureUsage.ShaderRead;
                descriptor.StorageMode = MTLStorageMode.Shared;

                IMTLTexture? texture = device.CreateTexture(descriptor, surface, 0);
                if (texture is null)
                {
                    surface.Dispose();
                    throw new InvalidOperationException("Unable to create Metal IOSurface texture.");
                }

                ICompositionImportedGpuImage? importedImage = null;
                IMTLSharedEvent? readyEvent = null;
                IMTLSharedEvent? consumedEvent = null;
                ICompositionImportedGpuSemaphore? readySemaphore = null;
                ICompositionImportedGpuSemaphore? consumedSemaphore = null;
                try
                {
                    importedImage = gpuInterop.ImportImage(
                        new PlatformHandle(surface.Handle, KnownPlatformGraphicsExternalImageHandleTypes.IOSurfaceRef),
                        new PlatformGraphicsExternalImageProperties
                        {
                            Width = width,
                            Height = height,
                            Format = PlatformGraphicsExternalImageFormat.B8G8R8A8UNorm,
                            MemorySize = surface.AllocationSize > 0
                                ? (ulong)surface.AllocationSize
                                : checked((ulong)bytesPerRow * (ulong)height),
                            MemoryOffset = 0,
                            TopLeftOrigin = true
                        });

                    if (syncMode == CompositionSyncMode.TimelineSemaphore)
                    {
                        readyEvent = device.CreateSharedEvent() ?? throw new InvalidOperationException("Unable to create Metal ready shared event.");
                        consumedEvent = device.CreateSharedEvent() ?? throw new InvalidOperationException("Unable to create Metal consumed shared event.");
                        readySemaphore = gpuInterop.ImportSemaphore(
                            new PlatformHandle(readyEvent.Handle, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
                        consumedSemaphore = gpuInterop.ImportSemaphore(
                            new PlatformHandle(consumedEvent.Handle, KnownPlatformGraphicsExternalSemaphoreHandleTypes.MetalSharedEvent));
                    }
                }
                catch
                {
                    _ = consumedSemaphore?.DisposeAsync();
                    _ = readySemaphore?.DisposeAsync();
                    _ = importedImage?.DisposeAsync();
                    consumedEvent?.Dispose();
                    readyEvent?.Dispose();
                    texture.Dispose();
                    surface.Dispose();
                    throw;
                }

                if (importedImage is null)
                {
                    texture.Dispose();
                    surface.Dispose();
                    throw new InvalidOperationException("Unable to import Metal IOSurface image.");
                }

                return new MetalFrameBuffer(surface, texture, importedImage, readyEvent, readySemaphore, consumedEvent, consumedSemaphore);
            }

            public FrameSynchronization CreateFrameSynchronization()
            {
                if (ReadyEvent is null || ReadySemaphore is null || ConsumedEvent is null || ConsumedSemaphore is null)
                {
                    return default;
                }

                return new FrameSynchronization(++_readyValue, ++_consumedValue);
            }

            public void SignalReady(FrameSynchronization synchronization)
            {
                if (ReadyEvent is not null && synchronization.ReadyValue != 0)
                {
                    ReadyEvent.SignaledValue = synchronization.ReadyValue;
                }
            }

            public void SignalConsumed(FrameSynchronization synchronization)
            {
                if (ConsumedEvent is not null && synchronization.ConsumedValue != 0)
                {
                    ConsumedEvent.SignaledValue = synchronization.ConsumedValue;
                }
            }

            public async Task UpdateDrawingSurfaceAsync(CompositionDrawingSurface drawingSurface, FrameSynchronization synchronization)
            {
                await ImportedImage.ImportCompleted;
                if (ReadySemaphore is not null && ConsumedSemaphore is not null)
                {
                    await ReadySemaphore.ImportCompleted;
                    await ConsumedSemaphore.ImportCompleted;
                    await drawingSurface.UpdateWithTimelineSemaphoresAsync(
                        ImportedImage,
                        ReadySemaphore,
                        synchronization.ReadyValue,
                        ConsumedSemaphore,
                        synchronization.ConsumedValue);
                    return;
                }

                await drawingSurface.UpdateAsync(ImportedImage);
            }

            private static int AlignUp(int value, int alignment)
            {
                if (alignment <= 1)
                {
                    return value;
                }

                return checked((value + alignment - 1) / alignment * alignment);
            }

            public void Dispose()
            {
                if (IsDisposed)
                {
                    return;
                }

                IsDisposed = true;
                IsBusy = false;
                _ = ConsumedSemaphore?.DisposeAsync();
                _ = ReadySemaphore?.DisposeAsync();
                _ = ImportedImage.DisposeAsync();
                Texture.Dispose();
                ConsumedEvent?.Dispose();
                ReadyEvent?.Dispose();
                Surface.Dispose();
            }
        }
    }
}
