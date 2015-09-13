using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    public class Payment
    {
        public bool isFloat;
        public double cpnRate;
        public double cpnPeriod;

        public Payment(bool isFloat, double cpnRate, double cpnPeriod)
        {
            this.isFloat = isFloat;
            this.cpnRate = cpnRate;
            this.cpnPeriod = cpnPeriod;
        }
    }
}
