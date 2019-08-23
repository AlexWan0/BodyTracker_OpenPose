using System;
using System.Collections.Generic;
using System.Text;
using OpenPoseDotNet;
using UserDatum = OpenPoseDotNet.CustomDatum;

namespace BodyTracker_OpenPose
{
    public class ImageDetector : Detector
    {
        private Wrapper<UserDatum> opWrapper; // openpose wrapper
        
        // data objects used to process results
        private StdSharedPtr<StdVector<StdSharedPtr<CustomDatum>>> datumProcessed;
        private StdVector<StdSharedPtr<CustomDatum>> data;

        private string filePath; // input file path

        /* 
         * initializer for webcam input
         * netRes = net resolution
         * faceRes = face detector resolution, disable by setting to null
         * handRes = hand detector resolution, disable by setting to null
         * modelPose = which pose model to use
         * filePath = image file path
         * outPath = output path for frames, set null to disable output, disable by setting to null
        */
        public ImageDetector(string netRes, string faceRes, string handRes, string modelPose, string filePath, string outPath)
        {
            this.filePath = filePath; // set image file path
            
            // set logging level
            OpenPose.Check(0 <= Flags.LoggingLevel && Flags.LoggingLevel <= 255, "Wrong logging_level value.");
            ConfigureLog.PriorityThreshold = (Priority)Flags.LoggingLevel;
            Profiler.SetDefaultX((ulong)Flags.ProfileSpeed);

            OpenPose.Log("Adding config...", Priority.High);

            InitFlags(netRes, faceRes, handRes, modelPose, outPath); // initialize flags with input values

            opWrapper = new Wrapper<UserDatum>(ThreadManagerMode.Asynchronous);

            // configure openpose wrapper obj based on the flags that we set
            ConfigOnFlags(opWrapper,
                false); // set input on config
        }
        // starts detector
        public override void Run(IProgress<DetectionResults> progress)
        {
            Datum result = ProcessFile();
            progress.Report(new DetectionResults() // report calculated keypoint data with DetectionResults object
            {
                data = result, // keypoint data
                isFinished = false // are we finished with the detection
            });
            
            // returns isFinished=true flag immediately after returning detection
            progress.Report(new DetectionResults()
            {
                data = null,
                isFinished = true
            });
        }
        public Datum ProcessFile() // gets result keypoints from this.filePath
        {
            using (Mat inputImage = Cv.ImRead(this.filePath)) // read image from filepath
            {
                return ProcessFrame(inputImage);
            }
        }
        public Datum ProcessFrame(Mat image) // gets result keypoints from OpenPoseDotNet.Mat
        {
            datumProcessed = opWrapper.EmplaceAndPop(image); // method detects on OpenPoseDotNet.Mat
            if (datumProcessed != null && datumProcessed.TryGet(out data) && !data.Empty) // if datumProcessed exists && we can get the data sucessfully && retrieved data exists
            {
                Datum result = data.ToArray()[0].Get(); // retrieve datum object which contains the keypoint data
                opWrapper.Dispose(); // dispose of wrapper after detection
                return result;
            }
            else
            {
                OpenPose.Log("Nullptr or empty datumsPtr found.", Priority.High);
                return null;
            }
        }
        public override void Stop() // to stop prematurely, dispose of the openpose wrapper
        {
            opWrapper.Dispose();
        }

        // method disposes data that was used during processing
        // call after we're done using the keypoint data
        public override void DisposeData()
        {
            // check if objects exist and aren't disposed already
            // if so, dispose them
            if (datumProcessed != null && !datumProcessed.IsDisposed)
                datumProcessed.Dispose();
            if (data != null && !data.IsDisposed)
                data.Dispose();
        }
        private static void Display(StdSharedPtr<StdVector<Datum>> datumsPtr)
        {
            // User's displaying/saving/other processing here
            // datum.cvOutputData: rendered frame with pose or heatmaps
            // datum.poseKeypoints: Array<float> with the estimated pose
            if (datumsPtr != null && datumsPtr.TryGet(out var data) && !data.Empty)
            {
                // Display image
                var temp = data.ToArray();
                Cv.ImShow("User worker GUI", temp[0].CvOutputData);
                Cv.WaitKey();
            }
            else
            {
                OpenPose.Log("Nullptr or empty datumsPtr found.", Priority.High);
            }
        }
    }
}
