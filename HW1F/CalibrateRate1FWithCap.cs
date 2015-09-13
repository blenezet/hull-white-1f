using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    public class CalibrateRate1FWithCap
    {
        double dtTree, tEndTree, dtCap, tStartCap;
        List<Tuple<double, double>> zrInput;
        List<Tuple<double, double, double>> capInput;
        double paramA, paramS;
        OneFactorTrinomialShortRateTree.ModelType rateModel;
        string name;

        //zrInput: tenor, zero rate
        //capInput: tenor, strike, premium
        public CalibrateRate1FWithCap(OneFactorTrinomialShortRateTree.ModelType rateModel, string name, double dtTree, double tEndTree, List<Tuple<double, double>> zrInput, double dtCap, double tStartCap, List<Tuple<double, double, double>> capInput)
        {
            this.name = name;
            this.dtTree = dtTree;
            this.tEndTree = tEndTree;
            this.zrInput = zrInput;
            this.dtCap = dtCap;
            this.tStartCap = tStartCap;
            this.capInput = capInput;
            this.rateModel = rateModel;
        }


        public void sqError(double[] x, ref double error, object obj)
        {
            double param_a = x[0];
            double param_s = x[1];

            //function trimming: 
            //both are positive
            if (param_a <= 0.0001 || param_s < 0.0001)
            {
                error = 1.0E+300;
                return;
            }

            OneFactorTrinomialShortRateTree tree = new OneFactorTrinomialShortRateTree(rateModel, param_a, param_s, dtTree, tEndTree, zrInput);
            tree.buildTree();


            double sqErr = 0.0;
            //capInput: tenor, strike, premium
            foreach (Tuple<double, double, double> c in capInput)
            {
                InterestRateCapModel cap = new InterestRateCapModel(tree, c.Item2, tStartCap, dtCap, c.Item1);
                double capPx = cap.price(); ;
                sqErr += Math.Pow(capPx - c.Item3, 2.0);
            }
            error = sqErr;
        }

        public OneFactorTrinomialShortRateTree calibrate()
        {
            // param_a0, param_s0 
            double[] x = new double[] { 0.1, 0.1 };
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

            paramA = x[0];
            paramS = x[1];

            OneFactorTrinomialShortRateTree tree = new OneFactorTrinomialShortRateTree(rateModel, paramA, paramS, dtTree, tEndTree, zrInput);
            tree.buildTree();

            Console.WriteLine("Calibration with Cap completed ...");

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
