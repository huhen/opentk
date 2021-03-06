﻿#region License
//
// The Open Toolkit Library License
//
// Copyright (c) 2006 - 2009 the Open Toolkit library, except where noted.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights to 
// use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of
// the Software, and to permit persons to whom the Software is furnished to do
// so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
// OTHER DEALINGS IN THE SOFTWARE.
//
#endregion

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;

using OpenTK.Platform;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;

using Mapsui;
using Mapsui.Rendering.OpenTK;
using Mapsui.Fetcher;
using Mapsui.Utilities;

namespace OpenTK
{
    /// <summary>
    /// OpenGL-aware WinForms control.
    /// The WinForms designer will always call the default constructor.
    /// Inherit from this class and call one of its specialized constructors
    /// to enable antialiasing or custom <see cref="GraphicsMode"/>s.
    /// </summary>
    public partial class MapGLControl : UserControl
    {
        //======MapControl==========
        private Map _map;
        private string _errorMessage;
        //private Bitmap _buffer;
        //private Graphics _bufferGraphics;
        //private readonly Brush _whiteBrush = new SolidBrush(Color.White);
        private Mapsui.Geometries.Point _mousePosition;
        //Indicates that a redraw is needed. This often coincides with 
        //manipulation but not in the case of new data arriving.
        private bool _viewInitialized;
        private readonly MapRenderer _renderer = new MapRenderer();

        public event EventHandler ErrorMessageChanged;

        public IViewport Transform
        {
            get { return _map.Viewport; }
        }

        public Map Map
        {
            get
            {
                return _map;
            }
            set
            {
                var temp = _map;
                _map = null;

                if (temp != null)
                {
                    temp.DataChanged -= MapDataChanged;
                    temp.Dispose();
                }

                _map = value;
                _map.DataChanged += MapDataChanged;

                ViewChanged(true);
                Invalidate();
            }
        }

        void MapDataChanged(object sender, DataChangedEventArgs e)
        {
            //ViewChanged should not be called here. This would cause a loop
            BeginInvoke((Action)(() => DataChanged(sender, e)));
        }

        public void ZoomIn()
        {
            Map.Viewport.Resolution = ZoomHelper.ZoomIn(_map.Resolutions, Map.Viewport.Resolution);
            ViewChanged(true);
            Invalidate();
        }

        public void ZoomIn(PointF mapPosition)
        {
            // When zooming in we want the mouse position to stay above the same world coordinate.
            // We do that in 3 steps.

            // 1) Temporarily center on where the mouse is
            Map.Viewport.Center = Map.Viewport.ScreenToWorld(mapPosition.X, mapPosition.Y);

            // 2) Then zoom 
            Map.Viewport.Resolution = ZoomHelper.ZoomIn(_map.Resolutions, Map.Viewport.Resolution);

            // 3) Then move the temporary center back to the mouse position
            Map.Viewport.Center = Map.Viewport.ScreenToWorld(
              Map.Viewport.Width - mapPosition.X,
              Map.Viewport.Height - mapPosition.Y);

            ViewChanged(true);
            Invalidate();
        }

        public void ZoomOut()
        {
            Map.Viewport.Resolution = ZoomHelper.ZoomOut(_map.Resolutions, Map.Viewport.Resolution);
            ViewChanged(true);
            Invalidate();
        }

        private void ViewChanged(bool changeEnd)
        {
            if (_map != null)
            {
                _map.ViewChanged(changeEnd);
            }
        }

        private void DataChanged(object sender, DataChangedEventArgs e)
        {
            if (e.Error == null && e.Cancelled == false)
            {
                Invalidate();
            }
            else if (e.Cancelled)
            {
                _errorMessage = "Cancelled";
                OnErrorMessageChanged();
            }
            else if (e.Error is System.Net.WebException)
            {
                _errorMessage = "WebException: " + e.Error.Message;
                OnErrorMessageChanged();
            }
            else if (e.Error == null)
            {
                _errorMessage = "Unknown Exception";
                OnErrorMessageChanged();
            }
            else
            {
                _errorMessage = "Exception: " + e.Error.Message;
                OnErrorMessageChanged();
            }
        }

