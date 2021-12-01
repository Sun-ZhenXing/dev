using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Monitor
{
    public partial class ThickForm : Form
    {
        private List<float> centxlist = new List<float>();
        private DataGridView dataGridView = new DataGridView();
        List<Point> listpointexcel = new List<Point>();

        public ThickForm(DataGridView thickdata, List<float> centxlist)
        { 

           // this.curmap = curmap;
            this.centxlist = centxlist; 
            this.dataGridView = thickdata;  
            InitializeComponent();
        }
      

        private void DnaChartForm_Load(object sender, EventArgs e)
        {
            if (centxlist.Count == 0)
                return;
            comboBox1.Items.Clear();
            comboBox1.Text = ("选样本");
            comboBox1.Items.Add("ladder");
            for (int j = 0; j < centxlist.Count-1; j++)
            {
                comboBox1.Items.Add("样本"+(j+1)); 
            }  
            this.SetVisibleCore(false);
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.SetVisibleCore(true);
            this.ShowInTaskbar = false;
           
            dataGridView2.Columns[0].Visible = false;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            this.Close(); 
        }

        private void comboBox1_DropDownClosed(object sender, EventArgs e)
        {
            if (comboBox1.Items.Count == 0)
                return;
            comboBox2.Items.Clear();
            comboBox2.Text = ("选条带");
            comboBox1.Items.Add("ladder");
            for (int j = 0; j <  dataGridView.RowCount-1; j++)
            {
                if (dataGridView[comboBox1.SelectedIndex+1, j].Value != null)
                    comboBox2.Items.Add("第"+(j+1).ToString()+"条带");
            } 
        }

        private void button2_Click(object sender, EventArgs e)
        {
            double thick = 1;
            if (textBox1.Text!="")
            thick = double.Parse(textBox1.Text);

            double datacellvalue = 0;
            if (dataGridView[comboBox1.SelectedIndex + 1, comboBox2.SelectedIndex].Value.ToString() != "")
              datacellvalue = double.Parse(dataGridView[comboBox1.SelectedIndex + 1, comboBox2.SelectedIndex].Value.ToString());

            dataGridView2.Rows.Clear();
            dataGridView2.Rows.Add(2);

            for (int j = 0; j < dataGridView.RowCount - 1; j++)
            {
                DataGridViewRow row = new DataGridViewRow();
                dataGridView2.Rows.Add(row);
                for (int k = 0; k < dataGridView.ColumnCount; k++)
                {
                    if (dataGridView[k, j].Value != null)
                        dataGridView2[k, j].Value = thick / double.Parse(dataGridView[k, j].Value.ToString()) * datacellvalue;
                }
            }
            dataGridView2.ClearSelection();
        }
        
        public void ExportChart(string fileName, Chart chart1,string Type)
        {
         //   string GR_Path = @"D:";
           // string fullFileName = GR_Path + "\\" + fileName + Type;// ".png";
            string fullFileName =  fileName + Type;

           // chart1.SaveImage(fullFileName, System.Windows.Forms.DataVisualization.Charting.ChartImageFormat.Bmp); 
            switch (Type)
            {
                case ".bmp":
                    chart1.SaveImage(fullFileName, System.Windows.Forms.DataVisualization.Charting.ChartImageFormat.Bmp);
                    break;
                case ".jpg":
                    chart1.SaveImage(fullFileName, System.Windows.Forms.DataVisualization.Charting.ChartImageFormat.Jpeg);
                    break;
                case ".png":
                    chart1.SaveImage(fullFileName, System.Windows.Forms.DataVisualization.Charting.ChartImageFormat.Png);
                    break;
                default:
                    break;
            }
        }

        
 

        private void button3_Click(object sender, EventArgs e)
        {
            //导出数据
           
            try
            {
               // chartToExecl(listpointexcel);
            }
            catch
            {
            }
        }
        /// <summary>
        /// 执行导出数据
        /// </summary>
        //public void chartToExecl(List<Point> listpointexcel)
        //{

        //    Monitor.MonitorForm.ShowInputPanel();
        //    SaveFileDialog saveFileDialog = new SaveFileDialog();
        //    saveFileDialog.Filter = "Execl files (*.xls)|*.xls";
        //    saveFileDialog.FilterIndex = 0;
        //    saveFileDialog.RestoreDirectory = true;
        //    saveFileDialog.CreatePrompt = true;
        //    saveFileDialog.Title = "Export Excel File To";
        //    saveFileDialog.ShowDialog();
        //    Stream myStream;
        //    myStream = saveFileDialog.OpenFile();
        //    StreamWriter sw = new StreamWriter(myStream, System.Text.Encoding.GetEncoding(-0));
        //    string str = "";
        //    try
        //    {
        //        //写标题 
        //        str += "X";
        //        str += "\t";
        //        str += "Y"; 
        //        sw.WriteLine(str);  
        //        for (int i = 0; i < listpointexcel.Count; i++)
        //        {
        //            string tempStr = "";
        //            tempStr += (listpointexcel[i].X+1 + "").ToString();
        //            tempStr += "\t";
        //            tempStr += (listpointexcel[i].Y + "").ToString(); 
        //            sw.WriteLine(tempStr); 
        //        }  
        //        sw.Close();
        //        myStream.Close();
        //    }
        //    catch (Exception e)
        //    {
        //        MessageBox.Show(e.ToString());
        //    }
        //    finally
        //    {
        //        sw.Close();
        //        myStream.Close();
        //        Monitor.MonitorForm.HideInputPanel();
        //    }
        //}

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (((int)e.KeyChar < 48 || (int)e.KeyChar > 57) && (int)e.KeyChar != 8 && (int)e.KeyChar != 46)
                e.Handled = true;
            //小数点的处理。
            if ((int)e.KeyChar == 46)                           //小数点
            {
                if (textBox1.Text.Length <= 0)
                    e.Handled = true;   //小数点不能在第一位
                else
                {
                    float f;
                    float oldf;
                    bool b1 = false, b2 = false;
                    b1 = float.TryParse(textBox1.Text, out oldf);
                    b2 = float.TryParse(textBox1.Text + e.KeyChar.ToString(), out f);
                    if (b2 == false)
                    {
                        if (b1 == true)
                            e.Handled = true;
                        else
                            e.Handled = false;
                    }
                }
            }
        }

        private void textBox1_Click(object sender, EventArgs e)
        {
            ShowInputPanel();
        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                HideInputPanel();
        }
        // 申明要使用的dll和api
        //[DllImport("User32.dll", EntryPoint = "FindWindow")]
        //public extern static IntPtr FindWindow(string lpClassName, string lpWindowName);
        //[System.Runtime.InteropServices.DllImportAttribute("user32.dll", EntryPoint = "MoveWindow")]
        //public static extern bool MoveWindow(System.IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);
        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);


        // private System.Diagnostics.Process softKey; 
        private const Int32 WM_MOVE = 0x0003;
        private const Int32 WM_SYSCOMMAND = 274;
        private const UInt32 SC_CLOSE = 61536;
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, int Msg, uint wParam, uint lParam);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "PostMessage")]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
        [DllImport("User32.dll", SetLastError = true, CharSet = CharSet.Auto, EntryPoint = "RegisterWindowMessage")]
        private static extern int RegisterWindowMessage(string lpString);
        private static readonly IntPtr HWND_TOP = new IntPtr(0); //将窗口置于Z序的顶部
        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1); //将窗口置于所有非顶层窗口之上。即使窗口未被激活窗口也将保持顶级位置
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);  //不是顶部


        //标识位uFlags
        private const UInt32 SWP_NOSIZE = 0x0001; //保持当前的大小（忽略cx和cy参数）
        private const UInt32 SWP_NOMOVE = 0x0002; //保持当前的位置（忽略x和y参数）
        private const UInt32 SWP_NOZORDER = 0x0004;//保持当前的次序（忽略pWndInsertAfter）
        private const UInt32 SWP_NOREDRAW = 0x0008;//不重画变化。如果设置了这个标志，则不发生任何种类的变化
        private const UInt32 SWP_NOACTIVATE = 0x0010;//不激活窗口。如果没有设置这个标志，则窗口将被激活并移动到顶层或非顶层窗口组（依赖于pWndInsertAfter参数的设置）的顶部
        private const UInt32 SWP_FRAMECHANGED = 0x0020;//向窗口发送一条WM_NCCALCSIZE消息，即使窗口的大小不会改变。如果没有指定这个标志，则仅当窗口的大小发生变化时才发送WM_NCCALCSIZE消息
        private const UInt32 SWP_SHOWWINDOW = 0x0040;//显示窗口
        private const UInt32 SWP_HIDEWINDOW = 0x0080;//隐藏窗口
        private const UInt32 SWP_NOCOPYBITS = 0x0100;// 废弃这个客户区的内容。如果没有指定这个参数，则客户区的有效内容将被保存，
        private const UInt32 SWP_NOOWNERZORDER = 0x0200;//不改变拥有者窗口在Z轴次序上的位置。
        private const UInt32 SWP_NOSENDCHANGING = 0x0400;//防止窗口接收WM_WINDOWPOSCHANGING消息
        private const UInt32 TOPMOST_FLAGS = SWP_NOMOVE | SWP_NOSIZE;

        /// <summary>
        /// 设置窗口位置
        /// </summary>
        /// <param name="hWnd">窗口句柄</param>
        /// <param name="hWndInsertAfter">在z序中的位于被置位的窗口前的窗口句柄</param>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <param name="cx"></param>
        /// <param name="cy"></param>
        /// <param name="uFlags">标识位uFlags</param>
        /// <returns></returns>
        [DllImport("user32.dll", SetLastError = true, EntryPoint = "SetWindowPos")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "MoveWindow")]
        private static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetForegroundWindow")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true, EntryPoint = "GetWindowRect")]
        public static extern int GetWindowRect(IntPtr hwnd, ref System.Drawing.Rectangle lpRect);
        //显示触摸键盘
        public static void ShowInputPanel()
        {
            String _file = "C:\\Program Files\\Common Files\\microsoft shared\\ink\\TabTip.exe";

            if (File.Exists(_file))
            {
                using (Process _process = Process.Start(_file)) { };
            }
        }
        //关闭触摸键盘
        public static void HideInputPanel()
        {
            try
            {
                IntPtr _touchhWnd = IntPtr.Zero;

                _touchhWnd = FindWindow("IPTip_Main_Window", null);

                if (_touchhWnd != IntPtr.Zero)
                    PostMessage(_touchhWnd, WM_SYSCOMMAND, SC_CLOSE, 0);
                // 获取屏幕尺寸
                int iActulaWidth = Screen.PrimaryScreen.Bounds.Width;
                int iActulaHeight = Screen.PrimaryScreen.Bounds.Height;


                // 设置软键盘的显示位置，底部居中
                int posX = (iActulaWidth - 1000) / 2;
                int posY = (iActulaHeight - 300) - 80;


                //设定键盘显示位置
                MoveWindow(_touchhWnd, posX, posY, 1000, 300, true);


                //设置软键盘到前端显示
                SetForegroundWindow(_touchhWnd);
            }
            catch { }
        }

        
    }
}
