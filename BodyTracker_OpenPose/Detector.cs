using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenPoseDotNet;
using System.IO;
using UserDatum = OpenPoseDotNet.CustomDatum;
using System.Drawing;

namespace BodyTracker_OpenPose
{
    public abstract class Detector
    {
        private readonly static string IMAGE_DIR = "images"; // image output subdir
        private readonly static string JSON_DIR = "json"; // json output subdir
        public abstract void Run(IProgress<DetectionResults> progress); // start detection, reporting detections after each frame to progress object
        public abstract void Stop(); // prematurely stop
        public abstract void DisposeData(); // disposes data that was used during processing, call after we're done using the keypoint data

        /* 
         * sets some common flag values
         * netRes = net resolution
         * faceRes = face detector resolution, disable by setting to null
         * handRes = hand detector resolution, disable by setting to null
         * modelPose = which pose model to use
         * outPath = output path for frames, set null to disable output
        */
        public static void InitFlags(string netRes, string faceRes, string handRes, string modelPose, string outPath)
        {
            // the Flags object is global, so we need to reset all the values so that they don't conflict
            Flags.Video = "";
            Flags.ImageDir = "";
            Flags.Camera = -1; 

            Flags.NetResolution = netRes; // set net resolution

            if (!String.IsNullOrEmpty(faceRes)) // if enabled (disable by setting to null)
            {
                Flags.FaceNetResolution = faceRes;
                Flags.Face = true;
            }
            else
                Flags.Face = false;

            if (!String.IsNullOrEmpty(handRes)) // if enabled (disable by setting to null)
            {
                Flags.HandNetResolution = handRes;
                Flags.Hand = true;
            }
            else
                Flags.Hand = false;

            Flags.ModelPose = modelPose; // sets which pose model to use

            if (!String.IsNullOrEmpty(outPath)) // if enabled (disable by setting to null)
            {
                // get full output path
                string image_out = Path.Combine(outPath, IMAGE_DIR);
                string json_out = Path.Combine(outPath, JSON_DIR);
                
                CreateDir(image_out);
                CreateDir(json_out);

                // set output path flag
                Flags.WriteImages = image_out;
                Flags.WriteJson = json_out;
            } 
        }
        private static void CreateDir(string filePath) // creates a directory in relative path string "filePath"
        {
            System.IO.FileInfo file = new System.IO.FileInfo(filePath);
            file.Directory.Create();
        }
        // configures openpose wrapper on set flags
        // setInput - set to true if setting input during configuration (e.g. for video file input, image directory, webcam)
        //            set to false if inputting later (e.g. for emblaceandpop on Mat object or raw image)
        public static void ConfigOnFlags(Wrapper<UserDatum> opWrapper, Boolean setInput)
        {
            // Configuring OpenPose

            // logging_level
            OpenPose.Check(0 <= Flags.LoggingLevel && Flags.LoggingLevel <= 255, "Wrong logging_level value.");
            ConfigureLog.PriorityThreshold = (Priority)Flags.LoggingLevel;
            Profiler.SetDefaultX((ulong)Flags.ProfileSpeed);

            // Applying user defined configuration - GFlags to program variables
            // producerType
            var tie = OpenPose.FlagsToProducer(Flags.ImageDir, Flags.Video, Flags.IpCamera, Flags.Camera, Flags.FlirCamera, Flags.FlirCameraIndex);
            var producerType = tie.Item1;
            var producerString = tie.Item2;
            // cameraSize
            var cameraSize = OpenPose.FlagsToPoint(Flags.CameraResolution, "-1x-1");
            // outputSize
            var outputSize = OpenPose.FlagsToPoint(Flags.OutputResolution, "-1x-1");
            // netInputSize
            var netInputSize = OpenPose.FlagsToPoint(Flags.NetResolution, "-1x368");
            // faceNetInputSize
            var faceNetInputSize = OpenPose.FlagsToPoint(Flags.FaceNetResolution, "368x368 (multiples of 16)");
            // handNetInputSize
            var handNetInputSize = OpenPose.FlagsToPoint(Flags.HandNetResolution, "368x368 (multiples of 16)");
            // poseMode
            var poseMode = OpenPose.FlagsToPoseMode(Flags.Body);
            // poseModel
            var poseModel = OpenPose.FlagsToPoseModel(Flags.ModelPose);
            // JSON saving
            if (!string.IsNullOrEmpty(Flags.WriteKeyPoint))
                OpenPose.Log("Flag `write_keypoint` is deprecated and will eventually be removed. Please, use `write_json` instead.", Priority.Max);
            // keyPointScale
            var keyPointScale = OpenPose.FlagsToScaleMode(Flags.KeyPointScale);
            // heatmaps to add
            var heatMapTypes = OpenPose.FlagsToHeatMaps(Flags.HeatmapsAddParts, Flags.HeatmapsAddBackground, Flags.HeatmapsAddPAFs);
            var heatMapScale = OpenPose.FlagsToHeatMapScaleMode(Flags.HeatmapsScale);
            // >1 camera view?
            var multipleView = (Flags.Enable3D || Flags.Views3D > 1 || Flags.FlirCamera);
            // Face and hand detectors
            var faceDetector = OpenPose.FlagsToDetector(Flags.FaceDetector);
            var handDetector = OpenPose.FlagsToDetector(Flags.HandDetector);
            // Enabling Google Logging
            const bool enableGoogleLogging = true;

            // Pose configuration (use WrapperStructPose() for default and recommended configuration)
            var pose = new WrapperStructPose(poseMode,
                                             netInputSize,
                                             outputSize,
                                             keyPointScale,
                                             Flags.NumGpu,
                                             Flags.NumGpuStart,
                                             Flags.ScaleNumber,
                                             (float)Flags.ScaleGap,
                                             OpenPose.FlagsToRenderMode(Flags.RenderPose, multipleView),
                                             poseModel,
                                             !Flags.DisableBlending,
                                             (float)Flags.AlphaPose,
                                             (float)Flags.AlphaHeatmap,
                                             Flags.PartToShow,
                                             Flags.ModelFolder,
                                             heatMapTypes,
                                             heatMapScale,
                                             Flags.PartCandidates,
                                             (float)Flags.RenderThreshold,
                                             Flags.NumberPeopleMax,
                                             Flags.MaximizePositives,
                                             Flags.FpsMax,
                                             Flags.PrototxtPath,
                                             Flags.CaffeModelPath,
                                             (float)Flags.UpsamplingRatio,
                                             enableGoogleLogging);

            // Face configuration (use WrapperStructPose() to disable it)
            var face = new WrapperStructFace(Flags.Face,
                                             faceDetector,
                                             faceNetInputSize,
                                             OpenPose.FlagsToRenderMode(Flags.FaceRender, multipleView, Flags.RenderPose),
                                             (float)Flags.FaceAlphaPose,
                                             (float)Flags.FaceAlphaHeatmap,
                                             (float)Flags.FaceRenderThreshold);

            // Hand configuration (use WrapperStructPose() to disable it)
            var hand = new WrapperStructHand(Flags.Hand,
                                             handDetector,
                                             handNetInputSize,
                                             Flags.HandScaleNumber,
                                             (float)Flags.HandScaleRange,
                                             OpenPose.FlagsToRenderMode(Flags.HandRender, multipleView, Flags.RenderPose),
                                             (float)Flags.HandAlphaPose,
                                             (float)Flags.HandAlphaHeatmap,
                                             (float)Flags.HandRenderThreshold);

            // Extra functionality configuration (use WrapperStructPose() to disable it)
            var extra = new WrapperStructExtra(Flags.Enable3D,
                                               Flags.MinViews3D,
                                               Flags.Identification,
                                               Flags.Tracking,
                                               Flags.IkThreads);

            // Output (comment or use default argument to disable any output)
            var output = new WrapperStructOutput(Flags.CliVerbose,
                                                 Flags.WriteKeyPoint,
                                                 OpenPose.StringToDataFormat(Flags.WriteKeyPointFormat),
                                                 Flags.WriteJson,
                                                 Flags.WriteCocoJson,
                                                 Flags.WriteCocoFootJson,
                                                 Flags.WriteCocoJsonVariant,
                                                 Flags.WriteImages,
                                                 Flags.WriteImagesFormat,
                                                 Flags.WriteVideo,
                                                 Flags.WriteVideoWithAudio,
                                                 Flags.WriteVideoFps,
                                                 Flags.WriteHeatmaps,
                                                 Flags.WriteHeatmapsFormat,
                                                 Flags.WriteVideoAdam,
                                                 Flags.WriteBvh,
                                                 Flags.UdpHost,
                                                 Flags.UdpPort);

            // GUI (comment or use default argument to disable any visual output)
            var gui = new WrapperStructGui(OpenPose.FlagsToDisplayMode(Flags.Display, Flags.Enable3D),
                                           !Flags.NoGuiVerbose,
                                           Flags.FullScreen);

            // config wrapper on set values
            opWrapper.Configure(pose);
            opWrapper.Configure(face);
            opWrapper.Configure(hand);
            opWrapper.Configure(extra);
            if (setInput)
            {
                // Producer (use default to disable any input)
                var input = new WrapperStructInput(producerType,
                                                   producerString,
                                                   Flags.FrameFirst,
                                                   Flags.FrameStep,
                                                   Flags.FrameLast,
                                                   Flags.ProcessRealTime,
                                                   Flags.FrameFlip,
                                                   Flags.FrameRotate,
                                                   Flags.FramesRepeat,
                                                   cameraSize,
                                                   Flags.CameraParameterPath,
                                                   Flags.FrameUndistort,
                                                   Flags.Views3D);
                opWrapper.Configure(input);
            }
            opWrapper.Configure(output);

            // Set to single-thread (for sequential processing and/or debugging and/or reducing latency)
            if (Flags.DisableMultiThread)
                opWrapper.DisableMultiThreading();

            // start openpose wrapper
            opWrapper.Start();
        }
    }
}