        private void MapControl_MouseDown(object sender, MouseEventArgs e)
        {
            _mousePosition = new Mapsui.Geometries.Point(e.X, e.Y);
        }

        private void MapControl_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_mousePosition == null) return;
                var newMousePosition = new Mapsui.Geometries.Point(e.X, e.Y);
                MapTransformHelpers.Pan(Map.Viewport, newMousePosition, _mousePosition);
                _mousePosition = newMousePosition;

                ViewChanged(false);
                Invalidate();
            }
        }

        private void MapControl_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (_mousePosition == null) return;
                var newMousePosition = new Mapsui.Geometries.Point(e.X, e.Y);
                MapTransformHelpers.Pan(Map.Viewport, newMousePosition, _mousePosition);
                _mousePosition = newMousePosition;

                ViewChanged(true);
                Invalidate();
            }
        }

        private void MapControl_Resize(object sender, EventArgs e)
        {
            if (Width == 0) return;
            if (Height == 0) return;

            Map.Viewport.Width = Width;
            Map.Viewport.Height = Height;

            /*if (_buffer == null || _buffer.Width != Width || _buffer.Height != Height)
            {
                _buffer = new Bitmap(Width, Height);
                _bufferGraphics = Graphics.FromImage(_buffer);
            }*/

            ViewChanged(true);
            Invalidate();
        }

        private void InitializeView()
        {
            if (double.IsNaN(Width) || Width == 0) return;
            if (_map == null || _map.Envelope == null || double.IsNaN(_map.Envelope.Width) || _map.Envelope.Width <= 0) return;
            if (_map.Envelope.GetCentroid() == null) return;

            Map.Viewport.Center = _map.Envelope.GetCentroid();
            Map.Viewport.Resolution = _map.Envelope.Width / Width;
            _viewInitialized = true;
            ViewChanged(true);
        }

        private void MapControl_Disposed(object sender, EventArgs e)
        {
            Map.Dispose();
        }

        protected void OnErrorMessageChanged()
        {
            if (ErrorMessageChanged != null) ErrorMessageChanged(this, null);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            if (DesignMode) base.OnPaintBackground(e);
            //by overriding this method and not calling the base class implementation 
            //we prevent flickering.
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0)
            {
                ZoomIn(e.Location);
            }
            else if (e.Delta < 0)
            {
                ZoomOut();
            }
        }
        //======EndMapControl==========

        IGraphicsContext context;
        IGLControl implementation;
        GraphicsMode format;
        int major, minor;
        GraphicsContextFlags flags;
        bool? initial_vsync_value;
        // Indicates that OnResize was called before OnHandleCreated.
        // To avoid issues with missing OpenGL contexts, we suppress
        // the premature Resize event and raise it as soon as the handle
        // is ready.
        bool resize_event_suppressed;
        // Indicates whether the control is in design mode. Due to issues
        // wiith the DesignMode property and nested controls,we need to
        // evaluate this in the constructor.
        readonly bool design_mode;

        #region --- Constructors ---

        /// <summary>
        /// Constructs a new instance.
        /// </summary>
        public MapGLControl()
            : this(GraphicsMode.Default)
        { }

        /// <summary>
        /// Constructs a new instance with the specified GraphicsMode.
        /// </summary>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the control.</param>
        public MapGLControl(GraphicsMode mode)
            : this(mode, 1, 0, GraphicsContextFlags.Default)
        { }

        /// <summary>
        /// Constructs a new instance with the specified GraphicsMode.
        /// </summary>
        /// <param name="mode">The OpenTK.Graphics.GraphicsMode of the control.</param>
        /// <param name="major">The major version for the OpenGL GraphicsContext.</param>
        /// <param name="minor">The minor version for the OpenGL GraphicsContext.</param>
        /// <param name="flags">The GraphicsContextFlags for the OpenGL GraphicsContext.</param>
        public MapGLControl(GraphicsMode mode, int major, int minor, GraphicsContextFlags flags)
        {
            if (mode == null)
                throw new ArgumentNullException("mode");

            //===MapControl====
            Map = new Map();
            Resize += MapControl_Resize;
            MouseDown += MapControl_MouseDown;
            MouseMove += MapControl_MouseMove;
            MouseUp += MapControl_MouseUp;
            Disposed += MapControl_Disposed;
            //===EndMapControl====

            // SDL does not currently support embedding
            // on external windows. If Open.Toolkit is not yet
            // initialized, we'll try to request a native backend
            // that supports embedding.
            // Most people are using GLControl through the
            // WinForms designer in Visual Studio. This approach
            // works perfectly in that case.
            Toolkit.Init(new ToolkitOptions
            {
                Backend = PlatformBackend.PreferNative
            });
            
            SetStyle(ControlStyles.Opaque, true);
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            DoubleBuffered = false;
            var p = new GraphicsMode(mode.ColorFormat, mode.Depth, mode.Stencil, 4, mode.ColorFormat, mode.Buffers, false);
            this.format = p;
            this.major = major;
            this.minor = minor;
            this.flags = flags;

            // Note: the DesignMode property may be incorrect when nesting controls.
            // We use LicenseManager.UsageMode as a workaround (this only works in
            // the constructor).
            design_mode =
                DesignMode ||
                LicenseManager.UsageMode == LicenseUsageMode.Designtime;

            InitializeComponent();
        }

        #endregion

        #region --- Private  Methods ---

        IGLControl Implementation
        {
            get
            {
                ValidateState();

                return implementation;
            }
        }

        [Conditional("DEBUG")]
        void ValidateContext(string message)
        {
            if (!Context.IsCurrent)
            {
                Debug.Print("[GLControl] Attempted to access {0} on a non-current context. Results undefined.", message);
            }
        }

        void ValidateState()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);

            if (!IsHandleCreated)
                CreateControl();

            if (implementation == null || context == null || context.IsDisposed)
                RecreateHandle();
        }

        #endregion

        #region --- Protected Methods ---

        /// <summary>
        /// Gets the <c>CreateParams</c> instance for this <c>GLControl</c>
        /// </summary>
        protected override CreateParams CreateParams
        {
            get
            {
                const int CS_VREDRAW = 0x1;
                const int CS_HREDRAW = 0x2;
                const int CS_OWNDC = 0x20;

                CreateParams cp = base.CreateParams;
                if (Configuration.RunningOnWindows)
                {
                    // Setup necessary class style for OpenGL on windows
                    cp.ClassStyle |= CS_VREDRAW | CS_HREDRAW | CS_OWNDC;
                }
                return cp;
            }
        }

        /// <summary>Raises the HandleCreated event.</summary>
        /// <param name="e">Not used.</param>
        protected override void OnHandleCreated(EventArgs e)
        {
            if (context != null)
                context.Dispose();

            if (implementation != null)
                implementation.WindowInfo.Dispose();

            if (design_mode)
                implementation = new DummyGLControl();
            else
                implementation = new GLControlFactory().CreateGLControl(format, this);

            context = implementation.CreateContext(major, minor, flags);
            MakeCurrent();

            if (!design_mode)
                ((IGraphicsContextInternal)Context).LoadAll();

            // Deferred setting of vsync mode. See VSync property for more information.
            if (initial_vsync_value.HasValue)
            {
                Context.SwapInterval = initial_vsync_value.Value ? 1 : 0;
                initial_vsync_value = null;
            }

            base.OnHandleCreated(e);

            if (resize_event_suppressed)
            {
                OnResize(EventArgs.Empty);
                resize_event_suppressed = false;
            }
        }

        /// <summary>Raises the HandleDestroyed event.</summary>
        /// <param name="e">Not used.</param>
        protected override void OnHandleDestroyed(EventArgs e)
        {
            // Ensure that context is still alive when passing to events
            // => This allows to perform cleanup operations in OnHandleDestroyed handlers
            base.OnHandleDestroyed(e);

            if (context != null)
            {
                context.Dispose();
                context = null;
            }

            if (implementation != null)
            {
                implementation.WindowInfo.Dispose();
                implementation = null;
            }

        }

        /// <summary>
        /// Raises the System.Windows.Forms.Control.Paint event.
        /// </summary>
        /// <param name="e">A System.Windows.Forms.PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            ValidateState();

            if (design_mode)
                e.Graphics.Clear(BackColor);

            base.OnPaint(e);

            //===MapControl===
            if (!_viewInitialized) InitializeView();
            if (!_viewInitialized) return; //initialize in the line above failed. 

            GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
            _renderer.Render(Map.Viewport, _map.Layers);
            SwapBuffers();
            //===EndMapControl====
        }

        /// <summary>
        /// Raises the Resize event.
        /// Note: this method may be called before the OpenGL context is ready.
        /// Check that IsHandleCreated is true before using any OpenGL methods.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            // Do not raise OnResize event before the handle and context are created.
            if (!IsHandleCreated)
            {
                resize_event_suppressed = true;
                return;
            }

            if (Configuration.RunningOnMacOS)
            {
                DelayUpdate delay = PerformContextUpdate;
                BeginInvoke(delay); //Need the native window to resize first otherwise our control will be in the wrong place.
            }
            else if (context != null)
                context.Update(Implementation.WindowInfo);

            SetupViewport();
            base.OnResize(e);
        }

        private void SetupViewport()
        {
            var w = Width;
            var h = Height;
            GL.MatrixMode(MatrixMode.Projection);
            GL.LoadIdentity();
            GL.Ortho(0, w, h, 0, -1, 1); // Верхний левый угол имеет кооординаты(0, 0)
            GL.Viewport(0, 0, w, h); // Использовать всю поверхность GLControl под рисование
        }

        /// <summary>
        /// Needed to delay the invoke on OS X. Also needed because OpenTK is .NET 2, otherwise I'd use an inline Action.
        /// </summary>
        public delegate void DelayUpdate();
        /// <summary>
        /// Execute the delayed context update
        /// </summary>
        public void PerformContextUpdate()
        {
            if (context != null)
                context.Update(Implementation.WindowInfo);
        }

        /// <summary>
        /// Raises the ParentChanged event.
        /// </summary>
        /// <param name="e">A System.EventArgs that contains the event data.</param>
        protected override void OnParentChanged(EventArgs e)
        {
            if (context != null)
                context.Update(Implementation.WindowInfo);

            base.OnParentChanged(e);
        }

        #endregion

        #region --- Public Methods ---

        #region public void SwapBuffers()

        /// <summary>
        /// Swaps the front and back buffers, presenting the rendered scene to the screen.
        /// This method will have no effect on a single-buffered <c>GraphicsMode</c>.
        /// </summary>
        public void SwapBuffers()
        {
            ValidateState();
            Context.SwapBuffers();
        }

        #endregion

        #region public void MakeCurrent()

        /// <summary>
        /// <para>
        /// Makes <see cref="GLControl.Context"/> current in the calling thread.
        /// All OpenGL commands issued are hereafter interpreted by this context.
        /// </para>
        /// <para>
        /// When using multiple <c>GLControl</c>s, calling <c>MakeCurrent</c> on
        /// one control will make all other controls non-current in the calling thread.
        /// </para>
        /// <seealso cref="Context"/>
        /// <para>
        /// A <c>GLControl</c> can only be current in one thread at a time.
        /// To make a control non-current, call <c>GLControl.Context.MakeCurrent(null)</c>.
        /// </para>
        /// </summary>
        public void MakeCurrent()
        {
            ValidateState();
            Context.MakeCurrent(Implementation.WindowInfo);
        }

        #endregion

        #region public bool IsIdle

        /// <summary>
        /// Gets a value indicating whether the current thread contains pending system messages.
        /// </summary>
        [Browsable(false)]
        public bool IsIdle
        {
            get
            {
                ValidateState();
                return Implementation.IsIdle;
            }
        }

        #endregion

        #region public IGraphicsContext Context

        /// <summary>
        /// Gets the <c>IGraphicsContext</c> instance that is associated with the <c>GLControl</c>.
        /// The associated <c>IGraphicsContext</c> is updated whenever the <c>GLControl</c>
        /// handle is created or recreated.
        /// When using multiple <c>GLControl</c>s, ensure that <c>Context</c>
        /// is current before performing any OpenGL operations.
        /// <seealso cref="MakeCurrent"/>
        /// </summary>
        [Browsable(false)]
        public IGraphicsContext Context
        {
            get
            {
                ValidateState();
                return context;
            }
            private set { context = value; }
        }

        #endregion

        #region public float AspectRatio

        /// <summary>
        /// Gets the aspect ratio of this GLControl.
        /// </summary>
        [Description("The aspect ratio of the client area of this GLControl.")]
        public float AspectRatio
        {
            get
            {
                ValidateState();
                return ClientSize.Width / (float)ClientSize.Height;
            }
        }

        #endregion

        #region public bool VSync

        /// <summary>
        /// Gets or sets a value indicating whether vsync is active for this <c>GLControl</c>.
        /// When using multiple <c>GLControl</c>s, ensure that <see cref="Context"/>
        /// is current before accessing this property.
        /// <seealso cref="Context"/>
        /// <seealso cref="MakeCurrent"/>
        /// </summary>
        [Description("Indicates whether GLControl updates are synced to the monitor's refresh rate.")]
        public bool VSync
        {
            get
            {
                if (!IsHandleCreated)
                {
                    return initial_vsync_value.HasValue ?
                        initial_vsync_value.Value : true;
                }

                ValidateState();
                ValidateContext("VSync");
                return Context.SwapInterval != 0;
            }
            set
            {
                // The winforms designer sets this to false by default which forces control creation.
                // However, events are typically connected after the VSync = false assignment, which
                // can lead to "event xyz is not fired" issues.
                // Work around this issue by deferring VSync mode setting to the HandleCreated event.
                if (!IsHandleCreated)
                {
                    initial_vsync_value = value;
                    return;
                }

                ValidateState();
                ValidateContext("VSync");
                Context.SwapInterval = value ? 1 : 0;
            }
        }

        #endregion

        #region public GraphicsMode GraphicsMode

        /// <summary>
        /// Gets the <c>GraphicsMode</c> of the <c>IGraphicsContext</c> associated with
        /// this <c>GLControl</c>. If you wish to change <c>GraphicsMode</c>, you must
        /// destroy and recreate the <c>GLControl</c>.
        /// </summary>
        public GraphicsMode GraphicsMode
        {
            get
            {
                ValidateState();
                return Context.GraphicsMode;
            }
        }

        #endregion

        #region WindowInfo

        /// <summary>
        /// Gets the <see cref="OpenTK.Platform.IWindowInfo"/> for this instance.
        /// </summary>
        public IWindowInfo WindowInfo
        {
            get { return implementation.WindowInfo; }
        }

        #endregion

        #region public Bitmap GrabScreenshot()

        /// <summary>
        /// Grabs a screenshot of the frontbuffer contents.
        /// When using multiple <c>GLControl</c>s, ensure that  <see cref="Context"/>
        /// is current before accessing this property.
        /// <seealso cref="Context"/>
        /// <seealso cref="MakeCurrent"/>
        /// </summary>
        /// <returns>A System.Drawing.Bitmap, containing the contents of the frontbuffer.</returns>
        /// <exception cref="OpenTK.Graphics.GraphicsContextException">
        /// Occurs when no OpenTK.Graphics.GraphicsContext is current in the calling thread.
        /// </exception>
        [Obsolete("This method will not work correctly with OpenGL|ES. Please use GL.ReadPixels to capture the contents of the framebuffer (refer to http://www.opentk.com/doc/graphics/save-opengl-rendering-to-disk for more information).")]
        public Bitmap GrabScreenshot()
        {
            ValidateState();
            ValidateContext("GrabScreenshot()");

            Bitmap bmp = new Bitmap(this.ClientSize.Width, this.ClientSize.Height);
            System.Drawing.Imaging.BitmapData data =
                bmp.LockBits(this.ClientRectangle, System.Drawing.Imaging.ImageLockMode.WriteOnly,
                             System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            GL.ReadPixels(0, 0, this.ClientSize.Width, this.ClientSize.Height, PixelFormat.Bgr, PixelType.UnsignedByte,
                          data.Scan0);
            bmp.UnlockBits(data);
            bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
            return bmp;
        }

        #endregion

        #endregion
    }
}
