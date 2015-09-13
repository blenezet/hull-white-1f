using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OneFactorInterestRateTree
{
    //The RateTreeXML contains info for the yield curve and the premium of a set of interest rate cap.
    /*Sample RateTreeXML
    <?xml version="1.0"?>
    <RateTreeInput>
        <EvaluationDate>2014-05-15</EvaluationDate>
        <Currency id = "USD">
            <YieldCurve>
                <Tenor>1,2,3,4,5,6,7,8,9,10,11,12,15,20,25,30,40,50</Tenor>	
                <ZeroRate>0.002545,0.0050721,0.0093085,0.0133154,0.0167937,0.0197113,0.0220818,0.0240687,0.0257589,0.0271769,0.0284009,0.0294532,0.0317454,0.0337747,0.0346537,0.0349814,0.0348355,0.0340782</ZeroRate>
            </YieldCurve>
            <Cap>
                <FwdStartTenor>0.25</FwdStartTenor>
                <PaymentPeriod>Q</PaymentPeriod>
                <Tenor>3,4,5,7,10,15,20,25,30</Tenor>
                <Strike>0.03,0.03,0.03,0.03,0.03,0.03,0.03,0.03,0.03</Strike>
                <Premium>0.00244,0.00859,0.0165,0.0383,0.07671,0.13776,0.18935,0.23349,0.27198</Premium>
            </Cap>
        </Currency>
    </RateTreeInput>
    */
    class CalibrateRateTreeFromXMLInput
    {
        string xmlname;
        double dtTree, tEndTree;
        List<string> ccyID;
        Dictionary<string, OneFactorTrinomialShortRateTree> treeDict;
        DateTime? evalDt;
        OneFactorTrinomialShortRateTree.ModelType rateModel;

        public CalibrateRateTreeFromXMLInput(string xmlname, OneFactorTrinomialShortRateTree.ModelType rateModel, double dtTree, double tEndTree)
        {
            this.rateModel = rateModel;
            this.xmlname = xmlname;
            this.dtTree = dtTree;
            this.tEndTree = tEndTree;
            evalDt = null;
            ccyID = new List<string>();
            treeDict = new Dictionary<string, OneFactorTrinomialShortRateTree>();
        }

        public DateTime getEvalDt()
        {
            if (!evalDt.HasValue)
            {
                XDocument document = XDocument.Load(xmlname);
                evalDt = (DateTime)document.Root.Element("EvaluationDate");
            }
            return evalDt.Value;
        }


        public List<string> getCurrencyIDs()
        {

            // get the list of supported currency
            if (ccyID.Count == 0)
            {
                XDocument document = XDocument.Load(xmlname);
                ccyID = (from r in document.Descendants("Currency")
                         select (string)r.Attribute("id")).ToList();
            }
            return ccyID;
        }

        double[] stringToDouble(string s)
        {
            return s.Split(',').Select(x => double.Parse(x)).ToArray<double>();
        }

        bool[] stringToBoolean(string s)
        {
            return s.Split(',').Select(x => bool.Parse(x)).ToArray<bool>();
        }

        double periodStrToDbl(string s)
        {
            if (s.ToUpper() == "A") return 1.0;
            switch (s.ToUpper())
            {
                case "A": return 1.0;
                case "S": return 0.5;
                case "Q": return 0.25;
                case "M": return 1.0 / 12.0;
                default:
                    return 0.5;
            }
        }

        public OneFactorTrinomialShortRateTree knownParam(string ccy, OneFactorTrinomialShortRateTree.ModelType model, double param_a, double param_s)
        {
            List<Tuple<double, double>> zrInput;
            List<Tuple<double, double, double>> capInput;
            List<Tuple<double, bool, double, double>> capletInput;

            double tStartCap;
            double dtCap;
            loadRateFile(ccy, out zrInput, out capInput, out tStartCap, out dtCap, out capletInput);

            OneFactorTrinomialShortRateTree tree = new OneFactorTrinomialShortRateTree(rateModel, param_a, param_s, dtTree, tEndTree, zrInput);
            tree.buildTree();
            treeDict[ccy] = tree;
            calcStat(ccy, capInput, tStartCap, dtCap, capletInput, param_a, param_s, tree);
            return treeDict[ccy];
        }



        public OneFactorTrinomialShortRateTree calibrateWithCap(string ccy)
        {
            if (ccyID.Find(x => x == ccy) == null) throw new ArgumentException("Cannot find " + ccy + " info when building interest rate tree.");

            if (treeDict.ContainsKey(ccy)) return treeDict[ccy];


            List<Tuple<double, double>> zrInput;
            List<Tuple<double, double, double>> capInput;
            List<Tuple<double, bool, double, double>> capletInput;
            double tStartCap;
            double dtCap;
            loadRateFile(ccy, out zrInput, out capInput, out tStartCap, out dtCap, out capletInput);

            CalibrateRate1FWithCap m = new CalibrateRate1FWithCap(rateModel, ccy, dtTree, tEndTree, zrInput, dtCap, tStartCap, capInput);
            OneFactorTrinomialShortRateTree tree = m.calibrate();
            treeDict[ccy] = tree;

            calcStat(ccy, capInput, tStartCap, dtCap, capletInput, m.param_a, m.param_s, tree);
            return treeDict[ccy];


        }

        public OneFactorTrinomialShortRateTree calibrateWithCaplet(string ccy)
        {
            if (ccyID.Find(x => x == ccy) == null) throw new ArgumentException("Cannot find " + ccy + " info when building interest rate tree.");

            if (treeDict.ContainsKey(ccy)) return treeDict[ccy];


            List<Tuple<double, double>> zrInput;
            List<Tuple<double, double, double>> capInput;
            List<Tuple<double, bool, double, double>> capletInput;
            double tStartCap;
            double dtCap;
            loadRateFile(ccy, out zrInput, out capInput, out tStartCap, out dtCap, out capletInput);

            CalibrateRate1FWithCaplet m = new CalibrateRate1FWithCaplet(rateModel, ccy, dtTree, tEndTree, zrInput, capletInput, dtCap);
            OneFactorTrinomialShortRateTree tree = m.calibrate();
            treeDict[ccy] = tree;

            calcStat(ccy, capInput, tStartCap, dtCap, capletInput, m.param_a, m.param_s, tree);
            return treeDict[ccy];


        }

        private void calcStat(string ccy, List<Tuple<double, double, double>> capInput, double tStartCap, double dtCap, List<Tuple<double, bool, double, double>> capletInput, double param_a, double param_s, OneFactorTrinomialShortRateTree tree)
        {
            System.Console.WriteLine(String.Format("{0} for {1}: a={2,6:f4} s={3,6:f4}", rateModel.ToString(), ccy, param_a, param_s));
            int nTSLast = tree.nTimeStep - 2;
            double minR = tree.getRateNode(nTSLast, -tree.jMax(nTSLast)).R;
            double maxR = tree.getRateNode(nTSLast, tree.jMax(nTSLast)).R;
            System.Console.WriteLine(String.Format("{0,2:d} {1,2:d} {2,6:f4} {3,6:f4}", tree.nTimeStep, tree.jMax(nTSLast), minR, maxR));


            //capInput: tenor, strike, premium            
            foreach (Tuple<double, double, double> c in capInput)
            {
                InterestRateCapModel capmodel = new InterestRateCapModel(tree, c.Item2, tStartCap, dtCap, c.Item1);
                double capPx = capmodel.price(); ;
                System.Console.WriteLine(String.Format("Cap {0,6:f4},{1,6:f4}: mkt:{2,6:f4} model:{3,6:f4}", c.Item1, c.Item2, c.Item3, capPx));
            }

            //capletInput: expiry, atmflag, strike, premium   
            foreach (Tuple<double, bool, double, double> c in capletInput)
            {
                //InterestRateCapletModel capletmodel = new InterestRateCapletModel(tree, 0.02, 0.75, 1.00, 0.25);
                // calculate forward(t,T,S) usefull for ATM caplet
                double P_t_T = tree.zcPrice[(int)(c.Item1 / tree.dtUnit)];
                double P_t_S = tree.zcPrice[(int)((c.Item1 + dtCap) / tree.dtUnit)];
                double F_t_T_S = (P_t_T / P_t_S) - 1d;
                //Console.WriteLine("P_t_T: {0}, P_t_S:{1}, F_t_T_S:{2}", P_t_T, P_t_S, F_t_T_S); 
                double K = c.Item2 == true ? F_t_T_S : c.Item3;

                InterestRateCapletModel capletmodel = new InterestRateCapletModel(tree, K, c.Item1, c.Item1 + dtCap, dtCap);
                double capletPx = capletmodel.price();
                System.Console.WriteLine(String.Format("Caplet {0,6:f8},{1,6:f4} ATM={2} : K:{3,6:f8} mkt:{4,6:f8} model:{5,6:f8}", c.Item1, c.Item1 + dtCap, c.Item2, K, c.Item4, capletPx));
                //if (c.Item2 == true) System.Console.WriteLine("ATM strike: {0}", F_t_T_S);
            }
        }

        private void loadRateFile(string ccy, out List<Tuple<double, double>> zrInput, out List<Tuple<double, double, double>> capInput, out double tStartCap, out double dtCap, out List<Tuple<double, bool, double, double>> capletInput)
        {

            XDocument document = XDocument.Load(xmlname);

            var yc = from elem in document.Root.Elements("Currency").Elements("YieldCurve")
                     where (string)elem.Parent.Attribute("id") == ccy
                     select new
                     {
                         Tenor = (string)elem.Element("Tenor"),
                         ZeroRate = (string)elem.Element("ZeroRate"),
                     };


            var cap = from elem in document.Root.Elements("Currency").Elements("Cap")
                      where (string)elem.Parent.Attribute("id") == ccy
                      select new
                      {
                          FwdStartTenor = (double)elem.Element("FwdStartTenor"),
                          PaymentPeriod = (string)elem.Element("PaymentPeriod"),
                          Tenor = (string)elem.Element("Tenor"),
                          Strike = (string)elem.Element("Strike"),
                          Premium = (string)elem.Element("Premium"),
                      };

            var caplet = from elem in document.Root.Elements("Currency").Elements("Caplet")
                         where (string)elem.Parent.Attribute("id") == ccy
                         select new
                         {
                             PaymentPeriod = (string)elem.Element("PaymentPeriod"),
                             Expiry = (string)elem.Element("Expiry"),
                             Atm = (string) elem.Element("Atm"),
                             Strike = (string)elem.Element("Strike"),
                             Premium = (string)elem.Element("Premium"),
                         };

            // add checks for caplet
            if (yc.Count() == 0 || cap.Count() == 0) throw new ArgumentException("Insufficient info to build an interest rate tree for " + ccy);
            if (yc.Count() > 1 || cap.Count() > 1) throw new ArgumentException("Duplicate entries for " + ccy);

            zrInput = new List<Tuple<double, double>>();

            double[] ycTenor = stringToDouble(yc.First().Tenor.ToString());
            double[] zeroRate = stringToDouble(yc.First().ZeroRate.ToString());
            if (ycTenor.Length != zeroRate.Length) throw new ArgumentException("Length of Yield Curve Tenor != Zero Rate");
            for (int i = 0; i < ycTenor.Length; i++)
                zrInput.Add(new Tuple<double, double>(ycTenor[i], zeroRate[i]));

            capInput = new List<Tuple<double, double, double>>();
            double[] capTenor = stringToDouble(cap.First().Tenor.ToString());
            double[] capStrike = stringToDouble(cap.First().Strike.ToString());
            double[] capPremium = stringToDouble(cap.First().Premium.ToString());
            if (capTenor.Length != capStrike.Length) throw new ArgumentException("Length of Cap Tenor != Strike");
            if (capTenor.Length != capPremium.Length) throw new ArgumentException("Length of Cap Tenor != Premium");
            for (int i = 0; i < capTenor.Length; i++)
                capInput.Add(new Tuple<double, double, double>(capTenor[i], capStrike[i], capPremium[i]));

            tStartCap = (double)cap.First().FwdStartTenor;
            dtCap = periodStrToDbl(cap.First().PaymentPeriod);

            capletInput = new List<Tuple<double, bool, double, double>>();
            double[] capletExpiry = stringToDouble(caplet.First().Expiry.ToString());
            bool[] capletAtm = stringToBoolean(caplet.First().Atm.ToString());
            double[] capletStrike = stringToDouble(caplet.First().Strike.ToString());
            double[] capletPremium = stringToDouble(caplet.First().Premium.ToString());
            if (capletExpiry.Length != capletStrike.Length) throw new ArgumentException("Length of Caplet Expiry != Strike");
            if (capletExpiry.Length != capletPremium.Length) throw new ArgumentException("Length of Cap Expiry != Premium");
            for (int i = 0; i < capletExpiry.Length; i++)
                capletInput.Add(new Tuple<double, bool, double, double>(capletExpiry[i], capletAtm[i], capletStrike[i], capletPremium[i]));
            
        }

    }
}
