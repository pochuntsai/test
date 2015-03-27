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
        private int[] d_safe = new int[5] { 15, 30, 35, 30, 15 };
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
        private int[] sonic;
        private int[] sonic_old;
        private Single[] cost;
        private Single d_delta;
        private Single err_delta;
        private int speed;
        private int turn;
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
        public Single Control
        {
            get { return u; }
        }

        /// <summary>
        /// To create an object from this class
        /// </summary>
        /// <param name="var1">input the max turning value</param>
        /// <param name="var2">input the duration of each cycle (in second)</param>
        public obstacle(int var1,Single var2)
        {
            turn_max = var1;
            cycle_interval = var2;
            hasObstacle = false;
            stop = false;
            u = 0;
            turn = 0;
            sonic = new int[sonic_number];
            sonic_old = new int[sonic_number];
            cost = new Single[sonic_number];
            d_delta = 0;
            err_delta = 0;
            for (i = 0; i < sonic_number; i++)
            {
                sonic[i] = d_max;
                sonic_old[i] = d_max;
                cost[i] = 1;
            }
        }

        /// <summary>
        /// To save the sensor readings and determine if there's any obstacle
        /// </summary>
        /// <param name="readings">The number of elements in the array has to be same as setting</param>
        public void save_sensor_reading(byte[] readings)
        {
            hasObstacle = false;
            for (i = 0; i < sonic_number; i++)
            {
                sonic_old[i] = sonic[i];
                sonic[i] = (int)readings[i];
                if (sonic[i] < d_safe[i]) hasObstacle = true;

                cost[i] = (Single)sonic[i] / d_safe[i];
                if (cost[i] > 1) cost[i] = 1;
            }
        }

        /// <summary>
        /// If there's an obstacle, initiate avoidance mechanism
        /// </summary>
        /// <param name="origin_turn">Give the current turning command as reference</param>
        public void avoid(int origin_turn)
        {
            int index_nearest;
            Single ss;
            Single cost_R, cost_L;

            turn = origin_turn;

            // calculate the cost of right and left
            cost_L = (cost[0] + cost[1] + cost[2]) / 3f;
            cost_R = (cost[2] + cost[3] + cost[4]) / 3f;

            index_nearest = 0;
            for (i = 0; i < sonic_number; i++)
            {
                if (cost[i] < cost[index_nearest])
                {
                    index_nearest = i;
                }
            }


            d_delta = (Single)(5)*((sonic[index_nearest] - sonic_old[index_nearest]) / cycle_interval) / (d_safe[index_nearest] / cycle_interval);
            err_delta = 1f * (d_safe[index_nearest] - sonic[index_nearest]) / d_safe[index_nearest];
            ss = kp * (err_delta + d_delta);
            
            if (cost_L >= cost_R)
            {
                right = false;
                left = true;
                stop = false;
                speed = speed_normal;
            }
            else
            {
                right = true;
                left = false;
                stop = false;
                speed = speed_normal;
            }
            u = Fuzzy(ss);
            tmpSingle = turn + u * 5f *ku;
            turn = (int)tmpSingle;
            
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
