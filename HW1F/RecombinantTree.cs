using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OneFactorInterestRateTree
{
    class RecombinantTree
    {
        List<RateNode> rTree;
        List<int> nNodePriorLvls, jMaxEachLvl;


        public RecombinantTree()
        {
            jMaxEachLvl = new List<int>();
            nNodePriorLvls = new List<int>();
            rTree = new List<RateNode>();

            RateNode parent = new RateNode();
            parent.transProb = null;
            parent.upChild = parent.midChild = parent.downChild = null;
            parent.forktype = ForkType.UNDEFINED;
            parent.i = 0;
            parent.j = 0;
            rTree.Add(parent);

            nNodePriorLvls.Add(1);
            jMaxEachLvl.Add(0);
        }


        //i,j => loc.  0,0 => 0.  1,1 => 1.  1,0=>2.  1,-1 =>3.  2,1 => 4.  2,0=>5.  2,-1 =>6 for a tree with rMax=1
        //nNodePriorLvl=[0,1,4]
        //jMaxEachLvl=[0,1,1]
        private int ijToLoc(int i, int j)
        {
            if (i > nNodePriorLvls.Count)
                throw new ArgumentOutOfRangeException("i>nTimeStep: i=" + i + ">nTimeStep=" + nNodePriorLvls.Count);
            int jMax = jMaxEachLvl[i];
            if (j > jMax || j < -jMax)
                throw new ArgumentOutOfRangeException("j outside jMax bound:  j=" + j + " & jMax=" + jMax);
            if (i == 0) return 0;
            else return nNodePriorLvls[i - 1] + jMax - j;

        }

        public RateNode getNode(int i, int j)
        {
            return rTree[ijToLoc(i, j)];
        }



        public int nTimeStep()
        {
            return nNodePriorLvls.Count;
        }

        public int jMax(int tStep)
        {
            return jMaxEachLvl[tStep];
        }

        public void addNextLevel(int rMax)
        {

            int next_i = nNodePriorLvls.Count;
            int curr_i = next_i - 1;

            int totNodeCreated = nNodePriorLvls[nNodePriorLvls.Count - 1];
            int jMaxCurr = jMaxEachLvl[jMaxEachLvl.Count - 1];

            //Add next level's nodes
            if (rMax > jMaxCurr)
            {
                //all centre branching						
                for (int j = jMaxCurr + 1; j >= -(jMaxCurr + 1); j--)
                {
                    RateNode nextNode = new RateNode();
                    nextNode.transProb = null;
                    nextNode.upChild = nextNode.midChild = nextNode.downChild = null;
                    nextNode.forktype = ForkType.UNDEFINED;
                    nextNode.i = next_i;
                    nextNode.j = j;
                    nextNode.R = Double.NaN;
                    rTree.Add(nextNode);
                }
                nNodePriorLvls.Add(totNodeCreated + jMaxCurr * 2 + 3);
                jMaxEachLvl.Add(jMaxCurr + 1);
            }
            else
            {
                //top node forking downwards
                //bottom node forking upwards
                for (int j = jMaxCurr; j >= -jMaxCurr; j--)
                {
                    RateNode nextNode = new RateNode();
                    nextNode.transProb = null;
                    nextNode.upChild = nextNode.midChild = nextNode.downChild = null;
                    nextNode.forktype = ForkType.UNDEFINED;
                    nextNode.i = next_i;
                    nextNode.j = j;
                    nextNode.R = Double.NaN;
                    rTree.Add(nextNode);
                }
                nNodePriorLvls.Add(totNodeCreated + jMaxCurr * 2 + 1);
                jMaxEachLvl.Add(jMaxCurr);
            }

            //Connect node from prev layer to current layer
            for (int j = jMaxCurr; j >= -jMaxCurr; j--)
            {
                RateNode currNode = getNode(curr_i, j);
                if (j >= rMax)
                {
                    currNode.forktype = ForkType.DOWNFORK;
                    currNode.upChild = getNode(next_i, j);
                    currNode.midChild = getNode(next_i, j - 1);
                    currNode.downChild = getNode(next_i, j - 2);
                }
                else if (j <= -rMax)
                {
                    currNode.forktype = ForkType.UPFORK;
                    currNode.upChild = getNode(next_i, j + 2);
                    currNode.midChild = getNode(next_i, j + 1);
                    currNode.downChild = getNode(next_i, j);
                }
                else
                {
                    currNode.forktype = ForkType.MIDFORK;
                    currNode.upChild = getNode(next_i, j + 1);
                    currNode.midChild = getNode(next_i, j);
                    currNode.downChild = getNode(next_i, j - 1);

                }
            }

        }

        public void traverseAll(TraverseFunc x)
        {
            foreach (RateNode r in rTree)
            {
                x.processNode(r);
            }
        }

        public void traverseTStep(int ts, TraverseFunc x)
        {
            if (ts >= nNodePriorLvls.Count)
                throw new ArgumentOutOfRangeException("Time step been sought is outside range.");
            int lb = (ts == 0) ? 0 : nNodePriorLvls[ts - 1];
            int ub = nNodePriorLvls[ts];

            for (int i = lb; i < ub; i++)
            {
                RateNode r = rTree[i];
                x.processNode(r);
            }
        }


    }
}
