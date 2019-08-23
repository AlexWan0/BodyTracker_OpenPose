using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using OpenPoseDotNet;
using OpenCvSharp;
using System.IO;

namespace BodyTracker_OpenPose
{
    public partial class Form1 : Form
    {
        private string filePath = ""; // input file path (if applicable)
        private BackgroundWorker stopWorker; // stopWorker stops and disposes of detector on another thread
        private int selectedIndex = 0; // input type selected
        private Detector d; // detector object uses openpose to return detections
        private KinectForm kinectForm; // kinect operations are run on seperate window
        public Form1()
        {
            // worker to handle stopping and disposing openpose instances
            stopWorker = new BackgroundWorker();
            stopWorker.DoWork += new DoWorkEventHandler(StopDetector);
            stopWorker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(Reset);

            // init form
            InitializeComponent();

            // set default selected index for input device
            comboBox1.SelectedIndex = 2;
        }
        private void SelectFile() // opens dialog to select input image/video
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.InitialDirectory = "C:\\Users";
                if (selectedIndex == 2)
                    openFileDialog.Filter = "Image files (*.jpg, *.jpeg, *.jpe, *.jfif, *.png) | *.jpg; *.jpeg; *.png"; // allowed image exts
                else if (selectedIndex == 1)
                    openFileDialog.Filter = "Video files (*.mp4, *.avi, *.mov) | *.mp4; *.avi; *.mov"; // allowed video exts

                openFileDialog.FilterIndex = 1;
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    // stores selected filepath
                    filePath = openFileDialog.FileName;

                    label2.Text = filePath;

