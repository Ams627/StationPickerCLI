using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace StationPickerCLI
{
    internal class Program
    {
        private static void Main(string[] args)
        {   
            try
            {
                if (args.Count() == 0)
                {
                    throw new Exception("You must specify at least one partial station match.");
                }
                
                var faresfile = "s:/FareLocationsRefData.xml";
                var stationfile = "s:/StationsRefData.xml";
                if (!File.Exists(faresfile) || !File.Exists(stationfile))
                {
                    throw new Exception($"This program relies on the S:\\ drive containing IDMS data." +
                        $"Either one or both the files {stationfile} and {faresfile} is missing.");
                }

                XDocument faredoc = XDocument.Load(faresfile);
                XDocument stationdoc = XDocument.Load(stationfile);

                var stationlist = (from station in stationdoc.Element("StationsReferenceData").Elements("Station")
                                   where (string)station.Element("UnattendedTIS") == "true" &&
                                   !string.IsNullOrWhiteSpace((string)station.Element("CRS")) &&
                                   (string)station.Element("OJPEnabled") == "true"
                                   join fare in faredoc.Element("FareLocationsReferenceData").Elements("FareLocation")
                                   on (string)station.Element("Nlc") equals (string)fare.Element("Nlc")
                                   where (string)fare.Element("UnattendedTIS") == "true"
                                   select new
                                   {
                                       CRS = (string)station.Element("CRS"),
                                       nlc = (string)fare.Element("Nlc"),
                                       Name = (string)fare.Element("OJPDisplayName"),
                                   }).Distinct();

                foreach (var partialname in args)
                {
                    var pattern = $@"\b{partialname}";
                    var searchresults = stationlist.Select(s =>
                    {
                        bool crsMatch = false;
                        var le = partialname.Length;
                        if (le <= 3)
                        {
                            crsMatch = string.Compare(s.CRS.Substring(0, le), partialname, StringComparison.OrdinalIgnoreCase) == 0;
                        }
                        var match = Regex.Match(s.Name, pattern, RegexOptions.IgnoreCase);

                        // if neither name nor CRS match the partial input then return null:
                        if (!match.Success && !crsMatch)
                        {
                            return null;
                        }

                        // rank for sorting stations -1 means not ranked yet:
                        int rank = -1;

                        // A full 3-letter CRS match is the highest rank:
                        if (le == 3 && crsMatch)
                        {
                            rank = 0;
                        }
                        else if (!match.Success)
                        {
                            // here the partial input doesn't match any stations - so there is just a partial CRS match - this is the lowest rank:
                            rank = 1000;
                        }
                        return new
                        {
                            crs = s.CRS,
                            name = s.Name,
                            // rank by the position of the word match within the sentence:
                            rank = rank == -1 ? s.Name.Substring(0, match.Index).Split().Count() : rank
                        };
                    }).Where(x => x != null).OrderBy(x => x.rank).ThenBy(x => x.name);

                    foreach (var result in searchresults)
                    {
                        Console.WriteLine($"{result.crs} {result.name} {result.rank}");
                    }
                }

            }
            catch (Exception ex)
            {
                var codeBase = System.Reflection.Assembly.GetExecutingAssembly().CodeBase;
                var progname = Path.GetFileNameWithoutExtension(codeBase);
                Console.Error.WriteLine(progname + ": Error: " + ex.Message);
            }

        }
    }
}
