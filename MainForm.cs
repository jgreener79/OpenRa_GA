// 1D Optimization using Genetic Algorithms
// AForge.NET framework
// http://www.aforgenet.com/framework/
//
// Copyright © AForge.NET, 2006-2011
// contacts@aforgenet.com
//

using System;
using System.Drawing;
using System.Collections;
using System.ComponentModel;
using System.Windows.Forms;
using System.Data;
using System.Threading;
using MySql.Data;
using MySql.Data.MySqlClient;

using AForge;
using AForge.Genetic;
using AForge.Controls;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenRa_GA
{
    /// <summary>
    /// Summary description for MainForm.
    /// </summary>
    public class MainForm : System.Windows.Forms.Form
    {
        private AForge.Controls.Chart chart;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox minXBox;
        private System.Windows.Forms.TextBox maxXBox;
        private System.Windows.Forms.Label label2;
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.Container components = null;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox populationSizeBox;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox chromosomeLengthBox;
        private System.Windows.Forms.CheckBox onlyBestCheck;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox iterationsBox;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Button startButton;
        private System.Windows.Forms.Button stopButton;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.ComboBox selectionBox;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.ComboBox modeBox;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.TextBox currentIterationBox;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.TextBox currentValueBox;

        private UserFunction userFunction = new UserFunction();
        private int populationSize = 10;
        private int chromosomeLength = 10;
        private int iterations = 100;
        private int selectionMethod = 1;
        private int optimizationMode = 0;
        private bool showOnlyBest = false;

        private Thread workerThread = null;
        private TextBox textBox1;
        private volatile bool needToStop = false;
        private TextBox GALaunch;
        private Label label11;
        private TextBox textBoxRun;
        private Label label12;
        private AForge.Range XRange;
        string cs = @"server=localhost;userid=root;password=root;database=mydb";
        private Label label13;
        private TextBox MaxCThread;
        private Label TC;
        private Label label14;
        MySqlConnection conn = null;
        List<Process> Processes;
        // Constructor
        public MainForm()
        {
            //
            // Required for Windows Form Designer support
            //
            InitializeComponent();
            try
            {
                conn = new MySqlConnection(cs);
                conn.Open();
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());

            }
            // add data series to chart
            chart.AddDataSeries("function", Color.Red, Chart.SeriesType.Line, 1);
            chart.AddDataSeries("solution", Color.Blue, Chart.SeriesType.Dots, 5);
            //UpdateChart( );

            // update controls
            //minXBox.Text = userFunction.Min.ToString( );
            //maxXBox.Text = userFunction.Max.ToString( );
            selectionBox.SelectedIndex = selectionMethod;
            modeBox.SelectedIndex = optimizationMode;
            UpdateSettings();
            SetText(textBoxRun, Convert.ToString(SQLGetInt("Select max(RunId)+1 from RunTrack")));
        }

        private int SQLGetInt(string SqlStatement)
        {
            try
            {
                if (conn.State.ToString ()== "Closed")
                {
                    conn.Open();
                }
                string stm = SqlStatement;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                return Convert.ToInt16(cmd.ExecuteScalar());
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                return -1;
            }
        }
        private Single SQLGetSingle(string SqlStatement)
        {
            try
            {
                if (conn.State.ToString() == "Closed")
                {
                    conn.Open();
                }
                string stm = SqlStatement;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                return Convert.ToSingle(cmd.ExecuteScalar());
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                return -1;
            }
        }
        private List<string> SQLGetRow(string SqlStatement)
        {
            List<string> RetValue = new List<string>();
            MySqlDataReader rdr = null;
            try
            {
                if (conn.State.ToString() == "Closed")
                {
                    conn.Open();
                }
                string stm = SqlStatement;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                rdr = cmd.ExecuteReader();

                while (rdr.Read())
                {
                    for (int x=0;x<rdr.FieldCount;x++)
                    {
                        RetValue.Add(rdr.GetString(x));
                    }
                }
                return RetValue;
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                return RetValue;
            }
            finally
            {
                rdr.Close();
            }
        }
        private Boolean SQLExecuteCommand(string SqlStatement){
             try
            {
                if (conn.State.ToString() == "Closed")
                {
                    conn.Open();
                }
                string stm = SqlStatement;
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                if (cmd.ExecuteNonQuery() == 0)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
             catch (MySqlException ex)
             {
                 Console.WriteLine("Error: {0}", ex.ToString());
                 return false;
             }
        }
        private Boolean SQLStoreFit(int RunId,int GenId,string Chrome,Single FitValue,String LogFile)
        {
             //INSERT INTO `mydb`.`fitresults` (`RunId`, `GenId`, `Chromosome`, `FitValue`) VALUES ('0', '0', '01 02 03 04 05 06 07 08 09 10 11 12 ', '3.02753');
            try
            {
                if (conn.State.ToString() == "Closed")
                {
                    conn.Open();
                }
                string stm = "";
                if (SQLGetInt("Select count(*) from fitresults where RunId=" + Convert.ToString(RunId) + " and GenId=" + Convert.ToString(GenId) + " and R_Log='" + LogFile.Replace("\\", "") + "'") == 0)
                {

                    stm = "Insert into FitResults(RunId,GenId,Chromosome,FitValue,R_log) Values(" + Convert.ToString(RunId) + "," + Convert.ToString(GenId) + ",'" + Chrome + "'," + Convert.ToString(FitValue) + ",'"+LogFile+"')";
                    
                }
                else
                {
                    stm = "Update FitResults set FitValue="+ Convert.ToString(FitValue) + "where  RunId=" + Convert.ToString(RunId) + " and GenId=" + Convert.ToString(GenId) + " and R_Log='" + LogFile.Replace("\\", "") + "'";
                }
                MySqlCommand cmd = new MySqlCommand(stm, conn);
                if (cmd.ExecuteNonQuery() == 1)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (MySqlException ex)
            {
                Console.WriteLine("Error: {0}", ex.ToString());
                return false;
            }
        }

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (components != null)
                {
                    components.Dispose();
                }
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code
        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.chart = new AForge.Controls.Chart();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.TC = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.label1 = new System.Windows.Forms.Label();
            this.minXBox = new System.Windows.Forms.TextBox();
            this.maxXBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.MaxCThread = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.modeBox = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.selectionBox = new System.Windows.Forms.ComboBox();
            this.label7 = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.iterationsBox = new System.Windows.Forms.TextBox();
            this.label5 = new System.Windows.Forms.Label();
            this.onlyBestCheck = new System.Windows.Forms.CheckBox();
            this.chromosomeLengthBox = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.populationSizeBox = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.startButton = new System.Windows.Forms.Button();
            this.stopButton = new System.Windows.Forms.Button();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.textBoxRun = new System.Windows.Forms.TextBox();
            this.label12 = new System.Windows.Forms.Label();
            this.GALaunch = new System.Windows.Forms.TextBox();
            this.label11 = new System.Windows.Forms.Label();
            this.currentValueBox = new System.Windows.Forms.TextBox();
            this.label10 = new System.Windows.Forms.Label();
            this.currentIterationBox = new System.Windows.Forms.TextBox();
            this.label9 = new System.Windows.Forms.Label();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.SuspendLayout();
            // 
            // chart
            // 
            this.chart.Location = new System.Drawing.Point(10, 20);
            this.chart.Name = "chart";
            this.chart.RangeX = ((AForge.Range)(resources.GetObject("chart.RangeX")));
            this.chart.RangeY = ((AForge.Range)(resources.GetObject("chart.RangeY")));
            this.chart.Size = new System.Drawing.Size(280, 202);
            this.chart.TabIndex = 0;
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.TC);
            this.groupBox1.Controls.Add(this.label14);
            this.groupBox1.Controls.Add(this.textBox1);
            this.groupBox1.Controls.Add(this.chart);
            this.groupBox1.Controls.Add(this.label1);
            this.groupBox1.Controls.Add(this.minXBox);
            this.groupBox1.Controls.Add(this.maxXBox);
            this.groupBox1.Controls.Add(this.label2);
            this.groupBox1.Location = new System.Drawing.Point(10, 10);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(300, 330);
            this.groupBox1.TabIndex = 0;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Function";
            this.groupBox1.Enter += new System.EventHandler(this.groupBox1_Enter);
            // 
            // TC
            // 
            this.TC.AutoSize = true;
            this.TC.Location = new System.Drawing.Point(253, 298);
            this.TC.Name = "TC";
            this.TC.Size = new System.Drawing.Size(0, 13);
            this.TC.TabIndex = 6;
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(176, 298);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(71, 13);
            this.label14.TabIndex = 5;
            this.label14.Text = "Thread count";
            // 
            // textBox1
            // 
            this.textBox1.Location = new System.Drawing.Point(6, 225);
            this.textBox1.Multiline = true;
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(284, 62);
            this.textBox1.TabIndex = 5;
            // 
            // label1
            // 
            this.label1.Location = new System.Drawing.Point(10, 297);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(41, 13);
            this.label1.TabIndex = 3;
            this.label1.Text = "Range:";
            // 
            // minXBox
            // 
            this.minXBox.Location = new System.Drawing.Point(60, 295);
            this.minXBox.Name = "minXBox";
            this.minXBox.Size = new System.Drawing.Size(50, 20);
            this.minXBox.TabIndex = 3;
            // 
            // maxXBox
            // 
            this.maxXBox.Location = new System.Drawing.Point(130, 295);
            this.maxXBox.Name = "maxXBox";
            this.maxXBox.Size = new System.Drawing.Size(50, 20);
            this.maxXBox.TabIndex = 4;
            // 
            // label2
            // 
            this.label2.Location = new System.Drawing.Point(115, 297);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(8, 16);
            this.label2.TabIndex = 3;
            this.label2.Text = "-";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.MaxCThread);
            this.groupBox2.Controls.Add(this.label13);
            this.groupBox2.Controls.Add(this.modeBox);
            this.groupBox2.Controls.Add(this.label8);
            this.groupBox2.Controls.Add(this.selectionBox);
            this.groupBox2.Controls.Add(this.label7);
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.iterationsBox);
            this.groupBox2.Controls.Add(this.label5);
            this.groupBox2.Controls.Add(this.onlyBestCheck);
            this.groupBox2.Controls.Add(this.chromosomeLengthBox);
            this.groupBox2.Controls.Add(this.label4);
            this.groupBox2.Controls.Add(this.populationSizeBox);
            this.groupBox2.Controls.Add(this.label3);
            this.groupBox2.Location = new System.Drawing.Point(320, 10);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(185, 187);
            this.groupBox2.TabIndex = 1;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Settings";
            // 
            // MaxCThread
            // 
            this.MaxCThread.Location = new System.Drawing.Point(125, 145);
            this.MaxCThread.Name = "MaxCThread";
            this.MaxCThread.Size = new System.Drawing.Size(49, 20);
            this.MaxCThread.TabIndex = 12;
            this.MaxCThread.Text = "25";
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(1, 148);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(119, 13);
            this.label13.TabIndex = 13;
            this.label13.Text = "Max Concurrent Thread";
            this.label13.Click += new System.EventHandler(this.label13_Click);
            // 
            // modeBox
            // 
            this.modeBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.modeBox.Items.AddRange(new object[] {
            "Maximize",
            "Minimize"});
            this.modeBox.Location = new System.Drawing.Point(110, 95);
            this.modeBox.Name = "modeBox";
            this.modeBox.Size = new System.Drawing.Size(65, 21);
            this.modeBox.TabIndex = 7;
            // 
            // label8
            // 
            this.label8.Location = new System.Drawing.Point(10, 97);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(110, 17);
            this.label8.TabIndex = 6;
            this.label8.Text = "Optimization mode:";
            // 
            // selectionBox
            // 
            this.selectionBox.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.selectionBox.Items.AddRange(new object[] {
            "Elite",
            "Rank",
            "Roulette"});
            this.selectionBox.Location = new System.Drawing.Point(110, 70);
            this.selectionBox.Name = "selectionBox";
            this.selectionBox.Size = new System.Drawing.Size(65, 21);
            this.selectionBox.TabIndex = 5;
            // 
            // label7
            // 
            this.label7.Location = new System.Drawing.Point(10, 72);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(100, 16);
            this.label7.TabIndex = 4;
            this.label7.Text = "Selection method:";
            // 
            // label6
            // 
            this.label6.Font = new System.Drawing.Font("Microsoft Sans Serif", 6.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label6.Location = new System.Drawing.Point(126, 133);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(56, 16);
            this.label6.TabIndex = 10;
            this.label6.Text = "( 0 - inifinity )";
            // 
            // iterationsBox
            // 
            this.iterationsBox.Location = new System.Drawing.Point(125, 119);
            this.iterationsBox.Name = "iterationsBox";
            this.iterationsBox.Size = new System.Drawing.Size(50, 20);
            this.iterationsBox.TabIndex = 9;
            // 
            // label5
            // 
            this.label5.Location = new System.Drawing.Point(10, 121);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(64, 16);
            this.label5.TabIndex = 8;
            this.label5.Text = "Iterations:";
            // 
            // onlyBestCheck
            // 
            this.onlyBestCheck.Location = new System.Drawing.Point(10, 162);
            this.onlyBestCheck.Name = "onlyBestCheck";
            this.onlyBestCheck.Size = new System.Drawing.Size(144, 16);
            this.onlyBestCheck.TabIndex = 11;
            this.onlyBestCheck.Text = "Show only best solution";
            // 
            // chromosomeLengthBox
            // 
            this.chromosomeLengthBox.Location = new System.Drawing.Point(125, 45);
            this.chromosomeLengthBox.Name = "chromosomeLengthBox";
            this.chromosomeLengthBox.Size = new System.Drawing.Size(50, 20);
            this.chromosomeLengthBox.TabIndex = 3;
            this.chromosomeLengthBox.Visible = false;
            // 
            // label4
            // 
            this.label4.Location = new System.Drawing.Point(10, 47);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(110, 12);
            this.label4.TabIndex = 2;
            this.label4.Text = "Chromosome length:";
            this.label4.Visible = false;
            // 
            // populationSizeBox
            // 
            this.populationSizeBox.Location = new System.Drawing.Point(125, 20);
            this.populationSizeBox.Name = "populationSizeBox";
            this.populationSizeBox.Size = new System.Drawing.Size(50, 20);
            this.populationSizeBox.TabIndex = 1;
            // 
            // label3
            // 
            this.label3.Location = new System.Drawing.Point(10, 22);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(85, 16);
            this.label3.TabIndex = 0;
            this.label3.Text = "Population size:";
            // 
            // startButton
            // 
            this.startButton.Location = new System.Drawing.Point(340, 317);
            this.startButton.Name = "startButton";
            this.startButton.Size = new System.Drawing.Size(75, 23);
            this.startButton.TabIndex = 3;
            this.startButton.Text = "&Start";
            this.startButton.Click += new System.EventHandler(this.startButton_Click);
            // 
            // stopButton
            // 
            this.stopButton.Enabled = false;
            this.stopButton.Location = new System.Drawing.Point(430, 317);
            this.stopButton.Name = "stopButton";
            this.stopButton.Size = new System.Drawing.Size(75, 23);
            this.stopButton.TabIndex = 4;
            this.stopButton.Text = "S&top";
            this.stopButton.Click += new System.EventHandler(this.stopButton_Click);
            // 
            // groupBox3
            // 
            this.groupBox3.Controls.Add(this.textBoxRun);
            this.groupBox3.Controls.Add(this.label12);
            this.groupBox3.Controls.Add(this.GALaunch);
            this.groupBox3.Controls.Add(this.label11);
            this.groupBox3.Controls.Add(this.currentValueBox);
            this.groupBox3.Controls.Add(this.label10);
            this.groupBox3.Controls.Add(this.currentIterationBox);
            this.groupBox3.Controls.Add(this.label9);
            this.groupBox3.Location = new System.Drawing.Point(320, 203);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(185, 107);
            this.groupBox3.TabIndex = 2;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Current iteration";
            // 
            // textBoxRun
            // 
            this.textBoxRun.Location = new System.Drawing.Point(125, 64);
            this.textBoxRun.Name = "textBoxRun";
            this.textBoxRun.ReadOnly = true;
            this.textBoxRun.Size = new System.Drawing.Size(50, 20);
            this.textBoxRun.TabIndex = 7;
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(6, 67);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(71, 13);
            this.label12.TabIndex = 6;
            this.label12.Text = "Current Run#";
            // 
            // GALaunch
            // 
            this.GALaunch.Location = new System.Drawing.Point(125, 44);
            this.GALaunch.Name = "GALaunch";
            this.GALaunch.ReadOnly = true;
            this.GALaunch.Size = new System.Drawing.Size(50, 20);
            this.GALaunch.TabIndex = 5;
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(6, 46);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(75, 13);
            this.label11.TabIndex = 4;
            this.label11.Text = "GA Launching";
            // 
            // currentValueBox
            // 
            this.currentValueBox.Location = new System.Drawing.Point(125, 26);
            this.currentValueBox.Name = "currentValueBox";
            this.currentValueBox.ReadOnly = true;
            this.currentValueBox.Size = new System.Drawing.Size(50, 20);
            this.currentValueBox.TabIndex = 3;
            // 
            // label10
            // 
            this.label10.Location = new System.Drawing.Point(6, 31);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(60, 15);
            this.label10.TabIndex = 2;
            this.label10.Text = "Value:";
            // 
            // currentIterationBox
            // 
            this.currentIterationBox.Location = new System.Drawing.Point(125, 9);
            this.currentIterationBox.Name = "currentIterationBox";
            this.currentIterationBox.ReadOnly = true;
            this.currentIterationBox.Size = new System.Drawing.Size(50, 20);
            this.currentIterationBox.TabIndex = 1;
            // 
            // label9
            // 
            this.label9.Location = new System.Drawing.Point(7, 13);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(60, 16);
            this.label9.TabIndex = 0;
            this.label9.Text = "Iteration:";
            // 
            // MainForm
            // 
            this.AutoScaleBaseSize = new System.Drawing.Size(5, 13);
            this.ClientSize = new System.Drawing.Size(514, 350);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.stopButton);
            this.Controls.Add(this.startButton);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.Text = "1D Optimization using Genetic Algorithms";
            this.Closing += new System.ComponentModel.CancelEventHandler(this.MainForm_Closing);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.ResumeLayout(false);

        }
        #endregion

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.Run(new MainForm());
        }


        // Delegates to enable async calls for setting controls properties
        private delegate void SetTextCallback(System.Windows.Forms.Control control, string text);

        // Thread safe updating of control's text property
        private void SetText(System.Windows.Forms.Control control, string text)
        {
            if (control.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                Invoke(d, new object[] { control, text });
            }
            else
            {
                control.Text = text;
            }
        }

        // On main form closing
        private void MainForm_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // check if worker thread is running
            if ((workerThread != null) && (workerThread.IsAlive))
            {
                needToStop = true;
                while (!workerThread.Join(100))
                    Application.DoEvents();
            }
        }

        // Update settings controls
        private void UpdateSettings()
        {
            populationSizeBox.Text = populationSize.ToString();
            chromosomeLengthBox.Text = chromosomeLength.ToString();
            iterationsBox.Text = iterations.ToString();
        }

        // Update chart

        /*private void UpdateChart( )
		{
			// update chart range
            chart.RangeX = userFunction.range;

			double[,] data = null;

			if ( chart.RangeX.Length > 0 )
			{
				// prepare data
				data = new double[501, 2];

				double minX = userFunction.range.Min;
				double length = userFunction.range.Length;

				for ( int i = 0; i <= 500; i++ )
				{
					data[i, 0] = minX + length * i / 500;
					data[i, 1] = userFunction.GetFitness( data[i, 0] );
				}
			}

			// update chart series
			chart.UpdateDataSeries( "function", data );
		}*/

        // Update min value
        /*
        private void minXBox_TextChanged(object sender, System.EventArgs e)
		{
			try
			{
				userFunction.Range = new Range( float.Parse( minXBox.Text ), userFunction.Range.Max );
				UpdateChart( );
			}
			catch
			{
			}
		}

		// Update max value
		private void maxXBox_TextChanged(object sender, System.EventArgs e)
		{
			try
			{
				userFunction.Range = new Range( userFunction.Range.Min, float.Parse( maxXBox.Text ) );
				UpdateChart( );
			}
			catch
			{
			}
		}*/

        // Delegates to enable async calls for setting controls properties
        private delegate void EnableCallback(bool enable);

        // Enable/disale controls (safe for threading)
        private void EnableControls(bool enable)
        {
            if (InvokeRequired)
            {
                EnableCallback d = new EnableCallback(EnableControls);
                Invoke(d, new object[] { enable });
            }
            else
            {
                minXBox.Enabled = enable;
                maxXBox.Enabled = enable;

                populationSizeBox.Enabled = enable;
                chromosomeLengthBox.Enabled = enable;
                iterationsBox.Enabled = enable;
                selectionBox.Enabled = enable;
                modeBox.Enabled = enable;
                onlyBestCheck.Enabled = enable;

                startButton.Enabled = enable;
                stopButton.Enabled = !enable;
            }
        }

        // On "Start" button click
        private void startButton_Click(object sender, System.EventArgs e)
        {
            // get population size
            try
            {
                populationSize = Math.Max(10, Math.Min(1000, int.Parse(populationSizeBox.Text)));
            }
            catch
            {
                populationSize = 40;
            }
            XRange.Min = 0;
            XRange.Max = populationSize;
            chart.RangeX = XRange;
            // get chromosome length
            try
            {
                chromosomeLength = Math.Max(8, Math.Min(64, int.Parse(chromosomeLengthBox.Text)));
            }
            catch
            {
                chromosomeLength = 32;
            }
            // iterations
            try
            {
                iterations = Math.Max(0, int.Parse(iterationsBox.Text));
            }
            catch
            {
                iterations = 100;
            }
            // update settings controls
            UpdateSettings();

            selectionMethod = selectionBox.SelectedIndex;
            optimizationMode = modeBox.SelectedIndex;
            showOnlyBest = onlyBestCheck.Checked;
            //MessageBox.Show(selectionBox.SelectedItem.ToString());
            SQLExecuteCommand("Insert Into RunTrack(RunId,StartingNotes) VALUES(" + textBoxRun.Text + ",'fitness=marmels, faction=russia, SpawnPoint=0, Pop=" + Convert.ToString(populationSize) + ", SelectMethod=" + Convert.ToString(selectionBox.SelectedItem.ToString()) + "')");
            // disable all settings controls except "Stop" button
            EnableControls(false);

            // run worker thread
            needToStop = false;
            workerThread = new Thread(new ThreadStart(SearchSolution));
            workerThread.Start();
        }

        // On "Stop" button click
        private void stopButton_Click(object sender, System.EventArgs e)
        {
            // stop worker thread
            needToStop = true;
            while (!workerThread.Join(100))
                Application.DoEvents();
            workerThread = null;
        }


        // Worker thread
        void SearchSolution()
        {
            //MessageBox.Show("Starting search");
            // create population
            userFunction.InitFitResult(populationSize);
#pragma warning disable CS0436 // Type conflicts with imported type
            Population population = new Population(populationSize,
#pragma warning restore CS0436 // Type conflicts with imported type
                new ShortArrayChromosome(12, 100),
                userFunction, (ISelectionMethod)new RankSelection());
            // set optimization mode
            //userFunction.Mode = OptimizationFunction1D.Modes.Maximization;
            // iterations
            int i = 1;
            // solution
            double[,] data = new double[(showOnlyBest) ? 1 : populationSize, 2];
            //since population intialization generates first set of chromosomes, 
            //we'll run them thru outside of loop, and come into the loop with the post game selection in place
            /*MakeYamls(population);
            LaunchOpenRA(population);
            FindFitness(population);
            population.ReEvaluate();
            population.FindBestChromosome();
            textBox1.Text =textBox1.Text +"Best Chromosome value is:" + population.BestChromosome.ToString() + "belonging to GA_" + userFunction.FindChromosome(population.BestChromosome) + " With fitness of:" + userFunction.Evaluate(population.BestChromosome) + " populations max fitness is:" + population.FitnessMax+"\n";
			// loop*/
            while (!needToStop)
            {
                SetText(currentIterationBox, i.ToString());
                SetText(textBox1, textBox1.Text + Convert.ToString(currentIterationBox.Text));
                // run one epoch of genetic algorithm
                //population.Crossover(); //epoch part1
                //population.Mutate();    //epoch part2
                //population.Selection();//epoch part3
                MakeYamls(population);
                //Init fitness array
                for (int x = 0; x < populationSize; x++)
                {
                    userFunction.Fitresults[x, 0] = population[x].ToString();
                    userFunction.Fitresults[x, 1] = "1";
                    userFunction.Fitresults[x, 2] = "0";
                }
                LaunchOpenRA(population);
                FindFitness(population,true);
                population.ReEvaluate();
                population.FindBestChromosome();
                SetText(textBox1, textBox1.Text + "Best Chromosome value is:" + population.BestChromosome.ToString() + "belonging to GA_" + userFunction.FindChromosome(population.BestChromosome) + " With fitness of:" + userFunction.Evaluate(population.BestChromosome) + " populations max fitness is:" + population.FitnessMax + "\n");
                //population.RunEpoch( ); //need to see if fitness function is run before or after new chromosomes are generated
                // show current solution
                /*
                if ( showOnlyBest )
				{
					data[0, 0] =  population.BestChromosome.ToString;
                    data[0, 1] = userFunction.GetFitness(population.BestChromosome);
				}
				else
				{*/
                for (int j = 0; j < populationSize; j++)
                {
                    data[j, 0] = j; //userFunction.Translate( population[j] );
                    data[j, 1] = userFunction.GetFitness(population[j]);
                }
                //}
                chart.UpdateDataSeries("solution", data);

                // set current iteration's info

                SetText(currentValueBox, userFunction.GetFitness(population.BestChromosome).ToString("F3"));

                // increase current iteration
                i++;

                //
                if ((iterations != 0) && (i > iterations))
                    break;
                population.RunEpoch();
            }

            // enable settings controls
            EnableControls(true);
        }

