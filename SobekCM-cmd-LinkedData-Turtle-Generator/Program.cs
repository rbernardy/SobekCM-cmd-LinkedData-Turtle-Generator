using System;
using System.Text;
using disUtility;
using System.Xml;
using System.IO;

namespace SobekCM_cmd_LinkedData_Turtle_Generator
{
    class Program
    {
        public static string myversion = "20200626-1304";

        public static void Main(string[] args)
        {
            if (args.Length==0)
            {
                Console.WriteLine("Requires attributes: --aggcode, --mode (url or local).");
                return;
            }

            string aggcode = null, accessMode=null;
            int limit = 99999;

            Console.WriteLine("\r\nSCLTG: version=[" + myversion + "].\r\n");

            foreach (string myarg in args)
            {
                if (myarg.StartsWith("--aggcode"))
                {
                    aggcode = myarg.Substring(10);
                }

                if (myarg.StartsWith("--mode"))
                {
                    accessMode = myarg.Substring(7);
                }

                if (myarg.StartsWith("--limit"))
                {
                    limit = int.Parse(myarg.Substring(8));
                }
            }

            if (aggcode==null || accessMode==null)
            {
                Console.WriteLine("Check attributes, some are missing.");
                return;
            }

            GenerateRDFsFromAnAggregation(aggcode, accessMode, limit);

            Console.WriteLine("________________________________________________________________________________________");

            Console.WriteLine("\r\nDone @ " + DateTime.Now.ToLocalTime() + "\r\n");
        }

        private static void GenerateRDFsFromAnAggregation(string aggcode, string accessMode, int limit)
        {
            string url = "http://solr.dss-test.org:8983/solr/documents_live/select?fl=did&q=aggregations:" + aggcode + "&rows=99999&sort=did%20asc&wt=xml";

            // Using a solr search to get the packageids for a specific aggregation, could just as easily use the OAI data provider - https://digital.lib.usf.edu//sobekcm_oai.aspx?verb=Identify&verb=Identify, etc.

            // xmlUtilities is helper class for working with xml in the disUtility library
            string data = xmlUtilities.getContentXML(url), packageid,path_mets;
            XmlDocument doc = new XmlDocument();
            XmlNodeList nodes;
            XmlNode node2, node3;
            Boolean mytry = false;
            int idx = 0;

            doc.LoadXml(data);

            nodes = doc.SelectNodes("//str[@name='did']");

            Console.WriteLine("\r\nThere are [" + nodes.Count + "] records for [" + aggcode + "].\r\n");

            foreach (XmlNode node in nodes)
            {
                idx++;

                if (idx > limit)
                {
                    Console.WriteLine("limit [" + limit + "] reached.\r\n");
                    return;
                }

                Console.WriteLine("____________________________________________________________________________________");
                Console.WriteLine(idx + "/" + nodes.Count + ": " + node.InnerText);
                packageid = node.InnerText.Replace(":", "_");

                if (accessMode == "url")
                {
                    // running locally, get mets file via URL.
                    // sobekcm is a helper class for working with a SobekCM-based repository in the disUtility
                    path_mets = sobekcm.GetContentFolderURLfromPackageID(packageid) + packageid + ".mets.xml";
                }
                else
                {
                    // runningon the server, get mets directly from the package folder.
                    path_mets = sobekcm.GetContentFolderPathFromPackageID(packageid) + packageid + ".mets.xml";
                }

                mytry=GenerateRDFfromMETS(packageid, path_mets, accessMode);

                if (mytry)
                {
                    Console.WriteLine("Successfully generated for [" + packageid + "].");
                }
                else
                {
                    Console.WriteLine("Generation failed for [" + packageid + "].");
                }

                Console.WriteLine("\r\n");
            }
        }

