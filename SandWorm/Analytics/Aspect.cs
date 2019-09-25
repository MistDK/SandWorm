﻿using System.Collections.Generic;
using Rhino.Display;
using Rhino.Geometry;

namespace SandWorm.Analytics
{
    public class Aspect : Analysis.MeshColorVisualisation
    {
        public Aspect() : base("Visualise Aspect")
        {
        }

        public override int GetPixelIndexForAnalysis(Point3d vertex, List<Point3d> analysisPts)
        {
            return 44; // TODO: implementation
        }

        public override void ComputeLookupTableForAnalysis(double sensorElevation)
        {
            var rightAspect = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = 180,
                ColorStart = new ColorHSL(1.0, 1.0, 1.0), // White
                ColorEnd = new ColorHSL(1.0, 1.0, 0.3) // Dark Red
            };
            var leftAspect = new Analysis.VisualisationRangeWithColor
            {
                ValueSpan = 180, // For the other side of the aspect we loop back to the 0 value
                ColorStart = new ColorHSL(1.0, 1.0, 0.3), // Dark Red
                ColorEnd = new ColorHSL(1.0, 1.0, 1.0) // White
            };
            ComputeLinearRanges(rightAspect, leftAspect);
        }
    }
}