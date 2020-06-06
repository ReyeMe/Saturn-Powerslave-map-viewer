namespace PowerslaveMapViewer
{
    using System;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Interop;
    using System.Windows.Media;
    using System.Windows.Media.Imaging;
    using System.Windows.Threading;
    using SharpGL;
    using SharpGL.RenderContextProviders;
    using SharpGL.SceneGraph;
    using SharpGL.Version;
    using SharpGL.WPF;

    /// <summary>
    /// OpenGL control for WPF (modified from original source)
    /// </summary>
    public partial class GLControl : UserControl
    {
        /// <summary>
        /// Default frame rate
        /// </summary>
        private const double DefaultFrameRate = 60.0;

        /// <summary>
        /// The DrawFPS property.
        /// </summary>
        private static readonly DependencyProperty DrawFPSProperty =
          DependencyProperty.Register("DrawFPS", typeof(bool), typeof(GLControl), new PropertyMetadata(false, null));

        /// <summary>
        /// The frame rate dependency property.
        /// </summary>
        private static readonly DependencyProperty FrameRateProperty =
          DependencyProperty.Register("FrameRate", typeof(double), typeof(GLControl), new PropertyMetadata(GLControl.DefaultFrameRate, new PropertyChangedCallback(GLControl.OnFrameRateChanged)));

        /// <summary>
        /// The OpenGL Version property.
        /// </summary>
        private static readonly DependencyProperty OpenGLVersionProperty =
          DependencyProperty.Register("OpenGLVersion", typeof(OpenGLVersion), typeof(GLControl), new PropertyMetadata(OpenGLVersion.OpenGL2_1));

        /// <summary>
        /// The render context type property.
        /// </summary>
        private static readonly DependencyProperty RenderContextTypeProperty =
          DependencyProperty.Register("RenderContextType", typeof(RenderContextType), typeof(GLControl), new PropertyMetadata(RenderContextType.DIBSection, new PropertyChangedCallback(GLControl.OnRenderContextTypeChanged)));

        /// <summary>
        /// The dispatcher timer.
        /// </summary>
        private readonly Thread timer = null;

        /// <summary>
        /// Current frame rate
        /// </summary>
        private int currentFrameRate = 0;

        /// <summary>
        /// A single event args for all our needs.
        /// </summary>
        private OpenGLEventArgs eventArgsFast;

        /// <summary>
        /// Current set frame rate
        /// </summary>
        private double frameRate = GLControl.DefaultFrameRate;

        /// <summary>
        /// The last frame time in milliseconds.
        /// </summary>
        private double frameTime = 0;

        /// <summary>
        /// A stopwatch used for timing rendering.
        /// </summary>
        private Stopwatch stopwatch = new Stopwatch();

        /// <summary>
        /// Initializes a new instance of the <see cref="GLControl"/> class.
        /// </summary>
        public GLControl()
        {
            this.InitializeComponent();

            this.timer = new Thread(() =>
            {
                int frameCounter = 0;
                Timer frameRateTimer = new Timer(
                    (state) =>
                    {
                        this.currentFrameRate = frameCounter;
                        frameCounter = 0;
                    },
                    null,
                    0,
                    (int)new TimeSpan(0, 0, 1).TotalMilliseconds);

                while (Thread.CurrentThread.IsAlive)
                {
                    try
                    {
                        this.TimerTick();
                        frameCounter++;
                    }
                    catch (TaskCanceledException ex)
                    {
                        frameRateTimer.Dispose();
                        ex.ToString();
                        return;
                    }
                }
            });

            this.Unloaded += this.OpenGLControlUnloaded;
            this.Loaded += this.OpenGLControlLoaded;
        }

        /// <summary>
        /// Occurs when OpenGL drawing should occur.
        /// </summary>
        [Description("Called whenever OpenGL drawing can should occur."), Category("SharpGL")]
        public event OpenGLEventHandler OpenGLDraw;

        /// <summary>
        /// Occurs when OpenGL should be initialized.
        /// </summary>
        [Description("Called when OpenGL has been initialized."), Category("SharpGL")]
        public event OpenGLEventHandler OpenGLInitialized;

        /// <summary>
        /// Occurs when the control is resized. This can be used to perform custom projections.
        /// </summary>
        [Description("Called when the control is resized - you can use this to do custom viewport projections."), Category("SharpGL")]
        public event OpenGLEventHandler Resized;

        /// <summary>
        /// Gets or sets a value indicating whether to draw FPS.
        /// </summary>
        /// <value>
        ///   <c>true</c> if draw FPS; otherwise, <c>false</c>.
        /// </value>
        public bool DrawFPS
        {
            get
            {
                return (bool)this.GetValue(GLControl.DrawFPSProperty);
            }

            set
            {
                this.SetValue(GLControl.DrawFPSProperty, value);
            }
        }

        /// <summary>
        /// Gets or sets the frame rate.
        /// </summary>
        /// <value>The frame rate.</value>
        public double FrameRate
        {
            get
            {
                return (double)this.GetValue(GLControl.FrameRateProperty);
            }

            set
            {
                this.SetValue(GLControl.FrameRateProperty, value);
            }
        }

        /// <summary>
        /// Gets the OpenGL instance.
        /// </summary>
        public OpenGL OpenGL { get; } = new OpenGL();

        /// <summary>
        /// Gets or sets the OpenGL Version requested for the control.
        /// </summary>
        /// <value>The type of the render context.</value>
        public OpenGLVersion OpenGLVersion
        {
            get
            {
                return (OpenGLVersion)this.GetValue(GLControl.OpenGLVersionProperty);
            }

            set
            {
                this.SetValue(GLControl.OpenGLVersionProperty, value);
            }
        }

        /// <summary>
        /// Gets or sets the type of the render context.
        /// </summary>
        /// <value>The type of the render context.</value>
        public RenderContextType RenderContextType
        {
            get
            {
                return (RenderContextType)this.GetValue(GLControl.RenderContextTypeProperty);
            }

            set
            {
                this.SetValue(GLControl.RenderContextTypeProperty, value);
            }
        }

        /// <summary>
        /// When overridden in a derived class, is invoked whenever application code or
        /// internal processes call <see cref="M:System.Windows.FrameworkElement.ApplyTemplate"/>.
        /// </summary>
        public override void OnApplyTemplate()
        {
            // Call the base
            base.OnApplyTemplate();

            // Fix for WPF
            IntPtr selection = ((HwndSource)HwndSource.FromVisual(this)).Handle;

            // Lock on OpenGL
            lock (this.OpenGL)
            {
                // Create OpenGL
                this.OpenGL.Create(this.OpenGLVersion, this.RenderContextType, 1, 1, 16, selection);
            }

            // Create our fast event args
            this.eventArgsFast = new OpenGLEventArgs(this.OpenGL);

            // Set the most basic OpenGL styles
            this.OpenGL.ShadeModel(OpenGL.GL_SMOOTH);
            this.OpenGL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f);
            this.OpenGL.ClearDepth(1.0f);
            this.OpenGL.Enable(OpenGL.GL_DEPTH_TEST);
            this.OpenGL.DepthFunc(OpenGL.GL_LEQUAL);
            this.OpenGL.Hint(OpenGL.GL_PERSPECTIVE_CORRECTION_HINT, OpenGL.GL_NICEST);

            // Fire the OpenGL initialised event
            this.OpenGLInitialized?.Invoke(this, this.eventArgsFast);
        }

        /// <summary>
        /// This method converts the output from the OpenGL render context provider to a
        /// FormatConvertedBitmap in order to show it in the image.
        /// </summary>
        /// <param name="handle">The handle of the bitmap from the OpenGL render context.</param>
        /// <returns>Returns the new format converted bitmap.</returns>
        private static FormatConvertedBitmap GetFormatedBitmapSource(IntPtr handle)
        {
            // TODO: We have to remove the alpha channel - for some reason it comes out as 0.0
            // meaning the drawing comes out transparent
            FormatConvertedBitmap newFormatedBitmapSource = new FormatConvertedBitmap();
            newFormatedBitmapSource.BeginInit();
            newFormatedBitmapSource.Source = BitmapConversion.HBitmapToBitmapSource(handle);
            newFormatedBitmapSource.DestinationFormat = PixelFormats.Rgb24;
            newFormatedBitmapSource.EndInit();

            return newFormatedBitmapSource;
        }

        /// <summary>
        /// Called when the frame rate is changed.
        /// </summary>
        /// <param name="o">The object.</param>
        /// <param name="args">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnFrameRateChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            ((GLControl)o).frameRate = (double)args.NewValue;
        }

        /// <summary>
        /// Called when [render context type changed].
        /// </summary>
        /// <param name="o">The o.</param>
        /// <param name="args">The <see cref="System.Windows.DependencyPropertyChangedEventArgs"/> instance containing the event data.</param>
        private static void OnRenderContextTypeChanged(DependencyObject o, DependencyPropertyChangedEventArgs args)
        {
            // Nothing to do here
        }

        /// <summary>
        /// Handles the Loaded event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
        private void OpenGLControlLoaded(object sender, RoutedEventArgs e)
        {
            this.SizeChanged += this.OpenGLControlSizeChanged;

            this.UpdateOpenGLControl((int)this.RenderSize.Width, (int)this.RenderSize.Height);

            // DispatcherTimer setup
            this.timer.Start();
        }

        /// <summary>
        /// Handles the SizeChanged event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.SizeChangedEventArgs"/> Instance containing the event data.</param>
        private void OpenGLControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            this.UpdateOpenGLControl((int)e.NewSize.Width, (int)e.NewSize.Height);
        }

        /// <summary>
        /// Handles the Unloaded event of the OpenGLControl control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="System.Windows.RoutedEventArgs"/> Instance containing the event data.</param>
        private void OpenGLControlUnloaded(object sender, RoutedEventArgs e)
        {
            this.SizeChanged -= this.OpenGLControlSizeChanged;

            this.timer.Abort();
        }

        /// <summary>
        /// Handles the Tick event of the timer control.
        /// </summary>
        private void TimerTick()
        {
            try
            {
                if (this.Dispatcher.HasShutdownStarted || this.Dispatcher.HasShutdownFinished)
                {
                    this.timer.Abort();
                    return;
                }

                this.Dispatcher.Invoke(
                    () =>
                    {
                        // Lock on OpenGL
                        lock (this.OpenGL)
                        {
                            // Start the stopwatch so that we can time the rendering
                            this.stopwatch.Restart();

                            // Make GL current
                            this.OpenGL.MakeCurrent();

                            // If there is a draw handler, then call it
                            OpenGLEventHandler handler = this.OpenGLDraw;

                            if (handler != null)
                            {
                                handler(this, this.eventArgsFast);
                            }
                            else
                            {
                                this.OpenGL.Clear(OpenGL.GL_COLOR_BUFFER_BIT);
                            }

                            // Draw the FPS
                            if (this.DrawFPS)
                            {
                                this.OpenGL.DrawText(5, 5, 1.0f, 0.0f, 0.0f, "Courier New", 12.0f, string.Format("Draw Time: {0:0.0000} ms ~ {1:0.0} FPS", this.frameTime, this.currentFrameRate));
                                this.OpenGL.Flush();
                            }

                            // Render
                            this.OpenGL.Blit(IntPtr.Zero);

                            switch (this.RenderContextType)
                            {
                                case RenderContextType.DIBSection:
                                    {
                                        DIBSectionRenderContextProvider provider = this.OpenGL.RenderContextProvider as DIBSectionRenderContextProvider;
                                        IntPtr hBitmap = provider.DIBSection.HBitmap;

                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            FormatConvertedBitmap newFormatedBitmapSource = GetFormatedBitmapSource(hBitmap);

                                            // Copy the pixels over
                                            this.image.Source = newFormatedBitmapSource;
                                        }
                                    }

                                    break;

                                case RenderContextType.NativeWindow:
                                    break;

                                case RenderContextType.HiddenWindow:
                                    break;

                                case RenderContextType.FBO:
                                    {
                                        FBORenderContextProvider provider = this.OpenGL.RenderContextProvider as FBORenderContextProvider;
                                        IntPtr hBitmap = provider.InternalDIBSection.HBitmap;

                                        if (hBitmap != IntPtr.Zero)
                                        {
                                            FormatConvertedBitmap newFormatedBitmapSource = GetFormatedBitmapSource(hBitmap);

                                            // Copy the pixels over
                                            this.image.Source = newFormatedBitmapSource;
                                        }
                                    }

                                    break;

                                default:
                                    break;
                            }

                            // Stop the stopwatch
                            this.stopwatch.Stop();

                            // Store the frame time
                            this.frameTime = this.stopwatch.Elapsed.TotalMilliseconds;
                        }
                    },
                    DispatcherPriority.Send);

                if (this.frameRate > 0.0)
                {
                    Thread.Sleep(new TimeSpan(0, 0, 0, 0, (int)(Math.Max(1000.0 - this.frameTime, this.frameRate) / this.frameRate)));
                }
            }
            catch (Exception ex)
            {
                ex.ToString();
            }
        }

        /// <summary>
        /// This method is used to set the dimensions and the viewport of the OpenGL control.
        /// </summary>
        /// <param name="width">The width of the OpenGL drawing area.</param>
        /// <param name="height">The height of the OpenGL drawing area.</param>
        private void UpdateOpenGLControl(int width, int height)
        {
            // Lock on OpenGL
            lock (this.OpenGL)
            {
                this.OpenGL.SetDimensions(width, height);

                // Set the viewport
                this.OpenGL.Viewport(0, 0, width, height);

                // If we have a project handler, call it
                if (width != -1 && height != -1)
                {
                    OpenGLEventHandler handler = this.Resized;

                    if (handler != null)
                    {
                        handler(this, this.eventArgsFast);
                    }
                    else
                    {
                        // Otherwise we do our own projection
                        this.OpenGL.MatrixMode(OpenGL.GL_PROJECTION);
                        this.OpenGL.LoadIdentity();

                        // Calculate The Aspect Ratio Of The Window
                        this.OpenGL.Perspective(45.0f, (float)width / (float)height, 0.1f, 100.0f);

                        this.OpenGL.MatrixMode(OpenGL.GL_MODELVIEW);
                        this.OpenGL.LoadIdentity();
                    }
                }
            }
        }
    }
}