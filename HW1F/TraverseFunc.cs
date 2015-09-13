using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    public interface TraverseFunc
    {
        void processNode(RateNode x);
    }
}
