
/*
Copyright (c) 2014, D.Why <OnRiskAndReturn@gmail.com>
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer. 
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies, 
either expressed or implied, of the FreeBSD Project.
*/

//Author: D.Why <OnRiskAndReturn@gmail.com>
//Developed and tested in VS Express 2013 for Desktop (C#)
//Dependence: ALGLIB C# numeric library (for optimisation code) http://www.alglib.net/download.php


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.IO;

namespace OneFactorInterestRateTree
{
    
    // convention: time in year.  Rate is continuous.  For 2Y time horizon with dt =0.5Y, nTStep = 5
    public class OneFactorTrinomialShortRateTree
    {
        public enum ModelType
        {
            HULL_WHITE, BLACK_KARASINSKI
        }

        ModelType model;
        double param_a, param_s , dR;
        public double dtUnit;
        public List<Double> zcPrice, alpha;
        RecombinantTree tree;
        int nTStep;

        //Hull, Options, Futures and Other Derivatives 6th Ed.  Ch28.7
        //Fixed time step, time-invariant volatility
        public OneFactorTrinomialShortRateTree(ModelType model, double param_a, double param_s, double dt, double tEnd, List<Tuple<double, double>> zrInput)
        {
            this.model = model;
            this.dtUnit = dt;
            this.param_a = param_a;
            this.param_s = param_s;
            zcPrice = new List<Double>();
            alpha = new List<Double>();

            interplZCPrice(dt, tEnd, zrInput);
            nTStep = zcPrice.Count;

        }

        private void interplZCPrice(double dt, double tEnd, List<Tuple<double, double>> zrInput)
        {
            if (zrInput.Count < 3)
                throw new ArgumentException("Need at least 3 sets of zero rate input ");
            for (int i = 1; i < zrInput.Count; i++)
            {
                if (zrInput[i].Item1 <= zrInput[i - 1].Item1)
                    throw new ArgumentException("The tenor of the zero rate input needs to be montonically increasing");
            }
            int nTS = (zrInput[0].Item1 == 0.0) ? zrInput.Count + 1 : zrInput.Count;
            double[] tArr = new double[nTS];
            double[] zrArr = new double[nTS];
            int count = 0;
            if (zrInput[0].Item1 == 0.0)
            {
                tArr[count] = 0.0;
                zrArr[count] = zrInput[0].Item2;
                count++;
            }
            for (int i = 0; i < zrInput.Count; i++)
            {
                tArr[count] = zrInput[i].Item1;
                zrArr[count] = zrInput[i].Item2;
                count++;
            }
            double tMax = tArr[nTS - 1];
            double zrAtTMax = zrArr[nTS - 1];

            alglib.spline1dinterpolant zrGen;
            if (zrInput.Count < 5)
                alglib.spline1dbuildlinear(tArr, zrArr, out zrGen);
            else
                alglib.spline1dbuildakima(tArr, zrArr, out zrGen);


            zcPrice.Add(1.0);
            double t = dt;
            while (t <= tEnd)
            {
                double zr = (t > tMax) ? zrAtTMax : alglib.spline1dcalc(zrGen, t);
                zcPrice.Add(Math.Exp(-zr * t));
                t += dt;
            }
        }




        public double dt
        {
            get { return dtUnit; }
            //set { dtUnit = value; }
        }

        public int nTimeStep
        {
            get { return nTStep; }
        }

        public int getNearestTStep(double t)
        {
            return (int)Math.Round(t / dt, 0);

        }

        public int jMax(int ts)
        {
            return tree.jMax(ts);
        }

        public RateNode getRateNode(int i, int j)
        {
            return tree.getNode(i, j);
        }


        public RateNode getRootRateNode()
        {
            return tree.getNode(0, 0);
        }

        private BranchProb midFork(double j, double dt)
        {
            BranchProb b = new BranchProb();
            double ajdt = param_a * j * dt;
            b.pu = 1.0 / 6.0 + 0.5 * (ajdt * ajdt - ajdt);
            b.pm = 2.0 / 3.0 - ajdt * ajdt;
            b.pd = 1.0 / 6.0 + 0.5 * (ajdt * ajdt + ajdt);
            return b;
        }

        private BranchProb upFork(double j, double dt)
        {
            BranchProb b = new BranchProb();
            double ajdt = param_a * j * dt;
            b.pu = 1.0 / 6.0 + 0.5 * (ajdt * ajdt + ajdt);
            b.pm = -1.0 / 3.0 - ajdt * ajdt - 2 * ajdt;
            b.pd = 7.0 / 6.0 + 0.5 * (ajdt * ajdt + 3 * ajdt);
            return b;
        }

        private BranchProb downFork(double j, double dt)
        {
            BranchProb b = new BranchProb();
            double ajdt = param_a * j * dt;
            b.pu = 7.0 / 6.0 + 0.5 * (ajdt * ajdt - 3.0 * ajdt);
            b.pm = -1.0 / 3.0 - ajdt * ajdt + 2 * ajdt;
            b.pd = 1.0 / 6.0 + 0.5 * (ajdt * ajdt - ajdt);
            return b;
        }

