// Copyright (c) 2010-2013 SharpDX - Alexandre Mutel
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
#if WIN8METRO
using Windows.UI.Core;
using Windows.Graphics.Display;
using Windows.UI.Xaml.Controls;
#elif WP8

#else
using System.Windows.Forms;
#endif

using SharpDX.DXGI;

namespace SharpDX.Toolkit.Graphics
{
    /// <summary>
    /// Graphics presenter for SwapChain.
    /// </summary>
    public class SwapChainGraphicsPresenter : GraphicsPresenter
    {
        private RenderTarget2D backBuffer;

        private SwapChain swapChain;

        private int bufferCount;

        public SwapChainGraphicsPresenter(GraphicsDevice device, PresentationParameters presentationParameters)
            : base(device, presentationParameters)
        {
            PresentInterval = presentationParameters.PresentationInterval;

            // Initialize the swap chain
            swapChain = ToDispose(CreateSwapChain());

            backBuffer = ToDispose(RenderTarget2D.New(device, swapChain.GetBackBuffer<Direct3D11.Texture2D>(0)));
        }

        public override RenderTarget2D BackBuffer
        {
            get
            {
                return backBuffer;
            }
        }

        public override object NativePresenter
        {
            get
            {
                return swapChain;
            }
        }

        public override bool IsFullScreen
        {
            get
            {
#if WIN8METRO
                return true;
#else
                return swapChain.IsFullScreen;
#endif
            }

            set
            {
#if WIN8METRO
                if (!value)
                {
                    throw new ArgumentException("Cannot switch to non-full screen in Windows RT");
                }
#else

                var outputIndex = PrefferedFullScreenOutputIndex;
                var availableOutputs = GraphicsDevice.Adapter.OutputsCount;

                // no outputs connected to the current graphics adapter
                var output = availableOutputs == 0 ? null : GraphicsDevice.Adapter.GetOutputAt(outputIndex);

                Output currentOutput = null;

                try
                {
                    Bool isCurrentlyFullscreen;
                    swapChain.GetFullscreenState(out isCurrentlyFullscreen, out currentOutput);

                    // check if the current fullscreen monitor is the same as new one
                    if (isCurrentlyFullscreen == value && output != null && currentOutput != null && currentOutput.NativePointer == ((Output)output).NativePointer)
                        return;
                }
                finally
                {
                    if (currentOutput != null)
                        currentOutput.Dispose();
                }

                bool switchToFullScreen = value;
                // If going to fullscreen mode: call 1) SwapChain.ResizeTarget 2) SwapChain.IsFullScreen
                var description = new ModeDescription(backBuffer.Width, backBuffer.Height, Description.RefreshRate, Description.BackBufferFormat);
                if (switchToFullScreen)
                {
                    swapChain.ResizeTarget(ref description);
                    swapChain.SetFullscreenState(true, output);
                }
                else
                    swapChain.IsFullScreen = false;

                // call 1) SwapChain.IsFullScreen 2) SwapChain.Resize
                Resize(backBuffer.Width, backBuffer.Height, backBuffer.Format);

                // If going to window mode: 
                if (!switchToFullScreen)
                {
                    // call 1) SwapChain.IsFullScreen 2) SwapChain.Resize
                    description.RefreshRate = new Rational(0, 0);
                    swapChain.ResizeTarget(ref description);
                }
#endif
            }
        }

        public override void Present()
        {
            swapChain.Present((int)PresentInterval, PresentFlags.None);
        }

        protected override void OnNameChanged()
        {
            base.OnNameChanged();
            if (GraphicsDevice.IsDebugMode && swapChain != null)
            {
                swapChain.DebugName = Name;
            }
        }

        public override void Resize(int width, int height, Format format)
        {
            base.Resize(width, height, format);

            RemoveAndDispose(ref backBuffer);

            swapChain.ResizeBuffers(bufferCount, width, height, format, Description.Flags);

            // Recreate the back buffer
            backBuffer = ToDispose(RenderTarget2D.New(GraphicsDevice, swapChain.GetBackBuffer<Direct3D11.Texture2D>(0)));

            // Reinit the Viewport
            DefaultViewport = new ViewportF(0, 0, backBuffer.Width, backBuffer.Height);
        }

