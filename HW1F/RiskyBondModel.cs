using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    //fixed or float or variable; bullet or callable; fixed maturity or perpetual
    //for floating rate note, only approximate Libor based index for 1 factor interest rate model
    public class RiskyBondModel
    {

        OneFactorTrinomialShortRateTree tree;
        Dictionary<int, Payment> pmtInTS;
        Dictionary<int, double> callSchInTS;

        bool isPerpetual;
        Payment perpPayment;
        int matInTS;

        public RiskyBondModel(OneFactorTrinomialShortRateTree tree)
        {
            this.tree = tree;
        }

        public void setBond(int matInTS, Dictionary<int, Payment> pmtInTS, Dictionary<int, double> callSchInTS)
        {
            this.callSchInTS = callSchInTS;
            this.matInTS = matInTS;
            this.pmtInTS = pmtInTS;
            this.isPerpetual = false;
            this.perpPayment = null;

        }


        public void setPerpetualBond(Payment perpPayment, Dictionary<int, Payment> pmtInTS, Dictionary<int, double> callSchInTS)
        {
            this.callSchInTS = callSchInTS;
            this.matInTS = int.MaxValue;
            this.pmtInTS = pmtInTS;
            this.isPerpetual = true;
            this.perpPayment = perpPayment;

        }


        //Payment has been setup previously.
        //Traverse backward
        class CalcPayoff : TraverseFunc
        {
            Dictionary<int, Payment> pmtInTS;
            Dictionary<int, double> callSchInTS; //callPrice
            bool isPerpetual;
            Payment perpPayment;
            double oas, dtTree, dr;
            int nTS;
            OneFactorTrinomialShortRateTree tree;

            public CalcPayoff(double dr, double oas, double dtTree, int nTS, bool isPerpetual, Payment perpPayment, Dictionary<int, Payment> pmtInTS, Dictionary<int, double> callSchInTS, OneFactorTrinomialShortRateTree tree)
            {
                this.dr = dr;
                this.oas = oas;
                this.isPerpetual = isPerpetual;
                this.perpPayment = perpPayment;
                this.pmtInTS = pmtInTS;
                this.callSchInTS = callSchInTS;
                this.dtTree = dtTree;
                this.nTS = nTS;
                this.tree = tree;
            }

            //Node i,j in the interest rate tree is connected to a number of parents at i-n
            //The look back here is only an approximation.  It takes the most probable node 
            //ie Node(i-n,j) or if not possible, Node(i-n, max(j)) if j is +ve, Node(i-n, min(j)) if j is -ve
            private double ratesLookback(int nPrevLookback, int treeLvlI, int treeLvlJ)
            {
                int lvl_i = treeLvlI - nPrevLookback;
                if (lvl_i < 0) lvl_i = 0;
                if (lvl_i == (tree.nTimeStep - 1)) lvl_i--;

                int jMaxAtI = tree.jMax(lvl_i);
                int lvl_j = treeLvlJ;
                if (Math.Abs(lvl_j) > jMaxAtI)
                    lvl_j = Math.Sign(lvl_j) * jMaxAtI;

                return tree.getRateNode(lvl_i, lvl_j).R;
            }

            public void processNode(RateNode x)
            {
                if (x.i > (nTS - 1)) return;


                //ccval1: curr cf
                //ccval2: sum(dcf)                
                //ccflag1: hasCalled

                //Calc current cash payment if this is a payment period
                if (pmtInTS.ContainsKey(x.i))
                {
                    Payment pmt = pmtInTS[x.i];
                    bool justMatured = x.i == (nTS - 1) && !isPerpetual;

                    double cpnRate = pmt.cpnRate;
                    if (pmt.isFloat)
                    {
                        //the convention for FRN is to take the 
                        int periodLookback = (int)Math.Round(pmt.cpnPeriod / dtTree, 0);
                        double floatIndex = ratesLookback(periodLookback, x.i, x.j) + dr;
                        cpnRate += floatIndex;
                    }
                    x.ccval1 = (justMatured ? 1.0 : 0.0) + pmt.cpnPeriod * cpnRate;

                }

                //discounted cash flow
                double onePeriodDF = Math.Exp(-dtTree * (x.R + oas + dr));
                double nextStepDCF = 0.0;
                if (x.i == (nTS - 1) && isPerpetual)
                {
                    double lastR = (double.IsNaN(x.R) ? ratesLookback(1, x.i, x.j) : x.R) + dr;
                    double cpnPerp = perpPayment.isFloat ? lastR + perpPayment.cpnRate : perpPayment.cpnRate;
                    double perpDCR = lastR + oas + dr;
                    double r = 1.0 / (1.0 + perpDCR);
                    nextStepDCF = cpnPerp * r / (1 - r);
                }
                else if (x.i == (nTS - 1) && !isPerpetual)
                {
                    nextStepDCF = 0.0;
                }
                else
                {
                    nextStepDCF = onePeriodDF *
                    (x.upChild.ccval2 * x.transProb.pu +
                    x.midChild.ccval2 * x.transProb.pm +
                    x.downChild.ccval2 * x.transProb.pd);

                }
                x.ccval2 = x.ccval1 + nextStepDCF;

                //Check if that's worthwhile to call if this is a call date
                if (callSchInTS.ContainsKey(x.i))
                {
                    double callPx = callSchInTS[x.i];
                    if (callPx < nextStepDCF)
                    {
                        x.ccval2 = callPx + x.ccval1;
                        x.ccflag1 = true; //isCalled
                    }
                }


            }

        }

        public double internalPrice(double dr, double oas)
        {
            tree.resetCustomCalc();

            int nTS;
            if (isPerpetual) nTS = tree.nTimeStep;
            else nTS = matInTS + 1;
            if (nTS > tree.nTimeStep)
                throw new ArgumentException("Bond maturity is outside the coverage of the interest rate tree");

            //Tranverse back the tree             
            CalcPayoff payoff = new CalcPayoff(dr, oas, tree.dt, nTS, isPerpetual, perpPayment, pmtInTS, callSchInTS, tree);
            for (int i = nTS - 1; i >= 0; i--)
            {
                tree.traverseTStep(i, payoff);
            }
            //ccval2: sum(dcf)
            return tree.getRootRateNode().ccval2;
        }

        const double oasMin = 0.0, oasMax = 1.0;
        const double drMin = -0.1, drMax = 0.1;
        const int nPt = 201;
        const double oasDynRange = 2.0;

        bool isCalc = false;
        Tuple<double, double> pxToOasRng, oasToPxRng;
        alglib.spline1dinterpolant pxToOas, oasToPx;
        public void buildSensCurve()
        {
            double[] oasArr = new double[nPt];
            double[] pxOasArr = new double[nPt];

            double oasScale = Math.Pow(10.0, oasDynRange);
            for (int i = 0; i < nPt; i++)
            {
                oasArr[i] = (Math.Pow(10.0, i * oasDynRange / (nPt - 1.0)) - 1.0) / oasScale;
            }

            for (int i = 0; i < nPt; i++)
            {
                pxOasArr[i] = internalPrice(0.0, oasArr[i]);
            }
            alglib.spline1dbuildakima(oasArr, pxOasArr, out oasToPx);
            alglib.spline1dbuildakima(pxOasArr, oasArr, out pxToOas);

            pxToOasRng = new Tuple<double, double>(pxOasArr[nPt - 1], pxOasArr[0]);
            oasToPxRng = new Tuple<double, double>(oasArr[0], oasArr[nPt - 1]);

            isCalc = true;
        }

        double interpolate(double x, Tuple<double, double> rng, alglib.spline1dinterpolant intpl)
        {
            if (x > rng.Item2 || x < rng.Item1) return double.NaN;
            return alglib.spline1dcalc(intpl, x);
        }

        public double price(double oas)
        {
            if (!isCalc) buildSensCurve();
            return interpolate(oas, oasToPxRng, oasToPx);
        }

        public double oas(double px)
        {
            if (!isCalc) buildSensCurve();
            return interpolate(px, pxToOasRng, pxToOas);
        }

        public double RateRisk(double dr, double oas)
        {
            const double OneBp = 0.0001;
            return (internalPrice(dr - OneBp, oas) - internalPrice(dr + OneBp, oas)) / 2.0 / OneBp;

        }

        public double RateDur(double dr, double oas)
        {
            return RateRisk(dr, oas) / interpolate(oas, oasToPxRng, oasToPx);
        }

        public double RateConvexity(double dr, double oas)
        {

            const double OneBp = 0.0001;
            double vp = internalPrice(dr + OneBp, oas);
            double vm = internalPrice(dr - OneBp, oas);
            double v0 = internalPrice(dr, oas);

            return (vm + vp - 2.0 * v0) / (2.0 * v0 * OneBp);
        }

        public double SpreadRisk(double oas)
        {
            double x = oas;
            Tuple<double, double> rng = oasToPxRng;
            alglib.spline1dinterpolant interpolator = oasToPx;

            if (x > rng.Item2 || x < rng.Item1) return double.NaN;
            if (!isCalc) buildSensCurve();
            const double OneBp = 0.0001;
            if (x - rng.Item1 < OneBp) x = rng.Item1 + OneBp;
            if (rng.Item2 - x < OneBp) x = rng.Item2 - OneBp;
            return (interpolate(x - OneBp, rng, interpolator) - interpolate(x + OneBp, rng, interpolator)) / 2.0 / OneBp;
        }

        public double SpreadDur(double oas)
        {
            return SpreadRisk(oas) / interpolate(oas, oasToPxRng, oasToPx);
        }

        public double SpreadConvexity(double oas)
        {

            double x = oas;
            Tuple<double, double> rng = oasToPxRng;
            alglib.spline1dinterpolant interpolator = oasToPx;

            if (x > rng.Item2 || x < rng.Item1) return double.NaN;
            if (!isCalc) buildSensCurve();
            const double OneBp = 0.0001;
            if (x - rng.Item1 < OneBp) x = rng.Item1 + OneBp;
            if (rng.Item2 - x < OneBp) x = rng.Item2 - OneBp;

            double vp = interpolate(x + OneBp, rng, interpolator);
            double vm = interpolate(x - OneBp, rng, interpolator);
            double v0 = interpolate(x, rng, interpolator);

            return (vm + vp - 2.0 * v0) / (2.0 * v0 * OneBp);
        }



    }
}
