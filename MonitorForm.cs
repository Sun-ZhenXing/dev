using System;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using MVSDK;//使用SDK接口
using CameraHandle = System.Int32;
using MvApi = MVSDK.MvApi;
using System.IO;
using Snapshot;
using System.Runtime.InteropServices;
using System.IO.Ports;
using System.Diagnostics;
namespace Monitor
{
    public partial class MonitorForm : Form
    {
        #region variable
        protected CameraHandle m_hCamera = 0;             // 句柄
        protected IntPtr m_ImageBuffer;             // 预览通道RGB图像缓存
        protected IntPtr m_ImageBufferSnapshot;     // 抓拍通道RGB图像缓存
        protected tSdkCameraCapbility tCameraCapability;  // 相机特性描述
        protected int m_iDisplayedFrames = 0;    //已经显示的总帧数
        protected CAMERA_SNAP_PROC m_CaptureCallback;
        protected IntPtr m_iCaptureCallbackCtx;     //图像回调函数的上下文参数
        protected Thread m_tCaptureThread;          //图像抓取线程
        protected bool m_bExitCaptureThread = false;//采用线程采集时，让线程退出的标志
        protected IntPtr m_iSettingPageMsgCallbackCtx; //相机配置界面消息回调函数的上下文参数   
        protected tSdkFrameHead m_tFrameHead;
        protected bool m_bEraseBk = false;
        protected bool m_bSaveImage = false;
        int m_iRecordState = 0;//记录录像状态
        int second = 0;
        int curinputvol = 0;
        string file_path= "c:\\test.bmp";
        bool closeflage = true;
        bool experimentstar=false;
        public enum emSdkRecordMode
        {
            RECORD_STOP = 0, //停止
            RECORD_START = 1, //录像中
            RECORD_PAUSE = 2, //暂停
        };

        #endregion

        public MonitorForm()
        {
            InitializeComponent(); 
            if (InitCamera() == true)
            {
                MvApi.CameraPlay(m_hCamera);
                BtnPlay.Text = "Pause";
            }
        }
#if USE_CALL_BACK
        public void ImageCaptureCallback(CameraHandle hCamera, IntPtr pFrameBuffer, ref tSdkFrameHead pFrameHead, IntPtr pContext)
        {
            //图像处理，将原始输出转换为RGB格式的位图数据，同时叠加白平衡、饱和度、LUT等ISP处理。
            MvApi.CameraImageProcess(hCamera, pFrameBuffer, m_ImageBuffer, ref pFrameHead);
            //叠加十字线、自动曝光窗口、白平衡窗口信息(仅叠加设置为可见状态的)。   
            MvApi.CameraImageOverlay(hCamera, m_ImageBuffer, ref pFrameHead);
            //调用SDK封装好的接口，显示预览图像
            MvApi.CameraDisplayRGB24(hCamera, m_ImageBuffer, ref pFrameHead);
            m_tFrameHead = pFrameHead;
            m_iDisplayedFrames++;

            if (pFrameHead.iWidth != m_tFrameHead.iWidth || pFrameHead.iHeight != m_tFrameHead.iHeight)
            {
                timer2.Enabled = true;
                timer2.Start();
                m_tFrameHead = pFrameHead;
            }
            
        }
#else
        public void CaptureThreadProc()
        {
            CameraSdkStatus eStatus;
            tSdkFrameHead FrameHead;
            IntPtr uRawBuffer;//rawbuffer由SDK内部申请。应用层不要调用delete之类的释放函数
            while (m_bExitCaptureThread == false)
            {
                //500毫秒超时,图像没捕获到前，线程会被挂起,释放CPU，所以该线程中无需调用sleep
                eStatus = MvApi.CameraGetImageBuffer(m_hCamera, out FrameHead, out uRawBuffer, 3000);
               
                if (eStatus == CameraSdkStatus.CAMERA_STATUS_SUCCESS)//如果是触发模式，则有可能超时
                {
                    //图像处理，将原始输出转换为RGB格式的位图数据，同时叠加白平衡、饱和度、LUT等ISP处理。
                    MvApi.CameraImageProcess(m_hCamera, uRawBuffer, m_ImageBuffer, ref FrameHead); 
                    //叠加十字线、自动曝光窗口、白平衡窗口信息(仅叠加设置为可见状态的)。    
                    MvApi.CameraImageOverlay(m_hCamera, m_ImageBuffer, ref FrameHead);
                    //调用SDK封装好的接口，显示预览图像
                    MvApi.CameraDisplayRGB24(m_hCamera, m_ImageBuffer, ref FrameHead);
                    //成功调用CameraGetImageBuffer后必须释放，下次才能继续调用CameraGetImageBuffer捕获图像。
                    MvApi.CameraReleaseImageBuffer(m_hCamera, uRawBuffer);
                    if (FrameHead.iWidth != m_tFrameHead.iWidth || FrameHead.iHeight != m_tFrameHead.iHeight)
                    {
                        m_bEraseBk = true;
                        m_tFrameHead = FrameHead;
                    }
                    m_iDisplayedFrames++;
                     
                    if (m_bSaveImage)
                    {
                        //string file_path;
                        //file_path = "c:\\test.bmp"; 
                       // byte[] file_path_bytes = Encoding.Default.GetBytes(file_path);
                        string file_path_bytes = file_path;
                        MvApi.CameraSaveImage(m_hCamera, file_path_bytes, m_ImageBuffer, ref FrameHead, emSdkFileType.FILE_BMP, 100);
                         m_bSaveImage = false;
                    }
                   
                    if (m_iRecordState == (int)emSdkRecordMode.RECORD_START)
                    {
                        MvApi.CameraPushFrame(m_hCamera, m_ImageBuffer, ref FrameHead);
                    }
                } 
            }

        }
#endif

