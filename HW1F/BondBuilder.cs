using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    public class BondBuilder
    {
        const double nDayPerYr = 365.0;

        OneFactorTrinomialShortRateTree tree;
        DateTime evalDate, matDate;

        bool? isPerpetual = null;
        double perpCpnRate, perpCpnPeriod;
        bool isPerpFlaot;
        Dictionary<int, Payment> pmtInTS = new Dictionary<int, Payment>();
        Dictionary<int, double> callSchInTS = new Dictionary<int, double>();  //call price

        public BondBuilder(OneFactorTrinomialShortRateTree tree, DateTime evalDate)
        {
            this.tree = tree;
            this.evalDate = evalDate;
        }

        public void clear()
        {
            isPerpetual = null;
            pmtInTS = new Dictionary<int, Payment>();
            callSchInTS = new Dictionary<int, double>();
        }

        public RiskyBondModel createBond()
        {
            RiskyBondModel bond = new RiskyBondModel(tree);
            if (isPerpetual == null) throw new ArgumentException("Maturity Date has not been set");

            if (isPerpetual.Value)
            {
                bond.setPerpetualBond(new Payment(isPerpFlaot, perpCpnRate, perpCpnPeriod), pmtInTS, callSchInTS);
            }
            else
            {
                int matInTS = tree.getNearestTStep(matDate.Subtract(evalDate).TotalDays / nDayPerYr);
                bond.setBond(matInTS, pmtInTS, callSchInTS);
            }
            return bond;
        }

        public BondBuilder maturityDate(DateTime matDate)
        {
            isPerpetual = false;
            this.matDate = matDate;
            return this;
        }

        public BondBuilder perpetualBond(bool isFloat, double cpnRate, double cpnPeriod)
        {
            isPerpetual = true;
            this.isPerpFlaot = isFloat;
            this.perpCpnPeriod = cpnPeriod;
            this.perpCpnRate = cpnRate;
            this.matDate = new DateTime();
            return this;
        }

        //assume endDt is a interest payment date and counting back
        public BondBuilder fixedCoupon(double cpnRate, double cpnPeriod, DateTime startDt, DateTime endDt)
        {
            addCoupon(false, cpnRate, cpnPeriod, startDt, endDt);
            return this;
        }

        //assume endDt is a interest payment date and counting back
        public BondBuilder floatCoupon(double cpnRate, double cpnPeriod, DateTime startDt, DateTime endDt)
        {
            addCoupon(true, cpnRate, cpnPeriod, startDt, endDt);
            return this;
        }


        private void addCoupon(bool isFloat, double cpnRate, double cpnPeriod, DateTime startDt, DateTime endDt)
        {
            const int dayTol = 1;
            Payment fullPmt = new Payment(isFloat, cpnRate, cpnPeriod);


            DateTime t = endDt;
            while (t.Subtract(startDt).TotalDays > cpnPeriod * nDayPerYr)
            {
                int ts = tree.getNearestTStep(t.Subtract(evalDate).TotalDays / nDayPerYr);
                if (!pmtInTS.ContainsKey(ts)) pmtInTS[ts] = fullPmt;
                t = t.AddDays(-(int)cpnPeriod * nDayPerYr);
            }

            //Account for possible short first coupon period
            if (t.Subtract(startDt).TotalDays > dayTol)
            {
                int ts = tree.getNearestTStep(t.Subtract(evalDate).TotalDays / nDayPerYr);
                if (!pmtInTS.ContainsKey(ts)) pmtInTS[ts] = new Payment(isFloat, cpnRate, t.Subtract(startDt).TotalDays / nDayPerYr);
            }
        }



        public BondBuilder callSchedule(DateTime callDate, double callPrice)
        {
            int ts = tree.getNearestTStep(callDate.Subtract(evalDate).TotalDays / nDayPerYr);
            if (!callSchInTS.ContainsKey(ts)) callSchInTS[ts] = callPrice;
            return this;
        }

    }
}
