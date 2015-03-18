using System;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Threading;
using Android.Runtime;

namespace Trilateration
{
    /// <summary>
    /// Counter use CPU clock
    /// </summary>
    public class HighPerformanceCounter
    {
        private  IntPtr class_ref;
        private  IntPtr id_nanoTime;
        private long startTime, stopTime;
        private double freq = 1000000000.0;
        public HighPerformanceCounter()
        {
            startTime = 0;
            stopTime = 0;
            class_ref = JNIEnv.FindClass("java/lang/System");
            id_nanoTime = JNIEnv.GetStaticMethodID(class_ref, "nanoTime", "()J");
        }

        [Register("nanoTime", "()J", "")]
        public  long NanoTime()
        {
            return JNIEnv.CallStaticLongMethod(class_ref, id_nanoTime);
        }
        ///// <summary>
        ///// Get counter from cpu
        ///// </summary>
        ///// <param name="lpPerformanceCount">clock data</param>
        ///// <returns>error output</returns>
        //[DllImport("Kernel32.dll")]
        //public static extern bool QueryPerformanceCounter(
        //    out long lpPerformanceCount);

        ///// <summary>
        ///// Get Frequency counter
        ///// </summary>
        ///// <param name="lpFrequency">freq data</param>
        ///// <returns>error output</returns>
        //[DllImport("Kernel32.dll")]
        //public static extern bool QueryPerformanceFrequency(
        //    out long lpFrequency);





        //private long freq;

        /// <summary>
        /// Constructor
        /// </summary>
        //public HighPerformanceCounter()
        //{
        //    startTime = 0;
        //    stopTime = 0;

        //    if (QueryPerformanceFrequency(out freq) == false)
        //    {
        //        // high-performance counter not supported

        //        throw new Win32Exception();
        //    }
        //}

        /// <summary>
        /// Start the timer
        /// </summary>
        public void Start()
        {
            // lets do the waiting threads there work

            Thread.Sleep(0);
            startTime = NanoTime();
          //  QueryPerformanceCounter(out startTime);
        }

        /// <summary>
        ///  Stop the timer
        /// </summary>
        public void Stop()
        {
            stopTime = NanoTime();
            //QueryPerformanceCounter(out stopTime);
        }

        /// <summary>
        /// Returns the duration of the timer from Start to stop (in seconds)
        /// </summary>
        public double Duration
        {
            get
            {
                double tSpan = (double)(stopTime - startTime) / (double)freq;
                if (tSpan < 0) tSpan = 0;
                return tSpan;
            }
        }

        /// <summary>
        /// from start to current counter in sec
        /// </summary>
        public double TimeFromStart
        {
            get
            {
                long timeNow;
                //   QueryPerformanceCounter(out timeNow);
                timeNow = NanoTime();
                double tSpan = (double)(timeNow - startTime) / (double)freq;
                return tSpan;
            }
        }
    }
}