        /*相机配置窗口的消息回调函数
        hCamera:当前相机的句柄
        MSG:消息类型，
	    SHEET_MSG_LOAD_PARAM_DEFAULT	= 0,//加载默认参数的按钮被点击，加载默认参数完成后触发该消息,
	    SHEET_MSG_LOAD_PARAM_GROUP		= 1,//切换参数组完成后触发该消息,
	    SHEET_MSG_LOAD_PARAM_FROMFILE	= 2,//加载参数按钮被点击，已从文件中加载相机参数后触发该消息
	    SHEET_MSG_SAVE_PARAM_GROUP		= 3//保存参数按钮被点击，参数保存后触发该消息
	    具体参见CameraDefine.h中emSdkPropSheetMsg类型

        uParam:消息附带的参数，不同的消息，参数意义不同。
	    当 MSG 为 SHEET_MSG_LOAD_PARAM_DEFAULT时，uParam表示被加载成默认参数组的索引号，从0开始，分别对应A,B,C,D四组
	    当 MSG 为 SHEET_MSG_LOAD_PARAM_GROUP时，uParam表示切换后的参数组的索引号，从0开始，分别对应A,B,C,D四组
	    当 MSG 为 SHEET_MSG_LOAD_PARAM_FROMFILE时，uParam表示被文件中参数覆盖的参数组的索引号，从0开始，分别对应A,B,C,D四组
	    当 MSG 为 SHEET_MSG_SAVE_PARAM_GROUP时，uParam表示当前保存的参数组的索引号，从0开始，分别对应A,B,C,D四组
        */
        public void SettingPageMsgCalBack(CameraHandle hCamera, uint MSG, uint uParam, IntPtr pContext)
        {

        }