#pragma warning disable CS0436 // Type conflicts with imported type
        private void FindFitness(Population population,Boolean final=false)
#pragma warning restore CS0436 // Type conflicts with imported type
        {
            double[,] data = new double[(showOnlyBest) ? 1 : populationSize, 2];
            try
            {
                string path = Environment.ExpandEnvironmentVariables("%userprofile%");
                string[] dirs = Directory.GetFiles(@path + "\\Documents\\OpenRA\\Logs", "Run"+ Convert.ToString(textBoxRun.Text)+"_Gen" + Convert.ToString(currentIterationBox.Text) + "GA_*.log");
                foreach (string myfile in dirs)
                {
                    int GA_Num = -1;
                    Regex logs = new Regex(@"^GA_(\d+)\s+[|]\s+(\d+)\s*[|]\s+(\d+)");
                    FileInfo f = new FileInfo(myfile);
                    if (f.Length > 0)
                    {
                        try
                        {
                            StreamReader reader = File.OpenText(myfile);
                            string line;
                            while ((line = reader.ReadLine()) != null)
                            {
                                Match match = logs.Match(line);
                                if (match.Success)
                                {
                                    int GA_member = int.Parse(match.Groups[1].Value);
                                    int Kill_cost = int.Parse(match.Groups[2].Value);
                                    int Death_cost = int.Parse(match.Groups[3].Value);
                                    float FitValue= (Convert.ToSingle(Kill_cost) / Convert.ToSingle(Death_cost))*1000;
                                    if (Death_cost == 0) { FitValue =10000; }
                                    GA_Num = GA_member;
                                    //TODO-done: debug why this is failing, msgbox on each passed element
                                    //MessageBox.Show(Convert.ToString(Convert.ToInt16(textBoxRun.Text)));
                                    //MessageBox.Show(Convert.ToString(Convert.ToInt16(currentIterationBox.Text )));
                                    //MessageBox.Show(userFunction.Fitresults[GA_member, 0]);
                                    //MessageBox.Show(Convert.ToString(Convert.ToSingle(Kill_cost) / Convert.ToSingle(Death_cost)));

                                    /*t = avg(FitValue) / Stdev(FitValues)
                                    f = (0.75 + min(0.25, avg(FitValue) / Stdev(FitValues) * 0.025))
                                    g = 1.001 ^ count
                                    ((Avg(FitValue) / 1000) * f * 1.001 ^ count(FitValue)*/
                                    SQLStoreFit(Convert.ToInt16(textBoxRun.Text), Convert.ToInt16(currentIterationBox.Text), userFunction.Fitresults[GA_member, 0], FitValue, myfile);
                                    List <string> fitRaw= SQLGetRow("Select AVG(FitValue),STD(FitValue),Count(FitValue) from fitResults where RunId=" + textBoxRun.Text + " and Chromosome='" + userFunction.Fitresults[GA_member, 0] + "'");
                                    if (Convert.ToSingle(fitRaw[1]) == 0)
                                    {
                                        fitRaw[1] = "1";
                                    }
                                    Single t = Convert.ToSingle(fitRaw[0]) / Convert.ToSingle(fitRaw[1]);
                                    Single f1 = 1;
                                    if (t * 0.025 < .25)
                                    {
                                        f1 = Convert.ToSingle (0.75) + (t * Convert.ToSingle(0.025));
                                    }
                                    Single g = Convert.ToSingle(Math.Pow(Convert.ToSingle(1.001),Convert.ToSingle( fitRaw[2])));
                                    Single Marmels = (Convert.ToSingle(fitRaw[0]) / 1000) * f1 * g;
                                    userFunction.Fitresults[GA_member, 1] = Convert.ToString(Marmels);
                                    //Fitness 1 AVG(FitValue)/STD(FitValue)*(Avg(FitValue)/1000)
                                }
                            }
                            reader.Close();
                            if (GA_Num != -1)
                            {
                                File.Copy(myfile, @path + "\\Documents\\OpenRA\\Logs\\End_Game_Backup_Gen"+ Convert.ToString(currentIterationBox.Text)+"GA_" + Convert.ToString(GA_Num), true);
                                File.Delete(myfile);
                            }
                        }
                        catch(Exception ex)
                        {
                           //MessageBox.Show("FindFitness error:"+ex.ToString());
                        }
                    }
                    else
                    {
                        /*
                        MessageBox.Show(myfile);
                        MessageBox.Show(myfile.Substring(myfile.LastIndexOf("GA_") + 1));
                        String holder = myfile.Substring(myfile.LastIndexOf("GA_") + 3);
                        holder = holder.Substring(0, holder.LastIndexOf("_"));
                        MessageBox.Show(holder);
                        StartGA(Processes, Convert.ToInt16(holder), -2);
                        //relaunch this GA attempt
                        fitness file is currently size 0, may simply in process*/
                    }
                }
                if (final)
                {
                    var process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            //FileName = "OpenRa"
                            FileName = @path + "\\Documents\\OpenRA\\Logs\\backup.bat",
                            WorkingDirectory = @path + "\\Documents\\OpenRA\\Logs"
                        }
                    };
                    process.StartInfo.Arguments = Convert.ToString(currentIterationBox.Text);
                    process.Start();
                    process.WaitForExit(15000);
                    for (int x = 0; x < populationSize; x++)
                    {
                        WriteLog("Gen" + Convert.ToString(currentIterationBox.Text) + "FitResults", userFunction.Fitresults[x, 1] + ":" + userFunction.Fitresults[x, 0]);
                    }
                }
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
            }
            for (int j = 0; j < populationSize; j++)
            {
                data[j, 0] = j; //userFunction.Translate( population[j] );
                data[j, 1] = userFunction.GetFitness(population[j]);
            }
            //}
            chart.UpdateDataSeries("solution", data);
        }
        private void WriteLog(string filename,string msg)
        {
            string path = Environment.ExpandEnvironmentVariables("%userprofile%");
            FileStream fs = new FileStream(@path + "\\Documents\\OpenRA\\Logs\\" + filename, FileMode.Append);
            StreamWriter writer = new StreamWriter(fs);
            StringBuilder output = new StringBuilder();
            try
            {

                writer.WriteLine(DateTime.Now.ToString("YYYY/MM/dd H:mm:ss")+"|"+ msg);
                
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
            }
            finally
            {
                writer.Close();
                //Copy the Yaml for review process to the correct Gen#
            }
        }
