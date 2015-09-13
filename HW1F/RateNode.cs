using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    public class RateNode
    {
        public BranchProb transProb;
        public RateNode upChild;
        public RateNode midChild;
        public RateNode downChild;
        public ForkType forktype;
        public int i;
        public int j;
        public double R, Q;

        //custom calc value
        public bool ccflag1;
        public double ccval1, ccval2, ccval3;

        public override String ToString()
        {
            //Show node type and Q value.  Useful for debugging interest rate tree model
            //String probStr = transProb == null ? "N/A" : String.Format("{0,6:f4},{1,6:f4},{2,6:f4}", transProb.pu, transProb.pm, transProb.pd);
            //String rgStr = String.Format("  R: {0,6:f4}   Q: {1,6:f4} ", R, Q);
            //String valStr = String.Format("  CustomVal: {0,6:f4},{1,6:f4},{2,6:f4}", val1, val2, val3);
            //return String.Format("Node {0,3:d},{1,3:d}: ", i, j) + forktype + rgStr + " TransProb: " + probStr + valStr;

            //Show custom calc value ccval1,2,3.  Useful for develop downstream model.
            String probStr = transProb == null ? "N/A" : String.Format("{0,6:f4},{1,6:f4},{2,6:f4}", transProb.pu, transProb.pm, transProb.pd);
            String rccvalStr = String.Format(" R:{0,6:f4}, CCVal: {1,6:f4},{2,6:f4},{3,6:f4},{4,1:s}", R, ccval1, ccval2, ccval3, ccflag1 ? "T" : "F");
            return String.Format("Node {0,3:d},{1,3:d}: ", i, j) + rccvalStr + " Pr:" + probStr;

        }
    }

    

    

    

    
}
