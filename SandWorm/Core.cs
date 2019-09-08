﻿using Rhino.Geometry;
using Rhino.Display;
using System.Drawing;
using System;

namespace SandWorm
{
    public static class Core
    {
        public static Mesh CreateQuadMesh(Mesh mesh, Point3f[] vertices, Color[] colors, int xStride, int yStride)
        {
            int xd = xStride;       // The x-dimension of the data
            int yd = yStride;       // They y-dimension of the data


            if (mesh.Faces.Count != (xStride - 2) * (yStride - 2))
            {
                SandWorm.output.Add("Face remeshing");
                mesh = new Mesh();
                mesh.Vertices.Capacity = vertices.Length;      // Don't resize array
                mesh.Vertices.UseDoublePrecisionVertices = true;
                mesh.Vertices.AddVertices(vertices);       

                for (int y = 1; y < yd - 1; y++)       // Iterate over y dimension
                {
                    for (int x = 1; x < xd - 1; x++)       // Iterate over x dimension
                    {
                        int i = y * xd + x;
                        int j = (y - 1) * xd + x;

                        mesh.Faces.AddFace(j - 1, j, i, i - 1);
                    }
                }
            }
            else
            {
                mesh.Vertices.Clear();
                mesh.Vertices.UseDoublePrecisionVertices = true; 
                mesh.Vertices.AddVertices(vertices);       
            }

            if (colors.Length > 0) // Colors only provided if the mesh style permits
            {
                mesh.VertexColors.SetColors(colors); 
            }
            return mesh;
        }

        public static Color[] ComputeLookupTable(int waterLevel, Color start, Color end, int span, Color[] lookupTable)
        {
            /*
            //precompute all vertex colors
            int j = 0;
            for (int i = waterLevel; i < lookupTable.Length; i++) //below water level
            {
                lookupTable[i] = new ColorHSL(0.6, 0.6, 0.60 - (j * 0.02)).ToArgbColor();
                j++;
            }

            j = 0;
            for (int i = waterLevel; i > 0; i--) //above water level
            {
                lookupTable[i] = new ColorHSL(0.01 + (j * 0.01), 1.0, 0.5).ToArgbColor();
                j++;
            }
            */
            int rMin = start.R;
            int rMax = end.R;
            int gMin = start.G;
            int gMax = end.G;
            int bMin = start.B;
            int bMax = end.B;
            int aMin = start.A;
            int aMax = end.A;
            int size = lookupTable.Length;
            int j = 0;
            for (int i = waterLevel; i < size; i++)
            {
                lookupTable[i] = new ColorHSL(0.6, 0.6, 0.60 - (j * 0.02)).ToArgbColor();
                j++;
            }

            j = 0;
            for (int i = waterLevel; i > 0; i--) //above water level
            {
                var rAverage = rMin + (int)((rMax - rMin) * j / span);
                var gAverage = gMin + (int)((gMax - gMin) * j / span);
                var bAverage = bMin + (int)((bMax - bMin) * j / span);
                var aAverage = aMin + (int)((aMax - aMin) * j / span);

                lookupTable[i] = Color.FromArgb(aAverage, rAverage, gAverage, bAverage);
                if (j == span)
                {
                    j = 0;
                }
                else
                {
                    j++;
                }
            }

            return lookupTable;
        }

        public struct PixelSize // Unfortunately no nice tuples in this version of C# :(
        {
            public double x;
            public double y;
        }

        public static PixelSize GetDepthPixelSpacing(double sensorHeight)
        {
            double kinect2FOVForX = 70.6; 
            double kinect2FOVForY = 60.0;
            double kinect2ResolutionForX = 512;
            double kinect2ResolutionForY = 404;

            PixelSize pixelsForHeight = new PixelSize
            {
                x = GetDepthPixelSizeInDimension(kinect2FOVForX, kinect2ResolutionForX, sensorHeight),
                y = GetDepthPixelSizeInDimension(kinect2FOVForY, kinect2ResolutionForY, sensorHeight)
            };
            return pixelsForHeight;
        }

        private static double GetDepthPixelSizeInDimension(double fovAngle, double resolution, double height)
        {
            double fovInRadians = (Math.PI / 180) * fovAngle;
            double dimensionSpan = 2 * height * Math.Tan(fovInRadians / 2);
            return dimensionSpan / resolution;
        }
    }
}
