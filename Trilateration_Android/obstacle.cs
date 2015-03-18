using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Trilateration
{
    class obstacle
    {
        // constant value
        private int d_max = 150;
        private int d_safe = 35;
        private int d_danger = 15;
        private int speed_normal = 25;
        private int sonic_number = 5;
        private Single kp = 0.8f;
        private int ku = 16; //32
        // need to know at the beginning
        private int turn_max;
        private Single cycle_interval;

        // private variables
        private int i;
        private int[] sonic=new int[5];
        private int speed;
        private int turn;
        private int d_shortest;
        private int d_shortest_old;
        private Single d_delta;
        private Single err_delta;
        private Single u;
        private Single tmpSingle;

        // flags
        private bool hasObstacle;
        private bool right;
        private bool left;
        private bool stop;

        // public variables
        public int OutSpeed
        {
            get {
                if (speed > 100) return 100;
                else if (speed < -100) return -100;
                else return speed;
            }
        }
        public int OutTurn
        {
            get {
                if (turn > turn_max) return turn_max;
                else if (turn < -turn_max) return -turn_max;
                else return turn;
            }
        }
        public bool HasObstacle
        {
            get { return hasObstacle; }
        }
        public Single Delta_error
        {
            get { return err_delta; }
        }
        public Single Delta_D
        {
            get { return d_delta; }
        }
        public Single Control
        {
            get { return u; }
        }

        public obstacle(int var1,Single var2)
        {
            turn_max = var1;
            cycle_interval = var2;
            hasObstacle = false;
            stop = false;
            d_shortest = d_safe;
            d_shortest_old = d_safe;
            d_delta = 0;
            err_delta = 0;
            u = 0;
            turn = 0;
        }

        public void save_sensor_reading(byte[] readings)
        {
            hasObstacle = false;
            for (i = 0; i < sonic_number; i++)
            {
                sonic[i] = (int)readings[i];
                if (sonic[i] < d_safe) hasObstacle = true;
            }
        }

        public void avoid(int origin_turn)
        {
            Single ss;
            Single cost_R, cost_L;

            turn = origin_turn;

            // calculate the cost of right and left
            cost_L = 1 / ((sonic[0] + sonic[1] + sonic[2]+1) / 3f);
            cost_R = 1 / ((sonic[2] + sonic[3] + sonic[4]+1) / 3f);

            // find the shortest distance between the obstacle and the vehicle
            for (i = 0; i < sonic_number; i++)
            {
                if (d_shortest > sonic[i]) d_shortest = sonic[i];
            }

            // check if the shortest distance is away from the safe zone
            if (d_shortest >= d_safe)
            {
                hasObstacle = false;
            }

            d_delta = (Single)(5)*((d_shortest - d_shortest_old) / cycle_interval) / (d_safe / cycle_interval);
            err_delta = 1f * (d_safe - d_shortest) / d_safe;
            ss = kp * (err_delta + d_delta);
            u = Fuzzy(ss);
        //    if (d_delta >= 0)
         //   {
                if (cost_L >= cost_R)
                {
                    right = true;
                    left = false;
                    stop = false;
                    speed = speed_normal;
                }
                else
                {
                    right = false;
                    left = true;
                    stop = false;
                    speed = speed_normal;
                }
                tmpSingle = turn + u * 5f *ku;
                turn = (int)tmpSingle;
           /* }
            else
            {
                right = false;
                left = false;
                stop = true;
                speed = 18;
                //turn = (int)(turn + 5);
                turn = 1 * turn;
            }*/

            // update old distance
            d_shortest_old = d_shortest;
            
        }

        private Single Fuzzy(Single input)
        {
            Single P = 1;
            Single Z = 0;
            Single N = -1;

            Single output;

            if (input >= P) output = -1;
            else if ((input < P) && (input > Z)) output = -1f * Math.Abs(input);
            else if ((input < Z) && (input > N)) output = Math.Abs(input);
            else if (input < N) output = 1;
            else output = 0;
            if (left== true) output = -1f * (output);
            return output;
        }
    }
}