#pragma warning disable CS0436 // Type conflicts with imported type
        private void MakeYamls(Population population)
#pragma warning restore CS0436 // Type conflicts with imported type
        {
            System.IO.Directory.CreateDirectory("mods\\ra\\rules");
            //FileStream fs = new FileStream("mods\\ra\\rules\\GA_AI.yaml", FileMode.Create);
            FileStream fs = new FileStream("C:\\Users\\Administrator\\Documents\\ESU_OpenRA\\mods\\ra\\rules\\GA_AI.yaml", FileMode.Create);
            StreamWriter writer = new StreamWriter(fs);
            StringBuilder output = new StringBuilder();
            try
            {

                writer.WriteLine("Player:");

                for (int x = 0; x < populationSize; x++)
                {
                    string[] chromo = population[x].ToString().Split(' ');
                    writer.WriteLine("	HackyAI@GA_" + x.ToString() + ":#" + population[x].ToString());
                    writer.WriteLine("		Name: GA_" + x.ToString());
                    writer.WriteLine("		MinimumExcessPower: {0,1:f0}", Convert.ToInt32(chromo[0]) * .8 + 20);
                    writer.WriteLine("		BuildingCommonNames:");
                    writer.WriteLine("			ConstructionYard: fact");
                    writer.WriteLine("			Refinery: proc");
                    writer.WriteLine("			Power: powr,apwr");
                    writer.WriteLine("			Barracks: barr,tent");
                    writer.WriteLine("			VehiclesFactory: weap");
                    writer.WriteLine("			Production: barr,tent,weap,afld,hpad");
                    writer.WriteLine("			Silo: silo");
                    writer.WriteLine("		UnitsCommonNames:");
                    writer.WriteLine("			Mcv: mcv");
                    writer.WriteLine("		BuildingLimits:");
                    writer.WriteLine("			proc: 4");
                    writer.WriteLine("			barr: 1");
                    writer.WriteLine("			tent: 1");
                    writer.WriteLine("			kenn: 1");
                    writer.WriteLine("			dome: 1");
                    writer.WriteLine("			weap: 1");
                    writer.WriteLine("			hpad: 4");
                    writer.WriteLine("			afld: 4");
                    writer.WriteLine("			atek: 1");
                    writer.WriteLine("			stek: 1");
                    writer.WriteLine("			fix: 1");
                    writer.WriteLine("		BuildingFractions:");
                    writer.WriteLine("			proc: {0}%", Convert.ToInt32(chromo[2]) * .5);
                    writer.WriteLine("			powr: {0}%", Convert.ToInt32(chromo[3]) * .5);
                    writer.WriteLine("			apwr: 20%");
                    writer.WriteLine("			barr: 1%");
                    writer.WriteLine("			kenn: 0.5%");
                    writer.WriteLine("			tent: 1%");
                    writer.WriteLine("			weap: 1%");
                    writer.WriteLine("			pbox: 7%");
                    writer.WriteLine("			gun: 7%");
                    writer.WriteLine("			tsla: 5%");
                    writer.WriteLine("			ftur: 10%");
                    writer.WriteLine("			agun: 5%");
                    writer.WriteLine("			sam: 5%");
                    writer.WriteLine("			atek: 1%");
                    writer.WriteLine("			stek: 1%");
                    writer.WriteLine("			fix: {0,1:F1}%", Convert.ToDecimal(chromo[4]) / 100);
                    writer.WriteLine("			dome: 10%");
                    writer.WriteLine("		UnitsToBuild:");
                    writer.WriteLine("			e1: 50%");
                    writer.WriteLine("			e2: {0}%", Convert.ToInt32(chromo[5]) * .5);
                    writer.WriteLine("			e3: 10%");
                    writer.WriteLine("			e4: {0}%", Convert.ToInt32(chromo[6]) * .2);
                    writer.WriteLine("			dog: {0}%", Convert.ToInt32(chromo[7]) * .2);
                    writer.WriteLine("			shok: {0}%", Convert.ToInt32(chromo[8]) * .2);
                    writer.WriteLine("			harv: 10%");
                    writer.WriteLine("			apc: 30%");
                    writer.WriteLine("			jeep: 40%");
                    writer.WriteLine("			arty: 15%");
                    writer.WriteLine("			v2rl: 40%");
                    writer.WriteLine("			ftrk: 50%");
                    writer.WriteLine("			1tnk: 70%");
                    writer.WriteLine("			2tnk: 25%");
                    writer.WriteLine("			3tnk: 50%");
                    writer.WriteLine("			4tnk: {0}%", Convert.ToInt32(chromo[9]) * .5);
                    writer.WriteLine("			ttnk: {0}%", Convert.ToInt32(chromo[10]) * .5);
                    writer.WriteLine("			stnk: {0}%", Convert.ToInt32(chromo[11]) * .5);
                    writer.WriteLine("		UnitLimits:");
                    writer.WriteLine("			dog: 4");
                    writer.WriteLine("			harv: 8");
                    writer.WriteLine("		SquadSize: {0,1:f0}", Convert.ToInt32(chromo[1]) * .6);
                    writer.WriteLine("		SupportPowerDecision@spyplane:");
                    writer.WriteLine("			OrderName: SovietSpyPlane");
                    writer.WriteLine("			MinimumAttractiveness: 1");
                    writer.WriteLine("			Consideration@1:");
                    writer.WriteLine("				Against: Enemy");
                    writer.WriteLine("				Types: Structure");
                    writer.WriteLine("				Attractiveness: 1");
                    writer.WriteLine("				TargetMetric: None");
                    writer.WriteLine("				CheckRadius: 5c0");
                    writer.WriteLine("		SupportPowerDecision@paratroopers:");
                    writer.WriteLine("			OrderName: SovietParatroopers");
                    writer.WriteLine("			MinimumAttractiveness: 5");
                    writer.WriteLine("			Consideration@1:");
                    writer.WriteLine("				Against: Enemy");
                    writer.WriteLine("				Types: Structure");
                    writer.WriteLine("				Attractiveness: 1");
                    writer.WriteLine("				TargetMetric: None");
                    writer.WriteLine("				CheckRadius: 8c0");
                    writer.WriteLine("			Consideration@2:");
                    writer.WriteLine("				Against: Enemy");
                    writer.WriteLine("				Types: Water");
                    writer.WriteLine("				Attractiveness: -5");
                    writer.WriteLine("				TargetMetric: None");
                    writer.WriteLine("				CheckRadius: 8c0");
                    writer.WriteLine("		SupportPowerDecision@parabombs:");
                    writer.WriteLine("			OrderName: UkraineParabombs");
                    writer.WriteLine("			MinimumAttractiveness: 1");
                    writer.WriteLine("			Consideration@1:");
                    writer.WriteLine("				Against: Enemy");
                    writer.WriteLine("				Types: Structure");
                    writer.WriteLine("				Attractiveness: 1");
                    writer.WriteLine("				TargetMetric: None");
                    writer.WriteLine("				CheckRadius: 5c0");
                    writer.WriteLine("		SupportPowerDecision@nukepower:");
                    writer.WriteLine("			OrderName: NukePowerInfoOrder");
                    writer.WriteLine("			MinimumAttractiveness: 3000");
                    writer.WriteLine("			Consideration@1:");
                    writer.WriteLine("				Against: Enemy");
                    writer.WriteLine("				Types: Structure");
                    writer.WriteLine("				Attractiveness: 1");
                    writer.WriteLine("				TargetMetric: Value");
                    writer.WriteLine("				CheckRadius: 5c0");
                    writer.WriteLine("			Consideration@2:");
                    writer.WriteLine("				Against: Ally");
                    writer.WriteLine("				Types: Air, Ground, Water");
                    writer.WriteLine("				Attractiveness: -10");
                    writer.WriteLine("				TargetMetric: Value");
                    writer.WriteLine("				CheckRadius: 7c0");
                }

                writer.Close();
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
            }
            finally
            {
                writer.Close();
                //Copy the Yaml for review process to the correct Gen#
                System.IO.File.Copy("C:\\Users\\Administrator\\Documents\\ESU_OpenRA\\mods\\ra\\rules\\GA_AI.yaml", "C:\\Users\\Administrator\\Documents\\ESU_OpenRA\\mods\\ra\\rules\\GA_AI" + Convert.ToString(currentIterationBox.Text) + ".yaml", true);
            }
        }

