using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    class CalibrateRate1FWithCapletAFixed
    {
        double dtTree, tEndTree, dtCap;
        List<Tuple<double, double>> zrInput;
        List<Tuple<double, bool, double, double>> capletInput;
        double paramA, paramS;
        OneFactorTrinomialShortRateTree.ModelType rateModel;
        string name;

        //zrInput: tenor, zero rate
        //capInput: tenor, strike, premium
        public CalibrateRate1FWithCapletAFixed(OneFactorTrinomialShortRateTree.ModelType rateModel, string name, double dtTree, double tEndTree, List<Tuple<double, double>> zrInput, List<Tuple<double, bool, double, double>> capletInput, double dtCap, double aInput)
        {
            this.name = name;
            this.dtTree = dtTree;
            this.tEndTree = tEndTree;
            this.zrInput = zrInput;
            this.capletInput = capletInput;
            this.dtCap = dtCap;
            this.rateModel = rateModel;
            this.paramA = aInput;
        }


        public void sqError(double[] x, ref double error, object obj)
        {
            double param_s = x[0];
            //double param_s = x[1];

            //function trimming: 
            //both are positive
            if (param_s < 0.0001)
            {
                error = 1.0E+300;
                return;
            }

            OneFactorTrinomialShortRateTree tree = new OneFactorTrinomialShortRateTree(rateModel, paramA, param_s, dtTree, tEndTree, zrInput);
            tree.buildTree();


            double sqErr = 0.0;
            //capInput: expiry, strike, premium
            foreach (Tuple<double, bool, double, double> c in capletInput)
            {
                double P_t_T = tree.zcPrice[(int)(c.Item1 / tree.dtUnit)];
                double P_t_S = tree.zcPrice[(int)((c.Item1 + dtCap) / tree.dtUnit)];
                double F_t_T_S = (P_t_T / P_t_S) - 1d;
                
                double K = c.Item2 == true ? F_t_T_S : c.Item3;
                InterestRateCapletModel caplet = new InterestRateCapletModel(tree, K, c.Item1, c.Item1 + dtCap, dtCap);
                double capletPx = caplet.price(); ;
                sqErr += Math.Pow(capletPx - K, 2.0);
            }
            error = sqErr;
        }

        public OneFactorTrinomialShortRateTree calibrate()
        {
            // param_a0, param_s0 
            double[] x = new double[] { 0.1 } ;
            double epsg = 0.000001;
            double epsf = 0.001;
            double epsx = 0;
            double diffstep = 1.0e-4;

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

            //paramA = x[0];
            paramS = x[0];

            OneFactorTrinomialShortRateTree tree = new OneFactorTrinomialShortRateTree(rateModel, paramA, paramS, dtTree, tEndTree, zrInput);
            tree.buildTree();

            Console.WriteLine("Calibration with Caplet completed (A fixed) ...");

            return tree;

        }


        public double param_a
        {
            get { return paramA; }
        }

        public double param_s
        {
            get { return paramS; }
        }

    }
}
