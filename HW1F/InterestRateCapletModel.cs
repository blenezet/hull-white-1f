using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    //European style: payment is determined only by the rates at interest rate determination dates.
    //Paid at date i for i-1 th payement.
    public class InterestRateCapletModel
    {
        OneFactorTrinomialShortRateTree tree;
        bool[] pmtArr;
        double dtCapletPmt;
        double strike;
        int nTS;
        //For a 3Y cap with 2 interest rate determination date per year, dtCapCalc=0.5, tCapMat=3
        public InterestRateCapletModel(OneFactorTrinomialShortRateTree tree, double strike, double tCapletStart, double tCapletEnd, double dtCapletCalc)
        {
            this.tree = tree;

            this.dtCapletPmt = dtCapletCalc;
            this.strike = strike;
            if (tree.nTimeStep * tree.dt < tCapletEnd)
                throw new ArgumentOutOfRangeException("Caplet end exceeds interest rate tree coverage. ");

            List<int> iCapletPmt = new List<int>();
            double t = tCapletStart;
            //while (tCapletEnd >= t)
            while (tCapletEnd > t) // exclude tCapletEnd
            {
                iCapletPmt.Add(tree.getNearestTStep(t));
                t += dtCapletCalc;
            }

            pmtArr = new bool[iCapletPmt.Last() + 1];
            foreach (int i in iCapletPmt) pmtArr[i] = true;
            nTS = pmtArr.Length;
        }


        class CalcPayoff : TraverseFunc
        {
            int nTS;
            double dtTree, dtCapletPmt, strike;
            bool[] pmtArr;

            public CalcPayoff(double strike, double dtCapletPmt, double dtTree, int nTS, bool[] pmtArr)
            {
                this.nTS = nTS;
                this.dtCapletPmt = dtCapletPmt;
                this.dtTree = dtTree;
                this.strike = strike;
                this.pmtArr = pmtArr;
            }

            public void processNode(RateNode x)
            {
                if (x.i > (nTS - 1)) return;
                
                //ccval1: discounted caplet payment
                //ccval2: discounted cap payment 
                //ccval3: floating iindex
                if (x.R > strike && pmtArr[x.i])
                {
                    //discounted for one dtCapPmt as cap payment is paid at the end of the period
                    x.ccval1 = dtCapletPmt * (x.R - strike) * Math.Exp(-dtCapletPmt * x.R);
                }
                
                double nextStepDCF = 0.0;
                if (x.i < (nTS - 1))
                {

                    nextStepDCF = (
                        x.upChild.ccval2 * x.transProb.pu +
                        x.midChild.ccval2 * x.transProb.pm +
                        x.downChild.ccval2 * x.transProb.pd) * Math.Exp(-dtTree * x.R);
                }


                x.ccval2 = x.ccval1 + nextStepDCF;

            }
        }

        public double price()
        {
            tree.resetCustomCalc();

            CalcPayoff payoff = new CalcPayoff(strike, dtCapletPmt, tree.dt, nTS, pmtArr);

            //Tranverse back the tree 
            for (int i = nTS - 1; i >= 0; i--)
            {
                tree.traverseTStep(i, payoff);
            }



            //v1: caplet payment (discounted)
            //v2: cap payment (discounted)
            return tree.getRootRateNode().ccval2;
        }


    }
}
