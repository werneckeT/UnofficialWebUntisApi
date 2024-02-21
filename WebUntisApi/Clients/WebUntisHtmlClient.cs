using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SeleniumExtras.WaitHelpers;
using WebUntisApi.Models;

namespace WebUntisApi.Clients
{
    public class WebUntisHtmlClient
    {
        private const string HtmlContentXPath =
            "/html/body/div/div/div/div[2]/div/div/section/div[1]/div/section/section/div/div/div[2]/div/div[1]/div/div[1]/div[9]/div/div[2]";

        private const string WebUntisUrl =
            "https://tipo.webuntis.com/WebUntis/?school=BBS+III+Magdeburg#/basic/timetable";

        public static void PrintData()
        {
            var untisContent = SeleniumReadContent();
            var entries = Parse(untisContent);
            foreach (var entry in entries)
            {
                Console.WriteLine($"Day: {entry.DayOfWeek}, Subject: {entry.SubjectName}, Room: {entry.RoomNumber}");
            }
        }

        private static List<ScheduleEntryModel> Parse(string content)
        {
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);
            var containerNode = htmlDoc.DocumentNode.SelectSingleNode(HtmlContentXPath);
            var linkNodes = containerNode?.SelectNodes(".//a");

            if (linkNodes == null)
                return new List<ScheduleEntryModel>();

            var entriesByLeftValue = new Dictionary<float, List<ScheduleEntryModel>>();

            foreach (var linkNode in linkNodes)
            {
                var entryDiv = linkNode.SelectSingleNode(".//div[contains(@class,'renderedEntry')]");
                var spans = entryDiv?.SelectNodes(".//span");

                if (spans is not { Count: >= 1 } || entryDiv == null)
                    continue;

                var subjectName = spans[0].InnerText.Trim();
                var roomNumber = spans.Count > 1 ? spans[1].InnerText.Trim() : string.Empty;
                var leftValue = ExtractLeftValueFromStyle(entryDiv.GetAttributeValue("style", string.Empty));

                var entryModel = new ScheduleEntryModel
                {
                    SubjectName = subjectName,
                    RoomNumber = roomNumber
                };

                if (!entriesByLeftValue.ContainsKey(leftValue))
                {
                    entriesByLeftValue[leftValue] = new List<ScheduleEntryModel>();
                }
                entriesByLeftValue[leftValue].Add(entryModel);
            }

            var orderedLeftValues = entriesByLeftValue.Keys.OrderBy(x => x).ToList();

            foreach (var leftValue in orderedLeftValues)
            {
                var dayIndex = orderedLeftValues.IndexOf(leftValue);
                var dayOfWeek = MapIndexToDayOfWeek(dayIndex);
                foreach (var entry in entriesByLeftValue[leftValue])
                {
                    entry.DayOfWeek = dayOfWeek;
                }
            }

            return entriesByLeftValue.SelectMany(pair => pair.Value).ToList();
        }

        private static float ExtractLeftValueFromStyle(string style)
        {
            var left = style.Split(';').FirstOrDefault(s => s.Contains("left"));
            var value = left?.Split(':').LastOrDefault()?.Replace("px", "").Trim();
            return float.TryParse(value, out var leftValue) ? leftValue : -1;
        }

        private static string MapIndexToDayOfWeek(int index)
        {
            return index switch
            {
                0 => "Monday",
                1 => "Tuesday",
                2 => "Wednesday",
                3 => "Thursday",
                4 => "Friday",
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
    }
}