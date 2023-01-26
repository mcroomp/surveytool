using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Web.UI;

namespace GenSurvey
{
    interface IHtmlOutput : IDisposable
    {
        IDisposable OpenTag(string tag, string parameters = null);
        void WriteTagWithInnerText(string tag, string parameters, string innerText);
        void WriteTag(string tag, string parameters = null);
        void WriteLine(string s);
    }

    class HtmlOutput : IHtmlOutput
    {
        TextWriter textWriter;
        int indent;

        public HtmlOutput(TextWriter textWriter)
        {
            this.textWriter = textWriter;
            this.indent = 0;
        }

        public IDisposable OpenTag(string tag)
        {
            this.textWriter.WriteLine($"<{tag}>");
            return new CloseTag(this, tag);
        }

        void WriteIndent()
        {
            for (int i = 0; i < indent; i++)
            {
                textWriter.Write(" ");
            }
        }

        public IDisposable OpenTag(string tag, string parameters)
        {
            this.WriteIndent();
            this.indent++;
            this.textWriter.WriteLine($"<{tag} {parameters ?? ""}>");
            return new CloseTag(this, tag);
        }

        public void WriteTagWithInnerText(string tag, string parameters, string innerText)
        {
            this.WriteIndent();
            this.textWriter.Write($"<{tag} {parameters ?? ""}>");
            this.textWriter.Write(innerText);
            this.textWriter.WriteLine($"</{tag}>");
        }

        public void WriteTag(string tag, string parameters)
        {
            this.WriteIndent();
            this.textWriter.WriteLine($"<{tag} {parameters ?? ""}/>");
        }

        public void WriteLine(string s)
        {
            this.textWriter.WriteLine(s);
        }

        public void Dispose()
        {
            this.textWriter.Dispose();
            this.textWriter = null;
        }

        struct CloseTag : IDisposable
        {
            public CloseTag(HtmlOutput output, string tag)
            {
                this.output = output;
                this.tag = tag;
            }

            string tag;
            HtmlOutput output;

            public void Dispose()
            {
                output.indent--;
                output.WriteIndent();
                output.textWriter.WriteLine($"</{tag}>");
            }
        }

    }

    class GenerateHtml
    {
        TranslationUsage usage;
        SurveyData surveyData;

        public GenerateHtml(SurveyData surveyData, string defaultLanguage)
        {
            this.surveyData = surveyData;
            this.usage = new TranslationUsage();
            this.usage.DefaultLanguage = defaultLanguage;
        }

        void WriteLabel(IHtmlOutput output, Question q)
        {
            q.label.writeSpan(output, this.usage);

            if (!q.hint.IsEmpty)
            {
                output.WriteLine($"<br><small>");
                q.hint.writeSpan(output, this.usage);
                output.WriteLine("</small>");
            }
        }

        void WriteSelect(Question q, List<Choice> choices, string type, IHtmlOutput output, int percentcomplete)
        {
            foreach (Choice c in choices)
            {
                if (c.list_name == q.listname)
                {
                    using (output.OpenTag("div", q.appearance.Contains("horizontal") ? "class='btn-group'" : "class='btn-group btn-group-vertical w-100'"))
                    {
                        using (output.OpenTag("label", "class='btn btn-outline-secondary inheritalign'"))
                        {
                            output.WriteTag("input", $"onclick='updateVisibility({percentcomplete})' type='{type}' name='{q.name}' value='{c.name}'");
                            c.label.writeSpan(output, this.usage);
                        }
                    }
                }
            }

        }

        class BreakGroup
        {
            bool inquestion = false;

            IHtmlOutput htmlOutput;

            public BreakGroup(IHtmlOutput h)
            {
                this.htmlOutput = h;
            }

            public void StartGroup()
            {
                htmlOutput.WriteLine("<div class='p-2 my-3 bg-white rounded shadow-sm'>");

                inquestion = false;
            }

            public void EndGroup()
            {
                htmlOutput.WriteLine("</div>");
            }

            public void StartQuestion()
            {
                if (inquestion)
                {
                    htmlOutput.WriteLine("</div>");
                    htmlOutput.WriteLine("<div class='p-2 my-3 bg-white rounded shadow-sm'>");
                }

                inquestion = true;
            }
        }

        public void Generate(string filename, string headerFile)
        {
            this.Generate(File.CreateText(filename), headerFile);
        }

