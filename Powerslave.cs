namespace PowerslaveMapViewer
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Windows.Media.Media3D;

    /// <summary>
    /// Powerslave map data types and methods
    /// </summary>
    public static class Powerslave
    {
        /// <summary>
        /// Plane flags (TODO: figure out missing, document)
        /// </summary>
        [Flags]
        public enum PLaneFlags : ushort
        {
            None = 0,
            Solid = 0x0001,
            Water = 0x0002,
            unknown1 = 0x0004,
            Trigger = 0x0008,
            Invisible = 0x0010,
            unknown2 = 0x0020,
            Unknown3 = 0x0040,
            Breakable = 0x0080,
            Toggle = 0x0100,
            Portal = 0x0200,
            Unknown4 = 0x0400,
            Unknown5 = 0x0800,
            Unknown6 = 0x1000,
            Unknown7 = 0x2000,
            Sky = 0x4000,
            Lava = 0x8000,
        }

        /// <summary>
        /// Sector flags (TODO: figure out missing, document)
        /// </summary>
        [Flags]
        public enum SectorFlags : ushort
        {
            unknown1 = 0,
            unknown2 = 0x0001,
            unknown3 = 0x0002,
            unknown4 = 0x0004,
            unknown5 = 0x0008,
            unknown6 = 0x0010,
            unknown7 = 0x0020,
            unknown8 = 0x0040,
            unknown9 = 0x0080,
            unknown10 = 0x0100,
            unknown11 = 0x0200,
            unknown12 = 0x0400,
            unknown13 = 0x0800,
            unknown14 = 0x1000,
            unknown15 = 0x2000,
            unknown16 = 0x4000,
            unknown17 = 0x8000,
        }

        /// <summary>
        /// Fix endianness
        /// </summary>
        /// <typeparam name="DataType">Type of the struct to fix endianness for</typeparam>
        /// <param name="data">Struct bytes</param>
        /// <param name="startOffset">Field byte offset</param>
        private static void FixEndianness<DataType>(byte[] data, int startOffset = 0) where DataType : struct
        {
            List<FieldInfo> fields = typeof(DataType).GetFields().Where(field => !field.IsStatic && field.FieldType != typeof(string)).ToList();

            if (!fields.Any())
            {
                int size = Marshal.SizeOf(typeof(DataType));
                Array.Reverse(data, startOffset, size);
            }

            foreach (FieldInfo field in fields)
            {
                Type fieldType = field.FieldType;

                int offset = Marshal.OffsetOf(typeof(DataType), field.Name).ToInt32() + startOffset;

                if (fieldType.IsEnum)
                {
                    fieldType = Enum.GetUnderlyingType(fieldType);
                }
                else if (fieldType.IsArray)
                {
                    MarshalAsAttribute attribute = field.GetCustomAttribute(typeof(MarshalAsAttribute)) as MarshalAsAttribute;
                    fieldType = fieldType.GetElementType();

                    if (attribute != null)
                    {
                        int size = Marshal.SizeOf(fieldType);
                        int count = attribute.SizeConst;

                        for (int counter = 0; counter < count; counter++)
                        {
                            typeof(Powerslave)
                                .GetMethod("FixEndianness", BindingFlags.NonPublic | BindingFlags.Static)
                                .MakeGenericMethod(fieldType)
                                .Invoke(null, new object[] { data, offset + (counter * size) });
                        }
                    }
                }
                else
                {
                    List<FieldInfo> sub = fieldType.GetFields().Where(subField => !subField.IsStatic && field.FieldType != typeof(string)).ToList();

                    if (sub.Any())
                    {
                        typeof(Powerslave)
                            .GetMethod("FixEndianness", BindingFlags.NonPublic | BindingFlags.Static)
                            .MakeGenericMethod(fieldType)
                            .Invoke(null, new object[] { data, offset });
                    }
                    else
                    {
                        int size = Marshal.SizeOf(fieldType);
                        Array.Reverse(data, offset, size);
                    }
                }
            }
        }

        /// <summary>
        /// Get triangles from quad
        /// </summary>
        /// <param name="quad">Quad polygon</param>
        /// <returns>List of triangles</returns>
        private static List<Powerslave.Vertex> GetTrianglesFromQuad(List<Powerslave.Vertex> quad)
        {
            List<List<Powerslave.Vertex>> triangles = new List<List<Powerslave.Vertex>>
            {
                new List<Powerslave.Vertex> { quad[0], quad[1], quad[2] },
                new List<Powerslave.Vertex> { quad[2], quad[3], quad[0] }
            };

            // Remove zero-area triangles
            for (int triangle = 0; triangle < triangles.Count; triangle++)
            {
                bool foundSame = false;

                for (int index = 0; index < 3 && !foundSame; index++)
                {
                    Powerslave.Vertex toCheck = triangles[triangle][index];

                    for (int compare = 0; compare < 3 && !foundSame; compare++)
                    {
                        Powerslave.Vertex toCompare = triangles[triangle][compare];
                        foundSame = index != compare && toCompare.X == toCheck.X && toCompare.Y == toCheck.Y && toCompare.Z == toCheck.Z;
                    }
                }

                if (foundSame)
                {
                    triangles.RemoveAt(triangle);
                    triangle--;
                }
            }

            return triangles.SelectMany(triangle => triangle).ToList();
        }

        /// <summary>
        /// Read struct from <see cref="FileStream"/>
        /// </summary>
        /// <typeparam name="DataType">Type of the struct to read</typeparam>
        /// <param name="stream">The <see cref="FileStream"/></param>
        /// <returns>Loaded struct</returns>
        private static DataType LoadStruct<DataType>(FileStream stream) where DataType : struct
        {
            DataType parsed = new DataType();
            int size = Marshal.SizeOf(parsed);
            IntPtr target = Marshal.AllocHGlobal(size);

            // Read bytes
            byte[] data = new byte[size];
            stream.Read(data, 0, size);
            Powerslave.FixEndianness<DataType>(data);

            // Convert bytes to struct
            Marshal.Copy(data, 0, target, size);
            parsed = (DataType)Marshal.PtrToStructure(target, typeof(DataType));
            Marshal.FreeHGlobal(target);

            return parsed;
        }

        /// <summary>
        /// Set vertex color
        /// </summary>
        /// <param name="gl">OpenGL isntance</param>
        /// <param name="planeflags">Plane flag</param>
        /// <param name="lightLevel">Vertex light level</param>
        private static void SetVertexColor(SharpGL.OpenGL gl, Powerslave.PLaneFlags planeflags, byte lightLevel)
        {
            float brightness = Math.Min(lightLevel, (byte)16) / 16.0f;

            if (planeflags.HasFlag(Powerslave.PLaneFlags.Water))
            {
                gl.Color(brightness * 0.2f, brightness * 0.4f, brightness * 1.0f, 0.5f);
            }
            else if (planeflags.HasFlag(Powerslave.PLaneFlags.Lava))
            {
                gl.Color(0.7f, 0.2f, 0.2f);
            }
            else if (planeflags.HasFlag(Powerslave.PLaneFlags.Sky))
            {
                gl.Color(0.0f, 0.3f, 1.0f);
            }
            else if (planeflags.HasFlag(Powerslave.PLaneFlags.Breakable))
            {
                gl.Color(brightness * 1.0f, brightness * 1.0f, brightness * 0.2f);
            }
            else
            {
                gl.Color(brightness, brightness, brightness);
            }
        }

        /// <summary>
        /// Map file header (should start at 0x2070C)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct FileHeader
        {
            /// <summary>
            /// Number of map sectors
            /// </summary>
            public uint SectorCount;

            /// <summary>
            /// Number of planes
            /// </summary>
            public uint PlaneCount;

            /// <summary>
            /// Number of vertices
            /// </summary>
            public uint VertexCount;

            /// <summary>
            /// Number of quads
            /// </summary>
            public uint QuadCount;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown1;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown2;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown3;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown4;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown5;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown6;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown7;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown8;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown9;

            /// <summary>
            /// Unknown data
            /// </summary>
            public uint Unknown10;
        }

        /// <summary>
        /// Plane data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Plane
        {
            /// <summary>
            /// Plane normal
            /// </summary>
            public Powerslave.Vector Normal;

            /// <summary>
            /// Plane angle (probably to Z axis)
            /// </summary>
            public int Angle;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown1;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown2;

            /// <summary>
            /// Plane flags (wall, door, platform, etc)
            /// </summary>
            public Powerslave.PLaneFlags Flags;

            /// <summary>
            /// Identifier of some texture maybe? (most of the time empty)
            /// </summary>
            public ushort GenTextureID;

            /// <summary>
            /// Inclusive start of quad list
            /// </summary>
            public short PolyStart;

            /// <summary>
            /// Inclusive end of quad list
            /// </summary>
            public short PolyEnd;

            /// <summary>
            /// Quad vertex offset start
            /// </summary>
            public ushort VertexStart;

            /// <summary>
            /// Quad vertex offset end
            /// </summary>
            public ushort VertexEnd;

            /// <summary>
            /// Plane vertices
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] PolyVert;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown3;

            /// <summary>
            /// Lookup table identifier
            /// </summary>
            public ushort Lookup;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown4;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown5;

            /// <summary>
            /// Draw plane (Must be inside GL_TRIANGLES or GL_LINES for wireframe)
            /// </summary>
            /// <param name="gl">OpenGL isntance</param>
            /// <param name="map">Shared map data</param>
            /// <param name="ignoreTiled">Do not draw tiled quads in planes</param>
            /// <param name="wireframe">Draw as wireframe</param>
            public void Draw(SharpGL.OpenGL gl, Powerslave.Map map, bool ignoreTiled, bool wireframe)
            {
                if (this.PolyStart != -1 && this.PolyEnd != -1 && !ignoreTiled)
                {
                    Vector3D planeNormal = new Vector3D(this.Normal.X / (double)short.MaxValue, this.Normal.Z / (double)short.MaxValue, this.Normal.Y / (double)short.MaxValue);
                    int start = Math.Min(this.PolyStart, this.PolyEnd);
                    int end = Math.Max(this.PolyStart, this.PolyEnd);

                    for (int polygon = start; polygon <= end; polygon++)
                    {
                        map.Quads[polygon].Draw(gl, map, this, planeNormal, wireframe);
                    }
                }
                else
                {
                    List<Powerslave.Vertex> vertices = this.PolyVert.Select(index => map.Vertices[index]).ToList();

                    foreach (Powerslave.Vertex point in wireframe ? vertices.SelectMany((vertex, index) => new List<Powerslave.Vertex> { vertex, vertices[(index + 1) % vertices.Count] }) : Powerslave.GetTrianglesFromQuad(vertices))
                    {
                        if (wireframe)
                        {
                            gl.Color(1.0f, 1.0f, 1.0f);
                        }
                        else
                        {
                            Powerslave.SetVertexColor(gl, this.Flags, point.Lightlevel);
                        }

                        gl.Vertex(point.X / 10.0f, point.Z / 10.0f, point.Y / 10.0f);
                    }
                }
            }
        }

        /// <summary>
        /// Polygon data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Quad
        {
            /// <summary>
            /// Quad vertices
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public ushort[] Indices;

            /// <summary>
            /// Unknown value
            /// </summary>
            public byte Unknown1;

            /// <summary>
            /// Unknown value
            /// </summary>
            public byte Unknown2;

            /// <summary>
            /// Draw quad (Must be inside GL_TRIANGLES or GL_LINES for wireframe)
            /// </summary>
            /// <param name="gl">OpenGL isntance</param>
            /// <param name="map">Shared map data</param>
            /// <param name="plane">Plane this quad belongs to</param>
            /// <param name="planeNormal">Normal vector of the plane</param>
            /// <param name="wireframe">Draw as wireframe</param>
            public void Draw(SharpGL.OpenGL gl, Powerslave.Map map, Powerslave.Plane plane, Vector3D planeNormal, bool wireframe)
            {
                List<Powerslave.Vertex> quad = this.Indices.Select(index => map.Vertices[index + plane.VertexStart]).ToList();

                if (Vector3D.DotProduct(planeNormal, Quad.GetQuadNormal(quad)) < 0.0f)
                {
                    // Quad is rotated incorrectly, reverse it (might be used as a texture flip)
                    quad.Reverse();
                }

                foreach (Powerslave.Vertex point in wireframe ? quad.SelectMany((vertex, index) => new List<Powerslave.Vertex> { vertex, quad[(index + 1) % quad.Count] }) : Powerslave.GetTrianglesFromQuad(quad))
                {
                    if (wireframe)
                    {
                        gl.Color(1.0f, 1.0f, 1.0f);
                    }
                    else
                    {
                        Powerslave.SetVertexColor(gl, plane.Flags, point.Lightlevel);
                    }

                    gl.Vertex(point.X / 10.0f, point.Z / 10.0f, point.Y / 10.0f);
                }
            }

            /// <summary>
            /// Get quad normal
            /// </summary>
            /// <param name="quad">Quad polygon</param>
            /// <returns>Quad normal</returns>
            private static Vector3D GetQuadNormal(List<Powerslave.Vertex> quad)
            {
                List<Vector3D> mediaVector = quad.Select(vertex => new Vector3D(vertex.X, vertex.Z, vertex.Y)).ToList();
                Vector3D second = mediaVector.FirstOrDefault(vector => (vector - mediaVector.First()).Length > 0.01);
                Vector3D normal = new Vector3D();

                if (second != null)
                {
                    Vector3D third = mediaVector.FirstOrDefault(vector => (vector - mediaVector.First()).Length > 0.01 && (vector - second).Length > 0.01);

                    if (third != null)
                    {
                        normal = Vector3D.CrossProduct(third - mediaVector.First(), second - mediaVector.First());
                        normal /= normal.Length;
                    }
                }

                return normal;
            }
        }

        /// <summary>
        /// Sector data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Sector
        {
            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown1;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown2;

            /// <summary>
            /// Sector ceiling slope
            /// </summary>
            public short CeilingSlope;

            /// <summary>
            /// Sector floor slope
            /// </summary>
            public short FloorSlope;

            /// <summary>
            /// Sector ceiling height
            /// </summary>
            public short CeilingHeight;

            /// <summary>
            /// Sector floor height
            /// </summary>
            public short FloorHeight;

            /// <summary>
            /// Inclusive start face index
            /// </summary>
            public short FaceStart;

            /// <summary>
            /// Inclusive last face index
            /// </summary>
            public short FaceEnd;

            /// <summary>
            /// Unknown value
            /// </summary>
            public ushort Unknown3;

            /// <summary>
            /// Sector flag (wall, door, platform, etc)
            /// </summary>
            public Powerslave.SectorFlags Flags;

            /// <summary>
            /// Unknown value (always 128?)
            /// </summary>
            public short Unknown4;

            /// <summary>
            /// Unknown value
            /// </summary>
            public short Unknown5;
        }

        /// <summary>
        /// Sky box data (at the start of the file)
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Skybox
        {
            /// <summary>
            /// Color pallet RGB555, 256 colors
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = byte.MaxValue + 1)]
            public ushort[] Pallet;

            /// <summary>
            /// Bitmap image (512x256)
            /// </summary>
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = ((byte.MaxValue + 1) * 2) * (byte.MaxValue + 1))]
            public byte[] Bitmap;
        }

        /// <summary>
        /// Fixed point vector
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Vector
        {
            /// <summary>
            /// X component
            /// </summary>
            public int X;

            /// <summary>
            /// Y component
            /// </summary>
            public int Y;

            /// <summary>
            /// Z component
            /// </summary>
            public int Z;
        }

        /// <summary>
        /// Vertex data
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct Vertex
        {
            /// <summary>
            /// X coordinate
            /// </summary>
            public short X;

            /// <summary>
            /// Y coordinate
            /// </summary>
            public short Y;

            /// <summary>
            /// Z coordinate
            /// </summary>
            public short Z;

            /// <summary>
            /// Light level
            /// </summary>
            public byte Lightlevel;

            /// <summary>
            /// Unknown value
            /// </summary>
            public byte Unknown1;
        }

        /// <summary>
        /// PowerSlave map
        /// </summary>
        public sealed class Map
        {
            /// <summary>
            /// Prevents a default instance of the <see cref="Map"/> class from being created
            /// </summary>
            private Map()
            {
                // Do nothing here
            }

            /// <summary>
            /// Map file name
            /// </summary>
            public string FileName { get; private set; }

            /// <summary>
            /// Gets map floor, celling and wall planes
            /// </summary>
            public List<Powerslave.Plane> Planes { get; private set; }

            /// <summary>
            /// Gets all quads (Coordinate precision with these is questionable at best)
            /// </summary>
            public List<Powerslave.Quad> Quads { get; private set; }

            /// <summary>
            /// Gets map sectors
            /// </summary>
            public List<Powerslave.Sector> Sectors { get; private set; }

            /// <summary>
            /// Gets sky texture (512x256)
            /// </summary>
            public Bitmap Sky { get; private set; }

            /// <summary>
            /// Gets map geometry vertices
            /// </summary>
            public List<Powerslave.Vertex> Vertices { get; private set; }

            /// <summary>
            /// Load map data
            /// </summary>
            /// <param name="file">Path to the file</param>
            /// <returns>Loaded <see cref="Map"/></returns>
            public static Map Load(string file)
            {
                Exception loadingException = null;
                Map map = new Map { FileName = file };

                using (FileStream stream = File.OpenRead(file))
                {
                    try
                    {
                        // Load sky texture first
                        map.Sky = Map.LoadSky(stream);

                        // Load map header (skip 1292 bytes of unknown/empty space)
                        stream.Position = 0x2070C;
                        Powerslave.FileHeader header = Powerslave.LoadStruct<Powerslave.FileHeader>(stream);

                        // Load map data
                        map.Sectors = Map.CreateSequence(() => Powerslave.LoadStruct<Powerslave.Sector>(stream), header.SectorCount).ToList();
                        map.Planes = Map.CreateSequence(() => Powerslave.LoadStruct<Powerslave.Plane>(stream), header.PlaneCount).ToList();
                        map.Vertices = Map.CreateSequence(() => Powerslave.LoadStruct<Powerslave.Vertex>(stream), header.VertexCount).ToList();
                        map.Quads = Map.CreateSequence(() => Powerslave.LoadStruct<Powerslave.Quad>(stream), header.QuadCount).ToList();
                    }
                    catch (Exception ex)
                    {
                        loadingException = ex;

                        if (map.Sky != null)
                        {
                            map.Sky.Dispose();
                        }
                    }
                }

                if (loadingException != null)
                {
                    throw loadingException;
                }

                return map;
            }

            /// <summary>
            /// Create sequence of N items
            /// </summary>
            /// <typeparam name="T">Item type</typeparam>
            /// <param name="itemCreator">Item creator</param>
            /// <param name="count">Number of items in sequence</param>
            /// <returns>Created sequence</returns>
            private static IEnumerable<T> CreateSequence<T>(Func<T> itemCreator, uint count)
            {
                if (itemCreator == null)
                {
                    throw new ArgumentNullException("Creator cannot be NULL");
                }

                for (uint counter = 0; counter < count; ++counter)
                {
                    yield return itemCreator();
                }
            }

            /// <summary>
            /// Load sky texture
            /// </summary>
            /// <param name="stream">The <see cref="FileStream"/></param>
            /// <returns>Sky texture</returns>
            private static Bitmap LoadSky(FileStream stream)
            {
                Powerslave.Skybox skyData = Powerslave.LoadStruct<Powerslave.Skybox>(stream);
                List<Color> pallet = skyData.Pallet.Select(rgb => Color.FromArgb((byte)(((rgb >> 0) & 31) * 8), (byte)(((rgb >> 5) & 31) * 8), (byte)((rgb >> 10 & 31) * 8))).ToList();

                Bitmap sky = new Bitmap((byte.MaxValue + 1) * 2, byte.MaxValue + 1);

                for (int y = 0; y < sky.Height; y++)
                {
                    for (int x = 0; x < sky.Width; x++)
                    {
                        sky.SetPixel(x, y, pallet[skyData.Bitmap[(y * sky.Width) + x]]);
                    }
                }

                // Bitmaps are flipped over Y axis
                sky.RotateFlip(RotateFlipType.RotateNoneFlipY);
                return sky;
            }
        }
    }
}