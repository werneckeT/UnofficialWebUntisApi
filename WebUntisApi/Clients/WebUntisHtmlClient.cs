using System.Globalization;
using HtmlAgilityPack;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Support.UI;
using OpenQA.Selenium;
using SeleniumExtras.WaitHelpers;
using WebUntisApi.Models;
using System.Text.RegularExpressions;
using WebUntisApi.Models.Enums;

namespace WebUntisApi.Clients
{
    public partial class WebUntisHtmlClient
    {
        private readonly ILogger<WebUntisHtmlClient> _logger;

        private readonly string _htmlContentXPath;
        private readonly string _currentWeekXPath;
        private readonly string _nextWeekButtonXPath;
        private readonly string _prevWeekButtonXPath;
        private readonly string _webUntisUrl;

        public WebUntisHtmlClient(IConfiguration configuration, ILogger<WebUntisHtmlClient> logger)
        {
            _logger = logger;

            _htmlContentXPath = configuration["WebUntisSettings:HtmlContentXPath"] ?? throw new InvalidOperationException($"Could not find WebUntisSettings:HtmlContentXPath");
            _currentWeekXPath = configuration["WebUntisSettings:CurrentWeekXPath"] ?? throw new InvalidOperationException($"Could not find WebUntisSettings:CurrentWeekXPath");
            _nextWeekButtonXPath = configuration["WebUntisSettings:NextWeekButtonXPath"] ?? throw new InvalidOperationException($"Could not find WebUntisSettings:NextWeekButtonXPath");
            _prevWeekButtonXPath = configuration["WebUntisSettings:PrevWeekButtonXPath"] ?? throw new InvalidOperationException($"Could not find WebUntisSettings:PrevWeekButtonXPath");
            _webUntisUrl = configuration["WebUntisSettings:WebUntisUrl"] ?? throw new InvalidOperationException($"Could not find WebUntisSettings:WebUntisUrl");
        }

        // Public Methods
        public Task<WebUntisWeekModel> RetrieveClassDataAsync(string cookieKey, int classId)
        {
            var untisContent = SeleniumReadContent(cookieKey, classId);
            var weekModel = ParseWebUntisContent(untisContent);
            return Task.FromResult(weekModel);
        }

        public Task<WebUntisWeekModel> RetrieveClassDataForWeekAsync(string cookieKey, int classId, int weekOffset)
        {
            var untisContent = SeleniumReadContent(cookieKey, classId, weekOffset);
            return Task.FromResult(ParseWebUntisContent(untisContent));
        }

        //Selenium Interactions
        private string SeleniumReadContent(string cookieKey, int classId, int weekOffset = 0)
        {
            var chromeOptions = new ChromeOptions();
            chromeOptions.AddArguments("--log-level=3");
            chromeOptions.AddArguments("--silent");
            chromeOptions.AddArguments("--disable-logging");
            chromeOptions.AddExcludedArgument("enable-logging");

            var chromeDriverService = ChromeDriverService.CreateDefaultService();
            chromeDriverService.SuppressInitialDiagnosticInformation = true;
            chromeDriverService.HideCommandPromptWindow = true;


            using IWebDriver driver = new ChromeDriver(chromeDriverService, chromeOptions);
            driver.Navigate().GoToUrl(_webUntisUrl);

            ((IJavaScriptExecutor)driver).ExecuteScript(
                $"localStorage.setItem('{cookieKey}', JSON.stringify({{\"id\":{classId},\"formatId\":4}}));");
            driver.Navigate().Refresh();

            if (weekOffset != 0)
                NavigateToWeek(driver, weekOffset);

            try
            {
                var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));
                wait.Until(ExpectedConditions.ElementIsVisible(By.CssSelector(".grupetWidgetTimetableEntryContent")));
            }
            catch (WebDriverTimeoutException)
            {
                _logger.LogError("Request timed out, there are probably no elements scheduled for the desired week");
            }