        private static Boolean GenerateRDFfromMETS(string packageid, string path_mets, string accessMode)
        {
            Console.WriteLine("\r\nGRFM: packageid=[" + packageid + "], mets=[" + path_mets + "] accessMode=[" + accessMode + "].\r\n");

            StringBuilder sb = new StringBuilder();
            string data=null,output=null, handle=null, subject=null, loi=null, path_rdf, dir;
            XmlDocument doc = new XmlDocument();
            XmlNodeList nodes,nodes2,nodes3;
            XmlNode node2, node3;
            Boolean mytry = false;

            dir = @"C:\Users\" + Environment.UserName + @"\Desktop\rdf\";

            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            path_rdf = dir + packageid + ".rdf";

            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8""?>");
            sb.AppendLine(@"<rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#""");
            sb.AppendLine(@"xmlns:dc=""http://purl.org/dc/elements/1.1""");
            sb.AppendLine(@"xmlns:dcterms=""http://http://purl.org/dc/terms""");
            sb.AppendLine(@"xmlns:edm=""http://www.europeana.eu/schemas/edm""");
            sb.AppendLine(@"xmlns:skos=""http://www.w3.org/2004/02/skos/core#""");
            sb.AppendLine(@"xmlns:owl=""http://www.w3.org/2002/07/owl#""");
            sb.AppendLine(@"xmlns:ore=""http://www.openarchives.org/ore/terms/""");
            sb.AppendLine(@"xmlns:rdfs=""http://www.w3.org/2000/01/rdf-schema#""");
            sb.AppendLine(@"xmlns:dwc=""http://rs.tdwg.org/dwc/terms/"">");

            sb.AppendLine("\r\n");

            data = xmlUtilities.getContentXML(path_mets);
            Console.WriteLine(data.Length + " bytes retrieved for [" + packageid + "].");

            if (data.Trim().Length==0)
            {
                Console.WriteLine("issue: 0 data length for [" + packageid + "].");
                return false;
            }

            //Console.WriteLine("\r\n\r\n" + data + "\r\n\r\n");

            doc.LoadXml(data);

            XmlNamespaceManager nsm = new XmlNamespaceManager(doc.NameTable);
            nsm.AddNamespace("METS", "http://www.loc.gov/METS/");
            nsm.AddNamespace("mods", "http://www.loc.gov/mods/v3");

            handle = sobekcm.Get_handle_from_doc(doc);

            if (handle != null)
            {
                subject = "https://digital.lib.usf.edu/?" + handle;
            }
            else
            {
                subject = "https://digital.lib.usf.edu/?novalue";
            }

            // Mapping accomplished according to the mapping document provided by the USF Libraries Linked data team

            // ------------------------------------------------------------------------------------
            // edm:ProvidedCHO

            sb.AppendLine(@"<edm:ProvidedCHO rdf:about=""" + subject + @""">");

            // mods:namePart -> http://purl.org/dc/terms/creator 
            nodes2 = doc.SelectNodes("//mods:namePart",nsm);

            foreach (XmlNode node in nodes2)
            {
                sb.AppendLine(@"<dcterms:creator>" + node.InnerText + "</dcterms:creator>");
            }

            //mods:title
            //must exist
            node2 = doc.SelectSingleNode("//mods:titleInfo/mods:title",nsm);
            sb.AppendLine(@"<dcterms:title>" + node2.InnerText + "</dcterms:title>");

            //mods:extent
            node2 = doc.SelectSingleNode("//mods:extent",nsm);
            if (node2 != null) sb.AppendLine(@"<dcterms:format>" + node2.InnerText + "</dcterms:format>");

            //mods:abstract
            node2 = doc.SelectSingleNode("//mods:abstract",nsm);
            if (node2 != null) sb.AppendLine(@"<dcterms:description>" + node2.InnerText + "</dcterms:description>");

            //mods:subject/mods:topic

            nodes2 = doc.SelectNodes("//mods:subject/mods:topic",nsm);

            foreach (XmlNode node in nodes2)
            {
                sb.AppendLine(@"<dcterms:subject>" + node.InnerText + "</dcterms:subject>");
            }

            //mods:hierarchicalGeographic
            nodes2 = doc.SelectNodes("//mods:hierarchicalGeographic/*",nsm);

            foreach (XmlNode node in nodes2)
            {
                sb.AppendLine(@"<dcterms:spatial>" + node.InnerText + "</dcterms:spatial>");
            }

            sb.AppendLine(@"</edm:ProvidedCHO>");
            sb.AppendLine("\r\n");

            //-------------------------------------------------------------------------------------
            // edm:WebResource

            sb.AppendLine(@"<edm:WebResource rdf:about=""" + subject + @""">");

            node2 = doc.SelectSingleNode("//mods:accessCondition",nsm);
            sb.AppendLine(@"<dcterms:rights>" + node2.InnerText + "</dcterms:rights>");

            sb.AppendLine(@"</edm:WebResource>");
            sb.AppendLine("\r\n");

            //-------------------------------------------------------------------------------------
            // ore:Aggregation

            sb.AppendLine(@"<ore:Aggregation rdf:about=""" + subject + @""">");
            loi = sobekcm.Get_LOI_from_doc(doc);

            if (loi != null)
            {
                sb.AppendLine(@"<edm:AggregatedCHO rdf:resource=""" + loi + @"""/>");
            }
            else
            {
                sb.AppendLine(@"<edm:AggregatedCHO rdf:resource=""novalue""/>");
            }

            sb.AppendLine(@"<edm:dataProvider>University of South Florida Libraries</edm:dataProvider>");
            sb.AppendLine(@"<edm:isShownAt rdf:resource=""" + subject + @"""/>");

            sb.AppendLine(@"</ore:Aggregation>");
            sb.AppendLine("\r\n");

            //-------------------------------------------------------------------------------------

            sb.AppendLine(@"</rdf:RDF>");

            //-------------------------------------------------------------------------------------

            File.WriteAllText(path_rdf, sb.ToString());
           
            if (!File.Exists(path_rdf))
            {
                Console.WriteLine("File does not exist [" + path_rdf + "].");
                return false;
            }
            else
            {
                FileInfo fi = new FileInfo(path_rdf);
                Console.WriteLine("File exist and is " + fi.Length + " bytes. [" + path_rdf + "].\r\n");
                Console.WriteLine(sb.ToString());
                return true;
            }

            // 20200605 - can't find rdf.xsd online yet for validation of the rdf.

            /*
            mytry = xmlUtilities.validateXML("rdf", @"C:\Users\" + Environment.UserName + @"\Dropbox\rdf.xsd", path_rdf);

            if (mytry)
            {
                Console.WriteLine(packageid + " IS valid.");
                return true;
            }
            else
            {
                Console.WriteLine(packageid + "is NOT valid.");
                return false;
            }
            */
        }
    }
}