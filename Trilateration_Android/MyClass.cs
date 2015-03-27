using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;

namespace Trilateration_Android
{
    public struct struct_config
    {
        public ushort MapWidth;
        public ushort MapHeight;
        public ushort GridWidth;
        public ushort GridHeight;
        public ushort ControlInv;
        public ushort MaxSpeed;
        public short MapEast;

    }

    public struct move_command
    {
        public int speed;
        public int turn;
        public Single var1;
        public Single var2;
        public Single var3;
        public bool arrived;
        public bool vehicle_done;
        public bool vehicle_turnning;
    }
    public class class_Vehicle
    {
        private Single p_compass;

        public Single compass
        {
            get { return p_compass; }
            set
            {
                if (value >= 0 && value <= 360)
                {
                    p_compass = value;
                }
            }
        }
        public short encoderL;
        public short encoderR;
        public byte[] sonic = new byte[5];

        public class_Vehicle()
        {
            compass = 0f;
            encoderL = 0;
            encoderR = 0;
        }
    }

    public struct struct_PointF
    {
        public Single X;
        public Single Y;
        public Single Theta;
    }

    public struct struct_Location
    {
        public int X;
        public int Y;
        public string Note;
    }

    public class class_flag
    {
        public bool loc_init_done;
        public bool loc_on;
        public bool moving;
        public bool testing;
        public bool sampling;
        public bool screen_ready;
        public int loc_init_step;

        public class_flag()
        {
            loc_init_done = false;
            loc_on = false;
            moving = false;
            testing = false;
            loc_init_step = 0;
        }
    }

    public class beacon
    {
        private int rate;

        public bool Updated;
        public Single X;
        public Single Y;
        public Single Z;
        public Single Range;
        public Single RangeOld;
        public int Rate
        {
            get { return rate; }
            set
            {
                if (value > 100) rate = 100;
                else if (value < 0) rate = 0;
                else rate = value;
            }
        }
        public double SampleTime;
        public string Message;
        public struct_PointF Avg = new struct_PointF();
    }

}