        private int maxRStep(double dt)
        {
            const double jMaxThres = 0.184;
            // smallest integer greate than 0.84 / (a*dt)
            return (int)Math.Ceiling(jMaxThres / param_a / dt);
        }

        class FillProb : TraverseFunc
        {
            OneFactorTrinomialShortRateTree tree_;
            public FillProb(OneFactorTrinomialShortRateTree tree)
            {
                tree_ = tree;
            }

            public void processNode(RateNode x)
            {
                if (x.forktype == ForkType.DOWNFORK)
                    x.transProb = tree_.downFork(x.j, tree_.dt);
                else if (x.forktype == ForkType.UPFORK)
                    x.transProb = tree_.upFork(x.j, tree_.dt);
                else if (x.forktype == ForkType.MIDFORK)
                    x.transProb = tree_.midFork(x.j, tree_.dt);
            }

        }

        class ShowNode : TraverseFunc
        {

            public void processNode(RateNode x)
            {
                System.Console.WriteLine(x.ToString());
            }

        }

        class CleanCustomCalcNode : TraverseFunc
        {
            public void processNode(RateNode x)
            {
                x.ccval1 = 0.0;
                x.ccval2 = 0.0;
                x.ccval3 = 0.0;
                x.ccflag1 = false;
            }
        }


        public void buildTree()
        {
            buildRStarTree();
            RStarToRTree();
        }

        public void resetCustomCalc()
        {
            tree.traverseAll(new CleanCustomCalcNode());
        }



        public void showAll()
        {
            tree.traverseAll(new ShowNode());
        }

        public void traverseAll(TraverseFunc fn)
        {
            tree.traverseAll(fn);
        }

        public void traverseTStep(int ts, TraverseFunc fn)
        {
            tree.traverseTStep(ts, fn);
        }

        //tZR: time, zr
        //assume tZR = ((1, 0.3%), (2, 0.7%), (4, 1.5%))
        private void buildRStarTree()
        {
            tree = new RecombinantTree();
            dR = param_s * Math.Sqrt(3 * dtUnit);
            //tree building
            int rMax = maxRStep(dtUnit);

            for (int i = 0; i < nTStep - 1; i++)
            {
                tree.addNextLevel(rMax);
            }


            //transvering the whole tree and fill the branching probability for each type of node
            FillProb fillProbTree = new FillProb(this);
            tree.traverseAll(fillProbTree);


        }


        private void RStarToRTree()
        {
            tree.getNode(0, 0).Q = 1.0;

            double dRdt = dR * dtUnit;

            for (int m = 0; m < tree.nTimeStep() - 1; m++)
            {
                int jMax = tree.jMax(m);

                double alpha_m = Double.NaN;
                if (model == ModelType.HULL_WHITE)
                {
                    double qSum = 0.0;
                    for (int j = -jMax; j <= jMax; j++)
                    {
                        qSum += tree.getNode(m, j).Q * Math.Exp(-j * dRdt);
                    }


                    double lnPm1 = Math.Log(zcPrice[m + 1]);
                    alpha_m = (Math.Log(qSum) - lnPm1) / dtUnit;
                }
                else if (model == ModelType.BLACK_KARASINSKI)
                {
                    double Pm1 = zcPrice[m + 1];
                    BKZeroCpnPxFn bkzc = new BKZeroCpnPxFn(m, jMax, dR, dt, Pm1, tree);
                    alpha_m = bkzc.solve();

                }


                alpha.Add(alpha_m);


                for (int k = -jMax; k <= jMax; k++)
                {
                    RateNode currNode = tree.getNode(m, k);
                    double QExpTerm = Double.NaN;
                    if (model == ModelType.HULL_WHITE)
                    {
                        QExpTerm = currNode.Q * Math.Exp(-dtUnit * (k * dR + alpha[m]));
                    }
                    else if (model == ModelType.BLACK_KARASINSKI)
                    {
                        QExpTerm = currNode.Q * Math.Exp(-dt * Math.Exp((k * dR + alpha[m])));
                    }


                    int ju, jd, jm;

                    if (currNode.forktype == ForkType.MIDFORK)
                    {
                        ju = k + 1;
                        jm = k;
                        jd = k - 1;
                    }
                    else if (currNode.forktype == ForkType.UPFORK)
                    {
                        ju = k + 2;
                        jm = k + 1;
                        jd = k;
                    }
                    else if (currNode.forktype == ForkType.DOWNFORK)
                    {
                        ju = k;
                        jm = k - 1;
                        jd = k - 2;
                    }
                    else
                    {
                        throw new InvalidOperationException("Should not reach here");
                    }
                    tree.getNode(m + 1, ju).Q += currNode.transProb.pu * QExpTerm;
                    tree.getNode(m + 1, jm).Q += currNode.transProb.pm * QExpTerm;
                    tree.getNode(m + 1, jd).Q += currNode.transProb.pd * QExpTerm;
                    if (model == ModelType.HULL_WHITE)
                    {
                        tree.getNode(m, k).R = alpha_m + k * dR;
                    }
                    else if (model == ModelType.BLACK_KARASINSKI)
                    {
                        tree.getNode(m, k).R = Math.Exp(alpha_m + k * dR);
                    }
                }

            }

        }

    }

}
