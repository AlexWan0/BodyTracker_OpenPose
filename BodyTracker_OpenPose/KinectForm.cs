using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Azure.Kinect.Sensor;
using Microsoft.Azure.Kinect.Sensor.WinForms;
using OpenPoseDotNet;

namespace BodyTracker_OpenPose
{
    public partial class KinectForm : Form
    {
        private Boolean isRunning; // set to false to break the detection loop

        Datum result; // keypoint data object

        // configuration variables
        private string netRes;
        private string faceRes;
        private string handRes;
        private string modelPose;
        private string filePath;
        private string outPath;

        // keypoint detector for bitmaps
        private BitmapDetector detector;

        /* 
         * initializer for form that processes and displays the kinect input
         * netRes = net resolution
         * faceRes = face detector resolution, disable by setting to null
         * handRes = hand detector resolution, disable by setting to null
         * modelPose = which pose model to use
         * outPath = output path for frames, set null to disable output, disable by setting to null
        */
        public KinectForm(string netRes, string faceRes, string handRes, string modelPose, string outPath)
        {
            InitializeComponent(); // init form

            // pictureBox1 displays the color image
            pictureBox1.SizeMode = PictureBoxSizeMode.StretchImage;

            // set config variables
            this.netRes = netRes;
            this.faceRes = faceRes;
            this.handRes = handRes;
            this.modelPose = modelPose;
            this.outPath = outPath;

            // set loop running flag to true
            this.isRunning = true;

            // start detection loop
            Run();
        }

        // method consists of loop that continually grabs a frame from the kinect
        // for each frame, it detects keypoints then reports it to the progress object
        private void RunKinect(IProgress<KinectResults> progress)
        {
            // bitmap detector used to calculate pose keypoints with openpose
            using (detector = new BitmapDetector(netRes, // net resolution 
                faceRes,
                handRes, 
                modelPose, 
                outPath))
            using (Device device = Device.Open(0)) // opens kinect device
            {
                device.StartCameras(new DeviceConfiguration // starts cameras
                {
                    ColorFormat = ImageFormat.ColorBGRA32,
                    ColorResolution = ColorResolution.r720p,
                    DepthMode = DepthMode.NFOV_2x2Binned,
                    SynchronizedImagesOnly = true,
                });

                while (isRunning) // loop breaks when isRunning is set to false
                {
                    using (Capture capture = device.GetCapture()) // get frame
                    {
                        Bitmap colorImage = capture.Color.CreateBitmap(); // get color image from frame

                        // initialize depth image with empty bitmap
                        Bitmap depthVisualization = new Bitmap(capture.Depth.WidthPixels, capture.Depth.HeightPixels, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

                        // get depth values
                        ushort[] depthValues = capture.Depth.GetPixels<ushort>().ToArray();

                        // for every pixel, convert depth value to color
                        for (int y = 0; y < capture.Depth.HeightPixels; y++)
                        {
                            for (int x = 0; x < capture.Depth.WidthPixels; x++)
                            {
                                ushort depthValue = depthValues[(y * capture.Depth.WidthPixels) + x];

                                if (depthValue == 0)
                                {
                                    depthVisualization.SetPixel(x, y, Color.Red);
                                }
                                else if (depthValue == ushort.MaxValue)
                                {
                                    depthVisualization.SetPixel(x, y, Color.Green);
                                }
                                else
                                {
                                    float brightness = depthValue / 2000f;

                                    if (brightness > 1.0f)
                                    {
                                        depthVisualization.SetPixel(x, y, Color.White);
                                    }
                                    else
                                    {
                                        int c = (int)(brightness * 250);
                                        depthVisualization.SetPixel(x, y, Color.FromArgb(c, c, c));
                                    }
                                }
                            }
                        }

                        // detect keypoints on bitmap
                        Datum result = detector.Run(colorImage);

                        // report data for this frame
                        progress.Report(new KinectResults()
                        {
                            colorImage = colorImage,
                            depthImage = depthVisualization,
                            poseData = result
                        });
                    }
                }
            }
        }
        
        // method is called after every frame once image data/detection results are received
        private void ProcessResults(KinectResults result)
        {
            // put images on form
            this.pictureBox1.Image = result.colorImage;
            this.pictureBox2.Image = result.depthImage;

            if(result.poseData.CvOutputData != null) // if pose keypoint image exists, display
                Cv.ImShow("Detection Result", result.poseData.CvOutputData);

            DisposeResults();
        }

        // when called, the "Run()" method starts the detection loop, which grabs frames, calculates keypoints, and displays them on the form
        private async void Run()
        {
            var progress = new Progress<KinectResults>(ProcessResults);
            await Task.Factory.StartNew(() => RunKinect(progress), TaskCreationOptions.LongRunning);
        }
        
        // "Stop()" method breaks the while loop by setting isRunning to false and closes this window
        public void Stop()
        {
            isRunning = false;
            this.Close();
        }

        // method disposes data that was used during processing
        // call after we're done using the keypoint data
        private void DisposeResults()
        {
            if (result != null && !result.IsDisposed)
                result.Dispose(); // dispose of results data returned by progress
            detector.DisposeData(); // dispose data objects used in the detector
        }
    }
}
