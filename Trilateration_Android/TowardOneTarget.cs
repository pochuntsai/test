//#define OBSTACLE
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Trilateration_Android;

namespace Trilateration
{
    // Declare a delegate event
    public delegate void ControlOutputEventHandler(object sender, EventArgs args);

    class TowardOneTarget
    {
        public enum e_status { None = 0x00, HasTask = 0x01, Initialized = 0x02, Moving = 0x03, HasObstacle = 0x04, Arrived = 0x08, 
            NoObstacle= 0xFB, NoMoving=0xFC};

        public struct s_position
        {
            public Single X;
            public Single Y;
            public Single Theta;
        }

        private static short max_speed = 95;
        private static short max_turn = 45;
        private static e_status status;
        private static short speed, turn;
        private static s_position target;
        private static Single target_range;
        private static obstacle ob = new obstacle(80, 0.1f);

        //private static Single[] OAbuff = new Single[5];
        //private static short OAcount;
        //private static Single OA;

        private static HighPerformanceCounter hpcounter1 = new HighPerformanceCounter();

        // Input
        public static s_position Pose;
        public static bool StopVehicle;
        public static bool PureMove;
        public static class_Vehicle Vehicle = new class_Vehicle();

        // Output
        public static string OutStr;

        public static e_status Status
        {
            get{return status;}
        }
        public static short OutSpeed
        {
            get { return speed; }
        }
        public static short OutTurn
        {
            get { return turn; }
        }

        public static short Counter;

        // Declare event
        public static event ControlOutputEventHandler ControlEvent;

        public static void Start()
        {
            StopVehicle = false;
            PureMove = false;
            Counter = 0;
            Thread mainloop = new Thread(new ThreadStart(MainLoop));
            mainloop.IsBackground = true;
            mainloop.Start();
        }

        /// <summary>
        /// To enable and proceed the calculation of moving toward a target
        /// </summary>
        /// <param name="x">The x of the target</param>
        /// <param name="y">The y of the target</param>
        /// <param name="theta">The bearings of the target</param>
        public static void NewTarget(Single x, Single y, Single theta, Single range)
        {
            target.X = x;
            target.Y = y;
            target.Theta = theta;
            target_range = range;
            status = e_status.HasTask;
            StopVehicle = false;
        }

