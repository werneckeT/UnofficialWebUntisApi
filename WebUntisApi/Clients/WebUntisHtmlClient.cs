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

        /// <summary>
        /// Initializes a new instance of the <see cref="WebUntisHtmlClient"/> class.
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <param name="logger">The logger.</param>
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
        /// <summary>
        /// Retrieves a web untis week model asynchronously based on the provided cookie key and class id.
        /// </summary>
        /// <param name="cookieKey">The cookie key to authenticate the request.</param>
        /// <param name="classId">The ID of the class for which data should be retrieved.</param>
        /// <returns>A task that resolves with a WebUntisWeekModel instance, containing the retrieved class data.</returns>
        public Task<WebUntisWeekModel> RetrieveClassDataAsync(string cookieKey, int classId)
        {
            var untisContent = SeleniumReadContent(cookieKey, classId);
            var weekModel = ParseWebUntisContent(untisContent);
            return Task.FromResult(weekModel);
        }

        /// <summary>
        /// Retrieves class data for a specified week from WebUntis.
        /// </summary>
        /// <param name="cookieKey">The cookie key used for authentication.</param>
        /// <param name="classId">The ID of the class to retrieve data for.</param>
        /// <param name="weekOffset">The offset of the week to retrieve data for (0-based).</param>
        /// <returns>A Task that resolves with a WebUntisWeekModel instance containing the retrieved class data.</returns>
        public Task<WebUntisWeekModel> RetrieveClassDataForWeekAsync(string cookieKey, int classId, int weekOffset)
        {
            var untisContent = SeleniumReadContent(cookieKey, classId, weekOffset);
            return Task.FromResult(ParseWebUntisContent(untisContent));
        }

        //Selenium Interactions
        /// <summary>
        /// Reads the content of the Selenium web driver for a given class ID and week offset.
        /// </summary>
        /// <param name="cookieKey">The key for the cookie to be set.</param>
        /// <param name="classId">The ID of the class.</param>
        /// <param name="weekOffset">Optional offset for navigating to a specific week (default is 0).</param>
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

        /// Navigates to the specified week by clicking next or previous buttons accordingly.
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
        /// <summary>
        /// Parses the provided WebUntis content and populates a WebUntisWeekModel with the extracted data.
        /// </summary>
        /// <param name="content">The WebUntis content to be parsed.</param>
        /// <returns>A populated WebUntisWeekModel object.</returns>
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

        /// <summary>
        /// Processes a link node in an HTML document representing a week of school days.
        /// </summary>
        /// <param name="linkNode">The link node to process.</param>
        /// <param name="week">The model representing the week of school days.</param>
        /// <param name="dayMapping">A dictionary mapping day of the week strings to WebUntisSchoolDayEnum values.</param>
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

        /// <summary>
        /// Extracts a WebUntisRenderEntryModel from an HTML entry div.
        /// </summary>
        /// <param name="entryDiv">The HTML entry div to extract the model from.</param>
        /// <param name="startTime">The start time of the entry, or null if unknown.</param>
        /// <param name="endTime">The end time of the entry, or null if unknown.</param>
        /// <returns>A WebUntisRenderEntryModel representing the extracted data, or null if unable to extract.</returns>
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
        /// <summary>
        /// Returns a dictionary mapping day names to their corresponding enum values.
        /// </summary>
        /// <returns>A dictionary containing day names as keys and their corresponding enum values as values.</returns>
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

        /// <summary>
        /// Adds a subject to the week model for the specified school day.
        /// </summary>
        /// <param name="week">The week model.</param>
        /// <param name="schoolDay">The school day enum.</param>
        /// <param name="subjectModel">The subject model.</param>
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
        /// <summary>
        /// Extracts date and time from a given URI href.
        /// </summary>
        /// <param name="href">The URI href to extract the date and time from.</param>
        /// <returns>A tuple containing the start date, start time, and end time as DateTime objects, or (null, null, null) if extraction fails.</returns>
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

        /// Determines the status of an entry from a given style string.
        ///
        /// This method uses regular expressions to extract values from the input style string and then
        /// matches these values against specific conditions to determine the corresponding entry status.
        ///
        /// Parameters:
        /// <param name="style">The style string to parse.</param>
        ///
        /// Returns:
        /// The determined entry status as a WebUntisRenderEntryStatusEnum value.
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