        private bool InitCamera()
        {
            CameraSdkStatus status;
            CameraSdkStatus statusdeadpix;
            tSdkCameraDevInfo[] tCameraDevInfoList;
            //IntPtr ptr;
            //int i;
#if USE_CALL_BACK
            CAMERA_SNAP_PROC pCaptureCallOld = null;
#endif
            if (m_hCamera > 0)
            {
                //已经初始化过，直接返回 true

                return true;
            }

            status = MvApi.CameraEnumerateDevice(out tCameraDevInfoList);
            if (status == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
            {
                if (tCameraDevInfoList != null)//此时iCameraCounts返回了实际连接的相机个数。如果大于1，则初始化第一个相机
                {
                    status = MvApi.CameraInit(ref tCameraDevInfoList[0], -1, -1, ref m_hCamera);
                    if (status == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                    {
                        //获得相机特性描述
                        MvApi.CameraGetCapability(m_hCamera, out tCameraCapability);

                        m_ImageBuffer = Marshal.AllocHGlobal(tCameraCapability.sResolutionRange.iWidthMax * tCameraCapability.sResolutionRange.iHeightMax * 3 + 1024);
                        m_ImageBufferSnapshot = Marshal.AllocHGlobal(tCameraCapability.sResolutionRange.iWidthMax * tCameraCapability.sResolutionRange.iHeightMax * 3 + 1024);

                        //初始化显示模块，使用SDK内部封装好的显示接口
                       // this.PreviewBox.Width = this.Width;

                        MvApi.CameraDisplayInit(m_hCamera, PreviewBox.Handle);
                        MvApi.CameraSetDisplaySize(m_hCamera, 1080, 810);// PreviewBox.Width, PreviewBox.Height);

                        //设置抓拍通道的分辨率。
                        tSdkImageResolution tResolution;
                        tResolution.uSkipMode = 0;
                        tResolution.uBinAverageMode = 0;
                        tResolution.uBinSumMode = 0;
                        tResolution.uResampleMask = 0;
                        tResolution.iVOffsetFOV = 0;
                        tResolution.iHOffsetFOV = 0;
                        tResolution.iWidthFOV = tCameraCapability.sResolutionRange.iWidthMax;
                        tResolution.iHeightFOV = tCameraCapability.sResolutionRange.iHeightMax;
                        tResolution.iWidth = tResolution.iWidthFOV;
                        tResolution.iHeight = tResolution.iHeightFOV;
                        //tResolution.iIndex = 0xff;表示自定义分辨率,如果tResolution.iWidth和tResolution.iHeight
                        //定义为0，则表示跟随预览通道的分辨率进行抓拍。抓拍通道的分辨率可以动态更改。
                        //本例中将抓拍分辨率固定为最大分辨率。
                        tResolution.iIndex = 0xff;
                        tResolution.acDescription = new byte[32];//描述信息可以不设置
                        tResolution.iWidthZoomHd = 0;
                        tResolution.iHeightZoomHd = 0;
                        tResolution.iWidthZoomSw = 0;
                        tResolution.iHeightZoomSw = 0;

                        MvApi.CameraSetResolutionForSnap(m_hCamera, ref tResolution);

                        //让SDK来根据相机的型号动态创建该相机的配置窗口。
                        MvApi.CameraCreateSettingPage(m_hCamera, this.Handle, tCameraDevInfoList[0].acFriendlyName,/*SettingPageMsgCalBack*/null,/*m_iSettingPageMsgCallbackCtx*/(IntPtr)null, 0);
                      
                        //两种方式来获得预览图像，设置回调函数或者使用定时器或者独立线程的方式，
                        //主动调用CameraGetImageBuffer接口来抓图。
                        //本例中仅演示了两种的方式,注意，两种方式也可以同时使用，但是在回调函数中，
                        //不要使用CameraGetImageBuffer，否则会造成死锁现象。
#if USE_CALL_BACK
                        m_CaptureCallback = new CAMERA_SNAP_PROC(ImageCaptureCallback);
                        MvApi.CameraSetCallbackFunction(m_hCamera, m_CaptureCallback, m_iCaptureCallbackCtx, ref pCaptureCallOld);
#else //如果需要采用多线程，使用下面的方式
                        m_bExitCaptureThread = false;
                        m_tCaptureThread = new Thread(new ThreadStart(CaptureThreadProc));
                        m_tCaptureThread.Start();

#endif
                        //MvApi.CameraReadSN 和 MvApi.CameraWriteSN 用于从相机中读写用户自定义的序列号或者其他数据，32个字节
                        //MvApi.CameraSaveUserData 和 MvApi.CameraLoadUserData用于从相机中读取自定义数据，512个字节
                       // MvApi.CameraSetAeState(m_hCamera, 0);
                       // MvApi.CameraSetExposureTime(m_hCamera, 1000);
                        statusdeadpix = MvApi.CameraLoadDeadPixelsFromFile(m_hCamera, "c:\\deadpix.mdp");
                        MvApi.CameraSetCorrectDeadPixel(m_hCamera, 1);
                       // MessageBox.Show(statusdeadpix.ToString(), "相机坏点加载错误码", MessageBoxButtons.OK);
                        return true;

                    }
                    else
                    {
                        m_hCamera = 0;
                       // StateLabel.Text = "Camera init error";
                        return false;
                    }
                   

                }
            }

            return false;

        }

        private void MonitorForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (m_hCamera > 0)
            {
#if !USE_CALL_BACK //使用回调函数的方式则不需要停止线程
                m_bExitCaptureThread = true;
                while (m_tCaptureThread.IsAlive)
                {
                    Thread.Sleep(10);
                }
#endif
                MvApi.CameraUnInit(m_hCamera);
                Marshal.FreeHGlobal(m_ImageBuffer);
                Marshal.FreeHGlobal(m_ImageBufferSnapshot);
                m_hCamera = 0;
            } 

            DialogResult result = MessageBox.Show("确定要退出系统吗？", "提示", MessageBoxButtons.OKCancel);
            if (result == DialogResult.OK)
            {
                byte[] send = hexStringToBytes(ConvertNumToHex("256"));
                byte[] sendlight = hexStringToBytes(ConvertNumToHex((200+ 512).ToString()));
                if (port1.IsOpen)
                {
                    port1.Write(sendlight, 0, 2);
                    port1.Write(send, 0, 2);
                    port1.Close();
                }
                Application.ExitThread();
            }
            else
            {
                e.Cancel = true;
            }

        }
        //1秒更新一次视频信息
        private void timer1_Tick(object sender, EventArgs e)
        {
            tSdkFrameStatistic tFrameStatistic;
            if (m_hCamera > 0)
            {
                //获得SDK中图像帧统计信息，捕获帧、错误帧等。
                MvApi.CameraGetFrameStatistic(m_hCamera, out tFrameStatistic);
                //显示帧率有应用程序自己记录。
                string sFrameInfomation = String.Format("| Resolution:{0}*{1} | Display frames{2} | Capture frames{3} |", m_tFrameHead.iWidth, m_tFrameHead.iHeight, m_iDisplayedFrames, tFrameStatistic.iCapture);
                //StateLabel.Text = sFrameInfomation;

            }
            else
            {
                //StateLabel.Text = "";
            } 
        }
        //文本转为十六进制
        public static string ConvertNumToHex(string ten)
        {
            ulong Numb = Convert.ToUInt64(ten);
            ulong divValue, resValue;
            string hex = "";
            do
            {
                divValue = (ulong)Math.Floor((decimal)(Numb / 16));

                resValue = Numb % 16;
                hex = GetNumb(resValue) + hex;
                Numb = divValue;
            }
            while (Numb >= 16);

            if (Numb != 0)
                hex = GetNumb(Numb) + hex;
            if (hex.Length == 1 || hex.Length == 3)
                hex = "0" + hex;
            return hex;
        }
        public static byte[] hexStringToBytes(String hexString)
        {
            if (hexString == null || hexString.Equals(""))
            {
                return null;
            }
            hexString = hexString.ToUpper();
            int length = hexString.Length / 2;
            char[] hexChars = hexString.ToCharArray();
            byte[] d = new byte[length];
            for (int i = 0; i < length; i++)
            {
                int pos = i * 2;
                d[i] = (byte)(charToByte(hexChars[pos]) << 4 | charToByte(hexChars[pos + 1]));
            }
            return d;
        }
        private static byte charToByte(char c)
        {
            return (byte)"0123456789ABCDEF".IndexOf(c);
        }
        public static string GetNumb(ulong Numb)
        {
            switch (Numb)
            {
                case 0:
                case 1:
                case 2:
                case 3:
                case 4:
                case 5:
                case 6:
                case 7:
                case 8:
                case 9:
                    return Numb.ToString();
                case 10:
                    return "A";
                case 11:
                    return "B";
                case 12:
                    return "C";
                case 13:
                    return "D";
                case 14:
                    return "E";
                case 15:
                    return "F";
                default:
                    return "";
            }
        }
        //用于分辨率切换时，刷新背景绘图
        private void timer2_Tick(object sender, EventArgs e)
        {
            //切换分辨率后，擦除一次背景
            if (m_bEraseBk == true)
            {
                m_bEraseBk = false;
                PreviewBox.Refresh();
            }
        }

        private void BtnPlay_Click(object sender, EventArgs e)
        {
            if (m_hCamera < 1)//还未初始化相机
            {
                if (InitCamera() == true)
                {
                    MvApi.CameraPlay(m_hCamera);
                    BtnPlay.Text = "Pause";
                }
            }
            else//已经初始化
            {
                if (BtnPlay.Text == "Play")
                {
                    MvApi.CameraPlay(m_hCamera);
                    BtnPlay.Text = "Pause";
                }
                else
                {
                    MvApi.CameraPause(m_hCamera);
                    BtnPlay.Text = "Play";
                }
            }
        }
        private void 抓拍ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            snapshotDNA();
        }
        public void snapshotDNA()
        {
            tSdkFrameHead tFrameHead;
            IntPtr uRawBuffer;//由SDK中给RAW数据分配内存，并释放 
            if (m_hCamera <= 0)
            {
                return;//相机还未初始化，句柄无效
            }

            if (MvApi.CameraSnapToBuffer(m_hCamera, out tFrameHead, out uRawBuffer, 2500) == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
            {
                //此时，uRawBuffer指向了相机原始数据的缓冲区地址，默认情况下为8bit位宽的Bayer格式，如果
                //您需要解析bayer数据，此时就可以直接处理了，后续的操作演示了如何将原始数据转换为RGB格式
                //并显示在窗口上。 
                //将相机输出的原始数据转换为RGB格式到内存m_ImageBufferSnapshot中
                MvApi.CameraImageProcess(m_hCamera, uRawBuffer, m_ImageBufferSnapshot, ref tFrameHead);
                //CameraSnapToBuffer成功调用后必须用CameraReleaseImageBuffer释放SDK中分配的RAW数据缓冲区
                //否则，将造成死锁现象，预览通道和抓拍通道会被一直阻塞，直到调用CameraReleaseImageBuffer释放后解锁。
                MvApi.CameraReleaseImageBuffer(m_hCamera, uRawBuffer);
                //更新抓拍显示窗口。
                SnapshotDlg m_DlgSnapshot = new SnapshotDlg();               //显示抓拍图像的窗口                
                //m_DlgSnapshot.MdiParent = this.ParentForm;
                m_DlgSnapshot.Show();
                m_DlgSnapshot.UpdateImage(ref tFrameHead, m_ImageBufferSnapshot);
            }
            else
            {
                MessageBox.Show("相机忙，请稍后抓拍！", "相机忙碌", MessageBoxButtons.OK);
            }
        }
        private void 相机设置ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (m_hCamera > 0)
            {
                MvApi.CameraShowSettingPage(m_hCamera, 1);//1 show ; 0 hide
            }
        }

