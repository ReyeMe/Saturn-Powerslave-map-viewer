namespace PowerslaveMapViewer
{
    using System.Windows.Media.Media3D;
    using GL = SharpGL.OpenGL;

    /// <summary>
    /// Perspective camera wrapper
    /// </summary>
    internal class PerspectiveCamera
    {
        /// <summary>
        /// Gets or sets aspect ratio
        /// </summary>
        internal double AspectRatio { get; set; } = 1.0;

        /// <summary>
        /// Gets or sets far clip
        /// </summary>
        internal double Far { get; set; } = 400.0;

        /// <summary>
        /// Gets or sets field of view
        /// </summary>
        internal double FieldOfView { get; set; } = 60.0;

        /// <summary>
        /// Gets or sets near clip
        /// </summary>
        internal double Near { get; set; } = 0.5;

        /// <summary>
        /// Gets or sets camera position
        /// </summary>
        internal Point3D Position { get; set; } = new Point3D();

        /// <summary>
        /// Gets or sets camera target
        /// </summary>
        internal Point3D Target { get; set; } = new Point3D(0.0, 0.0, -1.0);

        /// <summary>
        /// Gets or sets up vector
        /// </summary>
        internal Vector3D Up { get; set; } = new Vector3D(0.0, 0.0, 1.0);

        /// <summary>
        /// Get look direction
        /// </summary>
        /// <returns>Direction vector</returns>
        internal Vector3D GetLookDirection()
        {
            Vector3D look = this.Target - this.Position;
            look.Normalize();
            return look;
        }

        /// <summary>
        /// Set look direction (Use always after setting position)
        /// </summary>
        /// <param name="direction">Look direction</param>
        internal void SetLookDirection(Vector3D direction)
        {
            this.Target = this.Position + direction;
        }

        /// <summary>
        /// Transform projection matrix
        /// </summary>
        /// <param name="gl">GL instance</param>
        internal void TransformProjectionMatrix(GL gl)
        {
            gl.MatrixMode(GL.GL_PROJECTION);
            gl.LoadIdentity();

            // Perform the perspective transformation
            gl.Perspective(this.FieldOfView, this.AspectRatio, this.Near, this.Far);

            // Perform the look at transformation
            gl.LookAt(this.Position.X, this.Position.Y, this.Position.Z, this.Target.X, this.Target.Y, this.Target.Z, this.Up.X, this.Up.Y, this.Up.Z);

            gl.MatrixMode(GL.GL_MODELVIEW);
        }
    }
}