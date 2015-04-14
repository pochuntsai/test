//#define OBSTACLE

using System;
using Android.App;
using Android.Content;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using Android.Graphics;
using System.Linq;
using System.IO;
using Trilateration;
using Android.Graphics.Drawables;
using System.Threading;
using Idv.Android.Hellouart;
using Android.Util;

namespace Trilateration_Android
{
    [Activity(Label = "Trilateration_Android", MainLauncher = true, Icon = "@drawable/icon", ScreenOrientation = Android.Content.PM.ScreenOrientation.Landscape)]
    public class MainActivity : Activity
    {
        private int i;

        private int ComPortDataReady;
        private int MainWidth, MainHeight;
        private int offset;
        private System.Drawing.Point mapsize;
        private Single range_ratio;

        //private Single x_scale, y_scale;
        //private Single screen2map_x, screen2map_y;
        private Single screen2grid_x, screen2grid_y;
        private Single screen2cm_x, screen2cm_y;


        private int target_total;
        private int target_now;
        private Single[] err = new Single[5];
        private struct_PointF mouse_position = new struct_PointF();
        private struct_PointF previous_step = new struct_PointF();
        private struct_PointF[] target = new struct_PointF[100];
        private struct_Location[] favorite = new struct_Location[20];

        private beacon anchor1 = new beacon();
        private beacon anchor2 = new beacon();
        private beacon anchor3 = new beacon();
        private beacon myTag = new beacon();
        private Single[] myTag_Old_X = new Single[5];
        private Single[] myTag_Old_Y = new Single[5];

        //public SerialPort tagPort;
        //public SerialPort drivingPort;
        private int tagbufflen;
        private byte[] tagbuff = new byte[30];
        private int drivingbufflen;
        private byte[] drivingbuff = new byte[30];

        private int[] mField = new int[3];
        private int[] mOffset = new int[2];
        private int[] mMax = new int[5];
        private int[] mMin = new int[5];
        
        private class_iteration Iteration;

        private struct_config cfg = new struct_config();
        private move_command myCommand = new move_command();
        private class_Vehicle myVehicle = new class_Vehicle();
        private class_flag myFlag = new class_flag();
        private Map myMap = new Map();
        private class_EKFL5 myEKF;

        private static System.IO.StreamWriter wr;

        HighPerformanceCounter hpcounter1 = new HighPerformanceCounter();
        HighPerformanceCounter hpcounter2 = new HighPerformanceCounter();
        HighPerformanceCounter hpcounter3 = new HighPerformanceCounter();
        HighPerformanceCounter hpcounter4 = new HighPerformanceCounter();

        // for testing
        private int test_count = 0;
        private bool test_start;
        private string test_str;
        obstacle ob;
        DrawView view;
        TextView textView;
        Button buttonTest2, btnDelete, btnGo;
        FrameLayout linearContent;
        TextView labelA, labelB, labelC, labelTag, labelVehicle, labelTable, labelTableC;
        //Brian+: add UART releate variables
        int pic32_open = 0;
        int tag_open = 0;
        System.Timers.Timer timer1, Pic32DataRecvTimer, TagDataRecvTimer;
        private static System.IO.StreamWriter fwr;