        private void 保存ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_bSaveImage = true;//通知预览线程，保存一张图片。您也可以参考BtnSnapshot_Click 中抓图方式，重新抓一张图片，然后调用 MvApi.CameraSaveImage 进行图片保存。      
            //SnapshotDlg m_DlgSnapshot = new SnapshotDlg();               //显示抓拍图像的窗口
            //m_DlgSnapshot.Show();
        }

        private void 开始录像ToolStripMenuItem_Click(object sender, EventArgs e)
        {

           // CameraSdkStatus iret;
            if (m_iRecordState == (int)emSdkRecordMode.RECORD_START)
            {
                MessageBox.Show("Already started.");
                return;//已经是录像状态；
            }
            else if (m_iRecordState == (int)emSdkRecordMode.RECORD_PAUSE)
            {
                m_iRecordState = (int)emSdkRecordMode.RECORD_START;
                return;
            }
            String strFilePath;
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Title = "Save as";
            saveDlg.OverwritePrompt = true;

            //create a filter;
            saveDlg.Filter = "AVI file(*.avi)|*.avi||";
            saveDlg.ShowHelp = true;
            if (saveDlg.ShowDialog() == DialogResult.OK)
            {
                strFilePath = saveDlg.FileName;
                string strFilExtn = strFilePath.Remove(0, strFilePath.Length - 3);
                if (strFilePath == "")
                {
                    return;
                }
                if (strFilePath.IndexOf(".avi") < 0 && strFilePath.IndexOf(".AVI") < 0)
                {
                    strFilePath += ".avi";
                }
                //byte[] file_path_bytes = Encoding.Default.GetBytes(strFilePath);
                string file_path_bytes = strFilePath;
                //iret = ;
                System.Diagnostics.Debug.Assert(MvApi.CameraInitRecord(m_hCamera, 0, file_path_bytes, (uint)0, (uint)100, 60) == CameraSdkStatus.CAMERA_STATUS_SUCCESS);

                m_iRecordState = (int)emSdkRecordMode.RECORD_START;
                file_path_bytes = null;
                GC.Collect();
                // file_path_bytes = "";
                //  MvApi.(m_hCamera, uRawBuffer);

            }
        }

