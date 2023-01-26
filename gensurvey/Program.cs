using System.Text;
using System.IO;
using System.IO.Compression;
using CommandLine;

namespace GenSurvey
{

    class Program
    {
        public class Options
        {
            [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
            public bool Verbose { get; set; }

            [Option('i', "input", Required = true, HelpText = "source template excel file")]
            public string Source { get; set; }

            [Option('t', "templateheader", Required = true, HelpText = "HTML template to use")]
            public string Template { get; set; }

            [Option('d', "dumptext", Required = false, HelpText = "dumps content of survey in text for to target file")]
            public string DumpText { get; set; }

            [Option('z', "zip", Required = false, HelpText = "creates ZIP archive with contents of survey for all languages")]
            public string Zip { get; set; }

            [Option('h', "html", Required = false, HelpText = "creates html file with default language")]
            public string Html { get; set; }

            [Option('l', "lang", Required = false, HelpText = "default language for HTML", Default="en")]
            public string Language { get; set; }
        }


        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Options>(args).WithParsed(
                o =>
                {
                    SurveyData surveyData = new SurveyData();
                    surveyData.LoadData(o.Source);

                    if (o.DumpText != null)
                    {
                        surveyData.DumpSurveyText(o.DumpText);
                    }

                    if (o.Zip != null)
                    {
                        using (ZipArchive zip = new ZipArchive(new FileStream(o.Zip, FileMode.Create), ZipArchiveMode.Create))
                        {
                            foreach (string lang in surveyData.Languages.Keys)
                            {
                                ZipArchiveEntry entry = zip.CreateEntry($"output-{lang}.html");
                                using (Stream s = entry.Open())
                                {
                                    var perlang = new GenerateHtml(surveyData, lang);
                                    perlang.Generate(new StreamWriter(s, Encoding.UTF8), o.Template);
                                }
                            }
                        }
                    }

                    if (o.Html != null)
                    {
                        var plain = new GenerateHtml(surveyData, o.Language);
                        plain.Generate(o.Html, o.Template);
                    }
                });
        }
    }
}