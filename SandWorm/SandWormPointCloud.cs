﻿using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics; //debugging
using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
using Rhino.Geometry;
using Microsoft.Kinect;
using System.Windows.Forms;
// comment 
// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

//Test comment
namespace SandWorm
{
    public class SandWormPointCloud : GH_Component
    {
        private KinectSensor kinectSensor = null;
        private GH_Point[] pointCloud;

        public static List<String> output = null; //debugging
        private Queue<ushort[]> renderBuffer = new Queue<ushort[]>();
        public static int depthPoint;

        public double sensorElevation = 1000; // Arbitrary default value (must be >0)
        public int leftColumns = 0;
        public int rightColumns = 0;
        public int topRows = 0;
        public int bottomRows = 0;
        public int tickRate = 20; // In ms
        public int averageFrames = 1;
        public int blurRadius = 1;
        public static Rhino.UnitSystem units = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem;
        public static double unitsMultiplier;


        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SandWormPointCloud()
          : base("SandWorm", "SandWorm",
              "Kinect v2 Augmented Reality Sandbox",
              "Sandworm", "Sandbox")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddNumberParameter("SensorHeight", "SH", "The height (in document units) of the sensor above your model", GH_ParamAccess.item, sensorElevation);
            pManager.AddIntegerParameter("LeftColumns", "LC", "Number of columns to trim from the left", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("RightColumns", "RC", "Number of columns to trim from the right", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("TopRows", "TR", "Number of rows to trim from the top", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("BottomRows", "BR", "Number of rows to trim from the bottom", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("TickRate", "TR", "The time interval, in milliseconds, to update geometry from the Kinect. Set as 0 to disable automatic updates.", GH_ParamAccess.item, tickRate);
            pManager.AddIntegerParameter("AverageFrames", "AF", "Amount of depth frames to average across. This number has to be greater than zero.", GH_ParamAccess.item, averageFrames);
            pManager.AddIntegerParameter("BlurRadius", "BR", "Radius for the gaussian blur.", GH_ParamAccess.item, blurRadius);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
            pManager[7].Optional = true;

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddPointParameter ("PointCloud", "PC", "Resulting PointCloud", GH_ParamAccess.list);
            pManager.AddTextParameter("Output", "O", "Output", GH_ParamAccess.list); //debugging
        }



        private void ScheduleDelegate(GH_Document doc)
        {
            ExpireSolution(false);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object can be used to retrieve data from input parameters and 
        /// to store data in output parameters.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            DA.GetData<double>(0, ref sensorElevation);
            DA.GetData<int>(1, ref leftColumns);
            DA.GetData<int>(2, ref rightColumns);
            DA.GetData<int>(3, ref topRows);
            DA.GetData<int>(4, ref bottomRows);
            DA.GetData<int>(5, ref tickRate);
            DA.GetData<int>(6, ref averageFrames);
            DA.GetData<int>(7, ref blurRadius);

            switch (units.ToString())
            {
                case "Kilometers":
                    unitsMultiplier = 0.0001;
                    break;

                case "Meters":
                    unitsMultiplier = 0.001;
                    break;

                case "Decimeters":
                    unitsMultiplier = 0.01;
                    break;

                case "Centimeters":
                    unitsMultiplier = 0.1;
                    break;

                case "Millimeters":
                    unitsMultiplier = 1;
                    break;

                case "Inches":
                    unitsMultiplier = 0.0393701;
                    break;

                case "Feet":
                    unitsMultiplier = 0.0328084;
                    break;
            }
            sensorElevation /= unitsMultiplier; // Standardise to mm to match sensor units

            Stopwatch timer = Stopwatch.StartNew(); //debugging


            if (this.kinectSensor == null)
            {
                KinectController.AddRef();
                this.kinectSensor = KinectController.sensor;
            }


            if (this.kinectSensor != null)
            {
                if (KinectController.depthFrameData != null)
                {
                    pointCloud = new GH_Point[(KinectController.depthHeight - topRows - bottomRows) * (KinectController.depthWidth - leftColumns - rightColumns)];
                    Point3f tempPoint = new Point3f();
                    output = new List<String>(); //debugging
                    Core.PixelSize depthPixelSize = Core.getDepthPixelSpacing(sensorElevation);


                    if (blurRadius > 1)
                    {
                        var gaussianBlur = new GaussianBlur(KinectController.depthFrameData);
                        var blurredFrame = gaussianBlur.Process(blurRadius, KinectController.depthWidth, KinectController.depthHeight);

                        renderBuffer.Enqueue(blurredFrame);
                    }
                    else
                    {
                        renderBuffer.Enqueue(KinectController.depthFrameData);
                    }


                    int arrayIndex = 0;
                    for (int rows = topRows; rows < KinectController.depthHeight - bottomRows; rows++)

                    {
                        for (int columns = rightColumns; columns < KinectController.depthWidth - leftColumns; columns++)
                        {

                            int i = rows * KinectController.depthWidth + columns;

                            tempPoint.X = (float)(columns * -unitsMultiplier * depthPixelSize.x);
                            tempPoint.Y = (float)(rows * -unitsMultiplier * depthPixelSize.y);

                            if (averageFrames > 1)
                            {
                                int depthPointRunningSum = 0;
                                foreach (var frame in renderBuffer)
                                {
                                    depthPointRunningSum += frame[i];
                                }
                                depthPoint = depthPointRunningSum / renderBuffer.Count;
                            }
                            else
                            {
                                depthPoint = KinectController.depthFrameData[i];
                            }

                            tempPoint.Z = (float)((depthPoint - sensorElevation) * -unitsMultiplier);
                            pointCloud[arrayIndex] = new GH_Point(tempPoint);
                            arrayIndex++;
                        }
                    };
                    

                    //keep only the desired amount of frames in the buffer
                    while (renderBuffer.Count >= averageFrames && averageFrames > 0)
                    {
                        renderBuffer.Dequeue();
                    }

                    //debugging
                    timer.Stop();
                    output.Add("Point Cloud generation: " + timer.ElapsedMilliseconds.ToString() + " ms");

                }

                DA.SetDataList(0, pointCloud);
                DA.SetDataList(1, output); //debugging
            }

            if (tickRate > 0) // Allow users to force manual recalculation
            {
                base.OnPingDocument().ScheduleSolution(tickRate, new GH_Document.GH_ScheduleDelegate(ScheduleDelegate));
            }
        }

        /// <summary>
        /// Provides an Icon for every component that will be visible in the User Interface.
        /// Icons need to be 24x24 pixels.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                // You can add image files to your project resources and access them like this:
                //return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Each component must have a unique Guid to identify it. 
        /// It is vital this Guid doesn't change otherwise old ghx files 
        /// that use the old ID will partially fail during loading.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("b609c74e-0a15-4e78-8a23-3709b223f809"); }
        }
    }
}