#pragma warning disable CS0436 // Type conflicts with imported type
        private void LaunchOpenRA(Population population)
#pragma warning restore CS0436 // Type conflicts with imported type
        {
            string path = Environment.ExpandEnvironmentVariables("%userprofile%");
            try
            {
                FileInfo f = new FileInfo(@path + "\\Documents\\OpenRA\\Logs\\exception.log");
                f.Delete();
            }
            catch
            {

            }
            //MessageBox.Show("Ready to Launch games");
            Processes = new List<Process>();
            try
            {
                for (int x = 0; x < populationSize; x++)
                {
                    int repCount = 1;
                    int FitCount = SQLGetInt("Select count(*) from FitResults where RunId=" + Convert.ToString(textBoxRun.Text)+" and Chromosome='"+population[x]+"'");
                    if (FitCount < 3)
                    {
                        repCount=3-FitCount;
                    }
                    SetText(GALaunch, x.ToString());
                    for (int repStart = repCount; repStart > 0; repStart--)
                    {
                        StartGA(Processes, x, repStart);
                        System.Threading.Thread.Sleep(8000);
                        try
                        {
                            FileInfo f = new FileInfo(@path + "\\Documents\\OpenRA\\Logs\\exception.log");
                            if (f.Length != 0) { repStart++; f.Delete(); } //if we caused an exception, retry the last launch and delete the exception file. 
                        }
                        catch { }
                    }
                    SetText(TC, Convert.ToString (getProcessCount(Processes)));
                    //MessageBox.Show(MaxCThread.Text);
                    while (getProcessCount(Processes) > Convert.ToInt16(MaxCThread.Text))
                    {
                        System.Threading.Thread.Sleep(10000);
                        try
                        {
                            for (int pcnt = Processes.Count-1; pcnt > 0; pcnt--)
                            {
                                
                                if (Processes[pcnt].HasExited == false)
                                {
                                    //MessageBox.Show(Processes[pcnt].StartInfo.Arguments+"\n"+Convert.ToString(Processes[pcnt].HasExited)+":"+Convert.ToString(DateTime.Now.Subtract(Processes[pcnt].StartTime).Minutes));
                                    if (DateTime.Now.Subtract(Processes[pcnt].StartTime).TotalMinutes > 45)
                                    {
                                        Regex logs = new Regex(@"GA_(\d+)");
                                        Match match = logs.Match(Processes[pcnt].StartInfo.Arguments);
                                        Processes[pcnt].Kill();
                                        if (match.Success)
                                        {
                                            int GA_number = int.Parse(match.Groups[1].Value);
                                            if (Convert.ToInt16(userFunction.Fitresults[GA_number, 2]) < 3)
                                            {
                                                StartGA(Processes, GA_number, -1);
                                            }
                                        }
                                    }

                                }

                            }
                        }
                        finally { }
                    }
                    FindFitness(population);
                }
                //wait till all threads has ended
                Boolean done = false;
                while (done == false)
                {
                    SetText(TC, Convert.ToString(getProcessCount(Processes)));
                    //FindFitness(population, true);
                    done = true;
                    try
                    {
                        Processes.ForEach(delegate (Process p)
                            {
                                if (p.HasExited == false)
                                {
                                    done = false;
                                //MessageBox.Show(p.StartInfo.Arguments + "\n" + Convert.ToString(p.HasExited) + ":" + Convert.ToString(DateTime.Now.Subtract(p.StartTime).Minutes));
                                if (DateTime.Now.Subtract(p.StartTime).TotalMinutes > 45)
                                    {

                                        Regex logs = new Regex(@"GA_(\d+)");
                                        Match match = logs.Match(p.StartInfo.Arguments);
                                        p.Kill();
                                        if (match.Success)
                                        {
                                            int GA_number = int.Parse(match.Groups[1].Value);
                                            if (Convert.ToInt16(userFunction.Fitresults[GA_number, 2]) < 3)
                                            {
                                                StartGA(Processes, GA_number, -2);
                                            }
                                        }
                                    }
                                /*MessageBox.Show(p.StartInfo.Arguments);
                                MessageBox.Show(Convert.ToString(p.HasExited));
                                MessageBox.Show(Convert.ToString(DateTime.Now.Subtract(p.StartTime).Minutes));*/
                                }
                            }
                                );
                    }
                    finally { }
                    
                    System.Threading.Thread.Sleep(10000);
                }
                System.Threading.Thread.Sleep(15000);
                //FindFitness(population,true);
            }
            catch (Win32Exception ex)
            {
                Console.WriteLine(ex.GetType().FullName);
                Console.WriteLine(ex.Message);
            }
            // processes = Process.GetProcessesByName("OpenRa");
            FindFitness(population, true);
            //process.HasExited

        }

        private void StartGA(List<Process> processes, int GA_Num, int CopyNum)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    //FileName = "OpenRa"
                    FileName = "C:\\Users\\Administrator\\Documents\\ESU_OpenRA\\OpenRA.Game.exe",
                    WorkingDirectory = "C:\\Users\\Administrator\\Documents\\ESU_OpenRA"
                }
            };
            process.StartInfo.Arguments = "Launch.AiFaction = \"russia\" Launch.AiSpawnPoint=\"0\" Launch.Ai=\"GA_" + Convert.ToString(GA_Num) + "\" Launch.MapName=\"Forest Path\" Launch.FitnessLog=\"Run"+ Convert.ToString(textBoxRun.Text)+"_Gen" + Convert.ToString(currentIterationBox.Text) + "GA_" + Convert.ToString(GA_Num) + "_"+Convert.ToString(CopyNum)+"\"";
            process.Start();
            processes.Add(process);
        }

        public int getProcessCount(List<Process> Processes)
        {
            int activeCount = 0;
            Processes.ForEach(delegate (Process p)
            {
                if (p.HasExited == false) { activeCount++; }
            });
            return activeCount;
        }

        private void MainForm_Load(object sender, EventArgs e)
        {

        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (conn != null)
            {
                conn.Close();
            }
        }

        private void label13_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }
    }
    // Function to optimize
    public class UserFunction : IFitnessFunction
	{
        public const int Min =0;
        public const int Max = 1000;
        public const int length = 12;//genecount
        public int popSize;
        public string[,] Fitresults; //chromo string,fitresults
        public Range range = new Range(0, 10);
        public void InitFitResult(int FitSize)
        {
            Fitresults= new string[FitSize, 3];//Chromosome value,Fitness result, retry count
            popSize = FitSize;
        }
        public double Evaluate(IChromosome chromosome)
        {
            return  GetFitness(chromosome);
        }

        public Single GetFitness(IChromosome chromosome)
        {
            //pull fitness info from log file based on Chromosome ID(How do I set a static Id for the Chromosome?)
           // throw new NotImplementedException();
            //RunEpoch runs this after generation of the population.. not before the generation of the new pop... 
            for (int x = 0; x < popSize; x++)
            {
                if (chromosome.ToString() == Fitresults[x, 0])
                {
                    return Convert.ToSingle(Fitresults[x, 1]);
                }
            }
                return 1;
        }
        public int FindChromosome(IChromosome chromosome)
        {
            for (int x = 0; x < popSize; x++)
            {
                if (chromosome.ToString() == Fitresults[x, 0])
                {
                    return x;
                }
            }
            return -1;
        }

	}
}
