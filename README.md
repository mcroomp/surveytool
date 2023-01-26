# Responsive Static-HTML Multi-Lingual survey generation tool

This is a tool for generating a responsive [Bootstrap](https://getbootstrap.com/docs/4.3/getting-started/introduction/)-based static HTML survey form. This works well for low-powered mobile devices and also allows you to put host your survey yourself. Includes scripts for collecting answers in a DynamoDB table or ESRI form.

The generated HTML supports automatic language detection and switching between languages (including right-to-left in Hebrew, Arabic and Farsi).

The data for the survey is read from an Excel file containing the questions and choices, in a format similar to that used by Survey123. 

**Disclaimer:**

**This tool is in no way affiliated with ESRI or Survey123, and only supports a subset of features in Survey123.**

The tool is written in C# and can be compiled using VisualStudio 2022 in the gensurvey directory.

The input to GenSurvey.exe is as follows:

  -i, --input             

Required. source template excel file

  -t, --templateheader    

Required. HTML to insert into head of survey. This contains some support Javascript and HTML metadata. Also contains the post-action URL which accepts the response with the answers to the questions.  

You need to have server-side support for the POST action to store the results somewhere. There an included server-side JavaScript in the Amazon Lambda for posting the answer either to an ESRI form or to a Amazon DynamoDB table.

  -z, --zip               

Creates ZIP archive with contents of survey for all languages

  -h, --html              

Creates html file with language specified

  -l, --lang              

(Default: en) default language for HTML