        private static void MainLoop()
        {
            Single[] k = new Single[2] { 0.85f, 0.4f };

            Single diff_dist;
            Single diff_angle;
            Single tmpSingle1, tmpSingle2;
            Single Vcross;
            double deviation, deviation_old, d_deviation;
            double a, b;
            double tmpDouble1, tmpDouble2;
            int tmpInt;
            s_position Pose_old;
            s_position Vt;
            s_position Vr;
            short back_count = 0;
            short lock_count = 0;
            short hit_count = 0;
            short mark_count = 0;

            status = e_status.None;
            Pose_old.X = Pose.X;
            Pose_old.Y = Pose.Y;
            deviation = 0;
            deviation_old = 0;
            while (true)
            {

                hpcounter1.Start();

                #region Check status
                if (Vehicle.Bumper == 0xFF)
                {
                    hit_count++;
                    back_count = 20;
                }

                ob.save_sensor_reading(Vehicle.sonic);
                //if (ob.HasObstacle) status = status | e_status.HasObstacle;
                //else status = status & e_status.NoObstacle;
                #endregion

                #region Mode 1 : Force stop
                if (StopVehicle) goto Wait;
                #endregion

                #region Mode 2 : Move forward
                if(PureMove)
                {
                    if(back_count>0)
                    {
                        back_count--;
                        hit_count = 0;
                        speed = -30;
                        turn = 0;
                    }
                    else if(ob.HasObstacle)
                    {
                        ob.avoid(turn,0);
                        speed = (short)ob.OutSpeed;
                        turn = (short)ob.OutTurn;
                    }
                    else
                    {
                        speed = 60;
                        turn = 0;
                    }
                    if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                    goto Wait;
                }
                #endregion

                #region Mode 3 : Move to target
                if (status == e_status.HasTask)
                {
                    #region Get a new task, need to initial
                    hit_count = 0;
                    back_count = 0;
                    MakeTurn(target.Theta);
                    MakeTurn(target.Theta);
                    ForwardOnly(1);
                    status = status | e_status.Initialized;
                    #endregion
                }
                if ((status & e_status.Moving) >0)
                {
                    #region diff_dist, diff_angle and check arrival
                    // calculate distance difference and check if arrived to the target
                    diff_dist = (Single)Math.Sqrt((target.X - Pose.X) * (target.X - Pose.X) + (target.Y - Pose.Y) * (target.Y - Pose.Y));
                    if (diff_dist < target_range) status = status | e_status.Arrived;

                    // calculate angle difference
                    diff_angle = (Single)(Math.Atan2((target.Y - Pose.Y), (target.X - Pose.X)) * 180f / 3.14f) - Pose.Theta;
                    if (diff_angle > 180) diff_angle = diff_angle - 360;
                    else if (diff_angle < -180) diff_angle = diff_angle + 360;
                    #endregion

                    if ((status & e_status.Arrived)>0)
                    {
                        speed = 0;
                        turn = 0;
                        status = status & e_status.NoMoving;
                    }
                    else if (back_count > 0)
                    {
                        back_count--;
                        if (hit_count == 1)
                        {
                            if (back_count == 0)
                            {
                                hit_count++;
                            }
                            else
                            {
                                speed = -35;
                                turn = 0;
                            }
                        }
                        else
                        {
                            if (back_count == 1)
                            {
                                if (diff_angle >= 0) MakeTurn(Pose.Theta + 90);
                                else MakeTurn(Pose.Theta - 90);
                            }
                            else if (back_count == 0)
                            {
                                ForwardOnly(12);
                                hit_count = 0;
                            }
                            else
                            {
                                speed = -35;
                                turn = 0;
                            }
                        }
                        Pose_old.X = Pose.X;
                        Pose_old.Y = Pose.Y;
                    }
                    else if (ob.HasObstacle)
                    {
                        lock_count = 50;
                        ob.avoid(turn,diff_angle);
                        speed = (short)ob.OutSpeed;
                        turn = (short)ob.OutTurn;
                        Pose_old.X = Pose.X;
                        Pose_old.Y = Pose.Y;
                    }
                    else
                    {
                        if (lock_count > 0) lock_count--;

                        #region determine the speed of the vehicle (for reference)
                        if (diff_dist < 150)    // if pretty close to the target
                        {
                            speed = (short)(40 + diff_dist * (max_speed - 40) / 150f);
							if(speed<15) speed = 15;
                            max_turn = 100;
                        }
                        else                        // ordinary situation
                        {
                            speed = max_speed;
                        }
                        #endregion

                        if (lock_count == 0 && diff_dist < 150 && (diff_angle > 30 || diff_angle < -30))
                        {
                            #region if need to calibrate the bearings
                            Thread.Sleep(100);
                            MakeTurn((Single)(Math.Atan2((target.Y - Pose.Y), (target.X - Pose.X)) * 180f / 3.14f));
                            Pose_old.X = Pose.X;
                            Pose_old.Y = Pose.Y;
                            #endregion
                        }
                        else
                        {
                            #region determine the turn of the vehicle
                            tmpSingle1 = (Single)Math.Sqrt((Pose.X - Pose_old.X) * (Pose.X - Pose_old.X) + (Pose.Y - Pose_old.Y) * (Pose.Y - Pose_old.Y));
                            if (tmpSingle1 < 5)
                            {
                                // distance is too short, keep going but reduce turn angle
                                tmpSingle2 = turn * 0.8f;
                                turn = (short)tmpSingle2;
                            }
                            else
                            {
                                // calculate the cross of the two vectors 
                                Vr.X = Pose.X - Pose_old.X;
                                Vr.Y = Pose.Y - Pose_old.Y;
                                Vt.X = target.X - Pose.X;
                                Vt.Y = target.Y - Pose.Y;
                                Vcross = Vr.X * Vt.Y - Vr.Y * Vt.X;
                                
                                // calculate deviation between the vehicle and the given path
                                if ((target.X - Pose_old.X) > -1 && (target.X - Pose_old.X) < 1)
                                {
                                    a = 0;
                                    b = target.Y;
                                }
                                else
                                {
                                    a = (target.Y - Pose_old.Y) / (target.X - Pose_old.X);
                                    b = (target.Y * Pose_old.X - Pose_old.Y * target.X) / (Pose_old.X - target.X);
                                }
                                deviation = Math.Abs(a * Pose.X - Pose.Y + b) / Math.Sqrt(a * a + 1);
                                if (deviation > 20) deviation = 20;
                                else if (deviation < -20) deviation = -20;

                                // calculate compensation
                                d_deviation = deviation - deviation_old;
                                tmpDouble2 = deviation * k[0] + d_deviation * k[1];
                                if (Vcross < 0) tmpDouble2 = tmpDouble2 * -1;
                                //if (Vcross < 0 && tmpDouble2 > 5) turn = -50;
                                //else if (Vcross > 0 && tmpDouble2 > 5) turn = 50;
                                //else turn = 0;

                                // calculate turn
                                turn = (short)(turn + tmpDouble2);
                                if (turn > max_turn) turn = (short)max_turn;
                                else if (turn < max_turn * -1) turn = (short)(max_turn * -1);

                                OutStr = a.ToString("f2") + " , " + b.ToString("f2") + " , " + deviation.ToString("f2") + " , " + tmpDouble2.ToString("f2") + " , " + turn.ToString();
                                deviation_old = deviation;
                                if (mark_count >= 3)
                                {
                                    mark_count = 0;
                                    Pose_old.X = Pose.X;
                                    Pose_old.Y = Pose.Y;
                                }
                                else mark_count++;
                            }
                            #endregion
                        }

                        
                    }
                    if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                    goto Wait;

                }
                #endregion

            Wait:
                hpcounter1.Stop();
                tmpInt = (int)(hpcounter1.Duration * 1000f);
                if (tmpInt < 90)
                {
                    Thread.Sleep(90 - tmpInt);
                }

            }
        }

        private static void MakeTurn(Single TargetAngle)
        {
            Single diff_angle=10;

            while (diff_angle > 5 || diff_angle < -5)
            {
                diff_angle = TargetAngle - Pose.Theta;
                if (diff_angle > 180) diff_angle = diff_angle - 360;
                else if (diff_angle < -180) diff_angle = diff_angle + 360;

                turn = (short)diff_angle;
                if (turn > 0 && turn < 15) turn = 15;
                else if (turn < 0 && turn > -15) turn = -15;
                speed = 0;

                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);

            }
        }

        private static void ForwardOnly(int cycle)
        {
            if (cycle == 0) return;

            for (int i = 0; i < cycle; i++)
            {
                speed = 80;
                turn = 0;
                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);
            }
        }

    }
}
