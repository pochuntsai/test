using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Trilateration
{
    class class_EKFL5
    {
        struct anchor
        {
            public Single X;
            public Single Y;
            public Single Z;
            public Single Range;
        }

        struct position
        {
            public Single X;
            public Single Y;
            public Single Theta;
        }

        // constant variables
        private int dim = 5;

        // private variables
        private int i, j, k;      
        private Single tmpSingle1;
        private position d_move;

        // matrix for calculation
        private Single[,] H, HT, Z, hX,Q,R,K;
        private Single[,] PoHT, HPoHTR, IHPoHTR,KdZ,KH,KHPo;
        private Single[,] P, Po;    // covariance
        private Single[,] Xk, Xke;    // state

        // required
        private anchor[] An;
        private int encoder_l, encoder_r;

        // output
        private position tag;
        private position tag_old;
        private string out_val1;

        public int EncoderL
        {
            get { return encoder_l; }
            set { encoder_l = value; }
        }

        public int EncoderR
        {
            get { return encoder_r; }
            set { encoder_r = value; }
        }

        public Single[] AX
        {
            set {
                for (int c = 0; c < value.GetLength(0); c++)
                {
                    An[c].X = value[c];
                }
            }
        }

        public Single[] AY
        {
            set
            {
                for (int c = 0; c < value.GetLength(0); c++)
                {
                    An[c].Y = value[c];
                }
            }
        }

        public Single[] AZ
        {
            set
            {
                for (int c = 0; c < value.GetLength(0); c++)
                {
                    An[c].Z = value[c];
                }
            }
        }

        public Single[] ARange
        {
            set
            {
                for (int c = 0; c < value.GetLength(0); c++)
                {
                    An[c].Range = value[c];
                }
            }
        }

        public Single tagX
        {
            get { return tag.X; }
        }

        public Single tagY
        {
            get { return tag.Y; }
        }

        public String outval
        {
            get { return out_val1; }
        }

        public class_EKFL5(Single x, Single y, Single theta)
        {
            tag.X = x;
            tag.Y = y;
            tag.Theta = theta;
            tag_old = tag;
            
            An = new anchor[dim];
            for (i = 0; i < dim; i++)
            {
                An[i].X = 0;
                An[i].Y = 0;
                An[i].Z = 0;
                An[i].Range = 0;
            }
            
            R = new Single[dim, dim];
            Q = new Single[2, 2]{{0.01f,0f},{0f,0.01f}};  // 0.01
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    if (i == j) R[i,j] = 0.3f;  // 0.3
                    else R[i,j] = 0f;
                }
            }
            Po = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            ResetVariables();

        }

        public void Calculation()
        {

            ResetVariables();
            tag_old = tag;

            // state equation
            StateEquation(out d_move.X, out d_move.Y, out d_move.Theta, encoder_l, encoder_r);
            Xke[0, 0] = tag_old.X + d_move.X;
            Xke[1, 0] = tag_old.Y + d_move.Y;
            Xke[2, 0] = tag_old.Theta + d_move.Theta;
            
            // calculate measurement Z, estimate h(X) and its Jocobian H
            for (i = 0; i < dim; i++)
            {
                Z[i,0] = (Single)Math.Sqrt(Math.Pow(An[i].Range, 2) - Math.Pow(An[i].Z, 2));        // ranging value project to 2D plane
                if (An[i].Range > 0)
                {
                    hX[i, 0] = (Single)Math.Sqrt(Math.Pow((tag_old.X - An[i].X), 2) + Math.Pow((tag_old.Y - An[i].Y), 2));    // distance between previous tag and the anchor
                    H[i, 0] = (tag_old.X - An[i].X) / hX[i, 0];
                    H[i, 1] = (tag_old.Y - An[i].Y) / hX[i, 0];
                }
                else
                {
                    hX[i, 0] = 0;
                    H[i, 0] = 0;
                    H[i, 1] = 0;
                }
            }

            // transpose Jacobian matrix
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    HT[j,i] = H[i,j];
                }
            }

            // calculate Kalman gain K
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    PoHT[i,j] = 0;
                    for (k = 0; k < 2; k++)
                    {
                        PoHT[i,j] = PoHT[i,j] + (Po[i,k] * HT[k,j]);

                    }
                }
            }
            for (i = 0; i < dim; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    HPoHTR[i,j] = 0;
                    for (k = 0; k < 2; k++)
                    {
                        HPoHTR[i,j] = HPoHTR[i,j] + (H[i,k] * PoHT[k,j]);
                    }
                    HPoHTR[i, j] = HPoHTR[i, j] + R[i, j];
                }
            }
            // calculate the inverse matrix
            Inverse(out IHPoHTR, HPoHTR, dim);

            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < dim; j++)
                {
                    K[i,j] = 0;
                    for (k = 0; k < dim; k++)
                    {
                        K[i, j] = K[i, j] + (PoHT[i, k] * IHPoHTR[k, j]);
                    }
                }
            }

            // Update state with measurement and estimate
            for (i = 0; i < 2; i++)
            {
                KdZ[i,0] = 0;
                for (j = 0; j < dim; j++)
                {
                    KdZ[i, 0] = KdZ[i, 0] + (K[i, j] * (Z[j, 0] - hX[j, 0]));
                }
            }
            for (i = 0; i < 2; i++)
            {
                Xk[i,0] = Xke[i,0] + KdZ[i,0];
            }
            Xk[2,0] = Xke[2,0];

            tag.X = Xk[0, 0];
            tag.Y = Xk[1, 0];
            tag.Theta = Xk[2, 0];

            // update Error Covariance
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    for (k = 0; k < dim; k++)
                    {
                        KH[i,j] = KH[i,j] + (K[i,k] * H[k,j]);

                    }
                }
            }
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    KHPo[i, j] = KHPo[i,j] + (KH[i,j] * Po[i,j]);
                }
            }
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    P[i,j] = Po[i,j] - KHPo[i,j];
                }
            }
            for (i = 0; i < 2; i++)
            {
                for (j = 0; j < 2; j++)
                {
                    Po[i,j] = P[i,j] + Q[i,j];
                }
            }

            out_val1 = "";
            //for (i = 0; i < dim; i++)
            //{
            //    out_val1 = out_val1 + K[0, i].ToString("f3") + ",";
            //}
            //for (i = 0; i < dim; i++)
            //{
            //    out_val1 = out_val1 + K[1, i].ToString("f3") + ",";
            //}
        }

        private void Inverse(out Single[,] Ans, Single[,] origin, int column)
        {
            int i, j, k, l;
            Single tmpSingle1, tmpSingle2;

            Single[,] whole = new Single[column, column * 2];
            Single[,] Iinv = new Single[column, column];

            Ans = new Single[column, column];

            for (i = 0; i < column; i++)
            {
                for (j = 0; j < column; j++)
                {
                    if (i == j) Iinv[i,j] = 1;
                    else Iinv[i,j] = 0;
                }
            }
            for (i = 0; i < column; i++)
            {
                for (j = 0; j < column; j++)
                {
                    whole[i,j] = origin[i,j];
                    whole[i, j + column] = Iinv[i, j];
                }
            }
            for (i = 0; i < column; i++)
            {
                tmpSingle1 = whole[i,i];
                for (j = 0; j < (2 * column); j++)
                {
                    whole[i,j] = whole[i,j] / tmpSingle1;
                }
                for (k = 0; k < column; k++)
                {
                    if (k != i)
                    {
                        tmpSingle2 = whole[k,i];
                        for (l = 0; l < (2 * column); l++)
                        {
                            whole[k,l] = -(tmpSingle2 * whole[i,l]) + whole[k,l];
                        }
                    }
                }
            }

            for (i = 0; i < column; i++)
            {
                for (j = column; j < (2 * column); j++)
                {
                    Ans[i, j - column] = whole[i, j];    //get the answer matrix
                }
            }
        }

        private void StateEquation(out Single dx, out Single dy, out Single dtheta, int left, int right)
        {
            Single DegToRad =(Single)(Math.PI/180);
            Single RadToDeg =(Single)(180/Math.PI);
            Single D = 11.83f;
            Single piD = (Single)((D * Math.PI) / 60);
            Single dt = 1f;
            Single VL, VR;
            Single V, W;

            VL = (((left / 6) * piD) / dt);
            VR = (((right / 6) * piD) / dt);
            V = (VL + VR) / 2f;
            W = (VR - VL) / 22.26f;

            dtheta = W * dt * RadToDeg;
            dx = (Single)((V * dt) * Math.Cos(dtheta * DegToRad));
            dy = (Single)((V * dt) * Math.Sin(dtheta * DegToRad));
        }

        private void ResetVariables()
        {
            H = new Single[dim, 2];     // Jacobian matrix
            HT = new Single[2, dim];    // Transpose Jacobina matrix
            Z = new Single[dim, 1];     // measurement value matrix
            hX = new Single[dim, 1];     // estimate value matrix
            PoHT = new Single[2, dim];
            HPoHTR = new Single[dim, dim];
            IHPoHTR = new Single[dim, dim];
            K = new Single[2, dim];     // Kalman gain
            KdZ = new Single[2, 1];
            Xk = new Single[3, 1];      // state
            Xke = new Single[3, 1];     // estimate state
            KH = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            KHPo = new Single[2, 2] { { 1, 0 }, { 0, 1 } };
            P = new Single[2, 2];       // covariance matrix
        }
    }
}