        protected override void OnCreate(Bundle bundle)
        {
            string path = Android.OS.Environment.ExternalStorageDirectory.AbsolutePath;
            path=System.IO.Path.Combine(path, "Trilateration_Android");
            ComPortDataReady = 0;
            target_total = 0;
            target_now = 1;
            myFlag.screen_ready = false;
            
            // set initial value
            anchor1.X = 50;
            anchor2.X = 150;
            anchor3.X = 250;
            myTag.X = 100;
            myTag.Avg.X = 0;
            anchor1.Y = 100;
            anchor2.Y = 100;
            anchor3.Y = 100;
            myTag.Y = 200;
            myTag.Avg.Y = 0;
            mapsize.X = 1000;
            mapsize.Y = 800;
            offset = 10;
            
            for (i = 0; i <= myTag_Old_X.Length - 1; i++)
            {
                myTag_Old_X[i] = 0;
                myTag_Old_Y[i] = 0;
            }



            // obstacle-avoid algorithm
            ob = new obstacle(80, 0.1f);

            Iteration = new class_iteration();



            base.OnCreate(bundle);
            this.RequestWindowFeature(WindowFeatures.NoTitle);
            SetContentView(Resource.Layout.Main);
            var btnConnect = this.FindViewById<Button>(Resource.Id.btnConnect);
            btnDelete = this.FindViewById<Button>(Resource.Id.btnDelete);
            btnGo = this.FindViewById<Button>(Resource.Id.btnGo);
            var btnLoad = this.FindViewById<Button>(Resource.Id.btnLoad);
            linearContent = this.FindViewById<FrameLayout>(Resource.Id.linearContent);
            var spinnerTarget = this.FindViewById<Spinner>(Resource.Id.spinnerTarget);
            textView = this.FindViewById<TextView>(Resource.Id.textView);
            var buttonTest = this.FindViewById<Button>(Resource.Id.buttonTest);
            buttonTest2 = this.FindViewById<Button>(Resource.Id.buttonTest2);
            var buttonCalCompass = this.FindViewById<Button>(Resource.Id.buttonCalCompass);
            labelA= this.FindViewById<TextView>(Resource.Id.labelA);
            labelB= this.FindViewById<TextView>(Resource.Id.labelB);
            labelC=   this.FindViewById<TextView>(Resource.Id.labelC);
            labelTag=  this.FindViewById<TextView>(Resource.Id.labelTag);
            labelVehicle = this.FindViewById<TextView>(Resource.Id.labelVehicle);
            labelTable = this.FindViewById<TextView>(Resource.Id.labelTable);
            //Brian+ for debug
            labelTableC = this.FindViewById<TextView>(Resource.Id.labelTableC);   
            Toast.MakeText(this, path, ToastLength.Long).Show();

            btnLoad.SetX(300);
            //Brian+ for test: create new default.set and raw.bmp anyway
            //if (!System.IO.Directory.Exists(path))
            {
              
               System.IO.Directory.CreateDirectory(path);
               int i=0;
               using(var stream = Assets.Open("default.set"))
               {
                   //using (FileStream fs = new FileStream(path + @"/default.set", FileMode.CreateNew))
                   using (FileStream fs = new FileStream(path + @"/default.set", FileMode.Create))
                   {
                       byte[] byt = new byte[1024];
                       while ((i = stream.Read(byt, 0, byt.Length)) != 0)
                       {
                           fs.Write(byt, 0, i);
                       }
                       fs.Flush();
                   }
               }
               using (var stream = Assets.Open("Raw.bmp"))
               {
                   //using (FileStream fs = new FileStream(path + @"/Raw.bmp", FileMode.CreateNew))
                   using (FileStream fs = new FileStream(path + @"/Raw.bmp", FileMode.Create))
                   {
                       byte[] byt = new byte[1024];
                       while ((i = stream.Read(byt, 0, byt.Length)) != 0)
                       {
                           fs.Write(byt, 0, i);
                       }
                       fs.Flush();
                   }
               }
                
            }

          
            // create log file
            string logName = path + @"/" + string.Format("log{0:HH}", DateTime.Now) + ".txt";       // Set the file name
            wr = new System.IO.StreamWriter(logName, true);
            wr.Write("==== " + DateTime.Now.ToString("yyyyMMdd,HH:mm:ss ") + " ====\r\n");

            //Brian+: create go log file
            string logGo = path + @"/" + "log_go.txt";       // Set the file name
            //fwr = new System.IO.StreamWriter(logGo, true);
            //fwr.WriteLine("==== FUCK TEST ====");
            //fwr.Write("==== " + DateTime.Now.ToString("yyyyMMdd,HH:mm:ss ") + " ====\r\n");
            //fwr.Close();

            view = new DrawView(this);

            view.LayoutParameters = new LinearLayout.LayoutParams(ViewGroup.LayoutParams.MatchParent, ViewGroup.LayoutParams.MatchParent);
            linearContent.AddView(view);

            bool UseWaitCursor = false;
            buttonCalCompass.Enabled = false;

            buttonCalCompass.Click += (sender, e) =>
            {
                byte[] outbyte = new byte[10];
                int outbytelen = 0;

                if (UseWaitCursor)
                {
                    UseWaitCursor = false;
                    outbyte[0] = 0x53;
                    outbyte[1] = 0x01;
                    outbyte[2] = 0x00;
                    outbyte[3] = 0x00;
                    outbyte[4] = 0x00;
                    outbyte[5] = 0x45;
                    outbytelen = 6;
                    Uart2C.SendMsgUart(pic32_open, outbyte);
                    //drivingPort.Write(outbyte, 0, outbytelen);
                }
                else
                {
                    UseWaitCursor = true;
                    hpcounter4.Start();
                    outbyte[0] = 0x53;
                    outbyte[1] = 0x01;
                    outbyte[2] = 0x04;
                    outbyte[3] = 0x00;
                    outbyte[4] = 0x00;
                    outbyte[5] = 0x45;
                    outbytelen = 6;
                    Uart2C.SendMsgUart(pic32_open, outbyte);
                    //drivingPort.Write(outbyte, 0, outbytelen);
                }
            };

            buttonTest2.Click += (sender, e) =>
            {
                myCommand.speed = 95;
                myCommand.turn = 55;
                while (true)
                {
                    Thread.Sleep(100);
                    OutCommand(myCommand);
                }
            };
            view.Visibility = ViewStates.Gone;
            
            view.OnDrawing += (sender, e) =>
            {

                int i;
                Single tmpSingle1, tmpSingle2;

                // Set pens
                Paint pen_anchor = new Paint() { Color = Color.DarkSeaGreen, StrokeWidth = 3, };
                pen_anchor.SetStyle(Paint.Style.Stroke); //Brian+ for set paint style=stroke
                Paint pen_tag_s = new Paint() { Color = Color.LawnGreen, StrokeWidth = 6 }; 
                Paint pen_tag_m = new Paint() { Color = Color.Firebrick, StrokeWidth = 6 }; 
                Paint pen_indication = new Paint() { Color = Color.Brown, StrokeWidth = 8 }; 
                Paint pen_old = new Paint() { Color = Color.Gray, StrokeWidth = 5 }; 
                Paint pen_target = new Paint() { Color = Color.Orange, StrokeWidth = 5 };

                //pen_indication.StartCap = LineCap.ArrowAnchor;
                //pen_indication.EndCap = LineCap.Round;
                
                // Labels and contents
                labelTag.SetX((int)(offset + myTag.X / screen2cm_x));
                labelTag.SetY((int)(offset + myTag.Y / screen2cm_y));

                //labelTag.Text = "Tag\r\nPos " + myTag.X.ToString("f1") + ", " + myTag.Y.ToString("f1") + "\r\nRate " + myTag.Rate.ToString() + "%";
                labelTag.Text = "Tag\r\n";

                // Graphics
                if (anchor1.Rate > 0)
                {
                    e.DrawCircle((int)(anchor1.X / screen2cm_x), (int)(anchor1.Y / screen2cm_y), 5, pen_anchor);
                    //e.DrawCircle((int)((anchor1.X - anchor1.Range) * x_scale), (int)((anchor1.Y - anchor1.Range) * y_scale), (int)(anchor1.Range * 2 * x_scale), pen_anchor);
                    //C# original function
                    //e.Graphics.DrawEllipse(pen_anchor, (int)((anchor1.X - anchor1.Range) * x_scale), (int)((anchor1.Y - anchor1.Range) * y_scale), (int)(anchor1.Range * 2 * x_scale), (int)(anchor1.Range * 2 * y_scale));
                }
                if (anchor2.Rate > 0)
                {
                    e.DrawCircle((int)(anchor2.X / screen2cm_x), (int)(anchor2.Y / screen2cm_y), 5, pen_anchor);
                    //e.DrawCircle( (int)((anchor2.X - anchor2.Range) * x_scale), (int)((anchor2.Y - anchor2.Range) * y_scale), (int)(anchor2.Range * 2 * x_scale), pen_anchor);
                    //C# original function
                    //e.Graphics.DrawEllipse(pen_anchor, (int)((anchor2.X - anchor2.Range) * x_scale), (int)((anchor2.Y - anchor2.Range) * y_scale), (int)(anchor2.Range * 2 * x_scale), (int)(anchor2.Range * 2 * y_scale));
                }
                if (anchor3.Rate > 0)
                {
                    e.DrawCircle((int)(anchor3.X / screen2cm_x), (int)(anchor3.Y / screen2cm_y), 5, pen_anchor);
                    //e.DrawCircle((int)((anchor3.X - anchor3.Range) * x_scale), (int)((anchor3.Y - anchor3.Range) * y_scale), (int)(anchor3.Range * 2 * x_scale), pen_anchor);
                    //C# original function
                    //e.Graphics.DrawEllipse(pen_anchor, (int)((anchor3.X - anchor3.Range) * x_scale), (int)((anchor3.Y - anchor3.Range) * y_scale), (int)(anchor3.Range * 2 * x_scale), (int)(anchor3.Range * 2 * y_scale));
                }
                if (myTag.Rate > 0)
                {
                    for (i = 0; i <= myTag_Old_X.Length - 1; i++)
                    {
                        e.DrawCircle((int)(myTag_Old_X[i] / screen2cm_x), (int)(myTag_Old_Y[i] / screen2cm_y), 3, pen_old);
                    }
                   
                }

                tmpSingle1 = (Single)(30 * Math.Cos(myVehicle.compass * 3.14 / 180f)) + myTag.Avg.X / screen2cm_x;
                tmpSingle2 = (Single)(30 * Math.Sin(myVehicle.compass * 3.14 / 180f)) + myTag.Avg.Y / screen2cm_y;
                //e.Graphics.DrawLine(pen_indication, (int)tmpSingle1, (int)tmpSingle2, (int)(myTag.X / screen2cm_x), (int)(myTag.Y / screen2cm_y));
                e.DrawLine((int)tmpSingle1, (int)tmpSingle2, (int)(myTag.X / screen2cm_x), (int)(myTag.Y / screen2cm_y), pen_indication);
                /*
                // robot is moving
                if (myFlag.moving)
                {
                    e.DrawLine((int)(previous_step.X * x_scale), (int)(previous_step.Y * y_scale), (int)(myTag.X * x_scale), (int)(myTag.Y * y_scale), pen_indication);
                }
                */
                if (target_total >= 1 && target_now >= 1)
                {
                    for (i = target_now; i <= target_total; i++)
                    {
                        e.DrawCircle((int)(target[i].X / screen2cm_x) - 4, (int)(target[i].Y / screen2cm_y) - 4, 7, pen_target);
                        e.DrawLine((int)(target[i - 1].X / screen2cm_x), (int)(target[i - 1].Y / screen2cm_y), (int)(target[i].X / screen2cm_x), (int)(target[i].Y / screen2cm_y), pen_target);
                    }
                }


            };

            btnDelete.Click += (sender, e) =>
            {
                myFlag.moving = false;
                target_total = 0;
                target_now = 1;
                btnDelete.Enabled = false;
                btnGo.Enabled = false;

                view.Invalidate();
            };
            
            btnGo.Click += (sender, e) =>
            {
                //Log.Debug("Brian", "myFlag.moving=" + myFlag.moving);
                if (!myFlag.moving)
                {
                    previous_step.X = myTag.X;
                    previous_step.Y = myTag.Y;

                    myFlag.moving = true;
                    wr.Write(DateTime.Now.ToString("HH:mm:ss ")+"New Route");
                    wr.Write("\r\n");

                    Thread dispatchloop = new Thread(new ThreadStart(Dispatch));
                    dispatchloop.IsBackground = true;
                    dispatchloop.Priority = System.Threading.ThreadPriority.BelowNormal;
                    dispatchloop.Start();
                    
                    //System.Threading.WaitCallback waitCallback = new WaitCallback(cal_move);
                    //ThreadPool.QueueUserWorkItem(waitCallback, "First route");
                }
            };
            
            view.OnTouching += (sender, e) =>
             {
                 if (e.Action == MotionEventActions.Down)
                 {
                     if (!myFlag.screen_ready) return; //Toby's patch

                     
                     short walkable;
                     Single diffX;
                     Single diffY;
                     //Single tmpSingle;
                     Single tmpSingle1, tmpSingle2, tmpSingle3;
                     
                     Point a = new Point();
                     Point b = new Point();
                     //List<Point> corner = new List<Point>();
                     
                     walkable = myMap.CheckWalk((int)(e.GetX() * screen2grid_x), (int)(e.GetY() * screen2grid_y));
                     
                     tmpSingle1 = target[target_total].X - e.GetX() *screen2cm_x;
                     tmpSingle2 = target[target_total].Y - e.GetY() *screen2cm_y;
                     tmpSingle3 = tmpSingle1 * tmpSingle1 + tmpSingle2 * tmpSingle2;
                     if (tmpSingle3 < 400) return;
                     
                     if (walkable != 0)
                     {
                         //pictureBoxWalkable.Image = Properties.Resources.vehicleoff;
                         //pictureBoxWalkable.Top = e.Y;
                         //pictureBoxWalkable.Left = e.X;
                         //pictureBoxWalkable.Refresh();
                         //pictureBoxWalkable.Visible = true;
                     }
                     else
                     {
                         //pictureBoxWalkable.Image = Properties.Resources.vehicleon;
                         //pictureBoxWalkable.Top = e.Y;
                         //pictureBoxWalkable.Left = e.X;
                         //pictureBoxWalkable.Refresh();
                         //pictureBoxWalkable.Visible = true;

                         target[0].X = myTag.Avg.X;
                         target[0].Y = myTag.Avg.Y;

                        if (!myFlag.moving && target_total < 100)
                        {
                            a.X = (int)(target[target_total].X / screen2cm_x * screen2grid_x);
                            a.Y = (int)(target[target_total].Y / screen2cm_y * screen2grid_y);
                            b.X = (int)(e.GetX() * screen2grid_x);
                            b.Y = (int)(e.GetY() * screen2grid_y);
                            myMap.initial_position(a, b);
                            if (myMap.Autoflag == true) myMap.action();
                            RunOnUiThread(() => textView.Append("Add " + myMap.path_Result.Count.ToString() + " targets\r\n"));
                            //textBox1.Text = "Add " + myMap.path_Result.Count.ToString()+ " targets\r\n";
                            foreach (Point p in myMap.path_Result)
                            {
                                target_total++;
                                target[target_total].X = p.X / screen2grid_x * screen2cm_x;
                                target[target_total].Y = p.Y / screen2grid_y * screen2cm_y;
                                diffX = target[target_total].X - target[target_total - 1].X;
                                diffY = target[target_total].Y - target[target_total - 1].Y;
                                target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f);
                                if (target[target_total].Theta < -180) target[target_total].Theta = target[target_total].Theta + 360;
                                else if (target[target_total].Theta > 180) target[target_total].Theta = target[target_total].Theta - 360;
                                RunOnUiThread(() => textView.Append(target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", " + target[target_total].Theta.ToString("f2") + "\r\n"));
                                
                                //textBox1.Text = textBox1.Text + target[target_total].X.ToString() + ", " + target[target_total].Y.ToString() + ", "+target[target_total].Theta.ToString("f2")+ "\r\n";
                             }

                    //target_total++;
                    //target[target_total].X = e.X / x_scale;
                    //target[target_total].Y = e.Y / y_scale;
                    //diffX = target[target_total].X - target[target_total - 1].X;
                    //diffY = target[target_total].Y - target[target_total - 1].Y;
                    //target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f) + cfg.MapEast;
                    //if (target[target_total].Theta < 0) target[target_total].Theta = target[target_total].Theta + 360;
                    //else if (target[target_total].Theta > 360) target[target_total].Theta = target[target_total].Theta - 360;
                    //buttonTest2.Text = target[target_total].Theta.ToString("f1");
                }

                         if (target_total >= 1)
                         {

                             btnDelete.Enabled = true;
                             btnGo.Enabled = true;
                         }

                     }
                     view.Invalidate();
                 }
             };


