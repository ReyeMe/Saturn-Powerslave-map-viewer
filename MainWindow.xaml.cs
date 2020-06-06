namespace PowerslaveMapViewer
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Input;
    using System.Windows.Media.Media3D;
    using GL = SharpGL.OpenGL;
    using GLSG = SharpGL.SceneGraph;
    using MS = Microsoft.Win32;

    /// <summary>
    /// Main window logic
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        /// <summary>
        /// Perspective camera
        /// </summary>
        private readonly PerspectiveCamera camera = new PerspectiveCamera
        {
            Position = new Point3D(4.0, 4.0, 10.0),
            Target = new Point3D(0.0, 0.0, 0.0)
        };

        /// <summary>
        /// Is map loading
        /// </summary>
        private bool isMapNotLoading = true;

        /// <summary>
        /// Last mouse position
        /// </summary>
        private Point lastMousePosition;

        /// <summary>
        /// PowerSlave map data
        /// </summary>
        private Powerslave.Map map = null;

        /// <summary>
        /// Initializes a new instance of the <see cref="MainWindow"/> class
        /// </summary>
        public MainWindow()
        {
            this.DataContext = this;
            this.InitializeComponent();
        }

        /// <summary>
        /// Called when property changes
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Gets a value indicating whether map is loaded
        /// </summary>
        public bool IsMapLoaded
        {
            get
            {
                return this.map != null;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether map is loading
        /// </summary>
        public bool IsMapNotLoading
        {
            get
            {
                return this.isMapNotLoading;
            }

            set
            {
                this.isMapNotLoading = value;
                this.OnPropertyChanged("IsMapNotLoading");
            }
        }

        /// <summary>
        /// Get camera to plane distance
        /// </summary>
        /// <param name="point">Camera position</param>
        /// <param name="plane">Plane definition</param>
        /// <param name="map">Shared map data</param>
        /// <returns>Plane distance</returns>
        private static int GetDistance(Point3D point, Powerslave.Plane plane, Powerslave.Map map)
        {
            List<Powerslave.Vertex> vertices = plane.PolyVert.Select(vertex => map.Vertices[vertex]).ToList();
            Powerslave.Vertex center = new Powerslave.Vertex() { X = (short)(vertices.Sum(vertex => vertex.X) / 4), Y = (short)(vertices.Sum(vertex => vertex.Y) / 4), Z = (short)(vertices.Sum(vertex => vertex.Z) / 4) };
            return (int)(Math.Pow(point.X - (center.X / 10.0f), 2) + Math.Pow(point.Y - (center.Z / 10.0f), 2) + Math.Pow(point.Z - (center.Y / 10.0f), 2));
        }

        /// <summary>
        /// Export map to file
        /// </summary>
        /// <param name="sender">Button control</param>
        /// <param name="e">Empty event</param>
        private void ExportMap(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(this, "Not implemented yet!", ":(", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        /// <summary>
        /// Load map from file
        /// </summary>
        /// <param name="sender">Button control</param>
        /// <param name="e">Empty event</param>
        private void LoadMap(object sender, RoutedEventArgs e)
        {
            MS.OpenFileDialog openFileDialog = new MS.OpenFileDialog
            {
                Filter = "PowerSlave (Saturn) map|*.LEV"
            };

            if (openFileDialog.ShowDialog().Value)
            {
                if (this.map != null)
                {
                    if (this.map.Sky != null)
                    {
                        this.map.Sky.Dispose();
                    }

                    this.map = null;
                    this.OnPropertyChanged("IsMapLoaded");
                }

                this.camera.Position = new Point3D(4.0, 4.0, 10.0);
                this.camera.Target = new Point3D(0.0, 0.0, 0.0);
                this.IsMapNotLoading = false;

                new Thread(() =>
                {
                    Thread.CurrentThread.IsBackground = true;

                    try
                    {
                        Powerslave.Map loaded = Powerslave.Map.Load(openFileDialog.FileName);
                        loaded.Sky.Save("test.bmp");
                        this.Dispatcher.Invoke(() =>
                        {
                            this.map = loaded;
                            this.IsMapNotLoading = true;
                            this.OnPropertyChanged("IsMapLoaded");
                            this.SetCameraPosition();
                        });
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.Invoke(() =>
                        {
                            MessageBox.Show(this, ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            this.IsMapNotLoading = true;
                        });
                    }
                }).Start();
            }
        }

        /// <summary>
        /// Mouse moves
        /// </summary>
        /// <param name="sender">GL control</param>
        /// <param name="e">Mouse event</param>
        private void MouseMoves(object sender, MouseEventArgs e)
        {
            this.RotateCamera(e);
            this.lastMousePosition = this.glControl.PointToScreen(e.GetPosition(this.glControl));
        }

        /// <summary>
        /// CAll when property changes
        /// </summary>
        /// <param name="propertyName">Name of the property</param>
        private void OnPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        /// <summary>
        /// OpenGL is drawing on screen
        /// </summary>
        /// <param name="sender">OpenGL control</param>
        /// <param name="args">OpenGL event arguments</param>
        private void OpenGLDraw(object sender, GLSG.OpenGLEventArgs args)
        {
            if (Keyboard.IsKeyDown(Key.W) && this.IsActive)
            {
                Vector3D look = this.camera.GetLookDirection();
                this.camera.Position = this.camera.Position + (look * (Keyboard.IsKeyDown(Key.LeftShift) ? 1.0 : 0.5));
                this.camera.SetLookDirection(look);
            }
            else if (Keyboard.IsKeyDown(Key.S) && this.IsActive)
            {
                Vector3D look = this.camera.GetLookDirection();
                this.camera.Position = this.camera.Position + (look * -(Keyboard.IsKeyDown(Key.LeftShift) ? 1.0 : 0.5));
                this.camera.SetLookDirection(look);
            }

            args.OpenGL.Clear(GL.GL_COLOR_BUFFER_BIT | GL.GL_DEPTH_BUFFER_BIT);

            if (!this.IsMapNotLoading)
            {
                args.OpenGL.DrawText(5, 5, 1.0f, 1.0f, 1.0f, "Courier New", 12.0f, "Loading...");
            }
            else if (this.map != null)
            {
                this.camera.AspectRatio = ((GLControl)sender).ActualWidth / ((GLControl)sender).ActualHeight;
                this.camera.SetLookDirection(this.camera.GetLookDirection());
                this.camera.TransformProjectionMatrix(args.OpenGL);

                // Draw quads
                args.OpenGL.Begin(GL.GL_TRIANGLES);

                foreach (Powerslave.Plane plane in this.map.Planes
                    .Where(plane => !plane.Flags.HasFlag(Powerslave.PLaneFlags.Portal) && !plane.Flags.HasFlag(Powerslave.PLaneFlags.Invisible))
                    .OrderBy(plane => plane.Flags.HasFlag(Powerslave.PLaneFlags.Water))
                    .ThenByDescending(plane => MainWindow.GetDistance(this.camera.Position, plane, this.map)))
                {
                    plane.Draw(args.OpenGL, this.map, false, false);
                }

                args.OpenGL.End();
                args.OpenGL.Flush();

                // Draw Toggle planes (most are at the edges of triggers, also they are missing light level value)
                args.OpenGL.Begin(GL.GL_LINES);

                foreach (Powerslave.Plane plane in this.map.Planes.Where(plane => plane.Flags.HasFlag(Powerslave.PLaneFlags.Toggle)))
                {
                    plane.Draw(args.OpenGL, this.map, false, true);
                }

                args.OpenGL.End();
                args.OpenGL.Flush();

                args.OpenGL.DrawText(5, 5, 1.0f, 1.0f, 1.0f, "Courier New", 12.0f, Path.GetFileName(this.map.FileName));
            }
            else
            {
                args.OpenGL.DrawText(5, 5, 1.0f, 0.5f, 0.0f, "Courier New", 12.0f, "No map loaded!");
            }
        }

        /// <summary>
        /// OpenGL is initialized
        /// </summary>
        /// <param name="sender">OpenGL control</param>
        /// <param name="args">OpenGL event arguments</param>
        private void OpenGLInitialized(object sender, GLSG.OpenGLEventArgs args)
        {
            args.OpenGL.ClearColor(0.1f, 0.1f, 0.1f, 1.0f);
            args.OpenGL.Enable(GL.GL_BLEND);
            args.OpenGL.Enable(GL.GL_CULL_FACE);
            args.OpenGL.CullFace(GL.GL_FRONT);
            args.OpenGL.BlendFunc(GL.GL_SRC_ALPHA, GL.GL_ONE_MINUS_SRC_ALPHA);
            args.OpenGL.LineWidth(2.0f);
        }

        /// <summary>
        /// Rotate camera
        /// </summary>
        /// <param name="mouseEvent">Mouse event</param>
        private void RotateCamera(MouseEventArgs mouseEvent)
        {
            if (this.IsMouseOver && mouseEvent.LeftButton == MouseButtonState.Pressed)
            {
                Point lastPosition = this.glControl.PointFromScreen(this.lastMousePosition);
                Point currentPosition = mouseEvent.GetPosition(this.glControl);
                Point delta = (Point)((lastPosition - currentPosition) * 0.5);

                Vector3D direction = this.camera.GetLookDirection();
                Vector3D up = this.camera.Up;
                up.Normalize();
                Vector3D side = Vector3D.CrossProduct(direction, up);
                side.Normalize();
                up = Vector3D.CrossProduct(direction, side);
                up.Normalize();

                Point3D vectorBase = this.camera.Position + (direction * 15.0);
                Point3D rotationPoint = this.camera.Position;

                RotateTransform3D transform3D = new RotateTransform3D(new QuaternionRotation3D(new Quaternion(side, delta.Y)), rotationPoint);
                this.camera.Position = transform3D.Transform(this.camera.Position);
                vectorBase = transform3D.Transform(vectorBase);
                transform3D = new RotateTransform3D(new AxisAngleRotation3D(up, -delta.X), rotationPoint);
                this.camera.Position = transform3D.Transform(this.camera.Position);
                vectorBase = transform3D.Transform(vectorBase);
                Vector3D newLook = vectorBase - this.camera.Position;
                newLook.Normalize();

                this.camera.SetLookDirection(newLook);
            }
        }

        /// <summary>
        /// Set starting camera position
        /// </summary>
        private void SetCameraPosition()
        {
            if (this.map != null)
            {
                // Set camera to centroid of first sector
                Powerslave.Sector? sector = this.map.Sectors.FirstOrDefault();

                if (sector.HasValue)
                {
                    List<Powerslave.Vertex> vertices = this.map.Planes
                        .Skip(sector.Value.FaceStart)
                        .Take(sector.Value.FaceEnd - sector.Value.FaceStart)
                        .SelectMany(plane => plane.PolyVert.Select(vertex => this.map.Vertices[vertex]))
                        .ToList();

                    this.camera.Position = new Point3D(
                        vertices.Sum(vertex => vertex.X / 10.0f) / vertices.Count,
                        vertices.Sum(vertex => vertex.Z / 10.0f) / vertices.Count,
                        vertices.Sum(vertex => vertex.Y / 10.0f) / vertices.Count);

                    this.camera.SetLookDirection(new Vector3D(1.0, 0.0, 0.0));
                }
            }
        }

        /// <summary>
        /// Zoom camera
        /// </summary>
        /// <param name="sender">Mouse wheel</param>
        /// <param name="e">Mouse wheel event</param>
        private void ZoomInOut(object sender, MouseWheelEventArgs e)
        {
            double delta = e.Delta * (1.0 / 120.0);
            Vector3D look = this.camera.GetLookDirection();
            this.camera.Position = this.camera.Position + (look * delta);
            this.camera.SetLookDirection(look);
        }
    }
}