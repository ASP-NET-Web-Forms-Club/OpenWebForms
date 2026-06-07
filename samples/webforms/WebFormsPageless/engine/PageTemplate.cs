using System.Text;
using System.Web;

namespace WebFormsPageless
{
    // Replaces the master page. Emits the shared shell: <head> + app-header
    // + open .container in GenerateHtmlHeader(); closes .container + body/html
    // (plus any page scripts) in GenerateHtmlFooter().
    public class PageTemplate
    {
        public string Title = "Book Library";
        public string Description = "Book Library CRUD — Pageless Web Forms.";
        public string ProjectLabel = "Pageless";

        // Raw HTML injected just before </head> and just before </body>.
        public string ExtraHeaderText = "";
        public string ExtraFooterText = "";

        public string GenerateHtmlHeader()
        {
            string encodedTitle = HttpUtility.HtmlEncode(Title);
            string encodedDesc = HttpUtility.HtmlEncode(Description);
            string encodedLabel = HttpUtility.HtmlEncode(ProjectLabel);

            StringBuilder sb = new StringBuilder();

            sb.Append(@"<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='utf-8' />
    <meta name='viewport' content='width=device-width, initial-scale=1.0' />
    <title>" + encodedTitle + @"</title>
    <meta name='description' content='" + encodedDesc + @"' />
    <link rel='stylesheet' href='/css/site.css' />
");

            if (ExtraHeaderText.Length > 0)
                sb.Append(ExtraHeaderText);

            sb.Append(@"</head>
<body>
    <header class='app-header'>
        <div class='inner'>
            <h1>📚 Book Library</h1>
            <span class='tag'>" + encodedLabel + @"</span>
        </div>
    </header>
    <div class='container'>
");

            return sb.ToString();
        }

        public string GenerateHtmlFooter()
        {
            StringBuilder sb = new StringBuilder();

            sb.Append(@"    </div>
");

            if (ExtraFooterText.Length > 0)
                sb.Append(ExtraFooterText);

            sb.Append(@"</body>
</html>");

            return sb.ToString();
        }
    }
}
