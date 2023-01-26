using ExcelDataReader;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.IO;

namespace GenSurvey
{
    class TranslationUsage
    {
        public Dictionary<string, Dictionary<string, string>> AllTranslations = new Dictionary<string, Dictionary<string, string>>();
        public string DefaultLanguage;
        public HashSet<string> Languages = new HashSet<string>();
    }

    class Translation
    {
        static int idcounter = 0;

        HashSet<string> allLanguages = new HashSet<string>();
        int? id;

        public Translation()
        {
        }

        Dictionary<string, string> dictionary = new Dictionary<string, string>();

        public bool IsEmpty => dictionary.Count == 0 || dictionary.Values.All( x => string.IsNullOrEmpty(x)) ;

        public string SpanId => $"t{id}";

        public Dictionary<string,string> Values
        {
            get
            {
                return dictionary;
            }
        }

        public void AddTranslation(string lang, string value)
        {
            allLanguages.Add(lang);
            dictionary.Add(lang, value);
        }

        public void EnsureValue(string strValue, TranslationUsage usage)
        {
            if (strValue == null)
            {
                if (!id.HasValue)
                {
                    id = idcounter++;
                }

                strValue = $"t{id}";
            }

            foreach (var k in usage.Languages)
            {
                var v = dictionary[k];
                Dictionary<string, string> s;
                if (!usage.AllTranslations.TryGetValue(k, out s))
                {
                    s = new Dictionary<string, string>();
                    usage.AllTranslations.Add(k, s);
                }

                s[strValue] =  v;
            }
        }

        public string GetValueForLanguage(string lang)
        {
            if (dictionary.Count == 0)
            {
                return string.Empty;
            }

            return dictionary[lang];
        }

        public string GetDefaultText(TranslationUsage usage)
        {
            return dictionary[usage.DefaultLanguage];
        }

        public void writeSpan(IHtmlOutput output, TranslationUsage usage)
        {
            this.EnsureValue(null, usage);
            output.WriteTagWithInnerText("span", $"name='t{id}'", GetDefaultText(usage));
        }

        public static void WriteJson(IHtmlOutput output, TranslationUsage usage)
        {
            output.WriteLine($"var translations = {JsonConvert.SerializeObject(usage.AllTranslations, Formatting.Indented)}");
        }
    }


    class Question
    {
        public string type;

        public string listname;

        public string name;

        public Translation label = new Translation();

        public Translation hint = new Translation();

        public string relevant;

        public string appearance;
    }

    class Choice
    {
        public string list_name;
        public string name;
        public Translation label = new Translation();
    }

    class SurveyData
    {
        public readonly List<Question> Questions = new List<Question>();
        public readonly List<Choice> Choices = new List<Choice>();
        public readonly Dictionary<string, string> Languages = new Dictionary<string, string>();
        public readonly Dictionary<string, Translation> ExtraTranslation = new Dictionary<string, Translation>();

        static string CvtToString(object o)
        {
            if (o is DBNull)
            {
                return "";
            }
            else
            {
                return o.ToString();
            }
        }

        static string Crlf(string s)
        {
            return s.Replace("\n", "<br>");
        }

        static string GetLangCode(string s)
        {
            int start = s.IndexOf('(');
            int stop = s.IndexOf(')');

            return s.Substring(start + 1, stop - start - 1).ToLowerInvariant();
        }

        public void WriteLabel(StreamWriter writer, Translation label, string lang, string prefix)
        {
            if (lang == "en")
            {
                writer.WriteLine($"{prefix}{label.GetValueForLanguage(lang)}");
            }
            else
            {
                writer.WriteLine($"{prefix}{label.GetValueForLanguage(lang)}\t[{label.GetValueForLanguage("en")}]");
            }
        }

        public void DumpSurveyText(string filename)
        {
            using (var writer = new StreamWriter(filename, false, Encoding.UTF8))
            {
                writer.WriteLine(JsonConvert.SerializeObject(this, Formatting.Indented)); 
            }
        }

                /*
                foreach (string lang in this.Languages.Keys)
                {
                using (var writer = new StreamWriter(filename.Replace(".txt", "." + lang + ".txt"), false, Encoding.UTF8))
                {
                    foreach (var q in this.Questions)
                    {
                        if (!q.label.IsEmpty)
                        {
                            WriteLabel(writer, q.label, lang, "");
                        }

                        if (!q.hint.IsEmpty)
                        {
                            WriteLabel(writer, q.hint, lang, "");
                        }

                        foreach (var c in this.Choices)
                        {
                            if (c.list_name == q.listname)
                            {
                                WriteLabel(writer, c.label, lang, "");
                            }
                        }
                    }
                }
                */

        public void LoadData(string filename)
        {
            using (var reader = ExcelReaderFactory.CreateReader(System.IO.File.OpenRead(filename)))
            {
                var conf = new ExcelDataSetConfiguration
                {
                    ConfigureDataTable = _ => new ExcelDataTableConfiguration
                    {
                        UseHeaderRow = true
                    }
                };

                var dataSet = reader.AsDataSet(conf);

                var dataTable = dataSet.Tables["survey"];
                for (var i = 0; i < dataTable.Rows.Count; i++)
                {
                    var row = dataTable.Rows[i];

                    string type = CvtToString(row["type"]);

                    if (string.IsNullOrEmpty(type))
                    {
                        continue;
                    }

                    string[] typesplit = type.Split(' ');

                    string listname = null;

                    if (typesplit.Length == 2 && typesplit[0] == "select_one" || typesplit[0] == "select_multiple")
                    {
                        listname = typesplit[1];
                        type = typesplit[0];
                    }

                    string name = CvtToString(row["name"]);

                    Question q = new Question
                    {
                        type = type,
                        listname = listname,
                        name = CvtToString(row["name"]),
                        relevant = CvtToString(row["relevant"]),
                        appearance = CvtToString(row["appearance"]),
                    };

                    if (!string.IsNullOrEmpty(name))
                    {
                        foreach (DataColumn col in row.Table.Columns)
                        {
                            if (col.ColumnName.StartsWith("label::"))
                            {
                                string lang = CvtToString(col.ColumnName).Substring(7);
                                q.label.AddTranslation(GetLangCode(lang), Crlf(CvtToString(row[col])));
                                q.hint.AddTranslation(GetLangCode(lang), Crlf(CvtToString(row["hint::" + lang])));

                                this.Languages[GetLangCode(lang)] = lang;
                            }
                        }
                    }

                    if (q.name == "demo_region" || q.name == "demo_country" || q.relevant == "false")
                    {
                        continue;
                    }

                    this.Questions.Add(q);
                }

                var labels = dataSet.Tables["choices"];
                for (var i = 0; i < labels.Rows.Count; i++)
                {
                    var row = labels.Rows[i];

                    string listname = CvtToString(row["list_name"]);

                    if (string.IsNullOrEmpty(listname))
                    {
                        continue;
                    }

                    string name = CvtToString(row["name"]);

                    var choice = new Choice
                    {
                        list_name = CvtToString(row["list_name"]),
                        name = CvtToString(row["name"]),
                    };

                    foreach (DataColumn col in row.Table.Columns)
                    {
                        if (col.ColumnName.StartsWith("label::"))
                        {
                            string lang = CvtToString(col.ColumnName).Substring(7);
                            choice.label.AddTranslation(GetLangCode(lang), CvtToString(row[col]));
                        }
                    }

                    if (choice.list_name == "extratranslation")
                    {
                        this.ExtraTranslation.Add(choice.name, choice.label);
                    }

                    this.Choices.Add(choice);
                }
            }
        }
    }
}
