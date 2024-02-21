using System.Globalization;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SeleniumExtras.WaitHelpers;
using WebUntisApi.Models;
using System.Text.RegularExpressions;

namespace WebUntisApi.Clients
{
    public partial class WebUntisHtmlClient
    {
        private const string HtmlContentXPath =
            "/html/body/div/div/div/div[2]/div/div/section/div[1]/div/section/section/div/div/div[2]/div/div[1]/div/div[1]/div[9]/div/div[2]";

        private const string WebUntisUrl =
            "https://tipo.webuntis.com/WebUntis/?school=BBS+III+Magdeburg#/basic/timetable";

        public static async Task<List<ScheduleEntryModel>> PrintData()
        {
            var untisContent = SeleniumReadContent();
            var entries = Parse(untisContent);
            foreach (var entry in entries)
            {
                Console.WriteLine($"Day: {entry.DayOfWeek}, Subject: {entry.SubjectName}, Room: {entry.RoomNumber}, Status: {entry.Status}");
            }

            return entries;
        }

        private static List<ScheduleEntryModel> Parse(string content)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);
            var containerNode = htmlDoc.DocumentNode.SelectSingleNode(HtmlContentXPath);
            var linkNodes = containerNode?.SelectNodes(".//a");

            if (linkNodes == null)
                return new List<ScheduleEntryModel>();

            var entries = new List<ScheduleEntryModel>();

            foreach (var linkNode in linkNodes)
            {
                var href = linkNode.GetAttributeValue("href", "");
                var date = ExtractDateFromHref(href);
                if (!date.HasValue) continue; // Skip if no valid date is found

                var entryDiv = linkNode.SelectSingleNode(".//div[contains(@class,'renderedEntry')]");
                var spans = entryDiv?.SelectNodes(".//span");

                if (spans is not { Count: >= 1 }) 
                    continue;

                var subjectName = spans[0].InnerText.Trim();
                var roomNumber = spans.Count > 1 ? spans[1].InnerText.Trim() : string.Empty;
                var style = entryDiv.GetAttributeValue("style", string.Empty);
                var status = DetermineStatusFromStyle(style); // Determine status based on style

                entries.Add(new ScheduleEntryModel
                {
                    SubjectName = subjectName,
                    RoomNumber = roomNumber,
                    DayOfWeek = date.Value.ToString(CultureInfo.InvariantCulture),
                    Status = status
                });
            }

            return entries;
        }

        private static DateTime? ExtractDateFromHref(string href)
        {
            var regex = ExtractDateRegex();
            var match = regex.Match(href);

            if (match.Success && DateTime.TryParse(match.Groups[1].Value, out var date))
            {
                return date;
            }
            return null;
        }

        private static string DetermineStatusFromStyle(string style)
        {
            // Extracting the background-color value
            var regex = new Regex(@"background-color: rgba\((\d+), (\d+), (\d+), (\d*\.?\d+)\)");
            var match = regex.Match(style);
            if (!match.Success) 
                return "Unknown";

            var r = int.Parse(match.Groups[1].Value);
            var g = int.Parse(match.Groups[2].Value);
            var b = int.Parse(match.Groups[3].Value);

            return r switch
            {
                // Status: Takes Place - Light Green
                152 when g == 251 && b == 152 => "Takes Place",
                // Status: Dropped - Purple
                153 when g == 50 && b == 204 => "Dropped",
                // Status: Teacher missing - Light Coral
                240 when g == 128 && b == 128 => "Changed",
                _ => "Unknown"
            };
        }

        private static string SeleniumReadContent()
        {
            using IWebDriver driver = new ChromeDriver();
            driver.Navigate().GoToUrl(WebUntisUrl);

            ((IJavaScriptExecutor)driver).ExecuteScript(
                "localStorage.setItem('timetable-state-BBS III Magdeburg-1', JSON.stringify({\"id\":2974,\"formatId\":4}));");
            driver.Navigate().Refresh();

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".grupetWidgetTimetableEntryContent")));

            var pageContent = driver.PageSource;

            driver.Quit();

            return pageContent;
        }

        [GeneratedRegex(@"(\d{4}-\d{2}-\d{2})T")]
        private static partial Regex ExtractDateRegex();
    }
}