                    button2.Enabled = true; // enables "Detect" button
                }
            }
        }
        private void SelectFolder() // opens dialog to select folder
        {
            using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    // stores selected filepath
                    filePath = folderBrowserDialog.SelectedPath;
                    label2.Text = filePath;

                    button2.Enabled = true; // enables "Detect" button
                }
        }
        private void button1_Click(object sender, EventArgs e) // button1 opens the file selection dialog
        {
            if (selectedIndex == 3)
                SelectFolder();
            else
                SelectFile();
        }
        private async void button2_Click(object sender, EventArgs e) // button2 starts the detection
        {
            // disables gui when detection is running
            button2.Enabled = false;
            button3.Enabled = true;
            comboBox1.Enabled = false;

            // retrieves settings from gui
            Boolean face = checkBox1.Checked;
            Boolean hand = checkBox2.Checked;
            Boolean output = checkBox3.Checked;
            string netResolution =  textBox1.Text;
            string faceResolution = face ? textBox2.Text : null;
            string handResolution = hand ? textBox3.Text : null;
            string outPath = output ? textBox4.Text : null;
            int webcamId = Convert.ToInt16(textBox5.Text);

            // clear keypoint output box
            dataGridView1.Rows.Clear();

            // the "Detector" object uses openpose to detect keypoints
            // different detectors are used for different input types
            switch (this.selectedIndex)
            {
                case 0: // webcam input
                    d = new SequenceDetector(netResolution,
                        faceResolution,
                        handResolution,
                        "BODY_25", outPath, webcamId);
                    break;
                case 1: // video file input
                    d = new SequenceDetector(netResolution,
                        faceResolution,
                        handResolution,
                        "BODY_25",
                        this.filePath, 0, outPath);
                    break;
                case 2: // image input
                    d = new ImageDetector(netResolution,
                        faceResolution,
                        handResolution,
                        "BODY_25",
                        this.filePath, outPath);
                    break;
                case 3: // image folder input
                    d = new SequenceDetector(netResolution,
                        faceResolution,
                        handResolution,
                        "BODY_25",
                        this.filePath, 1, outPath);
                    break;
                case 4: // kinect uses a completely seperate form and detection loop
                    this.kinectForm = new KinectForm(netResolution,
                        faceResolution,
                        handResolution,
                        "BODY_25",
                        outPath);
                    this.kinectForm.Show(); // display window
                    break;
            }

            if (selectedIndex != 4) // if kinect isn't used as input, we start a task that returns detections results back to this class with the progress class
            {
                var progress = new Progress<DetectionResults>(ProcessResults); // ProcessResults() is called whenver is detection is found
                await Task.Factory.StartNew(() => d.Run(progress), TaskCreationOptions.LongRunning); // d.Run(progress) starts the previously set detector
            }
        }
        private Datum result; // Datum contains the keypoint data, e.g. keypoints, output image
        private void ProcessResults(DetectionResults dr) // called whenever a detection is returned by the detector. For image detector, this may just be a single frame
        {
            // Detection results contains both the data and a flag "isFinished"
            // isFinished is true if there are more frames coming, false if not
            if (!dr.isFinished)
            {
                result = dr.data;
                
                Array<float> keypoints = result.PoseKeyPoints; // converts result datum to OpenPoseDotNet.Array class
                if (result.PoseKeyPoints.GetSize().Length > 0)
                {
                    int numKeypoints = result.PoseKeyPoints.GetSize()[1]; // number of keypoints per person
                    int totalPoints = numKeypoints * result.PoseKeyPoints.GetSize()[0]; // number of keypoints total in this detection

                    for (int n = dataGridView1.Rows.Count; n < totalPoints; n++) // adds needed rows to gui output
                        dataGridView1.Rows.Add(new String[5]);

                    for (int i = 0; i < totalPoints; i++) // for every point, we output to the gui
                    {
                        float[] point = keypoints[i];
                        dataGridView1.Rows[i].SetValues(new string[] {
                                Convert.ToString(i % numKeypoints), // body part
                                Convert.ToString(point[0]), // x coord
                                Convert.ToString(point[1]), // y coord
                                Convert.ToString(point[2]), // confidence
                                Convert.ToString(Math.Floor(Convert.ToDouble(i/numKeypoints))) // person num
                            });
                    }

                    // clear gui output table
                    int initialCount = dataGridView1.Rows.Count;
                    for (int i = totalPoints; i < initialCount; i++)
                    {
                        dataGridView1.Rows[i].SetValues(new string[5]);
                    }
                }
                else
                { // config gui to display that no keypoints were found
                    if (dataGridView1.Rows.Count < 1)
                        dataGridView1.Rows.Add(new string[] { "No keypoints found" });
                    else
                    {
                        dataGridView1.Rows[0].SetValues(new string[] { "No keypoints found" });

                        int initialCount = dataGridView1.Rows.Count;
                        for (int i = 1; i < initialCount; i++)
                        {
                            dataGridView1.Rows[i].SetValues(new string[5]);
                        }
                    }
                    
                }
                if (!result.IsDisposed && result.CvOutputData.Cols > 0 && result.CvOutputData.Rows > 0) // if the output image exists
                    Cv.ImShow("result", result.CvOutputData); // display using OpenPoseDotNet.Cv
                else
                    Console.WriteLine("Invalid output image, not showing");
            }
            else // finished detecting, no more frames
            { 
                button3.Enabled = false; // disable "Stop" button
                ResetGUI();
            }
            // after each detection is received and processed, dispose of data objects
            DisposeResults();
        }
        private void ResetGUI() // re-enable gui after detection finished
        {
            button2.Enabled = true;
            comboBox1.Enabled = true;
        }
        private void StopDetector(object sender, DoWorkEventArgs e) // called by stopWorker to prematurely stop the detector
        {
            d.Stop();
            DisposeResults();
        }
        private void Reset(object sender, RunWorkerCompletedEventArgs e) // called by stopWorker when finished
        {
            ResetGUI();
        }
        // comboBox1 is the dropdown menu corresponding to the different input types
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) // update selectedIndex variable and reconfigures GUI to match newly selected input
        {
            this.selectedIndex = comboBox1.SelectedIndex;

            // alter gui
            switch (this.selectedIndex)
            {
                case 0: // webcam input
                    button2.Enabled = true;
                    button1.Enabled = false;
                    textBox5.Enabled = true;
                    break;
                case 1: // video file input
                    button1.Text = "Choose File";
                    button1.Enabled = true;
                    textBox5.Enabled = false;
                    String[] imageExt = { ".mp4", ".avi" };
                    button2.Enabled = Array.IndexOf(imageExt, Path.GetExtension(filePath)) != -1;
                    break;
                case 2: // image input
                    button1.Text = "Choose File";
                    button1.Enabled = true;
                    textBox5.Enabled = false;
                    String[] videoExt = { ".jpg", ".jpeg", ".png" };
                    button2.Enabled = Array.IndexOf(videoExt, Path.GetExtension(filePath)) != -1;
                    break;
                case 3: // image directory input
                    button1.Text = "Choose Dir";
                    button1.Enabled = true;
                    textBox5.Enabled = false;
                    button2.Enabled = Directory.Exists(filePath);
                    break;
                case 4: // kinect input
                    button2.Enabled = true;
                    button1.Enabled = false;
                    break;
            }
        }

        private void button3_Click(object sender, EventArgs e) // button3 allows the user to prematurely stop the detector
        {
            button3.Enabled = false;
            if (selectedIndex == 4) // kinect input selected
            {
                kinectForm.Stop();
                ResetGUI();
            }
            else // all other inputs
            {
                if (!stopWorker.IsBusy) // stop and dispose of Detector on another thread
                    stopWorker.RunWorkerAsync();
            }
        }
        private void DisposeResults()
        {
            if (result != null && !result.IsDisposed)
                result.Dispose(); // dispose of results data returned by progress
            d.DisposeData(); // dispose data objects used in the detector
        }
        
        // updates textboxes to match checkbox selection
        private void checkBox3_CheckedChanged(object sender, EventArgs e)
        {
            textBox4.Enabled = checkBox3.Checked;
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            textBox2.Enabled = checkBox1.Checked;
        }

        private void checkBox2_CheckedChanged(object sender, EventArgs e)
        {
            textBox3.Enabled = checkBox2.Checked;
        }
    }
}
