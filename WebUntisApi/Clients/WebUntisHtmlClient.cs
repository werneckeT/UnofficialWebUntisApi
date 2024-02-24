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

        public Task<WeekModel> RetrieveClassData(string cookieKey, int classId)
        {
            var untisContent = SeleniumReadContent(cookieKey, classId);
            var weekModel = Parse(untisContent);
            return Task.FromResult(weekModel);
        }

        public static async Task PrintData()
        {
            var untisContent = SeleniumReadContent();
            var weekModel = Parse(untisContent);
            foreach (var day in weekModel.Days ?? new List<DayModel>())
            {
                Console.WriteLine($"Day: {day.SchoolDay}");
                foreach (var subject in day.Subjects ?? new List<SubjectModel>())
                {
                    Console.WriteLine($"Subject: {subject.Name}, Room: {subject.Room}, Time: {subject.StartTime}, Info: {subject.AdditionalInformation}");
                }
            }
        }

        private static WeekModel Parse(string content)
        {
            var week = new WeekModel { Days = new List<DayModel>() };
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(content);
            var containerNode = htmlDoc.DocumentNode.SelectSingleNode(HtmlContentXPath);

            var dayMapping = new Dictionary<string, SchoolDayEnum>
            {
                {"Monday", SchoolDayEnum.Monday},
                {"Tuesday", SchoolDayEnum.Tuesday},
                {"Wednesday", SchoolDayEnum.Wednesday},
                {"Thursday", SchoolDayEnum.Thursday},
                {"Friday", SchoolDayEnum.Friday},
            };

            var linkNodes = containerNode?.SelectNodes(".//a");

            if (linkNodes == null)
                return week;

            foreach (var linkNode in linkNodes)
            {
                var href = linkNode.GetAttributeValue("href", "");
                var (date, extractedStartTime, extractedEndTime) = ExtractDateTimeFromHref(href);
                if (!date.HasValue) continue;

                var dayOfWeekStr = date.Value.ToString("dddd", CultureInfo.InvariantCulture);
                if (!dayMapping.TryGetValue(dayOfWeekStr, out var schoolDay))
                {
                    schoolDay = SchoolDayEnum.Unknown;
                }

                var entryDiv = linkNode.SelectSingleNode(".//div[contains(@class,'renderedEntry')]");
                var spans = entryDiv?.SelectNodes(".//span");
                if (spans is null || entryDiv == null) continue;

                var subjectName = spans[0].InnerText.Trim();
                var roomNumber = spans.Count > 1 ? spans[1].InnerText.Trim() : string.Empty;
                var style = entryDiv.GetAttributeValue("style", string.Empty);
                var status = DetermineStatusFromStyle(style);

                var subjectModel = new SubjectModel
                {
                    Name = subjectName,
                    Room = roomNumber,
                    StartTime = extractedStartTime,
                    EndTime = extractedEndTime,
                    AdditionalInformation = status
                };

                var dayModel = week.Days.FirstOrDefault(d => d.SchoolDay == schoolDay);
                if (dayModel == null)
                {
                    dayModel = new DayModel
                    {
                        SchoolDay = schoolDay,
                        Subjects = new List<SubjectModel>()
                    };
                    week.Days.Add(dayModel);
                }

                dayModel.Subjects?.Add(subjectModel);
                dayModel.Subjects = dayModel.Subjects?.OrderBy(s => s.StartTime).ToList();
            }

            return week;
        }

        private static (DateTime? Date, DateTime? StartTime, DateTime? EndTime) ExtractDateTimeFromHref(string href)
        {
            try
            {
                href = Uri.UnescapeDataString(href);

                const string pattern = @"(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2}:\d{2})\+(\d{2}:\d{2})/(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2}:\d{2})\+(\d{2}:\d{2})";

                var regex = new Regex(pattern);
                var match = regex.Match(href);

                if (!match.Success) 
                    return (null, null, null);

                var startDate = match.Groups[1].Value;
                var startTime = match.Groups[2].Value;
                var startDateTimeStr = $"{startDate}T{startTime}+{match.Groups[3].Value}";
                var endDate = match.Groups[4].Value;
                var endTime = match.Groups[5].Value;
                var endDateTimeStr = $"{endDate}T{endTime}+{match.Groups[6].Value}";

                if (DateTime.TryParseExact(startDateTimeStr, "yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var startDateTime) &&
                    DateTime.TryParseExact(endDateTimeStr, "yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var endDateTime))
                {
                    return (startDateTime.Date, startDateTime.ToLocalTime(), endDateTime.ToLocalTime());
                }

                return (null, null, null);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return (null, null, null);
            }
        }

        private static string DetermineStatusFromStyle(string style)
        {
            // Extracting the background-color value
            var regex = DetermineStatusRegex();
            var match = regex.Match(style);
            if (!match.Success) 
                return "Unknown";

            var r = int.Parse(match.Groups[1].Value);
            var g = int.Parse(match.Groups[2].Value);
            var b = int.Parse(match.Groups[3].Value);

            return r switch
            {
                152 when g == 251 && b == 152 => "Takes Place",
                153 when g == 50 && b == 204 => "Dropped",
                240 when g == 128 && b == 128 => "Changed",
                _ => "Unknown"
            };
        }

        private static string SeleniumReadContent(string cookieKey = "timetable-state-BBS III Magdeburg-1", int classId = 2974)
        {
            using IWebDriver driver = new ChromeDriver();
            driver.Navigate().GoToUrl(WebUntisUrl);

            ((IJavaScriptExecutor)driver).ExecuteScript(
                $"localStorage.setItem('{cookieKey}', JSON.stringify({{\"id\":{classId},\"formatId\":4}}));");
            driver.Navigate().Refresh();

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
            wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".grupetWidgetTimetableEntryContent")));

            var pageContent = driver.PageSource;

            driver.Quit();

            return pageContent;
        }

        [GeneratedRegex(@"background-color: rgba\((\d+), (\d+), (\d+), (\d*\.?\d+)\)")]
        private static partial Regex DetermineStatusRegex();
    }
}