using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Windows.Forms;
using System.IO;
using edu.stanford.nlp.ling;
using edu.stanford.nlp.neural.rnn;
using edu.stanford.nlp.pipeline;
using edu.stanford.nlp.sentiment;
using edu.stanford.nlp.trees;
using edu.stanford.nlp.util;
using java.util;
using System.Linq;



namespace WindowsFormsApplication1
{

    public partial class Form1 : Form
    {


        //initialize the space for our dictionary data
        DictionaryData DictData = new DictionaryData();



        //this is what runs at initialization
        public Form1()
        {

            InitializeComponent();

            foreach(var encoding in Encoding.GetEncodings())
            {
                EncodingDropdown.Items.Add(encoding.Name);
            }

            try
            {
                EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact("utf-8");
            }
            catch
            {
                EncodingDropdown.SelectedIndex = EncodingDropdown.FindStringExact(Encoding.Default.BodyName);
            }
            


        }







        private void StartButton_Click(object sender, EventArgs e)
        {


            

                    FolderBrowser.Description = "Please choose the location of your .txt files to analyze";
                    if (FolderBrowser.ShowDialog() != DialogResult.Cancel) {

                        DictData.TextFileFolder = FolderBrowser.SelectedPath.ToString();
                
                        if (DictData.TextFileFolder != "")
                        {

                            saveFileDialog.FileName = "Senti-Gent_Output.csv";

                            saveFileDialog.InitialDirectory = DictData.TextFileFolder;
                            if (saveFileDialog.ShowDialog() != DialogResult.Cancel) {


                                DictData.OutputFileLocation = saveFileDialog.FileName;

                                if (DictData.OutputFileLocation != "") {


                                    StartButton.Enabled = false;
                                    ScanSubfolderCheckbox.Enabled = false;
                                    EncodingDropdown.Enabled = false;
                            
                                    BgWorker.RunWorkerAsync(DictData);
                                }
                            }
                        }

                    }

                

        }






        private void BgWorkerClean_DoWork(object sender, DoWorkEventArgs e)
        {


            DictionaryData DictData = (DictionaryData)e.Argument;


            //report what we're working on
            FilenameLabel.Invoke((MethodInvoker)delegate
            {
                FilenameLabel.Text = "Loading CoreNLP models... please wait...";
            });

            //largely taken from here: https://github.com/sergey-tihon/Stanford.NLP.NET/issues/39
            var jarRoot = @"stanford-corenlp-full-2018-02-27\";
            var props = new java.util.Properties();
            props.setProperty("annotators", "tokenize, ssplit, parse, sentiment");
            props.setProperty("sutime.binders", "0");
            var curDir = Environment.CurrentDirectory;
            Directory.SetCurrentDirectory(Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory), jarRoot));
            var pipeline = new StanfordCoreNLP(props);
            

            

            //selects the text encoding based on user selection
            Encoding SelectedEncoding = null;
            this.Invoke((MethodInvoker)delegate ()
            {
                SelectedEncoding = Encoding.GetEncoding(EncodingDropdown.SelectedItem.ToString());
            });



            //get the list of files
            var SearchDepth = SearchOption.TopDirectoryOnly;
            if (ScanSubfolderCheckbox.Checked)
            {
                SearchDepth = SearchOption.AllDirectories;
            }
            var files = Directory.EnumerateFiles(DictData.TextFileFolder, "*.txt", SearchDepth);



            //try
            //{

