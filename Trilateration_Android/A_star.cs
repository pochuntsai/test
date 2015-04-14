using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Android.Graphics;
//using System.Drawing;

namespace Trilateration
{
    class propertiesList 
    {
        private short p_index;
        private short p_row, p_column;  // 在大地圖的行列位置 (這是用來計算 this point 到 目標點 的 成本)

        public short Index
        {
            get { return p_index; }
        }
        public short Row
        {
            get { return p_row; }
        }
        public short Column
        {
            get { return p_column; }
        }
        public short[] Neighbor = new short[8]; //鄰接點 的編號

        /// <summary>
        /// remark the boundary for each objects, the size is definded by user
        /// </summary>
        public propertiesList(short i, short j, uint mapWidth, uint mapHeight, short no)
        {
            //    7    0    1
            //    6    *    2
            //    5    4    3
            // 鄰居方塊 的編號(-1 is boundary)
            if (j == 0) this.Neighbor[0] = -1;
            else this.Neighbor[0] = (short)(no - mapWidth); // 上

            if (j == 0 || i == mapWidth - 1) this.Neighbor[1] = -1;
            else this.Neighbor[1] = (short)(no - mapWidth + 1); // 上 右

            if (i == mapWidth - 1) this.Neighbor[2] = -1;
            else this.Neighbor[2] = (short)(no + 1); // 右

            if (i == mapWidth - 1 || j == mapHeight - 1) this.Neighbor[3] = -1;
            else this.Neighbor[3] = (short)(no + mapWidth + 1); // 右 下

            if (j == mapHeight - 1) this.Neighbor[4] = -1;
            else this.Neighbor[4] = (short)(no + mapWidth); // 下

            if (j == mapHeight - 1 || i == 0) this.Neighbor[5] = -1;
            else this.Neighbor[5] = (short)(no + mapWidth - 1); // 下 左

            if (i == 0) this.Neighbor[6] = -1;
            else this.Neighbor[6] = (short)(no - 1); // 左

            if (i == 0 || j == 0) this.Neighbor[7] = -1;
            else this.Neighbor[7] = (short)(no - mapWidth - 1); //左上

            p_row = i;
            p_column = j;
            p_index = no;
        }
    }

