using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenPoseDotNet;
using UserDatum = OpenPoseDotNet.CustomDatum;

namespace BodyTracker_OpenPose
{
    public class SequenceDetector : Detector
    {
        private Wrapper<CustomDatum> opWrapper; // wrapper for openpose
        private Boolean userWantsToExit = false; // set this to true to break detection loop
        private StdSharedPtr<StdVector<StdSharedPtr<CustomDatum>>> datumProcessed;
        private StdVector<StdSharedPtr<CustomDatum>> data;
        private int mode;

        /* 
         * initializer for webcam input
         * netRes = net resolution
         * faceRes = face detector resolution, disable by setting to null
         * handRes = hand detector resolution, disable by setting to null
         * modelPose = which pose model to use
         * outPath = output path for frames, set null to disable output, disable by setting to null
         * webcamId = which webcam to use
        */
        public SequenceDetector(string netRes, string faceRes, string handRes, string modelPose, string outPath, int webcamId)
        {
            InitFlags(netRes, faceRes, handRes, modelPose, outPath); // initializes some flag values with input

            this.mode = 2; // set mode id for webcam

            Flags.Camera = webcamId; // set webcam id

            opWrapper = new Wrapper<UserDatum>(ThreadManagerMode.AsynchronousOut); // create openpose wrappper, setting to thread mode to async out because of our cutom method of output

            // configure openpose wrapper obj based on the flags that we set
            ConfigOnFlags(opWrapper, 
                true); // set input on config
        }

        /* 
         * initializer for video file or image directory input
         * netRes = net resolution
         * faceRes = face detector resolution
         * handRes = hand detector resolution
         * modelPose = which pose model to use
         * path = input file/directory path
         * mode = 0 for video file, 1 for image directory
         * outPath = output path for frames, set null to disable output
        */
        public SequenceDetector(string netRes, string faceRes, string handRes, string modelPose, string path, int mode, string outPath)
        {
            InitFlags(netRes, faceRes, handRes, modelPose, outPath); // initialize flags with input values

            // set input path
            if (mode == 0) // video input
            {
                Flags.Video = path;
            }
            else if(mode == 1) // img dir input
            {
                Flags.ImageDir = path;
            }

            this.mode = mode;

            opWrapper = new Wrapper<UserDatum>(ThreadManagerMode.AsynchronousOut);// create openpose wrappper, setting to thread mode to async out because of our cutom method of output

            // configure openpose wrapper obj based on the flags that we set
            ConfigOnFlags(opWrapper, 
                true); // set input on config
        }
        // starts detector
        public override void Run(IProgress<DetectionResults> progress)
        {
            // detection loop - detects on each frame
            while (!userWantsToExit) // setting userWantsToExit to true breaks this loop
            {
                // get latest detected frame from input, which we set during the config
                if (opWrapper.WaitAndPop(out datumProcessed)) // detection data gets put into datumProcessed
                {
                    if (datumProcessed != null && datumProcessed.TryGet(out data) && !data.Empty) // if datumProcessed exists && we can get the data sucessfully && retrieved data exists
                    {
                        Datum d = data.ToArray()[0].Get(); // retrieve datum object which contains the keypoint data

                        progress.Report(new DetectionResults() // report calculated keypoint data with DetectionResults object
                        {
                            data = d, // keypoint data
                            isFinished = false // are we finished with the detection
                        });
                    }
                }
                else // can't get next frame, so we end the detection loop
                {
                    OpenPose.Log("Processed datum could not be emplaced.", Priority.High);
                    userWantsToExit = true;
                    break;
                }
            }

            progress.Report(new DetectionResults() // when finished, we report with isFinished set to true
            {
                data = null,
                isFinished = true
            });

            opWrapper.Dispose(); // dispose of openpose wrapper
        }
        public override void Stop() // stopping sets the userWantsToExit variable in order to break to the loop
        {
            userWantsToExit = true;
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
    }
}