                //open up the output file
                using (StreamWriter outputFile = new StreamWriter(new FileStream(DictData.OutputFileLocation, FileMode.Create), SelectedEncoding))
                {

                    using (StreamWriter outputFileSentences = new StreamWriter(new FileStream(AddSuffix(DictData.OutputFileLocation, "_Sentences"), FileMode.Create), SelectedEncoding))
                    {


                        //write the header row to the output file
                        StringBuilder HeaderString = new StringBuilder();
                        HeaderString.Append("\"Filename\",\"Sentences\",\"Classification\",\"Classification_M\",\"Classification_SD\"");

                        outputFile.WriteLine(HeaderString.ToString());

                        StringBuilder HeaderStringSentence = new StringBuilder();
                        HeaderStringSentence.Append("\"Filename\",\"SentNumber\",\"SentenceText\",\"Classification\",\"Class_Prob\",\"Class_Number\"");
                        outputFileSentences.WriteLine(HeaderStringSentence.ToString());

                        foreach (string fileName in files)
                        {

                            //set up our variables to report
                            string Filename_Clean = Path.GetFileName(fileName);
                            Dictionary<string, int> DictionaryResults = new Dictionary<string, int>();

                            //report what we're working on
                            FilenameLabel.Invoke((MethodInvoker)delegate
                            {
                                FilenameLabel.Text = "Analyzing: " + Filename_Clean;
                            });




                            //read in the text file, convert everything to lowercase
                            string InputText = System.IO.File.ReadAllText(fileName, SelectedEncoding).Trim();





                            //     _                _                 _____         _   
                            //    / \   _ __   __ _| |_   _ _______  |_   _|____  _| |_ 
                            //   / _ \ | '_ \ / _` | | | | |_  / _ \   | |/ _ \ \/ / __|
                            //  / ___ \| | | | (_| | | |_| |/ /  __/   | |  __/>  <| |_ 
                            // /_/   \_\_| |_|\__,_|_|\__, /___\___|   |_|\___/_/\_\\__|
                            //                        |___/                             

                            var annotation = new edu.stanford.nlp.pipeline.Annotation(InputText);
                            pipeline.annotate(annotation);

                            List<double> SentimentValues = new List<double>();

                            var sentences = annotation.get(new CoreAnnotations.SentencesAnnotation().getClass()) as ArrayList;

                            int SentenceCount = 0;

                            foreach (CoreMap sentence in sentences)
                            {


                                SentenceCount++;
                                Tree tree = sentence.get(new SentimentCoreAnnotations.SentimentAnnotatedTree().getClass()) as Tree;

                                //add this sentence to our overall list of sentiment scores
                                SentimentValues.Add(RNNCoreAnnotations.getPredictedClass(tree));

                                // __        __    _ _          ___        _               _   
                                // \ \      / / __(_) |_ ___   / _ \ _   _| |_ _ __  _   _| |_ 
                                //  \ \ /\ / / '__| | __/ _ \ | | | | | | | __| '_ \| | | | __|
                                //   \ V  V /| |  | | ||  __/ | |_| | |_| | |_| |_) | |_| | |_ 
                                //    \_/\_/ |_|  |_|\__\___|  \___/ \__,_|\__| .__/ \__,_|\__|
                                //                                            |_|     

                                string[] OutputString_SentenceLevel = new string[6];

                                string Classification = GetClassification((double)RNNCoreAnnotations.getPredictedClass(tree));


                                OutputString_SentenceLevel[0] = "\"" + Filename_Clean + "\"";
                                OutputString_SentenceLevel[1] = SentenceCount.ToString();
                                OutputString_SentenceLevel[2] = "\"" + sentence.ToString().Replace("\"", "\"\"") + "\"";
                                OutputString_SentenceLevel[3] = Classification;
                                OutputString_SentenceLevel[4] = RNNCoreAnnotations.getPredictedClassProb(tree.label()).ToString();
                                OutputString_SentenceLevel[5] = RNNCoreAnnotations.getPredictedClass(tree).ToString();

                                outputFileSentences.WriteLine(String.Join(",", OutputString_SentenceLevel));

                            }



                            //write output at the file level
                            string[] OutputString = new string[5];
                            OutputString[0] = "\"" + Filename_Clean + "\"";
                            OutputString[1] = SentenceCount.ToString();
                            OutputString[2] = GetClassification(SentimentValues.Average());
                            OutputString[3] = SentimentValues.Average().ToString();
                            OutputString[4] = StandardDeviation(SentimentValues).ToString();

                            outputFile.WriteLine(String.Join(",", OutputString));

                        }








                        //this is the closing bracket for the sentence-level "using" filestream
                    }

                    //this is the closing bracket for the document-level "using" filestream
                }

            //}
            //catch
            //{
            //    MessageBox.Show("Senti-Gent encountered an issue somewhere while trying to analyze your texts. The most common cause of this is trying to open your output file while Senti-Gent is still running. Did any of your input files move, or is your output file being opened/modified by another application?", "Error while analyzing", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //}

        }        





        //when the bgworker is done running, we want to re-enable user controls and let them know that it's finished
        private void BgWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            StartButton.Enabled = true;
            ScanSubfolderCheckbox.Enabled = true;
            EncodingDropdown.Enabled = true;
            FilenameLabel.Text = "Finished!";
            MessageBox.Show("Senti-Gent has finished analyzing your texts.", "Analysis Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }







        public class DictionaryData
        {

            public string TextFileFolder { get; set; }
            public string OutputFileLocation { get; set; }

        }

        //https://stackoverflow.com/a/24367618
        string AddSuffix(string filename, string suffix)
        {
            string fDir = Path.GetDirectoryName(filename);
            string fName = Path.GetFileNameWithoutExtension(filename);
            string fExt = Path.GetExtension(filename);
            return Path.Combine(fDir, String.Concat(fName, suffix, fExt));
        }

        private string GetClassification(double y)
        {

            if (y < 0.8) return "Very Negative";
            else if (y < 1.6) return "Negative";
            else if (y < 2.4) return "Neutral";
            else if (y < 3.2) return "Positive";
            else if (y <= 4) return "Very Positive";
            else return "";
            
            
        }


        public static double StandardDeviation(IEnumerable<double> values)
        {
            double avg = values.Average();
            return Math.Sqrt(values.Average(v => Math.Pow(v - avg, 2)));
        }
        


}


        















    }