    partial class Map
    {
        private short Start = -1; // initial start no (default value -1)
        private short Target = -1; // initial target no
        private short NeighborStep = 1; // 斜行
        public bool Autoflag;
        private List<short> openList = new List<short>();
        private List<short> closedList = new List<short>();
        private List<short> pathList = new List<short>();
        private List<bool> cornerIndex = new List<bool>();
        private List<int> GIndex = new List<int>();  //開始點 到 這個點 的 成本
        private List<int> HIndex = new List<int>(); //這個點 到 目標點 的 成本
        private List<short> ParentIndex = new List<short>(); //這個點 的 父親的編號 
        private List<short> Heading = new List<short>();
        public List<Point> path_Result = new List<Point>(); //Result
        public List<Point> path_Detail = new List<Point>();
        public static List<propertiesList> NodeList = new List<propertiesList>();
        /// <summary>
        /// To creat objects first of all
        /// </summary>
        public void A_star()
        {
            short no = 0;
            bool corner = false;  // 是否為 corner
            int G = 0;
            int H = 0;
            for (short j = 0; j < Grid_H; j++)
            {
                for (short i = 0; i < Grid_W; i++)
                {
                    propertiesList A = new propertiesList(i, j, Grid_W, Grid_H, no);
                    GIndex.Add(G);
                    HIndex.Add(H);
                    cornerIndex.Add(corner);
                    NodeList.Add(A);
                    no++;
                }
            }
        }
        /// <summary>
        /// To import the robot position and target position by user
        /// </summary>
        public bool initial_position(Point robot, Point goal)
        {
            path_Result.Clear();
            if (robot.X < 0 || robot.Y < 0) return false;
            if (robot.X > 99 || robot.Y > 99) return false;
            if (goal.X < 0 || goal.Y < 0) return false;
            if (goal.X > 99 || goal.Y > 99) return false;
            Start = (short)(robot.X + Grid_H * robot.Y);
            Target = (short)(goal.X + Grid_H * goal.Y);
            Autoflag = AutoPlan();
            //check 
            if (Autoflag == true) if (Walkability[NodeList[Start].Row, NodeList[Start].Column] > 0) find(robot);
            return true;
        }
        /// <summary>
        /// Read only, if robot position fall over gray region 
        /// </summary>
        private void find(Point robot)
        {
            bool myflag = true;
            double Angle = 0;
            ushort R = 1;
            short X, Y;
            Point P;
            while ((R < 10)&&(myflag != false))
            {
                for (int i = 0; Angle < 2 * Math.PI; i++)
                {
                    if (Walkability[NodeList[Start].Row, NodeList[Start].Column] == 0)
                    {
                        P = new Point(NodeList[Start].Row, NodeList[Start].Column);
                        path_Result.Add(P);
                        myflag = false;
                        break;
                    }
                    Angle = (double)(i * 15 * Math.PI / 180);
                    Y = (short)(R * Math.Sin(Angle));
                    X = (short)(R * Math.Cos(Angle));
                    Start = (short)(X + robot.X + Grid_H * (robot.Y + Y));
                }
                R++;
                Angle = 0;
            }
        }
        /// <summary>
        /// Read only, find the corners to walking
        /// </summary>
        private void path_corner_finder()
        {
            List<short> path_Result_temp = new List<short>();
            //rearrange
            int i = pathList.Count - 1;
            int inf = 58; // inf value
            short tempIndex;
            short tempIndex_past = 0; //inital value
            Point tempP_past = new Point(0, 0); //inital value
            Point tempP;
            Point pathXY;
            Single slope;
            Single slope_past = -999; //inf
            bool flag;
            while (i >= 0)
            {
                tempIndex = pathList[i];
                //find corner
                tempP = new Point(NodeList[tempIndex].Row, NodeList[tempIndex].Column);
                if (i != pathList.Count - 1)
                {
                    slope = Slope(tempP, tempP_past, inf);
                    if (slope_past != -999)
                    {
                        flag = corner_point(tempP, tempP_past, slope, slope_past, inf);
                        if (flag == true)
                        {
                            path_Result_temp.Add(tempIndex_past); //find corner
                            cornerIndex[tempIndex_past] = true;
                        }
                    }
                    tempIndex_past = tempIndex;
                    slope_past = slope;
                }
                tempP_past = new Point(tempP.X, tempP.Y);
                if (i == 0)
                { //last index
                    path_Result_temp.Add(tempIndex); // Target
                    cornerIndex[tempIndex] = true;
                    for (int k = 0; k < path_Result_temp.Count; k++)
                    { //total corner count
                        pathXY = new Point(NodeList[path_Result_temp[k]].Row, NodeList[path_Result_temp[k]].Column);
                        path_Result.Add(pathXY);  //path no transfer to point
                    }
                     DeletePoint();
                }
                i--;
            }
        }
        /// <summary>
        /// Read only, delete the closest corners points
        /// </summary>
        private void DeletePoint()
        {
            double D = 3.5;
            int k = 0;
            if (Autoflag == true)
            {
                while (k < path_Result.Count - 1)
                {
                    if (Math.Sqrt(Math.Pow(path_Result[k].X - path_Result[k + 1].X, 2) + Math.Pow(path_Result[k].Y - path_Result[k + 1].Y, 2)) <= D)
                    {
                        if ((path_Result[k + 1].X != NodeList[Target].Row) || (path_Result[k + 1].Y != NodeList[Target].Column))
                        {
                            path_Result.Remove(path_Result[k + 1]);
                            k--;
                        }
                        else
                        {
                            if ((path_Result[k].X == NodeList[Target].Row) || (path_Result[k].Y == NodeList[Target].Column))
                            {
                                path_Result.Remove(path_Result[k]);
                            }
                        }
                    }
                    k++;
                }
            }
            else
            {
                for (int n = 0; n < path_Result.Count - 1; n++)
                {
                    path_Result.Remove(path_Result[n]);
                    n--;
                }
            }
        }
        /// <summary>
        /// Read only, find the corners points
        /// </summary>
        private bool corner_point(Point P, Point pastP, float S, float pastS, int inf)
        {
            S = Math.Abs(S);
            pastS = Math.Abs(pastS);
            double aaa = Math.Atan(1);
            double bbb= Math.Tan(Math.PI / 4);
            if ((Math.Abs(Math.Atan(S - pastS)) >= Math.PI / 4) || ((S == 0) && (pastS != 0)) || ((S != 0) && (pastS == 0)) || ((S != inf) && (pastS == inf)))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Read only, calculate the slope
        /// </summary>
        private Single Slope(Point tempP, Point tempP_past, int inf)
        {
            Single slope;
            Single b;
            if ((tempP.X - tempP_past.X) == 0)
            {
              if (tempP.Y > tempP_past.Y)
                {
                    slope = inf;
                } else {
                    slope = -inf;
                }
            }
            else
            {
                b = tempP.X - tempP_past.X;
                slope = (Single)(tempP.Y - tempP_past.Y) / b;
            }
            return slope;
        }
        /// <summary>
        /// Read only, main function for path plan
        /// </summary>
        public bool action()
        {
            short neighbor;

            if (Start == -1 || Target == -1) return false;
            //clear all
            pathList.Clear();
            openList.Clear();
            closedList.Clear();
            ParentIndex.Clear();
            Heading.Clear();
            for (int i = 0; i < GIndex.Count; i++)
                GIndex[i] = 0;
            for (int i = 0; i < HIndex.Count; i++)
                HIndex[i] = 0;
            for (int i = 0; i < cornerIndex.Count; i++)
                cornerIndex[i] = false;
            for (int i = 0; i < NodeList.Count; i++)
            {
                ParentIndex.Add(-1);
                Heading.Add(0);
            }
            GIndex[Start] = 0;
            HIndex[Start] = 0;
            openList.Add(Start);

            bool keepSearching = true;
            bool reachTarget = false;
            short k; // the current node
            short previous_heading;
            int G;
            while (keepSearching)
            {
                k = Find_The_Smallest_F_in_OpenList();

                if (k == -1) // no node in openList
                {
                    keepSearching = false;
                    break;
                }

                if (k == Target) // reach the Target
                {
                    reachTarget = true;
                    keepSearching = false;
                    break;
                }

                previous_heading = Heading[k];
                for (int i = 0; i < 8; i = i + NeighborStep)  // 2 不允許斜行，( = 1 允許斜行) can  change by manual
                {
                    neighbor = NodeList[k].Neighbor[i];

                    if (NodeList[k].Neighbor[i] == -1) continue;  // no this Neighbor
                    else if (Walkability[NodeList[neighbor].Row, NodeList[neighbor].Column] > 0) continue;// this Neighbor is a wall
                    else if (closedList.IndexOf(NodeList[k].Neighbor[i]) != -1) continue; // this Neighbor is in closedList
                    else if (openList.IndexOf(NodeList[k].Neighbor[i]) == -1)  // this Neighbor is not in openList
                    {
                        openList.Add(NodeList[k].Neighbor[i]); // add it to openList
                        ParentIndex[NodeList[k].Neighbor[i]] = k; // set parent
                        Heading[NodeList[k].Neighbor[i]] = (short)i;
                        if (i == previous_heading)
                            GIndex[NodeList[k].Neighbor[i]] = GIndex[k] + 10; // 臨邊
                        else
                            GIndex[NodeList[k].Neighbor[i]] = GIndex[k] + 14; // 斜邊
                        HIndex[NodeList[k].Neighbor[i]] = 10 * (Math.Abs(NodeList[Target].Row - NodeList[neighbor].Row) +
                            Math.Abs(NodeList[Target].Column - NodeList[neighbor].Column));
                    }
                    else // this Neighbor is already in openList
                    {
                        if (i == previous_heading)
                            G = GIndex[k] + 10; // 臨邊
                        else
                            G = GIndex[k] + 14; // 斜邊

                        if (G < GIndex[NodeList[k].Neighbor[i]])  // 是否要 更新 這個鄰居點的路徑
                        {
                            ParentIndex[NodeList[k].Neighbor[i]] = k; // 重設 parent
                            GIndex[NodeList[k].Neighbor[i]] = G;
                        }
                    }
                }
            }
            if (reachTarget) // if find out path
            {
                pathList.Clear();
                pathList.Add(Target); // 組回路徑
                short b = ParentIndex[Target];
                while (b != -1)
                {
                    pathList.Add(b);
                    b = ParentIndex[b];
                }
                path_corner_finder();
                return true;
            }
            else
            {
                return false;
            }
        }
        /// <summary>
        /// Read only, find min cost in open list
        /// </summary>
        private short Find_The_Smallest_F_in_OpenList()
        {
            if (openList.Count == 0) return -1; // openList 是空的
            short[] openL;
            int F = Int32.MaxValue;
            int n = -1; // 有最小 F 的 node 是在 openList 的第幾個
            openL = openList.ToArray();
            for (int i = 0; i < openList.Count; i++)
            {
                if (FC(openL[i]) < F)
                {
                    F = FC(openL[i]);
                    n = i;
                }
            }
            short K = openList[n];
            openList.RemoveAt(n); // 將選到的 node 移出 openList
            closedList.Add(K);    // 將選到的 node 加入 closedList
            return K;
        }
        /// <summary>
        /// Read only, F cost
        /// </summary>
        private int FC(short openL)
        {
            return GIndex[openL] + HIndex[openL];
        }
        /// <summary>
        /// Read only, slope to angle
        /// </summary>
        private double StoA(Point target, Point start, double slope) 
        {
            double Angle = 0;
            if ((target.X - start.X >= 0) && (target.Y - start.Y < 0)) //I
            {
                if (target.X - start.X == 0)
                {
                    Angle = Math.PI / 2;
                }
                else 
                {
                     Angle = Math.Atan(slope);
                }               
            }
            else if ((target.X - start.X < 0) && (target.Y - start.Y <= 0)) //II
            {
                if (target.Y - start.Y == 0) 
                {
                    Angle = Math.PI;
                }
                else
                {
                    Angle = Math.Atan(slope) + Math.PI;
                }
            }
            else if ((target.X - start.X <= 0) && (target.Y - start.Y > 0)) //III
            {
                if (target.X - start.X == 0) 
                {
                    Angle = Math.PI * 3 / 2;
                }
                else
                {
                    Angle = Math.Atan(slope) + Math.PI;
                }
            }
            else if ((target.X - start.X > 0) && (target.Y - start.Y >= 0)) //IV
            {
                if (target.Y - start.Y == 0) 
                {
                    Angle = 2 * Math.PI;
                }
                else
                {
                    Angle = Math.Atan(slope) + (2 * Math.PI);
                }
            }
            return Angle;
        }
        /// <summary>
        /// Read only, this function will determine whether "auto path plan" or not 
        /// </summary>
        private bool AutoPlan() 
        {
            int D = (int)Math.Sqrt(Math.Pow(NodeList[Target].Row - NodeList[Start].Row, 2) + Math.Pow(NodeList[Target].Column - NodeList[Start].Column, 2));
            Point start = new Point(NodeList[Start].Row, NodeList[Start].Column);
            Point target = new Point(NodeList[Target].Row, NodeList[Target].Column);
            Single slope =  -Slope(target, start, 58);
            int unitD = 1;
            int S, C;
            while(unitD < D)
            {
                S = (int)(unitD * Math.Sin(StoA(target, start, slope)));
                C = (int)(unitD * Math.Cos(StoA(target, start, slope)));
                if (Walkability[NodeList[Start].Row + C, NodeList[Start].Column - S] > 0)
                {
                    return true;
                }
                unitD++;
            }
                path_Result.Add(target);
                return false;            
        }
        /// <summary>
        /// Show indicative point on UI 
        /// </summary>
        /*
        public void Draw(Graphics G)
        {
            Rectangle rect;
            for (short k = 0; k < NodeList.Count; k++)
            {
                rect = new Rectangle(NodeList[k].Row, NodeList[k].Column, 5, 5);
                if (NodeList[k].Index == Start)   // Start
                    G.FillRectangle(Brushes.Brown, rect);
                if (NodeList[k].Index == Target)  // Target
                    G.FillRectangle(Brushes.Yellow, rect);
                if (cornerIndex[k])  // corner
                    G.FillRectangle(Brushes.Red, rect);
            }
        }
        
        */
        // <summary>
        /// Show the path which was found in action function 
        /// </summary>
        public void Drawpath()//Graphics G)
        {
            int b = 0;
            int a;
            Point p = new Point(); ;
            path_Detail.Clear();
            while (pathList.Count != b)
            {
                a = pathList[b];
                p.X = NodeList[a].Row;
                p.Y = NodeList[a].Column;
                path_Detail.Add(p);
                b++;
            }
        }
    }
}