        private void 停止录像ToolStripMenuItem_Click(object sender, EventArgs e)
        { 
            // TODO: 在此添加控件通知处理程序代码
            if (m_iRecordState == (int)emSdkRecordMode.RECORD_STOP)
            {
                MessageBox.Show("No record is started!");
                return;//已经是录像状态；
            } 
            m_iRecordState = (int)emSdkRecordMode.RECORD_STOP; 
            MvApi.CameraStopRecord(m_hCamera); 
            MessageBox.Show("Record saved.");
        }
        //private const int SW_HIDE = 0;  //隐藏任务栏
        //private const int SW_RESTORE = 9;//显示任务栏
        //[DllImport("user32.dll")]
        //public static extern int ShowWindow(int hwnd, int nCmdShow);
        //[DllImport("user32.dll")]
        //public static extern int FindWindow1(string lpClassName, string lpWindowName);
        private void MonitorForm_Load(object sender, EventArgs e)
        { 
            this.SetVisibleCore(false);
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.SetVisibleCore(true);
            this.ShowInTaskbar = false;
           // ShowWindow(FindWindow1("Shell_TrayWnd",null),SW_HIDE);
            ////隐藏窗口边框  
            //this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            ////获取屏幕的宽度和高度  
            //int w = System.Windows.Forms.SystemInformation.VirtualScreen.Width;
            //int h = System.Windows.Forms.SystemInformation.VirtualScreen.Height;
            ////设置最大尺寸  和  最小尺寸  （如果没有修改默认值，则不用设置）  
            //this.MaximumSize = new Size(w, h);
            //this.MinimumSize = new Size(w, h);
            ////设置窗口位置  
            //this.Location = new Point(0, 0);
            ////设置窗口大小  
            ////this.Width = w;
            ////this.Height = h;
          //  PreviewBox.Height = this.Height - 34;
           // PreviewBox.Top = (this.Height - PreviewBox.Height) / 2;
          //  PreviewBox.Left = (this.Width - PreviewBox.Width) / 2;
             PreviewBox.Top = 0;
           // this.panel2.Top = this.Height-60;
           // this.panel2.Left = (this.Width - panel2.Width) / 2;
            //置顶显示  
            //this.TopMost = true;  
            openport();
            
        }
        private void openport()
        {
            try
            {
                if (port1.IsOpen)
                    port1.Close();
               
                else
                {
                    InitCOM("COM8");
                    OpenPort();
                }
            }
            catch
            {
            }
        }


