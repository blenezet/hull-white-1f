using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    //Evaluate eq 28.24: f(am)=[sum_{j=-jMax:Max} Qm,j*exp[-exp(am+j*dx)*dt]]-P_{m+1} 
    class BKZeroCpnPxFn
    {
        int m, jMax;
        double dx, dt, Pm1;
        RecombinantTree tree;

        public BKZeroCpnPxFn(int m, int jMax, double dx, double dt, double Pm1, RecombinantTree tree)
        {
            this.jMax = jMax;
            this.dx = dx;
            this.dt = dt;
            this.Pm1 = Pm1;
            this.m = m;
            this.tree = tree;
        }


        public void sqError(double[] am, ref double err, object obj)
        {

            double sum = 0.0;
            for (int j = -jMax; j <= jMax; j++)
            {
                double Qmj = tree.getNode(m, j).Q;
                sum += Qmj * Math.Exp(-Math.Exp(am[0] + j * dx) * dt);
            }
            err = (sum - Pm1) * (sum - Pm1);
        }

        public double solve()
        {
            double[] x = new double[] { 0.01 };
            double epsg = 0.0000000001;
            double epsf = 0;
            double epsx = 0;
            double diffstep = 1.0e-6;
            int maxits = 0;
            alglib.minlbfgsstate state;
            alglib.minlbfgsreport report;

            //create BFGS algo - finite difference version
            alglib.minlbfgscreatef(1, x, diffstep, out state);
            //set stopping conditions
            alglib.minlbfgssetcond(state, epsg, epsf, epsx, maxits);
            //run optimize()
            alglib.minlbfgsoptimize(state, sqError, null, null);
            //get results
            alglib.minlbfgsresults(state, out x, out report);

            return x[0];
        }


    }
}