        private SwapChain CreateSwapChain()
        {
            // Check for Window Handle parameter
            if (Description.DeviceWindowHandle == null)
            {
                throw new ArgumentException("DeviceWindowHandle cannot be null");
            }

#if WIN8METRO
            return CreateSwapChainForWinRT();
#else
            return CreateSwapChainForDesktop();
#endif
        }

#if WIN8METRO
        private SwapChain CreateSwapChainForWinRT()
        {
            var coreWindow = Description.DeviceWindowHandle as CoreWindow;
            var swapChainBackgroundPanel = Description.DeviceWindowHandle as SwapChainBackgroundPanel;

            bufferCount = 2;
            var description = new SwapChainDescription1
            {
                // Automatic sizing
                Width = Description.BackBufferWidth,
                Height = Description.BackBufferHeight,
                Format = SharpDX.DXGI.Format.B8G8R8A8_UNorm, // TODO: Check if we can use the Description.BackBufferFormat
                Stereo = false,
                SampleDescription = new SharpDX.DXGI.SampleDescription((int)Description.MultiSampleCount, 0),
                Usage = Description.RenderTargetUsage,
                // Use two buffers to enable flip effect.
                BufferCount = bufferCount,
                Scaling = SharpDX.DXGI.Scaling.Stretch,
                SwapEffect = SharpDX.DXGI.SwapEffect.FlipSequential,
            }; 
            
            if (coreWindow != null)
            {
                // Creates a SwapChain from a CoreWindow pointer
                using (var comWindow = new ComObject(coreWindow)) return ((DXGI.Factory2)GraphicsAdapter.Factory).CreateSwapChainForCoreWindow((Direct3D11.Device)GraphicsDevice, comWindow, ref description, null);
            }
            else if (swapChainBackgroundPanel != null)
            {
                var nativePanel = ComObject.As<ISwapChainBackgroundPanelNative>(swapChainBackgroundPanel);
                // Creates the swap chain for XAML composition
                var swapChain = ((DXGI.Factory2)GraphicsAdapter.Factory).CreateSwapChainForComposition((Direct3D11.Device)GraphicsDevice, ref description, null);

                // Associate the SwapChainBackgroundPanel with the swap chain
                nativePanel.SwapChain = swapChain;
                return swapChain;
            }
            else
            {
                throw new NotSupportedException();
            }
        }
#elif WP8
        private SwapChain CreateSwapChainForDesktop()
        {
            throw new NotImplementedException();
        }
#else
        private SwapChain CreateSwapChainForDesktop()
        {
            var control = Description.DeviceWindowHandle as Control;
            if (control == null)
            {
                throw new NotSupportedException(string.Format("Form of type [{0}] is not supported. Only System.Windows.Control are supported", Description.DeviceWindowHandle != null ? Description.DeviceWindowHandle.GetType().Name : "null"));
            }

            bufferCount = 1;
            var description = new SwapChainDescription
                {
                    ModeDescription = new ModeDescription(Description.BackBufferWidth, Description.BackBufferHeight, Description.RefreshRate, Description.BackBufferFormat),
                    BufferCount = bufferCount, // TODO: Do we really need this to be configurable by the user?
                    OutputHandle = control.Handle,
                    SampleDescription = new SampleDescription((int)Description.MultiSampleCount, 0),
                    SwapEffect = SwapEffect.Discard,
                    Usage = Description.RenderTargetUsage,
                    IsWindowed = true,
                    Flags = Description.Flags,
                };

            var newSwapChain = new SwapChain(GraphicsAdapter.Factory, (Direct3D11.Device)GraphicsDevice, description);
            if (Description.IsFullScreen)
            {
                // Before fullscreen switch
                newSwapChain.ResizeTarget(ref description.ModeDescription);

                // Switch to full screen
                newSwapChain.IsFullScreen = true;

                // This is really important to call ResizeBuffers AFTER switching to IsFullScreen 
                newSwapChain.ResizeBuffers(bufferCount, Description.BackBufferWidth, Description.BackBufferHeight, Description.BackBufferFormat, SwapChainFlags.AllowModeSwitch);
            }

            return newSwapChain;
        }
#endif
    }
}