            var pageContent = driver.PageSource;
            driver.Quit();
            return pageContent;
        }

        private void NavigateToWeek(IWebDriver driver, int weekOffset)
        {
            for (var i = 0; i < Math.Abs(weekOffset); i++)
            {
                switch (weekOffset)
                {
                    case > 0:
                        driver.FindElement(By.XPath(_nextWeekButtonXPath)).Click();
                        break;
                    case < 0:
                        driver.FindElement(By.XPath(_prevWeekButtonXPath)).Click();
                        break;
                }

                new WebDriverWait(driver, TimeSpan.FromSeconds(5)).Until(ExpectedConditions.ElementIsVisible(By.XPath(_currentWeekXPath)));
            }
        }

        // Data Processing
        private WebUntisWeekModel ParseWebUntisContent(string content)
        {
            try
            {
                var week = new WebUntisWeekModel { Days = new List<WebUntisDayModel>() };
                var htmlDoc = new HtmlDocument();
                htmlDoc.LoadHtml(content);
                var containerNode = htmlDoc.DocumentNode.SelectSingleNode(_htmlContentXPath);

                var dayMapping = GetDayMapping();
                var linkNodes = containerNode?.SelectNodes(".//a") ?? Enumerable.Empty<HtmlNode>();

                foreach (var linkNode in linkNodes)
                {
                    ProcessLinkNode(linkNode, week, dayMapping);
                }

                return week;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing WebUntisHtmlContent");
                return new WebUntisWeekModel();
            }
        }

        private void ProcessLinkNode(HtmlNode linkNode, WebUntisWeekModel week, IReadOnlyDictionary<string, WebUntisSchoolDayEnum> dayMapping)
        {
            var (date, extractedStartTime, extractedEndTime) = ExtractDateTimeFromHref(linkNode.GetAttributeValue("href", string.Empty));

            if (!date.HasValue) 
                return;

            var dayOfWeekStr = date.Value.ToString("dddd", CultureInfo.InvariantCulture);
            var schoolDay = dayMapping.GetValueOrDefault(dayOfWeekStr, WebUntisSchoolDayEnum.Unknown);

            var entryDiv = linkNode.SelectSingleNode(".//div[contains(@class,'renderedEntry')]");

            if (entryDiv == null)
                return;

            var subjectModel = ExtractSubjectModelFromEntry(entryDiv, extractedStartTime, extractedEndTime);

            if (subjectModel != null) 
                AddSubjectToWeekModel(week, schoolDay, subjectModel);
        }

        private static WebUntisRenderEntryModel? ExtractSubjectModelFromEntry(HtmlNode entryDiv, DateTime? startTime, DateTime? endTime)
        {
            var spans = entryDiv.SelectNodes(".//span");
            if (spans == null || spans.Count < 1) 
                return null;

            var subjectName = spans[0].InnerText.Trim();
            var roomNumber = spans.Count > 1 ? spans[1].InnerText.Trim() : string.Empty;
            var style = entryDiv.GetAttributeValue("style", string.Empty);
            var status = DetermineStatusFromStyle(style);

            return new WebUntisRenderEntryModel
            {
                Name = subjectName,
                Room = roomNumber,
                StartTime = startTime,
                EndTime = endTime,
                RenderEntryStatus = status
            };
        }

        // Utility Methods
        private static Dictionary<string, WebUntisSchoolDayEnum> GetDayMapping()
        {
            return new Dictionary<string, WebUntisSchoolDayEnum>
            {
                {"Monday", WebUntisSchoolDayEnum.Monday},
                {"Tuesday", WebUntisSchoolDayEnum.Tuesday},
                {"Wednesday", WebUntisSchoolDayEnum.Wednesday},
                {"Thursday", WebUntisSchoolDayEnum.Thursday},
                {"Friday", WebUntisSchoolDayEnum.Friday}
            };
        }

        private static void AddSubjectToWeekModel(WebUntisWeekModel week, WebUntisSchoolDayEnum schoolDay, WebUntisRenderEntryModel subjectModel)
        {
            if (week.Days == null) 
                return;

            var dayModel = week.Days.FirstOrDefault(d => d.WebUntisSchoolDay == schoolDay) ?? new WebUntisDayModel
            {
                WebUntisSchoolDay = schoolDay,
                Subjects = new List<WebUntisRenderEntryModel>()
            };

            if (!week.Days.Contains(dayModel))
            {
                week.Days.Add(dayModel);
            }

            dayModel.Subjects?.Add(subjectModel);

            if (dayModel.Subjects != null)
                dayModel.Subjects = dayModel.Subjects.OrderBy(s => s.StartTime).ToList();
        }

        // Extraction & Conversion
        private (DateTime? Date, DateTime? StartTime, DateTime? EndTime) ExtractDateTimeFromHref(string href)
        {
            try
            {
                href = Uri.UnescapeDataString(href);

                var regex = ExtractDateTimeRegex();
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
                _logger.LogError(ex, "Error Extracting the Datetime for href: {href}", href);
                return (null, null, null);
            }
        }

        private static WebUntisRenderEntryStatusEnum DetermineStatusFromStyle(string style)
        {
            var regex = DetermineStatusRegex();
            var match = regex.Match(style);
            if (!match.Success)
                return WebUntisRenderEntryStatusEnum.Unknown;

            var r = int.Parse(match.Groups[1].Value);
            var g = int.Parse(match.Groups[2].Value);
            var b = int.Parse(match.Groups[3].Value);

            return r switch
            {
                152 when g == 251 && b == 152 => WebUntisRenderEntryStatusEnum.TakesPlace,
                153 when g == 50 && b == 204 => WebUntisRenderEntryStatusEnum.Dropped,
                240 when g == 128 && b == 128 => WebUntisRenderEntryStatusEnum.Changed,
                _ => WebUntisRenderEntryStatusEnum.Unknown
            };
        }

        // Regex Methods
        [GeneratedRegex(@"background-color: rgba\((\d+), (\d+), (\d+), (\d*\.?\d+)\)")]
        private static partial Regex DetermineStatusRegex();

        [GeneratedRegex(@"(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2}:\d{2})\+(\d{2}:\d{2})/(\d{4}-\d{2}-\d{2})T(\d{2}:\d{2}:\d{2})\+(\d{2}:\d{2})")]
        private static partial Regex ExtractDateTimeRegex();
    }
}