        private void 暂停录像ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            m_iRecordState = (int)emSdkRecordMode.RECORD_PAUSE;
        }
        public void InitCOM(string PortName)
        {
            port1 = new SerialPort(PortName);
            port1.BaudRate = 9600;//波特率
            port1.Parity = Parity.None;//无奇偶校验位
            port1.StopBits = StopBits.One;//两个停止位
            port1.Handshake = Handshake.None;//控制协议
            port1.ReceivedBytesThreshold = 4;//设置 DataReceived 事件发生前内部输入缓冲区中的字节数
            port1.DataReceived += new SerialDataReceivedEventHandler(port1_DataReceived);//DataReceived事件委托
        }
        
        private void port1_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int ReceiveNums = port1.BytesToRead;
            port1.Encoding = System.Text.Encoding.GetEncoding("GB2312");//解决中午乱码问题，国标2312编码格式
            //String porline = "";
            //porline = port1.ReadLine(); 
            SetText(port1.ReadLine());
        }
        delegate void SetTextCallBack(string text);
        private void SetText(string text)
        {
            if (this.textBox1.InvokeRequired)
            {
                SetTextCallBack stcb = new SetTextCallBack(SetText);
                this.Invoke(stcb, new object[] { text });
            }
            else
            {
                if (text.Equals("SOS"+"\r"))
                    MessageBox.Show("请重新插入芯片", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                else if (text.Equals("OK" + "\r"))
                    SendKeys.Send("ENTER");
                else if (text.Equals("OFF"+"\r"))
                {
                    DialogResult result = MessageBox.Show("退出保护模式", "提示", MessageBoxButtons.OK, MessageBoxIcon.Question);
                    if (result == DialogResult.OK)
                    {
                        try
                        {
                            if (port1.IsOpen)
                            {
                                byte[] send = hexStringToBytes("0301");
                                port1.Write(send, 0, 2);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show("错误：" + ex.Message);
                        }
                    }
                }
                else
                {
                    try
                    {
                        int curval = int.Parse(text);
                        if (closeflage)
                        {
                            this.textBox3.Text = "0";
                        }
                        else if (curval < 12)
                            this.textBox3.Text = "";
                        else if (System.Math.Abs(curval - curinputvol) < 3)
                            this.textBox3.Text = curinputvol.ToString();
                        else
                            this.textBox3.Text = text;
                    }
                    catch
                    { 
                    } 
                }
            }
        }

      


        private void BtnMainThread_Click(object sender, EventArgs e) //主线程调用textBox1 
        {
            this.textBox1.Text = "Main Thread";
        }


        //打开串口的方法
        public void OpenPort()
        {
            try
            {
                if (!port1.IsOpen)
                {
                    port1.Open();
                }
            }
            catch
            {
                MessageBox.Show("未发现端口");
               // MessageBox.Show("未发现端口", "提示", MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            }

        }
        
        //关闭串口的方法
        public void ClosePort()
        {
            if (port1.IsOpen)
            {
                port1.Close();
            }
        }

        private void timer3_Tick(object sender, EventArgs e)
        {
            second = second + 1;
            string timNewTime = Convert.ToString((int)Math.Floor((double)second));
            // string timNewHour = Convert.ToString((int)Math.Floor((double)(second / 3600)));
            //定义“时”的值 
            //if (Convert.ToInt32(timNewHour) < 10) //值小于10的时候,在前面加0 
            //{
            //    timNewHour = "0" + timNewHour;
            //}
            string timNewMinute = Convert.ToString(((int)Math.Floor((double)(second / 60)) % 60));
            if (Convert.ToInt32(timNewMinute) < 10) //值小于10的时候,在前面加0 
            {
                timNewMinute = "0" + timNewMinute;
            }
            string timNewSecond = Convert.ToString(second % 60);
            if (Convert.ToInt32(timNewSecond) < 10) //值小于10的时候,在前面加0 
            {
                timNewSecond = "0" + timNewSecond;
            }
            timNewTime = timNewMinute + "分" + timNewSecond + "秒";
            //timNewTime = timNewHour + "时" + timNewMinute + "分" + timNewSecond + "秒";
            textBox4.Text = timNewTime;// +"秒";  //显示时间 
            if (second.ToString().Equals(textBox5.Text))
            { 
                byte[] send = hexStringToBytes(ConvertNumToHex("256"));
                try
                {
                    if (port1.IsOpen)
                        port1.Write(send, 0, 2); 
                    timer3.Enabled = false;
                    closeflage = true; 
                }
                catch (Exception ex)
                {
                    MessageBox.Show("错误：" + ex.Message);
                }  
            } 
        }
         
        private void label5_Click(object sender, EventArgs e)
        {
            if (textBox5.Visible == true)
            {
                textBox5.Visible = false;
                label6.Visible = false;
            }
            else
            {
                textBox5.Visible = true;
                label6.Visible = true;
            }
        }

        

        

        private void button3_Click(object sender, EventArgs e)
        {
            ShowInputPanel();
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Title = "Save as";
            saveDlg.OverwritePrompt = true;

            //create a filter;
            saveDlg.Filter = "BMP files(*.bmp)|*.bmp|" +
                             "Gif files(*.gif)|*.gif|" +
                             "JPEG files(*jpg)|*.jpg";
            saveDlg.ShowHelp = true;
            if (saveDlg.ShowDialog() == DialogResult.OK)
            {
                file_path = saveDlg.FileName;
                m_bSaveImage = true;
                HideInputPanel();
            } 
            
        }

        private void button8_Click(object sender, EventArgs e)
        {
          //  textBox4.Text = ConvertNumToHex((256 + Convert.ToInt32(textBox2.Text)).ToString());
            if (!closeflage)
            {
                MessageBox.Show("请先停止实验再开始下一实验！");
                return;
            }
            if (textBox2.Text == "")
            {
                MessageBox.Show("请输入电压值！");
                return;
            }
            try
            {
                if (port1.IsOpen)
                    port1.Close(); 

                if (!port1.IsOpen)
                {
                    port1.Open();
                }
               
                if (port1.IsOpen)
                {
                    byte[] send = hexStringToBytes(ConvertNumToHex((256+Convert.ToInt32(textBox2.Text)).ToString()));
                    curinputvol = int.Parse(textBox2.Text);
                    port1.Write(send, 0, 2);
                    second = 0;
                    timer3.Enabled = true;
                    closeflage = false;
                    experimentstar = true;
                }
                

            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }

        private void button9_Click(object sender, EventArgs e)
        {
            byte[] send = hexStringToBytes(ConvertNumToHex("256"));
            try
            {
                if (port1.IsOpen)
                    port1.Write(send, 0, 2); 
                timer3.Enabled = false; 
                closeflage = true;
                experimentstar = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (m_hCamera > 0)
            {
                MvApi.CameraShowSettingPage(m_hCamera, 1);//1 show ; 0 hide
            }
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // CameraSdkStatus iret;
            if (m_iRecordState == (int)emSdkRecordMode.RECORD_START)
            {
                MessageBox.Show("Already started.");
                return;//已经是录像状态；
            }
            else if (m_iRecordState == (int)emSdkRecordMode.RECORD_PAUSE)
            {
                m_iRecordState = (int)emSdkRecordMode.RECORD_START;
                return;
            }
            String strFilePath;
            SaveFileDialog saveDlg = new SaveFileDialog();
            saveDlg.Title = "Save as";
            saveDlg.OverwritePrompt = true;

            //create a filter;
            saveDlg.Filter = "AVI file(*.avi)|*.avi||";
            saveDlg.ShowHelp = true;
            if (saveDlg.ShowDialog() == DialogResult.OK)
            {
                strFilePath = saveDlg.FileName;
                string strFilExtn = strFilePath.Remove(0, strFilePath.Length - 3);
                if (strFilePath == "")
                {
                    return;
                }
                if (strFilePath.IndexOf(".avi") < 0 && strFilePath.IndexOf(".AVI") < 0)
                {
                    strFilePath += ".avi";
                }
               // byte[] file_path_bytes = Encoding.Default.GetBytes(strFilePath);
                string file_path_bytes = strFilePath;
                //iret = ;
                System.Diagnostics.Debug.Assert(MvApi.CameraInitRecord(m_hCamera, 0, file_path_bytes, (uint)0, (uint)100, 60) == CameraSdkStatus.CAMERA_STATUS_SUCCESS);

                m_iRecordState = (int)emSdkRecordMode.RECORD_START;
                file_path_bytes = null;
                GC.Collect();
                // file_path_bytes = "";
                //  MvApi.(m_hCamera, uRawBuffer);

            }
        }

        private void button6_Click(object sender, EventArgs e)
        {
            m_iRecordState = (int)emSdkRecordMode.RECORD_PAUSE;
        }

        private void button7_Click(object sender, EventArgs e)
        {
            // TODO: 在此添加控件通知处理程序代码
            if (m_iRecordState == (int)emSdkRecordMode.RECORD_STOP)
            {
                MessageBox.Show("No record is started!");
                return;//已经是录像状态；
            }
            m_iRecordState = (int)emSdkRecordMode.RECORD_STOP;
            MvApi.CameraStopRecord(m_hCamera);
            MessageBox.Show("Record saved.");
        }

        private void button10_Click(object sender, EventArgs e)
        { 
            try
            {
                if (experimentstar)
                    MessageBox.Show("请先停止电泳,再关闭程序");
                else
                    this.Close();  
            }
            catch
            {
            }
        }

        private void comboBox1_DropDownClosed(object sender, EventArgs e)
        {
            try
            {
                if (port1.IsOpen)
                    port1.Close();
                if (comboBox1.SelectedIndex == 0)
                    MessageBox.Show("未选择端口");
                else
                {
                  //  InitCOM(comboBox1.Items[comboBox1.SelectedIndex].ToString());
                    InitCOM("COM8");
                    OpenPort();
                }
            }
            catch
            {
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            SnapshotDlg m_DlgSnapshot = new SnapshotDlg();               //显示抓拍图像的窗口
            // m_DlgSnapshot.MdiParent = this;
            m_DlgSnapshot.Show();
        }

        private void button2_Click(object sender, EventArgs e)
        {
            snapshotDNA();
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
        private void textBox2_Click(object sender, EventArgs e)
        {
            ShowInputPanel();
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            HideInputPanel();
        }

        private void textBox5_Click(object sender, EventArgs e)
        {
            ShowInputPanel();
        }

        private void textBox5_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
                HideInputPanel();
        }

        private void trackBar1_Scroll(object sender, EventArgs e)
        {
          //  textBox2.Text = trackBar1.Value.ToString();
            label9.Text = string.Format("{0}", trackBar1.Value);
            try
            {
                if (port1.IsOpen)
                {
                   // byte[] send = hexStringToBytes(ConvertNumToHex((200-trackBar1.Value+512).ToString()));
                    byte[] send = hexStringToBytes(ConvertNumToHex((trackBar1.Value + 512).ToString()));
                   // curinputvol = int.Parse(textBox2.Text);
                    port1.Write(send, 0, 2);
                    
                }
               // textBox4.Text = ConvertNumToHex((trackBar1.Value + 512).ToString());

            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }

        private void button11_Click(object sender, EventArgs e)
        {
            if (trackBar1.Value + 1 <= trackBar1.Maximum)
            {
                trackBar1.Value = trackBar1.Value + 1;
                label9.Text = string.Format("{0}", trackBar1.Value);
                try
                {
                    if (port1.IsOpen)
                    {
                        byte[] send = hexStringToBytes(ConvertNumToHex((trackBar1.Value + 512).ToString()));
                        // curinputvol = int.Parse(textBox2.Text);
                        port1.Write(send, 0, 2);

                    }
                    // textBox4.Text = ConvertNumToHex((trackBar1.Value + 512).ToString());

                }
                catch (Exception ex)
                {
                    MessageBox.Show("错误：" + ex.Message);
                }
            }
        }

        private void button12_Click(object sender, EventArgs e)
        {
            if (trackBar1.Value - 1 >=0)
            {
                trackBar1.Value = trackBar1.Value - 1;
                label9.Text = string.Format("{0}", trackBar1.Value);
                try
                {
                    if (port1.IsOpen)
                    {
                        byte[] send = hexStringToBytes(ConvertNumToHex((trackBar1.Value + 512).ToString()));
                        // curinputvol = int.Parse(textBox2.Text);
                        port1.Write(send, 0, 2);

                    }
                    // textBox4.Text = ConvertNumToHex((trackBar1.Value + 512).ToString());

                }
                catch (Exception ex)
                {
                    MessageBox.Show("错误：" + ex.Message);
                }
            }
        }

        private void textBox2_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (((int)e.KeyChar < 48 || (int)e.KeyChar > 57) && (int)e.KeyChar != 8 && (int)e.KeyChar != 46)
                e.Handled = true;
            //小数点的处理。
            if ((int)e.KeyChar == 46)                           //小数点
            {
                if (textBox2.Text.Length <= 0)
                    e.Handled = true;   //小数点不能在第一位
                else
                {
                    float f;
                    float oldf;
                    bool b1 = false, b2 = false;
                    b1 = float.TryParse(textBox2.Text, out oldf);
                    b2 = float.TryParse(textBox2.Text + e.KeyChar.ToString(), out f);
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

        private void textBox5_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (((int)e.KeyChar < 48 || (int)e.KeyChar > 57) && (int)e.KeyChar != 8 && (int)e.KeyChar != 46)
                e.Handled = true;
            //小数点的处理。
            if ((int)e.KeyChar == 46)                           //小数点
            {
                if (textBox5.Text.Length <= 0)
                    e.Handled = true;   //小数点不能在第一位
                else
                {
                    float f;
                    float oldf;
                    bool b1 = false, b2 = false;
                    b1 = float.TryParse(textBox5.Text, out oldf);
                    b2 = float.TryParse(textBox5.Text + e.KeyChar.ToString(), out f);
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

        private void button13_Click(object sender, EventArgs e)
        {
            try
            {
                if (port1.IsOpen)
                {
                    byte[] send = hexStringToBytes("0301");
                    port1.Write(send, 0, 2);
                    Thread.Sleep(1000);
                    MessageBox.Show("已复位");

                }
                // textBox4.Text = ConvertNumToHex((trackBar1.Value + 512).ToString());

            }
            catch (Exception ex)
            {
                MessageBox.Show("错误：" + ex.Message);
            }
        }
    }

}
