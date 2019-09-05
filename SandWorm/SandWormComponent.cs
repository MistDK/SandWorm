﻿using System;
using System.Drawing;
using System.Collections.Generic;
using System.Diagnostics; //debugging
using Grasshopper.Kernel;
using Rhino.Geometry;
using Microsoft.Kinect;
// comment 
// In order to load the result of this wizard, you will also need to
// add the output bin/ folder of this project to the list of loaded
// folder in Grasshopper.
// You can use the _GrasshopperDeveloperSettings Rhino command for that.

//Test comment
namespace SandWorm
{
    public class SandWorm : GH_Component
    {
        private KinectSensor kinectSensor = null;
        private List<Point3f> pointCloud = null;
        private List<Mesh> outputMesh = null;
        public static List<String> output = null;//debugging
        private Queue<ushort[]> renderBuffer = new Queue<ushort[]>();


        public static int depthPoint;
        public static Color[] lookupTable = new Color[1500]; //to do - fix arbitrary value assuming 1500 mm as max distance from the kinect sensor
        public List<Color> vertexColors;
        public Mesh quadMesh = new Mesh();

        public int waterLevel;
        public double sensorElevation = 1060; //to do - fix hard wiring
        public int leftColumns = 0;
        public int rightColumns = 0;
        public int topRows = 0;
        public int bottomRows = 0;
        public int tickRate = 20; // In ms
        public int averageFrames = 1;
        public static Rhino.UnitSystem units = Rhino.RhinoDoc.ActiveDoc.ModelUnitSystem;
        public static double unitsMultiplier;


        /// <summary>
        /// Each implementation of GH_Component must provide a public 
        /// constructor without any arguments.
        /// Category represents the Tab in which the component will appear, 
        /// Subcategory the panel. If you use non-existing tab or panel names, 
        /// new tabs/panels will automatically be created.
        /// </summary>
        public SandWorm()
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
            pManager.AddIntegerParameter("WaterLevel", "WL", "WaterLevel", GH_ParamAccess.item, 1000);
            pManager.AddIntegerParameter("LeftColumns", "LC", "Number of columns to trim from the left", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("RightColumns", "RC", "Number of columns to trim from the right", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("TopRows", "TR", "Number of rows to trim from the top", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("BottomRows", "BR", "Number of rows to trim from the bottom", GH_ParamAccess.item, 0);
            pManager.AddIntegerParameter("TickRate", "TR", "The time interval, in milliseconds, to update geometry from the Kinect. Set as 0 to disable automatic updates.", GH_ParamAccess.item, tickRate);
            pManager.AddIntegerParameter("AverageFrames", "AF", "Amount of depth frames to average across. This number has to be greater than zero.", GH_ParamAccess.item, averageFrames);

            pManager[0].Optional = true;
            pManager[1].Optional = true;
            pManager[2].Optional = true;
            pManager[3].Optional = true;
            pManager[4].Optional = true;
            pManager[5].Optional = true;
            pManager[6].Optional = true;
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.AddMeshParameter("Mesh", "M", "Resulting Mesh", GH_ParamAccess.list);
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
            DA.GetData<int>(0, ref waterLevel);
            DA.GetData<int>(1, ref leftColumns);
            DA.GetData<int>(2, ref rightColumns);
            DA.GetData<int>(3, ref topRows);
            DA.GetData<int>(4, ref bottomRows);
            DA.GetData<int>(5, ref tickRate);
            DA.GetData<int>(6, ref averageFrames);


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

            Stopwatch timer = Stopwatch.StartNew(); //debugging

            Core.ComputeLookupTable(waterLevel, lookupTable); //precompute all vertex colors


            if (this.kinectSensor == null)
            {
                KinectController.AddRef();
                this.kinectSensor = KinectController.sensor;
            }


            if (this.kinectSensor != null)
            {
                if (KinectController.depthFrameData != null)
                {
                    pointCloud = new List<Point3f>();
                    Point3f tempPoint = new Point3f();
                    outputMesh = new List<Mesh>();
                    output = new List<String>(); //debugging
                    vertexColors = new List<Color>();

                    renderBuffer.Enqueue(KinectController.depthFrameData); 

                    for (int rows = topRows; rows < KinectController.depthHeight - bottomRows; rows++)

                    {
                        for (int columns = rightColumns; columns < KinectController.depthWidth - leftColumns; columns++)
                        {

                            int i = rows * KinectController.depthWidth + columns;

                            tempPoint.X = (float)(columns * -unitsMultiplier * 3); //to do - fix arbitrary grid size of 3mm
                            tempPoint.Y = (float)(rows * -unitsMultiplier * 3); //to do - fix arbitrary grid size of 3mm

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

                            if (depthPoint == 0 || depthPoint >= lookupTable.Length) //check for invalid pixels
                            {
                                depthPoint = (int)sensorElevation;
                            }


                            tempPoint.Z = (float)((depthPoint - sensorElevation) * -unitsMultiplier);
                            vertexColors.Add(lookupTable[depthPoint]);

                            pointCloud.Add(tempPoint);
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


                    timer.Restart(); //debugging


                    quadMesh = Core.CreateQuadMesh(quadMesh, pointCloud, vertexColors, KinectController.depthWidth - leftColumns - rightColumns, KinectController.depthHeight - topRows - bottomRows);
                    outputMesh.Add(quadMesh);

                    timer.Stop(); //debugging
                    output.Add("Meshing: " + timer.ElapsedMilliseconds.ToString() + " ms");
                }

                DA.SetDataList(0, outputMesh);
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
            get { return new Guid("f923f24d-86a0-4b7a-9373-23c6b7d2e162"); }
        }
    }
}