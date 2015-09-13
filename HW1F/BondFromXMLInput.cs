using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace OneFactorInterestRateTree
{
    //The BondXML contains info for the callable bond to be priced.
    /* Sample BondXML
    <?xml version="1.0"?>
    <BondInput>
        <Bond id = "US67054LAA52">
            <Name>NUMFP 4 7/8 05/15/19</Name>
            <Description>1st Lien - 5NC2</Description>
            <Currency>USD</Currency>
            <IssueDate>2014-05-08</IssueDate>
            <IsPerptual>F</IsPerptual>
            <Maturity>2019-05-15</Maturity>
            <Coupon>
                <IsFloat>F</IsFloat>
                <CpnRate>0.04875</CpnRate>
                <CpnStart>2014-05-08</CpnStart>
                <CpnPeriod>S</CpnPeriod>
                <CpnEnd>2019-05-15</CpnEnd>
            </Coupon>		
			
            <CallSchedule>
                <CallDate>2016-05-15,2017-05-15,2018-05-15</CallDate>
                <CallPrice>1.03656,1.01828,1.00000</CallPrice>
            </CallSchedule>
        </Bond>
    </BondInput>
    */
    class BondFromXMLInput
    {
        CalibrateRateTreeFromXMLInput treeInput;

        string xmlname;
        List<string> bondID;
        Dictionary<string, RiskyBondModel> bondDict;

        public BondFromXMLInput(string xmlname, CalibrateRateTreeFromXMLInput treeInput)
        {
            this.xmlname = xmlname;
            this.treeInput = treeInput;
            bondID = new List<string>();
            bondDict = new Dictionary<string, RiskyBondModel>();

        }

        public List<string> getAllBondIDs()
        {
            XDocument document = XDocument.Load(xmlname);
            // get the list of supported currency
            if (bondID.Count == 0)
            {
                bondID = (from r in document.Descendants("Bond")
                          select (string)r.Attribute("id")).ToList();
            }
            return bondID;
        }

        double[] stringToDouble(string s)
        {
            return s.Split(',').Select(x => double.Parse(x)).ToArray<double>();
        }

        DateTime[] stringToDateTime(string s)
        {
            return s.Split(',').Select(x => DateTime.Parse(x)).ToArray<DateTime>();
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

        int tToTS(DateTime evalDt, DateTime t, OneFactorTrinomialShortRateTree tree)
        {
            const double nDayPerYr = 365.0;
            return tree.getNearestTStep(t.Subtract(evalDt).TotalDays / nDayPerYr);
        }


        public RiskyBondModel getBond(string id)
        {
            if (bondID.Find(x => x == id) == null)
                throw new ArgumentException("Cannot find " + id + " info when building bond model.");

            if (bondDict.ContainsKey(id)) return bondDict[id];

            XDocument document = XDocument.Load(xmlname);
            var general = from elem in document.Root.Elements("Bond")
                          where (string)elem.Attribute("id") == id
                          select new
                          {
                              Name = (string)elem.Element("Tenor"),
                              Desc = (string)elem.Element("Description"),
                              Ccy = (string)elem.Element("Currency"),
                              IssueDt = (DateTime)elem.Element("IssueDate"),
                              MatDt = (string)elem.Element("Maturity"),  //set to string such that we can leave that blank in the case of perp
                              IsPerptual = (string)elem.Element("IsPerptual"),
                          };
            if (general.Count() > 1)
                throw new ArgumentException("Duplication desc for bond " + id);

            var cpn = from elem in document.Root.Elements("Bond").Elements("Coupon")
                      where (string)elem.Parent.Attribute("id") == id
                      select new
                      {
                          IsFloat = (string)elem.Element("IsFloat"),
                          CpnRate = (double)elem.Element("CpnRate"),
                          CpnStart = (DateTime)elem.Element("CpnStart"),
                          CpnEnd = (DateTime)elem.Element("CpnEnd"),
                          CpnPeriod = (string)elem.Element("CpnPeriod"),
                      };

            var callSch = from elem in document.Root.Elements("Bond").Elements("CallSchedule")
                          where (string)elem.Parent.Attribute("id") == id
                          select new
                          {
                              CallDt = (string)elem.Element("CallDate"),
                              CallPx = (string)elem.Element("CallPrice"),
                          };

            var perpcpn = from elem in document.Root.Elements("Bond").Elements("PerptualCoupon")
                          where (string)elem.Parent.Attribute("id") == id
                          select new
                          {
                              IsFloat = (string)elem.Element("IsFloat"),
                              CpnRate = (double)elem.Element("CpnRate"),
                              CpnPeriod = (string)elem.Element("CpnPeriod"),
                          };



            List<string> ccyIDList = treeInput.getCurrencyIDs();
            string bondCcy = (string)general.First().Ccy;

            if (ccyIDList.Find(x => x == bondCcy) == null)
                throw new ArgumentException("Currency data cannot be found in interest rate tree for bond " + id);

            OneFactorTrinomialShortRateTree tree = treeInput.calibrateWithCap(bondCcy);
            DateTime evalDt = treeInput.getEvalDt();
            BondBuilder bondBuilder = new BondBuilder(tree, evalDt);

            bool isPerptual = general.First().IsPerptual == "T";
            Payment perpPayment = null;
            if (isPerptual)
            {
                perpPayment = new Payment(perpcpn.First().IsFloat == "T", perpcpn.First().CpnRate, periodStrToDbl(perpcpn.First().CpnPeriod));
            }


            int matInTS = isPerptual ? int.MaxValue : tToTS(evalDt, DateTime.Parse(general.First().MatDt), tree);
            const double dayTol = 5.0;
            const double nDayPerYear = 365.0;
            Dictionary<int, Payment> pmtInTS = new Dictionary<int, Payment>();
            foreach (var c in cpn)
            {
                DateTime t = c.CpnEnd, startT = c.CpnStart;
                double dt = periodStrToDbl(c.CpnPeriod);
                int dtInDay = (int)(dt * nDayPerYear);
                double dayRem;
                while ((dayRem = t.Subtract(c.CpnStart).TotalDays) > dayTol)
                {
                    if (t <= evalDt) break;
                    double cpnPeriod = dayRem > dtInDay ? dt : dayRem / nDayPerYear;
                    pmtInTS[tToTS(evalDt, t, tree)] = new Payment(c.IsFloat == "T", c.CpnRate, dt);
                    t = t.AddDays(-dtInDay);
                }
            }


            Dictionary<int, double> callSchInTS = new Dictionary<int, double>();
            if (callSch.Count() > 0 && callSch.First().CallDt != null && callSch.First().CallPx != null)
            {
                DateTime[] callDt = stringToDateTime(callSch.First().CallDt.ToString());
                double[] callPx = stringToDouble(callSch.First().CallPx.ToString());
                if (callDt.Length != callPx.Length) throw new ArgumentException("Call Date and Call Price arrays are of different size.");
                for (int i = 0; i < callDt.Length; i++)
                {
                    if (callDt[i] <= evalDt) continue;
                    callSchInTS[tToTS(evalDt, callDt[i], tree)] = callPx[i];
                }
            }


            RiskyBondModel bond = new RiskyBondModel(tree);
            if (isPerptual)
                bond.setPerpetualBond(perpPayment, pmtInTS, callSchInTS);
            else
                bond.setBond(matInTS, pmtInTS, callSchInTS);

            bondDict[id] = bond;
            return bond;

        }
    }
}
