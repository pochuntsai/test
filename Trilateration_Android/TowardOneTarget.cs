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
        private static short max_turn;
        private static e_status status;
        private static short speed, turn;
        private static s_position target;
        private static Single target_range;
        private static obstacle ob = new obstacle(80, 0.1f);

        private static HighPerformanceCounter hpcounter1 = new HighPerformanceCounter();

        // Input
        public static s_position Pose;
        public static bool StopVehicle;
        public static class_Vehicle Vehicle = new class_Vehicle();

        // Output
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

        // Declare event
        public static event ControlOutputEventHandler ControlEvent;

        public static void Start()
        {
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
            Single[] k = new Single[5] { 30f, 10f, 5f, 0f, 0f };

            Single diff_dist;
            Single diff_angle;
            Single tmpSingle1, tmpSingle2;
            Single Vcross;
            Single Sr, St;
            Single Ucross, Udot;
            int tmpInt;
            s_position Pose_old;
            s_position Vt;
            s_position Vr;
            Single[] err = new Single[5];

            status = e_status.None;
            Pose_old.X = Pose.X;
            Pose_old.Y = Pose.Y;
            while (true)
            {
                hpcounter1.Start();
                if (StopVehicle) goto Wait;

                if (status == e_status.HasTask)
                {
                    #region Get a new task, need to initial
                    for (tmpInt = 0; tmpInt < err.Length; tmpInt++)
                    {
                        err[tmpInt] = 0;
                    }
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

                    #region check obstacles
                    //Brian+: Disble obstacle for test
#if OBSTACLE                   
                    //ob.save_sensor_reading(Vehicle.sonic);
                    //if (ob.HasObstacle) status = status | e_status.HasObstacle;
                    //else status = status & e_status.NoObstacle;
#else                    
                    status = status & e_status.NoObstacle;
#endif
                    #endregion

                    if ((status & e_status.Arrived)>0)
                    {
                        speed = 0;
                        turn = 0;
                        status = status & e_status.NoMoving;
                    }
                    else if ((status & e_status.HasObstacle) > 0)
                    {
                        ob.avoid(turn);
                        speed = (short)ob.OutSpeed;
                        turn = (short)ob.OutTurn;
                    }
                    else
                    {
                        #region determine the speed of the vehicle (for reference)
                        if (diff_dist < 150)    // if pretty close to the target
                        {
                            speed = (short)(40 + diff_dist * (max_speed - 40) / 150f);
                            max_turn = 100;
                        }
                        else                        // ordinary situation
                        {
                            speed = max_speed;
                        }
                        #endregion

                        if (diff_dist < 200 && (diff_angle > 30 || diff_angle < -30))
                        {
                            #region if need to calibrate the bearings
                            Thread.Sleep(500);
                            MakeTurn((Single)(Math.Atan2((target.Y - Pose.Y), (target.X - Pose.X)) * 180f / 3.14f));
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
                                // calculate the two vectors and corresponding unit scalar
                                Vr.X = Pose.X - Pose_old.X;
                                Vr.Y = Pose.Y - Pose_old.Y;
                                Vt.X = target.X - Pose.X;
                                Vt.Y = target.Y - Pose.Y;
                                Vcross = Vr.X * Vt.Y - Vr.Y * Vt.X;
                                Sr = (Single)Math.Sqrt(Vr.X * Vr.X + Vr.Y * Vr.Y);
                                St = (Single)Math.Sqrt(Vt.X * Vt.X + Vt.Y * Vt.Y);
                                if (Sr == 0 || St == 0)
                                {
                                    Ucross = 0;
                                    Udot = 0;
                                }
                                else
                                {
                                    Ucross = Vcross / St / Sr;
                                    Udot = (Vr.X * Vt.X + Vr.Y * Vt.Y) / Sr / St;
                                }
                                if (Ucross > 1) Ucross = 1;
                                else if (Ucross < -1) Ucross = -1;
                                if (Udot > 1) Udot = 1;
                                else if (Udot < -1) Udot = -1;

                                if (Udot < 0)
                                {
                                    if (Ucross < 0) Ucross = -1;
                                    else Ucross = 1;
                                }

                                // decide max turn
                                if (Ucross > 0.5 || Ucross < -0.5) max_turn = 70;
                                else max_turn = 60;

                                // update previous error
                                for (int i = 4; i >= 1; i--)
                                {
                                    err[i] = err[i - 1];
                                }
                                err[0] = Ucross;

                                // calculate totoal error
                                tmpSingle1 = 0;
                                for (int i = 0; i <= 4; i++)
                                {
                                    tmpSingle1 = tmpSingle1 + err[i] * k[i];
                                }

                                // calculate turn
                                turn = (short)(turn + tmpSingle1);
                                if (turn > max_turn) turn = (short)max_turn;
                                else if (turn < max_turn * -1) turn = (short)(max_turn * -1);

                                if (err[0] < 0.1 && err[0] > -0.1)
                                {
                                    if (err[1] < 0.1 && err[1] > -0.1) turn = 0;
                                }

                                // update previous step
                                Pose_old.X = Pose.X;
                                Pose_old.Y = Pose.Y;
                            }
                            #endregion
                        }

                        
                    }
                    if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                    goto Wait;

                }

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
            Single diff_angle;
            do
            {
                diff_angle = TargetAngle - Pose.Theta;
                if (diff_angle > 180) diff_angle = diff_angle - 360;
                else if (diff_angle < -180) diff_angle = diff_angle + 360;

                turn = (short)diff_angle;
                if (turn > 90)turn = 90;
                else if (turn < -90) turn = -90;
                else if (turn > 0 && turn < 15) diff_angle = 15;
                else if (turn < 0 && turn > -15) diff_angle = -15;
                speed = 0;

                if (ControlEvent != null) ControlEvent(null, EventArgs.Empty);
                Thread.Sleep(100);

            } while (diff_angle > 5 || diff_angle < -5);
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