            btnConnect.Click+=(sender,e)=>
            {
                string Port1="";
                string Port2="";
                int Baund1=0;
                int Baund2=0;
                int count1 = 0;

                string fileName = path+ @"/default.set";
                if (File.Exists(fileName))
                {
                    textView.Append("Reading " + fileName + "\r\n");
                    StreamReader sr = new StreamReader(fileName);
                    string line;
                    string[] linesplit;
                    while (!sr.EndOfStream)
                    {
                        line = sr.ReadLine();
                        linesplit = line.Split('=');

                        if (linesplit[0] == "tagCOMName")
                        {
                            Port1 = linesplit[1];
                            ComPortDataReady++;
                        }
                        else if (linesplit[0] == "tagCOMRate")
                        {
                            Baund1 = Convert.ToInt32(linesplit[1]);
                            ComPortDataReady++;
                        }
                        else if (linesplit[0] == "drivingCOMName")
                        {
                            Port2 = linesplit[1];
                            ComPortDataReady++;
                        }
                        else if (linesplit[0] == "drivingCOMRate")
                        {
                            Baund2 = Convert.ToInt32(linesplit[1]);
                            ComPortDataReady++;
                        }
                        else if (linesplit[0] == "A1X") anchor1.X = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A1Y") anchor1.Y = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A1Z") anchor1.Z = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A2X") anchor2.X = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A2Y") anchor2.Y = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A2Z") anchor2.Z = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A3X") anchor3.X = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A3Y") anchor3.Y = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "A3Z") anchor3.Z = Convert.ToSingle(linesplit[1]);
                        else if (linesplit[0] == "mapwidth") mapsize.X = Convert.ToInt16(linesplit[1]);
                        else if (linesplit[0] == "mapheight") mapsize.Y = Convert.ToInt16(linesplit[1]);
                        else if (linesplit[0] == "range_ratio") range_ratio = Convert.ToInt16(linesplit[1]);
                        else if (linesplit[0] == "favorite")
                        {
                            favorite[count1].Note = linesplit[1];
                            favorite[count1].X = Convert.ToInt16(linesplit[2]);
                            favorite[count1].Y = Convert.ToInt16(linesplit[3]);
                            //comboTarget.Items.Add(favorite[count1].Note); //spinner
                            count1++;
                        }
                    }
                    sr.Close();
                }
                else 
                    textView.Append(fileName + " doesn't exist.\r\n");

                if (ComPortDataReady >= 4)
                {
                    //Brian+: Add UART control code here
                    pic32_open = Uart2C.OpenUart("ttymxc3");
                    if (pic32_open > 0)
                    {
                        Uart2C.SetUart(2); //0: B9600, 1:B115200, 2:B19200                    
                        textView.Append("Open ttymxc3 successfully, Baund Rate=19200, fd_num=" + pic32_open + "\r\n");
                      
                    } 
                    else
                    {
                        textView.Append("Open ttymx3 fail!!\r\n");
                    
                    }
                
                    tag_open = Uart2C.OpenUart("ttymxc2");
                    if (tag_open > 0)
                    {
                        Uart2C.SetUart(1); //0: B9600, 1:B115200, 2:B19200                    
                        textView.Append("Open ttymxc2 successfully, Baund Rate=115200, fd_num=" + tag_open +"\r\n");

                    } 
                    else
                    {
                        textView.Append("Open ttymx2 fail!!\r\n");

                    }

                    //myFlag.screen_ready = true;
                
                
                    //tagPort = new SerialPort(Port1);
                    //tagPort.BaudRate = Baund1;
                    //tagPort.Parity = Parity.None;
                    //tagPort.StopBits = StopBits.One;
                    //tagPort.DataBits = 8;
                    //tagPort.Handshake = Handshake.None;
                    //tagPort.DataReceived += new SerialDataReceivedEventHandler(tag_DataReceived);
                    //tagPort.Open();
                    //if (!tagPort.IsOpen) textBox1.AppendText("Tag connect fail");

                    //drivingPort = new SerialPort(Port2);
                    //drivingPort.BaudRate = Baund2;
                    //drivingPort.Parity = Parity.None;
                    //drivingPort.StopBits = StopBits.One;
                    //drivingPort.DataBits = 8;
                    //drivingPort.Handshake = Handshake.None;
                    //drivingPort.DataReceived += new SerialDataReceivedEventHandler(driving_DataReceived);
                    //drivingPort.Open();
                    //if (!drivingPort.IsOpen) textBox1.AppendText("Driving board connect fail");
                }
                //Brian+ for test: call SetAppearance() to set screen2map_x and screen2map_y 
                SetAppearance();
                
                //if (tagPort.IsOpen && drivingPort.IsOpen)
                if ((pic32_open > 0) && (tag_open > 0))    
                {
                    timer1 = new System.Timers.Timer();
                    timer1.Interval = 200;
                    timer1.Elapsed += new System.Timers.ElapsedEventHandler(timer1_Tick);
                    timer1.Start();
                    //timer1.Enabled = true;

                    //Brian+ Add timers to hook driving_DataReceived() and tag_DataReceived()
                    Pic32DataRecvTimer = new System.Timers.Timer();
                    Pic32DataRecvTimer.Interval = 200;
                    Pic32DataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(driving_DataReceived);
                    Pic32DataRecvTimer.Start();

                    TagDataRecvTimer = new System.Timers.Timer();
                    TagDataRecvTimer.Interval = 200;
                    TagDataRecvTimer.Elapsed += new System.Timers.ElapsedEventHandler(tag_DataReceived);
                    TagDataRecvTimer.Start();


                    hpcounter1.Start();
                    hpcounter2.Start();
                    hpcounter3.Start();
                    hpcounter4.Start();
                    btnConnect.Enabled = false;
                    myFlag.screen_ready = true;
                    buttonCalCompass.Enabled = true;

                    TowardOneTarget.Start();
                    TowardOneTarget.ControlEvent += ControlOut;
                }
            };

            buttonTest.Click += (sender, e) =>
            {
                int ii;
                //Log.Debug("Brian", "Press buttonTest event!!");
                myFlag.testing = false;
                ii = 0;
                while (true)
                {
                    if (ii > 10) break;

                    if (myFlag.testing)
                    {
                        myFlag.testing = false;
                        wr.Write(myTag.Avg.X.ToString("f1") + "," + myTag.Avg.Y.ToString("f1") + "\r\n");
                    }

                    if (!myFlag.moving)
                    {
                        ii++;
                        Thread.Sleep(500);
                        previous_step.X = myTag.X;
                        previous_step.Y = myTag.Y;
                        target[0].X = myTag.Avg.X;
                        target[0].Y = myTag.Avg.Y;
                        target[1].X = favorite[0].X;
                        target[1].Y = favorite[0].X;
                        target[2].X = favorite[1].X;
                        target[2].Y = favorite[1].X;
                        target_total = 2;

                        myFlag.moving = true;
                        System.Threading.WaitCallback waitCallback = new WaitCallback(cal_move);
                        ThreadPool.QueueUserWorkItem(waitCallback, "First route");
                    }
                    else Thread.Sleep(100);

                }
            
            };


            btnLoad.Click += (sender, e) =>
            {
                view.Visibility = ViewStates.Visible;
                bool success;
                string file1, file2;
                file1 = path + @"/default.set";
                file2 = path + @"/Raw";

                success = ReadConfig(file1);
                if (!success)
                {
                    
                    textView.Append(file1 + " doesn't exist.\r\n");
                }
                else
                {
                   textView.Append("Reading " + file1 + "\r\n");
                    myMap.Grid_W = cfg.GridWidth;
                    myMap.Grid_H = cfg.GridHeight;
                    myMap.East = cfg.MapEast;

                    textView.Append("Loading image file " + file2 + ".bmp \r\n");
                    success = myMap.LoadFile(file2);
                    if (!success)
                    {
                        textView.Append(file2 + " doesn't exist.\r\n");
                    }
                    else
                    {
                        textView.Append("Processing data.\r\n");
                        success = myMap.Preprocess();
                        //success = true;
                        if (!success)
                        {
                           textView.Append("Initial fail. \r\n");
                        }
                        else
                        {
                            Bitmap bitmap=BitmapFactory.DecodeFile(file2 + ".bmp");
                            Drawable drawable = new BitmapDrawable(bitmap);
                            linearContent.SetBackgroundDrawable(drawable);

                            
                            //groupBox2.Visible = true;
                            //buttonHide.Visible = true;
                            labelTable.Visibility = ViewStates.Visible;
                            //labelVehicle.Visibility = ViewStates.Gone;
                            btnLoad.Visibility = ViewStates.Gone;
                            btnConnect.Enabled = true;
                            //Brian+ for test: call SetAppearance() to set screen2map_x and screen2map_y 
                            //SetAppearance();
                        }

                    }

                }

            };

        }
        private void comboTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            Single diffX;
            Single diffY;

            target[0].X = myTag.Avg.X;
            target[0].Y = myTag.Avg.Y;

            if (!myFlag.moving && target_total < 100)
            {
                target_total++;
               // target[target_total].X = favorite[comboTarget.SelectedIndex].X;
               // target[target_total].Y = favorite[comboTarget.SelectedIndex].Y;
                diffX = target[target_total].X - target[target_total - 1].X;
                diffY = target[target_total].Y - target[target_total - 1].Y;
                target[target_total].Theta = (Single)(Math.Atan2(diffY, diffX) * 180f / 3.14f);
                if (target[target_total].Theta < 0) target[target_total].Theta = target[target_total].Theta + 360;
                else if (target[target_total].Theta > 360) target[target_total].Theta = target[target_total].Theta - 360;
               
                if (target_total >= 1)
                {
                    btnDelete.Enabled = true;
                    btnGo.Enabled = true;
                }
            }
            view.Invalidate();
        }

        private void tag_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int tmpInt1, tmpInt2;
            String TagRecvData = null;
            Single tmpSingle1;
            //int DataLen = 0;

            TagRecvData = Uart2C.ReceiveMsgUart(tag_open);

            //RunOnUiThread(() => textView.Append("TagRecvData =" + TagRecvData+ "\r\n"));
            //byte[] tmpBytes = new byte[3];

            //SerialPort p = (SerialPort)sender;
            //DataLen = TagRecvData.Length;
            //Log.Debug("Brian", "TagRecvData(Original)=" + TagRecvData);
            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(TagRecvData);
            //Log.Debug("Brian", "TagRecvData(Convert)=" + ToHexString(byteArray));
            //Log.Debug("Brian", "TagRecvData(Length)=" + byteArray.Length);
            //while (TagRecvData != null)
            for (int i = 0; i < byteArray.Length; i++)
            {
                //readbuff = p.ReadByte();
                //byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(TagRecvData);
                readbuff = byteArray[i];
                //RunOnUiThread(() => textView.Append("readbuff =" + readbuff + "\r\n"));

                if (readbuff == 0x23)
                {
                    Array.Clear(tagbuff, 0, 30);
                    tagbufflen = 0;
                }
                else if (tagbufflen >= 17)
                {
                    tagbufflen = 0;
                    myTag.Rate = myTag.Rate + 1;

                    tmpInt1 = (tagbuff[1] - 48) * 100 + (tagbuff[2] - 48) * 10 + (tagbuff[3] - 48);
                    tmpInt2 = (tagbuff[5] - 48) * 10 + (tagbuff[6] - 48);
                    //Log.Debug("Brian", "tagbuff=" + ToHexString(tagbuff));
                    if (tagbuff[14] == 0x31)    // anchor #1
                    {
                        anchor1.Updated = true;
                        anchor1.RangeOld = anchor1.Range;
                        anchor1.Range = (short)(range_ratio / 100 * (tmpInt1 * 100 + tmpInt2 + anchor1.Range) / 2);
                        //Log.Debug("Brian", "anchor1.Range=" + anchor1.Range);
                        hpcounter1.Stop();
                        anchor1.SampleTime = hpcounter1.Duration;
                        hpcounter1.Start();

                        tmpSingle1 = (Single)(anchor1.SampleTime * cfg.MaxSpeed);
                        if (Math.Abs(anchor1.Range - anchor1.RangeOld) > tmpSingle1)
                        {
                            if (anchor1.Range > anchor1.RangeOld) anchor1.Range = anchor1.RangeOld + tmpSingle1;
                            else anchor1.Range = anchor1.RangeOld - tmpSingle1;
                        }
                    }
                    else if (tagbuff[14] == 0x32)   // anchor #2
                    {
                        anchor2.Updated = true;
                        anchor2.RangeOld = anchor2.Range;
                        anchor2.Range = (short)(range_ratio / 100 * (tmpInt1 * 100 + tmpInt2 + anchor2.Range) / 2);
                        //Log.Debug("Brian", "anchor2.Range=" + anchor2.Range);
                        hpcounter2.Stop();
                        anchor2.SampleTime = hpcounter2.Duration;
                        hpcounter2.Start();

                        tmpSingle1 = (Single)(anchor2.SampleTime * cfg.MaxSpeed);
                        if (Math.Abs(anchor2.Range - anchor2.RangeOld) > tmpSingle1)
                        {
                            if (anchor2.Range > anchor2.RangeOld) anchor2.Range = anchor2.RangeOld + tmpSingle1;
                            else anchor2.Range = anchor2.RangeOld - tmpSingle1;
                        }
                    }
                    else if (tagbuff[14] == 0x33)   // anchor #3
                    {
                        anchor3.Updated = true;
                        anchor3.RangeOld = anchor3.Range;
                        anchor3.Range = (short)(range_ratio / 100 * (tmpInt1 * 100 + tmpInt2 + anchor3.Range) / 2);
                        //Log.Debug("Brian", "anchor3.Range=" + anchor3.Range);
                        hpcounter3.Stop();
                        anchor3.SampleTime = hpcounter3.Duration;
                        hpcounter3.Start();

                        tmpSingle1 = (Single)(anchor3.SampleTime * cfg.MaxSpeed);
                        if (Math.Abs(anchor3.Range - anchor3.RangeOld) > tmpSingle1)
                        {
                            if (anchor3.Range > anchor3.RangeOld) anchor3.Range = anchor3.RangeOld + tmpSingle1;
                            else anchor3.Range = anchor3.RangeOld - tmpSingle1;
                        }
                    }
                }
                else
                {
                    tagbuff[tagbufflen] = (byte)readbuff;
                    tagbufflen++;
                }
            }

        }

        //Brian+ for test:
        public static string ToHexString(byte[] bytes) // 0xae00cf => "AE00CF "
        {
            string hexString = string.Empty;
            if (bytes != null)
            {
                System.Text.StringBuilder strB = new System.Text.StringBuilder();

                for (int i = 0; i < bytes.Length; i++)
                {
                    strB.Append(bytes[i].ToString("X2"));
                }
                hexString = strB.ToString();
            }
            return hexString;
        }
        private void driving_DataReceived(object sender, EventArgs e)
        {
            int readbuff;
            int tmpInt;
            short tmpShort;
            byte[] Pic32RecvData = new byte[255];
            //String Pic32RecvData = null;

            //Pic32RecvData = Uart2C.ReceiveMsgUart(pic32_open);
            Pic32RecvData = Uart2C.ReceiveMsgUartByte(pic32_open);
            //Pic32RecvData = Uart2C.ReceiveMsgUart(pic32_open);
            //RunOnUiThread(() => textView.Append("Pic32RecvData =" + Pic32RecvData + "\r\n"));
            //Log.Debug("Brian", "Pic32RecvData(Original)=" + Pic32RecvData);
            
            //byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(Pic32RecvData);
            //Log.Debug("Brian", "[driving_DataReceived]Pic32RecvData(Raw)=" + Pic32RecvData);
            Log.Debug("Brian", "[driving_DataReceived]Pic32RecvData(Convert)=" + ToHexString(Pic32RecvData));
            //Log.Debug("Brian", "Pic32RecvData(Length)=" + byteArray.Length);
            //SerialPort p = (SerialPort)sender;
            //while (p.BytesToRead > 0)
            //while (Pic32RecvData != null)
            //for (int j = 0; j < byteArray.Length; j++)
            for (int j = 0; j < Pic32RecvData.Length; j++)
            {
                
                //byte[] byteArray = System.Text.Encoding.ASCII.GetBytes(Pic32RecvData);
                readbuff = Pic32RecvData[j];

                if (readbuff == 0x53)
                {
                    Array.Clear(drivingbuff, 0, 30);
                    drivingbufflen = 0;
                }
                else if (drivingbufflen >= 12 && drivingbuff[0] == 0x20)
                {
                    drivingbufflen = 0;
                    //Toby's patch
                    tmpShort = (short)((drivingbuff[1] << 8) + drivingbuff[2]);
                    tmpShort = (short)(tmpShort - cfg.MapEast);
                    if (tmpShort < -360) tmpShort = (short)(tmpShort + 360);
                    else if (tmpShort > 360) tmpShort = (short)(tmpShort - 360);
                    if (tmpShort < 360 && tmpShort > -360)
                    {
                        myVehicle.compass = tmpShort;
                    }
                    tmpShort = (short)((drivingbuff[3] << 8) + drivingbuff[4]);
                    if (tmpShort > 100) tmpShort = 100;
                    else if (tmpShort < -100) tmpShort = -100;
                    myVehicle.encoderL = (short)(myVehicle.encoderL + tmpShort);
                    tmpShort = (short)((drivingbuff[5] << 8) + drivingbuff[6]);
                    if (tmpShort > 100) tmpShort = 100;
                    else if (tmpShort < -100) tmpShort = -100;
                    myVehicle.encoderR = (short)(myVehicle.encoderR + tmpShort);
                    //Toby's end
                    Log.Debug("Brian", "myVehicle.compass=" + myVehicle.compass);
                                        
                    for (int i = 0; i <= 4; i++)
                    {
                        myVehicle.sonic[i] = drivingbuff[7 + i];
                    }
                }
                else if (drivingbufflen >= 3 && drivingbuff[0] == 0x12)
                {
                    drivingbufflen = 0;
                    if (drivingbuff[1] == 0x01) myCommand.vehicle_done = true;
                }
                else if (drivingbufflen >= 17 && drivingbuff[0] == 0x21)
                {
                    int[] data = new int[8];

                    drivingbufflen = 0;
                    wr.Write("{0:mm:ss} ", DateTime.Now);
                    for (i = 0; i < 8; i++)
                    {
                        data[i] = ((drivingbuff[1 + i * 2] << 8) + drivingbuff[2 + i * 2]);
                        if (data[i] > 32768) data[i] = data[i] - 65535;
                        wr.Write("," + data[i].ToString());
                    }
                    wr.Write(anchor1.Range.ToString("f1") + ",");
                    wr.Write(anchor2.Range.ToString("f1") + ",");
                    wr.Write(anchor3.Range.ToString("f1") + ",");
                    wr.Write("\r\n");
                }
                //Toby's patch
                else if (drivingbufflen >= 12 && drivingbuff[0] == 0x22)
                {
                    drivingbufflen = 0;
                    mOffset[0] = ((drivingbuff[1] << 8) + drivingbuff[2]);
                    mOffset[1] = ((drivingbuff[3] << 8) + drivingbuff[4]);
                    for (i = 0; i < 4; i++)
                    {
                        mMin[i] = ((drivingbuff[5 + i * 2] << 8) + drivingbuff[6 + i * 2]);
                        wr.Write("max_y," + mMin[i].ToString() + "\r\n");
                    }
                    wr.Write("Offset," + mOffset[0].ToString() + "," + mOffset[1].ToString() + "\r\n");

                }
                //Toby's End
                else
                {
                    if (drivingbufflen >= 30) drivingbufflen = 0;
                    drivingbuff[drivingbufflen] = (byte)readbuff;
                    drivingbufflen++;
                }
            }
        }

        private void SetAppearance()
        {
            MainWidth = this.linearContent.Width;
            //MainHeight = this.linearContent.Height - 10;
            MainHeight = this.linearContent.Height;

            screen2cm_x = (Single)cfg.MapWidth / (Single)MainWidth;
            screen2cm_y = (Single)cfg.MapHeight / (Single)MainHeight;
            screen2grid_x = (Single)cfg.GridWidth / (Single)MainWidth;
            screen2grid_y = (Single)cfg.GridHeight / (Single)MainHeight;

            //groupBox2.Left = MainWidth - groupBox2.Width - offset;
            //textBox1.Left = MainWidth - textBox1.Width - offset;
            //buttonHide.Left = MainWidth - buttonHide.Width - offset;
            //labelTable.Left = offset;
            //labelTable.Top = offset;

            labelVehicle.SetX(offset);
            //labelVehicle.SetY(MainHeight - labelVehicle.Height - offset);
            labelVehicle.SetY(MainHeight -  40);

            labelA.SetX((int)(offset + anchor1.X / screen2cm_x));
            labelA.SetY((int)(offset + anchor1.Y / screen2cm_y));
            labelB.SetX((int)(offset + anchor2.X / screen2cm_x));
            labelB.SetY((int)(offset + anchor2.Y / screen2cm_y));
            labelC.SetX((int)(offset + anchor3.X / screen2cm_x));
            labelC.SetY((int)(offset + anchor3.Y / screen2cm_y));
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string tmpString;
            Single std_x, std_y;

            RunOnUiThread(() => buttonTest2.Text = test_str);
            myTag.Rate--;

            // labelTable
            //labelTable.Text = "    Range,   Rate\r\nA " + anchor1.Message + "\r\nB " + anchor2.Message + "\r\nC " + anchor3.Message;
            RunOnUiThread(() => labelTable.Text = "    Range,   Rate\r\nA " + anchor1.Message + "\r\nB " + anchor2.Message + "\r\nC " + anchor3.Message);
            RunOnUiThread(() => labelTableC.Text = "    Range,   Rate\r\nC " + anchor3.Message);
                        
            //Log.Debug("Brian", "anchor3.Message=" + anchor3.Message);
            // labelVehicle
            //tmpString = " D=" + myVehicle.compass.ToString() + ", A=" + myCommand.var1.ToString() + ", S=" + myCommand.var2.ToString();
            //tmpString = tmpString + ", X=" + myTag.X.ToString("f1") + ", Y=" + myTag.Y.ToString("f1");
            
            //Toby's patch
            tmpString = " D=" + myVehicle.compass.ToString() + ", X=" + myTag.X.ToString("f1") + ", Y=" + myTag.Y.ToString("f1");
            //tmpString = tmpString + "\r\nCursor : " + mouse_position.X.ToString("f0") + ", " + mouse_position.Y.ToString("f0");
            //Toby's end

            //labelVehicle.Text = tmpString;
            RunOnUiThread(() => labelVehicle.Text = tmpString);
            
            if (myFlag.loc_init_step == 0)
            {
                myFlag.loc_init_done = false;
                //textView.Append("Searching for beacon...\r\n");
                RunOnUiThread(() => textView.Append("Searching for beacon...\r\n"));
                Log.Debug("Brian", "Searching for beacon...");
                if (loc_initial())
                {
                    myFlag.loc_init_step = 1;
                    //myFlag.loc_init_done = true;
                    Thread local1 = new Thread(new ParameterizedThreadStart(loc_preliminary));
                    local1.IsBackground = true;
                    local1.Start(20);
                    //textView.Append("Beacon found.\r\n");
                    RunOnUiThread(() => textView.Append("Beacon found.\r\n"));
                    Log.Debug("Brian", "Beacon found!!");
                
                }
                else
                {
                    myFlag.loc_init_done = false;
                    //textView.Append("Beacon doesn't exist.\r\n");
                    RunOnUiThread(() => textView.Append("Beacon doesn't exist.\r\n"));
                    Log.Debug("Brian", "Beacon doesn't exist!!");
                }
            }
            if (myFlag.loc_init_step == 2)
            {
                Single[] tmpSingleA = new Single[3];
                myEKF = new class_EKFL5(myTag.X, myTag.Y, 0f);
                tmpSingleA[0] = anchor1.X;
                tmpSingleA[1] = anchor2.X;
                tmpSingleA[2] = anchor3.X;
                myEKF.AX = tmpSingleA;
                tmpSingleA[0] = anchor1.Y;
                tmpSingleA[1] = anchor2.Y;
                tmpSingleA[2] = anchor3.Y;
                myEKF.AY = tmpSingleA;
                tmpSingleA[0] = anchor1.Z;
                tmpSingleA[1] = anchor2.Z;
                tmpSingleA[2] = anchor3.Z;
                myEKF.AZ = tmpSingleA;
                myFlag.loc_init_step = 3;
                myFlag.loc_init_done = true;
            }

            if (myFlag.loc_init_done)
            {
                Single[] tmpSingleA = new Single[3];
                
                //Toby's patch
                hpcounter4.Stop();
                myEKF.dT = (Single)hpcounter4.Duration * 1000f;
                hpcounter4.Start();
                myEKF.EncoderL = myVehicle.encoderL;
                myEKF.EncoderR = myVehicle.encoderR;
                myVehicle.encoderL = 0;
                myVehicle.encoderR = 0;
                //Toby's done
                
                tmpSingleA[0] = anchor1.Range;
                tmpSingleA[1] = anchor2.Range;
                tmpSingleA[2] = anchor3.Range;
                myEKF.ARange = tmpSingleA;
                myEKF.Calculation();
                myTag.X = myEKF.tagX;
                myTag.Y = myEKF.tagY;
                Log.Debug("Brian", "[After_EKF]myTag.X=" + myTag.X + "," + "mytag.Y=" + myTag.Y);
                renew_anchor_disp();

                for (int j = myTag_Old_X.Length - 1; j >= 1; j--)
                {
                    myTag_Old_X[j] = myTag_Old_X[j - 1];
                    myTag_Old_Y[j] = myTag_Old_Y[j - 1];
                }
                myTag_Old_X[0] = myTag.X;
                myTag_Old_Y[0] = myTag.Y;
                avg_stdev(out myTag.Avg.X, out std_x, myTag_Old_X);
                avg_stdev(out myTag.Avg.Y, out std_y, myTag_Old_Y);
                TowardOneTarget.Pose.X = myTag.Avg.X;
                TowardOneTarget.Pose.Y = myTag.Avg.Y;
                TowardOneTarget.Pose.Theta = myVehicle.compass;
                for (i = 0; i < 5; i++) TowardOneTarget.Vehicle.sonic[i] = myVehicle.sonic[i];
                myFlag.sampling = true; //Toby's patch
            }

            if (myFlag.moving)
            {
                wr.Write("{0:mm:ss} ", DateTime.Now);
                wr.Write(myCommand.turn.ToString());
                wr.Write("\r\n");

                //Brian+ 2015/03/31: Log some data to log file
                if (true)
                {
                    //fwr.WriteLine("----------------------------------------------");
                    //fwr.WriteLine("anchor1.Range=" + anchor1.Range.ToString("f1"));
                    //fwr.WriteLine("anchor2.Range=" + anchor2.Range.ToString("f1"));
                    //fwr.WriteLine("anchor3.Range=" + anchor3.Range.ToString("f1"));
                    //fwr.WriteLine("myTag.X=" + myTag.X.ToString("f1") + " ," + "myTag.Y=" + myTag.Y.ToString("f1"));
                    //fwr.WriteLine("----------------------------------------------");
                }
                
                //Toby's patch
                /* 
                if (checkBoxRecord.Checked)
                {
                    wr.Write(myEKF.dT.ToString("f1") + ",");
                    wr.Write(TowardOneTarget.OutSpeed.ToString() + ",");
                    wr.Write(TowardOneTarget.OutTurn.ToString() + ",");
                    wr.Write(anchor1.Range.ToString("f1") + ",");
                    wr.Write(anchor2.Range.ToString("f1") + ",");
                    wr.Write(anchor3.Range.ToString("f1") + ",");
                    wr.Write(myEKF.EncoderL.ToString() + ",");
                    wr.Write(myEKF.EncoderR.ToString() + ",");
                    wr.Write(myVehicle.compass.ToString("f1") + ",");
                    wr.Write(myTag.X.ToString("f1") + "," + myTag.Y.ToString("f1"));
                    wr.Write("\r\n");
                    //wr.Flush();
                }
                */
                //Toby's end
            }
            //view.Invalidate();
            RunOnUiThread(() => view.Invalidate());
        }

        private void OutCommand(move_command command)
        {
            byte[] outbyte = new byte[10];
            //int outbytelen = 0;

            if (command.speed == 0 && command.turn != 0)
            {
                outbyte[0] = 0x53;
                outbyte[1] = 0x12;
                outbyte[2] = (byte)((command.turn >> 8) & 0xFF);
                outbyte[3] = (byte)(command.turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                //outbytelen = 6;
                //drivingPort.Write(outbyte, 0, outbytelen);
                Log.Debug("Brian", "[OutCommnad]event1 trigger!!");

                Uart2C.SendMsgUart(pic32_open, outbyte);
            }
            else
            {
                if (command.speed > 100) command.speed = 100;
                else if (command.speed < -100) command.speed = -100;
                if (command.turn > 100) command.turn = 100;
                else if (command.turn < -100) command.turn = -100;

                outbyte[0] = 0x53;
                outbyte[1] = 0x13;
                outbyte[2] = (byte)(command.speed & 0xFF);
                outbyte[3] = (byte)(command.turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                //outbytelen = 6;
                //drivingPort.Write(outbyte, 0, outbytelen);
                Log.Debug("Brian", "[OutCommnad]event2 trigger!!");
                Uart2C.SendMsgUart(pic32_open, outbyte);
            }

        }

        private void renew_anchor_disp()
        {
            Single tmpSingle1;
            int Rate_total;

            if (anchor1.Updated || anchor2.Updated || anchor3.Updated)
            {
                // reset receiving rate
                if (anchor1.Rate >= 100 || anchor2.Rate >= 100 || anchor3.Rate >= 100)
                {
                    tmpSingle1 = anchor1.Rate * 0.1f;
                    anchor1.Rate = (int)tmpSingle1;
                    tmpSingle1 = anchor2.Rate * 0.1f;
                    anchor2.Rate = (int)tmpSingle1;
                    tmpSingle1 = anchor3.Rate * 0.1f;
                    anchor3.Rate = (int)tmpSingle1;
                }
                Rate_total = anchor1.Rate + anchor2.Rate + anchor3.Rate + 1;

                if (anchor1.Updated)
                {
                    anchor1.Updated = false;
                    anchor1.Rate = anchor1.Rate + 1;
                }
                if (anchor2.Updated)
                {
                    anchor2.Updated = false;
                    anchor2.Rate = anchor2.Rate + 1;
                }
                if (anchor3.Updated)
                {
                    anchor3.Updated = false;
                    anchor3.Rate = anchor3.Rate + 1;
                }
                
                tmpSingle1 = (Single)(anchor1.Rate * myTag.Rate / Rate_total);
                anchor1.Message = anchor1.Range.ToString("f1") + " cm  " + tmpSingle1.ToString("f1") + " %";
                tmpSingle1 = (Single)(anchor2.Rate * myTag.Rate / Rate_total);
                anchor2.Message = anchor2.Range.ToString("f1") + " cm  " + tmpSingle1.ToString("f1") + " %";
                tmpSingle1 = (Single)(anchor3.Rate * myTag.Rate / Rate_total);
                anchor3.Message = anchor3.Range.ToString("f1") + " cm  " + tmpSingle1.ToString("f1") + " %";
            }
        }
     



        protected override void OnDestroy()
        {
            myFlag.moving = false;

            wr.Write("{0:mm:ss} ", DateTime.Now);
            wr.Write("Program End\r\n");
            wr.Close();
            //Brian+ for log go file
            //fwr.Write("{0:mm:ss} ", DateTime.Now);
            //fwr.Write("Program End\r\n");
            //fwr.Close();
                        
            base.OnDestroy();
             

        }


        private bool ReadConfig(string ff)
        {
            if (File.Exists(ff))
            {
                StreamReader sr = new StreamReader(ff);
                int tmpInt;
                string line;
                string[] linesplit;
                while (!sr.EndOfStream)
                {
                    line = sr.ReadLine();
                    linesplit = line.Split('=');

                    if (linesplit[0] == "mapwidth") cfg.MapWidth = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "mapheight") cfg.MapHeight = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "mapeast") cfg.MapEast = Convert.ToInt16(linesplit[1]);
                    else if (linesplit[0] == "gridwidth") cfg.GridWidth = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "gridheight") cfg.GridHeight = Convert.ToUInt16(linesplit[1]);
                    //else if (linesplit[0] == "control_inv") cfg.ControlInv = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "agent_max_speed") cfg.MaxSpeed = Convert.ToUInt16(linesplit[1]);
                    else if (linesplit[0] == "iteration")
                    {
                        tmpInt = Convert.ToInt16(linesplit[1]);
                        Iteration.Node_num = tmpInt;
                        Iteration.Repeat_num = 10;
                        Iteration.Busy = false;
                        for (i = 0; i < tmpInt; i++)
                        {
                            Iteration.Location[i].X = Convert.ToInt16(linesplit[2 + i * 2]);
                            Iteration.Location[i].Y = Convert.ToInt16(linesplit[3 + i * 2]);
                        }
                    }
                }
                sr.Close();
                return true;
            }
            else return false;
        }

        private bool loc_initial()
        {
            int anchor_count = 0;
            ushort tmpShort;

            anchor1.Rate = 0;
            anchor2.Rate = 0;
            anchor3.Rate = 0;
            myTag.Rate = 100;
            anchor1.Range = 0;
            anchor2.Range = 0;
            anchor3.Range = 0;
            myTag.Range = 0;
            anchor1.Message = "";
            anchor2.Message = "";
            anchor3.Message = "";
            myTag.Message = "";
            tmpShort = cfg.MaxSpeed;
            cfg.MaxSpeed = 1000;

            Thread.Sleep(500);
            if (anchor1.Range != 0) anchor_count++;
            if (anchor2.Range != 0) anchor_count++;
            if (anchor3.Range != 0) anchor_count++;
            cfg.MaxSpeed = tmpShort;
            //Log.Debug("Brian", "anchor1.Range=" + anchor1.Range);
            //Log.Debug("Brian", "anchor2.Range=" + anchor2.Range);
            //Log.Debug("Brian", "anchor3.Range=" + anchor3.Range);
            if (anchor_count < 2)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private void loc_preliminary(object arg)
        {
            int i, cycles;
            Single x, y, x2, y2, x3, y3;
            Single ca, cb, cc, cd, ce, cf, cg;
            Single std_x, std_y;

            cycles = Convert.ToInt32(arg);
            for (i = 0; i < cycles; i++)
            {
                // calculate avg & stdev
                avg_stdev(out myTag.Avg.X, out std_x, myTag_Old_X);
                avg_stdev(out myTag.Avg.Y, out std_y, myTag_Old_Y);

                // calculate trilateration
                ca = anchor1.X - anchor2.X;
                cb = anchor1.Y - anchor2.Y;
                cc = 0.5f * ((anchor2.Range * anchor2.Range - anchor3.Range * anchor3.Range) - (anchor2.X * anchor2.X - anchor3.X * anchor3.X) - (anchor2.Y * anchor2.Y - anchor3.Y * anchor3.Y));
                cd = anchor3.X - anchor2.X;
                ce = anchor3.Y - anchor2.Y;
                cf = 0.5f * ((anchor2.Range * anchor2.Range - anchor1.Range * anchor1.Range) - (anchor2.X * anchor2.X - anchor1.X * anchor1.X) - (anchor2.Y * anchor2.Y - anchor1.Y * anchor1.Y));
                cg = cb * cd - ce * ca;
                if (cg == 0) y = 0;
                else y = (cf * cd - cc * ca) / cg;
                if (cd == 0) x = 0;
                else x = (cc - y * ce) / cd;
                ca = anchor2.X - anchor1.X;
                cb = anchor2.Y - anchor1.Y;
                cc = 0.5f * ((anchor1.Range * anchor1.Range - anchor3.Range * anchor3.Range) - (anchor1.X * anchor1.X - anchor3.X * anchor3.X) - (anchor1.Y * anchor1.Y - anchor3.Y * anchor3.Y));
                cd = anchor3.X - anchor1.X;
                ce = anchor3.Y - anchor1.Y;
                cf = 0.5f * ((anchor1.Range * anchor1.Range - anchor2.Range * anchor2.Range) - (anchor1.X * anchor1.X - anchor2.X * anchor2.X) - (anchor1.Y * anchor1.Y - anchor2.Y * anchor2.Y));
                cg = cb * cd - ce * ca;
                if (cg == 0) y2 = 0;
                else y2 = (cf * cd - cc * ca) / cg;
                if (cd == 0) x2 = 0;
                else x2 = (cc - y2 * ce) / cd;
                ca = anchor3.X - anchor1.X;
                cb = anchor3.Y - anchor1.Y;
                cc = 0.5f * ((anchor1.Range * anchor1.Range - anchor2.Range * anchor2.Range) - (anchor1.X * anchor1.X - anchor2.X * anchor2.X) - (anchor1.Y * anchor1.Y - anchor2.Y * anchor2.Y));
                cd = anchor2.X - anchor1.X;
                ce = anchor2.Y - anchor1.Y;
                cf = 0.5f * ((anchor1.Range * anchor1.Range - anchor3.Range * anchor3.Range) - (anchor1.X * anchor1.X - anchor3.X * anchor3.X) - (anchor1.Y * anchor1.Y - anchor3.Y * anchor3.Y));
                cg = cb * cd - ce * ca;
                if (cg == 0) y3 = 0;
                else y3 = (cf * cd - cc * ca) / cg;
                if (cd == 0) x3 = 0;
                else x3 = (cc - y3 * ce) / cd;

                myTag.X = (myTag.Avg.X * 3 + x + x3) / 5;
                myTag.Y = (myTag.Avg.Y * 3 + y + y3) / 5;

                // update old positions no matter the new range received or not 
                for (int j = myTag_Old_X.Length - 1; j >= 1; j--)
                {
                    myTag_Old_X[j] = myTag_Old_X[j - 1];
                    myTag_Old_Y[j] = myTag_Old_Y[j - 1];
                }
                myTag_Old_X[0] = myTag.X;
                myTag_Old_Y[0] = myTag.Y;

                Thread.Sleep(200);
            }

            myFlag.loc_init_step = 2;
        }

        private void loc_calculation()
        {
            Single x, y, x2, y2, x3, y3;
            Single ca, cb, cc, cd, ce, cf, cg;
            Single std_x, std_y;
            Single tmpSingle1;
            int Rate_total;

            while (true)
            {
                if (anchor1.Updated || anchor2.Updated || anchor3.Updated)
                {
                    // reset receiving rate
                    if (anchor1.Rate >= 100 || anchor2.Rate >= 100 || anchor3.Rate >= 100)
                    {
                        tmpSingle1 = anchor1.Rate * 0.1f;
                        anchor1.Rate = (int)tmpSingle1;
                        tmpSingle1 = anchor2.Rate * 0.1f;
                        anchor2.Rate = (int)tmpSingle1;
                        tmpSingle1 = anchor3.Rate * 0.1f;
                        anchor3.Rate = (int)tmpSingle1;
                    }
                    Rate_total = anchor1.Rate + anchor2.Rate + anchor3.Rate + 1;

                    // calculate avg & stdev
                    avg_stdev(out myTag.Avg.X, out std_x, myTag_Old_X);
                    avg_stdev(out myTag.Avg.Y, out std_y, myTag_Old_Y);

                    if (anchor1.Updated)
                    {
                        anchor1.Updated = false;
                        anchor1.Rate = anchor1.Rate + 1;
                        tmpSingle1 = Math.Abs(anchor1.Range - anchor1.RangeOld);
                        if (tmpSingle1 > anchor1.SampleTime * cfg.MaxSpeed)
                        {
                            if (anchor1.Range > anchor1.RangeOld) anchor1.Range = anchor1.RangeOld + (Single)(anchor1.SampleTime * cfg.MaxSpeed);
                            else anchor1.Range = anchor1.RangeOld - (Single)(anchor1.SampleTime * cfg.MaxSpeed);
                        }
                        tmpSingle1 = (Single)(anchor1.Rate * myTag.Rate / Rate_total);
                        anchor1.Message = anchor1.Range.ToString("f1") + " cm\r\n" + tmpSingle1.ToString("f1") + "%\r\n";
                    }
                    if (anchor2.Updated)
                    {
                        anchor2.Updated = false;
                        anchor2.Rate = anchor2.Rate + 1;
                        tmpSingle1 = Math.Abs(anchor2.Range - anchor2.RangeOld);
                        if (tmpSingle1 > anchor2.SampleTime * cfg.MaxSpeed)
                        {
                            if (anchor2.Range > anchor2.RangeOld) anchor2.Range = anchor2.RangeOld + (Single)(anchor2.SampleTime * cfg.MaxSpeed);
                            else anchor2.Range = anchor2.RangeOld - (Single)(anchor2.SampleTime * cfg.MaxSpeed);
                        }
                        tmpSingle1 = (Single)(anchor2.Rate * myTag.Rate / Rate_total);
                        anchor2.Message = anchor2.Range.ToString("f1") + " cm\r\n" + tmpSingle1.ToString("f1") + "%";
                    }
                    if (anchor3.Updated)
                    {
                        anchor3.Updated = false;
                        anchor3.Rate = anchor3.Rate + 1;
                        tmpSingle1 = Math.Abs(anchor3.Range - anchor3.RangeOld);
                        if (tmpSingle1 > anchor3.SampleTime * cfg.MaxSpeed)
                        {
                            if (anchor3.Range > anchor3.RangeOld) anchor3.Range = anchor3.RangeOld + (Single)(anchor3.SampleTime * cfg.MaxSpeed);
                            else anchor3.Range = anchor3.RangeOld - (Single)(anchor3.SampleTime * cfg.MaxSpeed);
                        }
                        tmpSingle1 = (Single)(anchor3.Rate * myTag.Rate / Rate_total);
                        anchor3.Message = anchor3.Range.ToString("f1") + " cm\r\n" + tmpSingle1.ToString("f1") + "%";
                    }

                    // calculate trilateration
                    ca = anchor1.X - anchor2.X;
                    cb = anchor1.Y - anchor2.Y;
                    cc = 0.5f * ((anchor2.Range * anchor2.Range - anchor3.Range * anchor3.Range) - (anchor2.X * anchor2.X - anchor3.X * anchor3.X) - (anchor2.Y * anchor2.Y - anchor3.Y * anchor3.Y));
                    cd = anchor3.X - anchor2.X;
                    ce = anchor3.Y - anchor2.Y;
                    cf = 0.5f * ((anchor2.Range * anchor2.Range - anchor1.Range * anchor1.Range) - (anchor2.X * anchor2.X - anchor1.X * anchor1.X) - (anchor2.Y * anchor2.Y - anchor1.Y * anchor1.Y));
                    cg = cb * cd - ce * ca;
                    if (cg == 0) y = 0;
                    else y = (cf * cd - cc * ca) / cg;
                    if (cd == 0) x = 0;
                    else x = (cc - y * ce) / cd;
                    ca = anchor2.X - anchor1.X;
                    cb = anchor2.Y - anchor1.Y;
                    cc = 0.5f * ((anchor1.Range * anchor1.Range - anchor3.Range * anchor3.Range) - (anchor1.X * anchor1.X - anchor3.X * anchor3.X) - (anchor1.Y * anchor1.Y - anchor3.Y * anchor3.Y));
                    cd = anchor3.X - anchor1.X;
                    ce = anchor3.Y - anchor1.Y;
                    cf = 0.5f * ((anchor1.Range * anchor1.Range - anchor2.Range * anchor2.Range) - (anchor1.X * anchor1.X - anchor2.X * anchor2.X) - (anchor1.Y * anchor1.Y - anchor2.Y * anchor2.Y));
                    cg = cb * cd - ce * ca;
                    if (cg == 0) y2 = 0;
                    else y2 = (cf * cd - cc * ca) / cg;
                    if (cd == 0) x2 = 0;
                    else x2 = (cc - y2 * ce) / cd;
                    ca = anchor3.X - anchor1.X;
                    cb = anchor3.Y - anchor1.Y;
                    cc = 0.5f * ((anchor1.Range * anchor1.Range - anchor2.Range * anchor2.Range) - (anchor1.X * anchor1.X - anchor2.X * anchor2.X) - (anchor1.Y * anchor1.Y - anchor2.Y * anchor2.Y));
                    cd = anchor2.X - anchor1.X;
                    ce = anchor2.Y - anchor1.Y;
                    cf = 0.5f * ((anchor1.Range * anchor1.Range - anchor3.Range * anchor3.Range) - (anchor1.X * anchor1.X - anchor3.X * anchor3.X) - (anchor1.Y * anchor1.Y - anchor3.Y * anchor3.Y));
                    cg = cb * cd - ce * ca;
                    if (cg == 0) y3 = 0;
                    else y3 = (cf * cd - cc * ca) / cg;
                    if (cd == 0) x3 = 0;
                    else x3 = (cc - y3 * ce) / cd;

                    myTag.X = (myTag.Avg.X * 3 + x + x3) / 5;
                    myTag.Y = (myTag.Avg.Y * 3 + y + y3) / 5;
                    Thread.Sleep(50);
                }
                else Thread.Sleep(100);

                // update old positions no matter the new range received or not 
                for (int j = myTag_Old_X.Length - 1; j >= 1; j--)
                {
                    myTag_Old_X[i] = myTag_Old_X[i - 1];
                    myTag_Old_Y[i] = myTag_Old_Y[i - 1];
                }
                myTag_Old_X[0] = myTag.X;
                myTag_Old_Y[0] = myTag.Y;
            }
        }

        private void cal_move(object state)
        {
            // coefficient
            Single[] k = new Single[5] { 20f, 5f, 5f, 0f, 0f };
            Single max_turn = 70;

            int max_speed = 95;
            int[] ref_range = new int[2] { 20, 20 };
            bool turn_1m, turn_2m;
            int target_range;
            int target_now;
            Single diff_dist;
            Single diff_angle;
            Single vector_cross;
            Single unit_cross;
            Single unit_dot;
            Single scalar_robot;
            Single scalar_target;
            Single tmpSingle1, tmpSingle2;
            struct_PointF vector_old;
            struct_PointF vector_now;

            turn_1m = false;
            turn_2m = false;
            target_now = 1;
            unit_cross = 0;
            unit_dot = 0;
            myCommand.arrived = false;

            // other variables
            myCommand.var1 = target[target_now].Theta;
            myCommand.var2 = target_now;
            //Toby's patch
            cal_turn(target[target_now].Theta);
            cal_turn(target[target_now].Theta);
            forward_only(1);
            //fwr.Write("[cal_move]forward_only done!!!" + "\r\n");
            Log.Debug("Brian", "[cal_move]forward_only done!!!");
            //fwr.Write("[cal_move]myFlag.moving=" + myFlag.moving + "\r\n");
            
            Log.Debug("Brian", "[cal_move]myFlag.moving=" + myFlag.moving);

            while (myFlag.moving)
            {
                // define tolarence range
                //Toby's patch
                if (target_now < target_total) target_range = ref_range[1];
                else target_range = ref_range[0];
                //Toby's end

                // check obstacles
                ob.save_sensor_reading(myVehicle.sonic);
#if OBSTACLE
                //Brian+: 2015/03/25 Disable sonic for test
                if (ob.HasObstacle && !myCommand.arrived)
                {
                    ob.avoid(myCommand.turn);
                    myCommand.speed = ob.OutSpeed;
                    myCommand.turn = ob.OutTurn;
                }
                else 
#endif          
                if (myFlag.sampling)
                {
                    myFlag.sampling = false;
                    #region diff_dist & diff_angle
                    // calculate distance difference and check if arrived to the target
                    diff_dist = (Single)Math.Sqrt((target[target_now].X - myTag.Avg.X) * (target[target_now].X - myTag.Avg.X) + (target[target_now].Y - myTag.Avg.Y) * (target[target_now].Y - myTag.Avg.Y));
                    if (diff_dist < target_range) myCommand.arrived = true;

                    // calculate angle difference
                    diff_angle = (Single)(Math.Atan2((target[target_now].Y - myTag.Avg.Y), (target[target_now].X - myTag.Avg.X)) * 180f / 3.14f) + cfg.MapEast - myVehicle.compass;
                    if (diff_angle > 180) diff_angle = diff_angle - 360;
                    else if (diff_angle < -180) diff_angle = diff_angle + 360;
                    #endregion

                    #region determine the speed of the vehicle (for reference)
                    if (diff_dist < target_range)      // if arrived the target
                    {
                        myCommand.speed = 0;
                        myCommand.turn = 0;
                        myCommand.arrived = true;
                    }
                    //Toby's patch
                    else if (diff_dist < 150)    // if pretty close to the target
                    {
                        myCommand.speed = (int)(30 + diff_dist * (max_speed - 30) / 150f);
                        myCommand.arrived = false;
                        max_turn = 100;
                    }
                    //Toby's end
                    else                        // ordinary situation
                    {
                        myCommand.speed = max_speed;
                        myCommand.arrived = false;
                        max_turn = 60; //Toby's patch
                    }

                    #endregion

                    if (diff_dist < 200 && diff_dist > 100)
                    {
                        #region if need to calibrate the bearings
                        if ((diff_angle > 10 || diff_angle < -10) && !turn_2m)
                        {
                            turn_2m = true;
                            cal_turn((Single)(Math.Atan2((target[target_now].Y - myTag.Avg.Y), (target[target_now].X - myTag.Avg.X)) * 180f / 3.14f) + cfg.MapEast);
                        }
                        #endregion
                    }
                    else if (diff_dist < 100)
                    {
                        #region if need to calibrate the bearings
                        if ((diff_angle > 10 || diff_angle < -10) && !turn_1m)
                        {
                            turn_1m = true;
                            cal_turn((Single)(Math.Atan2((target[target_now].Y - myTag.Avg.Y), (target[target_now].X - myTag.Avg.X)) * 180f / 3.14f) + cfg.MapEast);
                        }
                        #endregion
                    }
                    else
                    {
                        #region determine the turn of the vehicle
                        tmpSingle1 = (Single)Math.Sqrt((myTag.Avg.X - previous_step.X) * (myTag.Avg.X - previous_step.X) + (myTag.Avg.Y - previous_step.Y) * (myTag.Avg.Y - previous_step.Y));
                        if (tmpSingle1 < 10)
                        {
                            // distance is too short, keep going but reduce turn angle
                            tmpSingle2 = myCommand.turn * 0.8f;
                            myCommand.turn = (int)tmpSingle2;
                        }
                        else
                        {
                            // calculate the two vectors and corresponding unit scalar
                            vector_old.X = myTag.Avg.X - previous_step.X;
                            vector_old.Y = myTag.Avg.Y - previous_step.Y;
                            vector_now.X = target[target_now].X - myTag.Avg.X;
                            vector_now.Y = target[target_now].Y - myTag.Avg.Y;
                            vector_cross = vector_old.X * vector_now.Y - vector_old.Y * vector_now.X;
                            scalar_robot = (Single)Math.Sqrt(vector_old.X * vector_old.X + vector_old.Y * vector_old.Y);
                            scalar_target = (Single)Math.Sqrt(vector_now.X * vector_now.X + vector_now.Y * vector_now.Y);
                            unit_cross = vector_cross / scalar_target / scalar_robot; //Toby's patch
                            unit_dot = (vector_old.X * vector_now.X + vector_old.Y * vector_now.Y) / scalar_robot / scalar_target;
                            if (unit_cross > 1) unit_cross = 1;
                            else if (unit_cross < -1) unit_cross = -1;
                            if (unit_dot > 1) unit_dot = 1;
                            else if (unit_dot < -1) unit_dot = -1;

                            if (unit_dot < 0)
                            {
                                if (unit_cross < 0) unit_cross = -1;
                                else unit_cross = 1;
                            }

                            // update previous error
                            for (int i = 4; i >= 1; i--)
                            {
                                err[i] = err[i - 1];
                            }
                            err[0] = unit_cross;

                            // calculate totoal error
                            tmpSingle1 = 0;
                            for (int i = 0; i <= 4; i++)
                            {
                                tmpSingle1 = tmpSingle1 + err[i] * k[i];
                            }
                            //if (tmpSingle1 > k_total) tmpSingle1 = k_total;
                            //else if (tmpSingle1 < k_total * -1) tmpSingle1 = k_total * -1f;

                            // calculate turn
                            //Toby's patch
                            myCommand.turn = (int)(myCommand.turn + tmpSingle1);
                            if (myCommand.turn > max_turn) myCommand.turn = (int)max_turn;
                            else if (myCommand.turn < max_turn * -1) myCommand.turn = (int)(max_turn * -1);

                            if (err[0] < 0.1 && err[0] > -0.1)
                            {
                                if (err[1] < 0.1 && err[1] > -0.1) myCommand.turn = 0;
                            }
                            //Toby's end

                            // update previous step
                            previous_step.X = myTag.Avg.X;
                            previous_step.Y = myTag.Avg.Y;
                        }
                        #endregion
                    }

                }
                #region arrived the target or any waypoint
                if (myCommand.arrived)
                {
                    myCommand.arrived = false;
                    turn_1m = false;
                    turn_2m = false;
                    
                    if (target_now < target_total)
                    {
                        target_now++;
                        for (i = 0; i <= 2; i++)
                        {
                            Thread.Sleep(500);
                            cal_turn(target[target_now].Theta);
                        }
                        forward_only(1);
                    }
                    else
                    {
                        myFlag.moving = false;
                        target_total = 0;
                        target_now = 1;
                        
                        //Brian+ 2015/03/31: Close log file
                        //fwr.WriteLine("Navigation Stop!!");
                        //fwr.Close();
                        //Brian+ for renew UI
                        //btnDelete.Enabled = false;
                        //btnGo.Enabled = false;
                        //view.Invalidate();

                    }
                }
                #endregion
                // at last, output the command
                
                OutCommand(myCommand);
                Thread.Sleep(90);
                //fwr.Write("[cal_move]block3 entered!!" + "\r\n");
            }
        }

        private void cal_turn(Single TargetTheta)
        {
            Single diff_angle;

            Thread.Sleep(500);

            diff_angle = TargetTheta - myVehicle.compass;
            if (diff_angle > 180) diff_angle = diff_angle - 360;
            else if (diff_angle < -180) diff_angle = diff_angle + 360;
            while (diff_angle > 10 || diff_angle < -10)
            {
                diff_angle = TargetTheta - myVehicle.compass;
                if (diff_angle > 180) diff_angle = diff_angle - 360;
                else if (diff_angle < -180) diff_angle = diff_angle + 360;

                myCommand.turn = (int)diff_angle;
                if (myCommand.turn > 90) myCommand.turn = 90;
                else if (myCommand.turn < -90) myCommand.turn = -90;
                else if (myCommand.turn > 0 && myCommand.turn < 15) diff_angle = 15;
                else if (myCommand.turn < 0 && myCommand.turn > -15) diff_angle = -15;


                myCommand.speed = 0;
                myCommand.vehicle_turnning = true;
                myCommand.vehicle_done = false;
                OutCommand(myCommand);

                Thread.Sleep(100);
            }
        }

        private void forward_only(int cycle)
        {
            if (cycle == 0) return;

            for (int i = 0; i < cycle; i++)
            {
                myCommand.speed = 80;
                myCommand.turn = 0;
                OutCommand(myCommand);
                Thread.Sleep(100);
            }
        }

        private void backward_only(int cycle)
        {
            if (cycle == 0) return;

            for (int i = 0; i < cycle; i++)
            {
                myCommand.speed = -60;
                myCommand.turn = 0;
                OutCommand(myCommand);
                Thread.Sleep(100);
            }
        }

        private void avg_stdev(out Single avg, out Single stdev, Single[] set)
        {
            short len, i;
            Single sum;

            avg = 0;
            stdev = 0;
            sum = 0;

            len = (short)set.Length;
            if (len > 0)
            {
                for (i = 0; i <= len - 1; i++)
                {
                    avg = avg + set[i];
                }
                avg = avg / len;
                for (i = 0; i <= len - 1; i++)
                {
                    sum = sum + (Single)Math.Pow(set[i] - avg, 2);
                }
                stdev = (Single)Math.Sqrt(sum / len);
            }
            
        }

        private void ControlOut(object sender, EventArgs e)
        {
            byte[] outbyte = new byte[10];
            int outbytelen = 0;
            short speed = TowardOneTarget.OutSpeed;
            short turn = TowardOneTarget.OutTurn;

            if (speed == 0 && turn != 0)
            {
                outbyte[0] = 0x53;
                outbyte[1] = 0x12;
                outbyte[2] = (byte)((turn >> 8) & 0xFF);
                outbyte[3] = (byte)(turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                outbytelen = 6;
                Uart2C.SendMsgUart(pic32_open, outbyte);
                //drivingPort.Write(outbyte, 0, outbytelen);
            }
            else
            {
                if (speed > 100) speed = 100;
                else if (speed < -100) speed = -100;
                if (turn > 100) turn = 100;
                else if (turn < -100) turn = -100;

                outbyte[0] = 0x53;
                outbyte[1] = 0x13;
                outbyte[2] = (byte)(speed & 0xFF);
                outbyte[3] = (byte)(turn & 0xFF);
                outbyte[4] = 0x00;
                outbyte[5] = 0x45;
                outbytelen = 6;
                Uart2C.SendMsgUart(pic32_open, outbyte);
                //drivingPort.Write(outbyte, 0, outbytelen);

            }
        }

        private void Dispatch()
        {
            Single arrive_range;

            target_now = 1;
            TowardOneTarget.NewTarget(target[target_now].X, target[target_now].Y, target[target_now].Theta, 20);
            while (myFlag.moving)
            {
                if ((TowardOneTarget.Status & TowardOneTarget.e_status.Moving) > 0)
                {
                    Thread.Sleep(1);
                }
                else if ((TowardOneTarget.Status & TowardOneTarget.e_status.Arrived) > 0)
                {
                    Thread.Sleep(500);
                    if (target_now == target_total)
                    {
                        target_total = 0;
                        myFlag.moving = false;
                    }
                    else
                    {
                        target_now++;
                        if (target_now == target_total) arrive_range = 20;
                        else arrive_range = 20;
                        TowardOneTarget.NewTarget(target[target_now].X, target[target_now].Y, target[target_now].Theta, arrive_range);
                    }
                }
            }
        }



    }


    public class DrawView : View
    {
        public event EventHandler<Canvas> OnDrawing;
        public event EventHandler<MotionEvent> OnTouching;
        
        public DrawView(Context context)
            : base(context)
        {

        }
        protected override void OnSizeChanged(int w, int h, int oldw, int oldh)
        {
        }
        protected override void OnDraw(Canvas canvas)
        {
            if (OnDrawing != null)
                OnDrawing(this, canvas);
        }
        public override bool OnTouchEvent(MotionEvent e)
        {
            if (OnTouching != null)
                OnTouching(this, e);
            return false;
        }
    }
}

