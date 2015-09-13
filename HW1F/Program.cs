using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

namespace OneFactorInterestRateTree
{
    //A simple console program to run the code
    class Program
    {
        static int readInt()
        {
            var read = Console.ReadLine();
            int sel;
            if (!int.TryParse(read, out sel)) sel = -1;
            return sel;
        }

        static double readDbl()
        {
            var read = Console.ReadLine();
            double sel;
            if (!double.TryParse(read, out sel)) sel = double.NaN;
            return sel;
        }


        static void Main(string[] args)
        {
            //Change the following four lines to match your own file names and path.
            string filePath = @"D:\visual studio 2010\Projects\HW1F\HW1F\bin\Debug\";
            string ratexml = filePath + "RateTreeInput.xml";
            string bondxml = filePath + "BondInput.xml";
            string outFile = filePath + "out.csv";

            System.Console.WriteLine("Set one-factor rate model: ");
            System.Console.WriteLine("1: Hull-White");
            System.Console.WriteLine("2: Black-Karasinki");
            System.Console.WriteLine("0: Exit");
            System.Console.WriteLine("Enter [1,2,or 0]: ");
            OneFactorTrinomialShortRateTree.ModelType rateModel = new OneFactorTrinomialShortRateTree.ModelType();
            int modelSel = readInt();
            switch (modelSel)
            {
                case 1:
                    rateModel = OneFactorTrinomialShortRateTree.ModelType.HULL_WHITE;
                    break;
                case 2:
                    rateModel = OneFactorTrinomialShortRateTree.ModelType.BLACK_KARASINSKI;
                    break;
                default:
                    return;
            }


            string x = Directory.GetCurrentDirectory();

            CalibrateRateTreeFromXMLInput rateTreeInput = null;
            while (1 == 1)
            {
                System.Console.WriteLine("Choose from the following: ");
                System.Console.WriteLine("1: Calibration of rate tree from cap premium");
                System.Console.WriteLine("2: Calibration of rate tree from caplet premium");
                System.Console.WriteLine("3: Calibration of sigma from cap premium");
                System.Console.WriteLine("4: Calibration of sigma from caplet premium");
                System.Console.WriteLine("5: Set rate tree param");
                System.Console.WriteLine("6: Debug mode");
                System.Console.WriteLine("7: Bond pricing");
                System.Console.WriteLine("0: Exit");
                System.Console.WriteLine("Enter [1,2,3,4,5 or 0]: ");




                double dtTree = 0.125, tEndTree = 32.0;

                int numSel = readInt();
                switch (numSel)
                {
                    case 1:
                        rateTreeInput = new CalibrateRateTreeFromXMLInput(ratexml, rateModel, dtTree, tEndTree);
                        foreach (string ccy in rateTreeInput.getCurrencyIDs())
                        {
                            OneFactorTrinomialShortRateTree tree = rateTreeInput.calibrateWithCap(ccy);
                            System.Console.WriteLine();
                        }
                        break;
                    case 2:
                        rateTreeInput = new CalibrateRateTreeFromXMLInput(ratexml, rateModel, dtTree, tEndTree);
                        foreach (string ccy in rateTreeInput.getCurrencyIDs())
                        {
                            OneFactorTrinomialShortRateTree tree = rateTreeInput.calibrateWithCaplet(ccy);
                            System.Console.WriteLine();
                        }
                        break;
                    case 4:
                        System.Console.WriteLine("Enter param_a: ");
                        double param_a_fixed = readDbl();

                        rateTreeInput = new CalibrateRateTreeFromXMLInput(ratexml, rateModel, dtTree, tEndTree);
                        foreach (string ccy in rateTreeInput.getCurrencyIDs())
                        {
                            OneFactorTrinomialShortRateTree tree = rateTreeInput.calibrateWithCapletAFixed(ccy, param_a_fixed);
                            System.Console.WriteLine();
                        }
                        break;
                    case 5:
                        System.Console.WriteLine("Enter param_a: ");
                        double param_a = readDbl();
                        System.Console.WriteLine("Enter param_s: ");
                        double param_s = readDbl();


                        rateTreeInput = new CalibrateRateTreeFromXMLInput(ratexml, rateModel, dtTree, tEndTree);
                        foreach (string ccy in rateTreeInput.getCurrencyIDs())
                        {
                            OneFactorTrinomialShortRateTree tree = rateTreeInput.knownParam(ccy, rateModel, param_a, param_s);
                            System.Console.WriteLine();
                        }
                        break;
                    case 6:
                        // debug mode to analyze model
                        double my_a = 0.1;
                        double my_s = 0.01;
                        dtTree = 0.25;

                        rateTreeInput = new CalibrateRateTreeFromXMLInput(ratexml, rateModel, dtTree, tEndTree);
                        foreach (string ccy in rateTreeInput.getCurrencyIDs())
                        {
                            OneFactorTrinomialShortRateTree tree = rateTreeInput.knownParam(ccy, rateModel, my_a, my_s);
                            System.Console.WriteLine();
                        }
                        break;
                    case 7:

                        List<string> ccyList = rateTreeInput.getCurrencyIDs();


                        BondFromXMLInput b = new BondFromXMLInput(bondxml, rateTreeInput);
                        List<string> bondList = b.getAllBondIDs();
                        System.Console.WriteLine("Choose bond to price");


                        int bondNum = 1;
                        foreach (string s in bondList)
                            System.Console.WriteLine(String.Format("{0,2:d}: {1}", bondNum++, s));

                        int bondSel = readInt();


                        RiskyBondModel bond = null;
                        if (bondSel > 0)
                            bond = b.getBond(bondList[bondSel - 1]);
                        else
                            return;


                        double[] oasRng = new double[] { 0.0, 0.01, 0.02, 0.03, 0.04, 0.05, 0.06, 0.07, 0.08, 0.09, 0.1 };
                        System.Console.WriteLine("OAS, Price,  Spread Dur, Spread Risk, Spread Convx");
                        foreach (double o in oasRng)
                        {
                            System.Console.WriteLine(String.Format("{0,6:f4} {1,6:f4} {2,6:f4} {3,6:f4} {4,8:f6} ", o, bond.price(o), bond.SpreadDur(o), bond.SpreadRisk(o), bond.SpreadConvexity(o)));
                        }
                        double oasAtPar = bond.oas(1.00);

                        System.Console.WriteLine("Rate Dur , Rate Risk");
                        System.Console.WriteLine(String.Format("{0,6:f4} {1,6:f4} ", bond.RateDur(0.0, oasAtPar), bond.RateRisk(0.0, oasAtPar)));


                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("oas, price,  spread_dur, spread_risk, spread_convx");
                        double oasMin = 0.0, oasMax = 0.10, oasInc = 0.005;
                        double oas = oasMin;
                        while (oas < oasMax)
                        {
                            string str = String.Format("{0,6:f4}, {1,6:f4}, {2,6:f4}, {3,6:f4}, {4,8:f6}", oas, bond.price(oas), bond.SpreadDur(oas), bond.SpreadRisk(oas), bond.SpreadConvexity(oas));
                            sb.AppendLine(str);
                            oas += oasInc;
                        }

                        File.WriteAllText(outFile, sb.ToString());

                        break;

                    default:
                        return;
                }


            }
        }
    }
}