        public void Generate(StreamWriter stream, string headerFile)
        {
            Dictionary<string,string> languages = surveyData.Languages;
            usage.Languages = languages.Keys.ToHashSet();

            using (var output = new HtmlOutput(stream))
            {
                using (output.OpenTag("html", "class='notranslate' translate='no'"))
                {
                    using (output.OpenTag("head"))
                    {
                        using (output.OpenTag("script"))
                        {
                            surveyData.ExtraTranslation["progress_1"].EnsureValue("progress_1", usage);
                            surveyData.ExtraTranslation["progress_2"].EnsureValue("progress_2", usage);
                            surveyData.ExtraTranslation["progress_3"].EnsureValue("progress_3", usage);

                            output.WriteLine($"var currentlanguage = [{this.usage.DefaultLanguage}];");

                            var langs = string.Join(",", from l in languages.Keys select '"' + l + '"');
                            output.WriteLine($"var languages = [{langs}];");

                            output.WriteLine("function updateVisibility(percentcomplete) { ");
                            foreach (Question q in surveyData.Questions)
                            {
                                if (!string.IsNullOrEmpty(q.relevant))
                                {

                                    string v = q.relevant;
                                    v = v.Replace("${", "getFieldValue('");
                                    v = v.Replace("}", "')");
                                    v = v.Replace("!=", "_ne_");
                                    v = v.Replace("=", "_eq_");
                                    v = v.Replace("_eq_", "===");
                                    v = v.Replace("_ne_", "!==");
                                    v = v.Replace(" and ", " && ");
                                    v = v.Replace(" or ", " || ");

                                    output.WriteLine($"document.getElementById('q_{q.name}').style.display = ({v}) ? 'block' : 'none';");
                                }
                            }
                            output.WriteLine("document.getElementById('submitbutton').style.display = (getFieldValue('consent')=='yes') ? 'block' : 'none'");
                            output.WriteLine("activityhappened(percentcomplete);");

                            output.WriteLine("}");
                        }

                        output.WriteLine(File.ReadAllText(headerFile, Encoding.UTF8));
                    }

                    BreakGroup breakgroup = new BreakGroup(output);

                    using (output.OpenTag("body", "onload='onload()' class='bg-info inheritalign'"))
                    {
                        output.WriteTagWithInnerText("noscript", null, "JavaScript debe estar habilitado para que la encuesta funcione");

                        using (output.OpenTag("div", "style='position:fixed; top: 0px; z-index: 99999;' class='progress bg-secondary w-100'"))
                        using (output.OpenTag("div", "id='progress' class='progress-bar progress-bar-striped bg-primary' role='progressbar' style='width: 0%' aria-valuenow='0' aria-valuemin='0' aria-valuemax='100'"))
                        {
                        }

                        using (output.OpenTag("main", "role='main' class='container'"))
                        using (output.OpenTag("form", "method='post'  onsubmit='return submitform();' id ='myform'"))
                        {
                            bool closegroup = false;
                            output.WriteLine("<div class='my-3 p-3 rounded shadow-sm bg-white'>");

                            using (output.OpenTag("div", "style='text-align: right'"))
                            {
                                surveyData.ExtraTranslation["language"].writeSpan(output, usage);

                                using (output.OpenTag("select", "id='languageselector' onchange='updateLanguage(this.value)'"))
                                {
                                    foreach (var l in languages)
                                    {
                                        output.WriteTagWithInnerText($"option", $"value='{l.Key}'", l.Value);
                                    }
                                }
                            }

                            Translation groupLabelToWrite = null;

                            for (int i = 0; i < surveyData.Questions.Count; i++)
                            {
                                var q = surveyData.Questions[i];

                                string style = "";
                                if (!string.IsNullOrEmpty(q.relevant))
                                {
                                    style = "style='display:none;'";
                                }

                                switch (q.type)
                                {
                                    case "begin group":
                                        if (!closegroup)
                                        {
                                            output.WriteLine("</div>");
                                            closegroup = true;
                                        }

                                        output.WriteLine($"<div id='q_{q.name}' {style}>");

                                        groupLabelToWrite = q.label;

                                        break;
                                    case "end group":
                                        breakgroup.EndGroup();

                                        output.WriteLine("</div>");

                                        break;
                                    case "hidden":
                                        break;
                                    case "note":

                                        using (output.OpenTag("div", $"id='q_{q.name}' style=\"background-size:contain; background-repeat:no-repeat; background-image: url('data:image/svg+xml;base64,PHN2ZyB3aWR0aD0iMjA0OCIgaGVpZ2h0PSIyMDQ4IiB2aWV3Qm94PSIwIDAgMjA0OCAyMDQ4IiB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjxwYXRoIHN0eWxlPSJmaWxsLW9wYWNpdHk6MC4wNjgyNzMwOTtmaWxsOiMwMDAwMDAiIGQ9Ik0xODAzIDk2MHEwIDUzLTM3IDkwbC02NTEgNjUycS0zOSAzNy05MSAzNy01MyAwLTkwLTM3bC02NTEtNjUycS0zOC0zNi0zOC05MCAwLTUzIDM4LTkxbDc0LTc1cTM5LTM3IDkxLTM3IDUzIDAgOTAgMzdsMjk0IDI5NHYtNzA0cTAtNTIgMzgtOTB0OTAtMzhoMTI4cTUyIDAgOTAgMzh0MzggOTB2NzA0bDI5NC0yOTRxMzctMzcgOTAtMzcgNTIgMCA5MSAzN2w3NSA3NXEzNyAzOSAzNyA5MXoiLz48L3N2Zz4=')\""))
                                        {
                                            using (output.OpenTag("div", "class='mt-2'"))
                                            {
                                                q.label.writeSpan(output, usage);
                                            }
                                        }
                                        break;
                                    case "select_one":
                                        using (output.OpenTag("div", $"id='q_{q.name}' class='p-2 my-2 bg-white rounded shadow-sm'"))
                                        {
                                            if (groupLabelToWrite != null)
                                            {
                                                using (output.OpenTag("div", "class='h4 border-gray mb-0'"))
                                                {
                                                    groupLabelToWrite.writeSpan(output, usage);
                                                }

                                                groupLabelToWrite = null;
                                            }

                                            using (output.OpenTag("div", "class='h6'"))
                                            {
                                                WriteLabel(output, q);
                                            }

                                            WriteSelect(q, surveyData.Choices, "radio", output, (i + 1) * 100 / (surveyData.Questions.Count - 1));
                                        }
                                        break;
                                    case "select_multiple":
                                        using (output.OpenTag("div", $"id='q_{q.name}' class='p-2 my-2 bg-white rounded shadow-sm'"))
                                        {
                                            if (groupLabelToWrite != null)
                                            {
                                                using (output.OpenTag("div", "class='h4 border-gray mb-0'"))
                                                {
                                                    groupLabelToWrite.writeSpan(output, usage);
                                                }

                                                groupLabelToWrite = null;
                                            }

                                            using (output.OpenTag("div", "class='h6'"))
                                            {
                                                WriteLabel(output, q);
                                            }

                                            WriteSelect(q, surveyData.Choices, "checkbox", output, (i + 1) * 100 / (surveyData.Questions.Count - 1));
                                        }
                                        break;
                                    case "text":
                                        using (output.OpenTag("div", $"id='q_{q.name}' class='p-2 my-2 bg-white rounded shadow-sm'"))
                                        {
                                            if (groupLabelToWrite != null)
                                            {
                                                using (output.OpenTag("div", "class='h4 border-gray mb-0'"))
                                                {
                                                    groupLabelToWrite.writeSpan(output, usage);
                                                }

                                                groupLabelToWrite = null;
                                            }

                                            using (output.OpenTag("div", "class='h6'"))
                                            {
                                                WriteLabel(output, q);
                                            }

                                            using (output.OpenTag("div", "class='input-group'"))
                                            {
                                                output.WriteTag($"input", $"class='form-control border border-secondary' type='text' name='{q.name}'");
                                            }
                                        }

                                        break;
                                }
                            }

                            using (output.OpenTag("div", "class='card' id='submitbutton' style='display:none;'"))
                            {
                                using (output.OpenTag("div", "class='card-header'"))
                                {
                                    using (output.OpenTag("button", "type='submit' class='btn btn-primary'"))
                                    {
                                        surveyData.ExtraTranslation["submit"].writeSpan(output, usage);
                                    }
                                }
                            }
                                
                            output.WriteLine("</div>");

                            output.WriteTag("input", "type='hidden' name='surveysource'");
                            output.WriteTag("input", "type='hidden' name='survey_channel'");
                            output.WriteTag("input", "type='hidden' name='survey_start_time'");
                            output.WriteTag("input", "type='hidden' name='survey_mid_time'");
                            output.WriteTag("input", "type='hidden' name='survey_end_time'");
                            output.WriteTag("input", "type='hidden' name='language'");
                        }

                        output.WriteLine("<script>");

                        Translation.WriteJson(output, usage);

                        output.WriteLine(@"</script>");
                    }
                }
            }
        }
    }
}
