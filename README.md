# Unofficial WebUntis API

## Disclaimer
This project is an unofficial API and is not affiliated with the official WebUntis service. It has been created by scraping the WebUntis website. Note that this means functionality could break if the WebUntis webpage structure changes.

## Introduction
This .NET integration allows easy access to scheduling information from WebUntis platforms, enabling users to retrieve weekly schedules and class data dynamically.

## Features
- Retrieve current and specific week schedules for classes.
- Configurable WebUntis URL to adapt to different schools.
- Built-in logging for monitoring and debugging.

## Configuration
Update your `appsettings.json` with the WebUntis URL of your school:

```json
{
  "WebUntisSettings": {
    "WebUntisUrl": "https://tipo.webuntis.com/WebUntis/?school=YOUR_SCHOOL_NAME"
  }
}
```

Replace YOUR_SCHOOL_NAME with your actual school's WebUntis details.

## Installation
This project requires .NET 7.0 and ChromeDriver. 

### Installing .NET 7.0
For detailed instructions on how to install .NET 7.0, visit [Microsoft's official .NET 7.0 documentation](https://dotnet.microsoft.com/en-us/download/dotnet/7.0).

### Installing ChromeDriver
For detailed instructions on how to install ChromeDriver, visit [ChromeDriver - WebDriver for Chrome](https://chromedriver.chromium.org/getting-started).

Ensure ChromeDriver is in your PATH and follow these steps:

### Restore dependencies:

```bash
dotnet restore
```

### Build the project:

```bash
dotnet build
```

### Usage
Run the project:

```bash
dotnet run
```

## Data Models
Below are the structures for the primary data models used in this API:

| Model                     | Property              | Type                           | Description                            | Example Value          |
|---------------------------|-----------------------|--------------------------------|----------------------------------------|------------------------|
| WebUntisWeekModel         | Days                  | List\<WebUntisDayModel\>       | List of days with schedule entries     | [See WebUntisDayModel] |
| WebUntisDayModel          | WebUntisSchoolDay     | WebUntisSchoolDayEnum          | Enum for the day of the week           | Monday                 |
|                           | Subjects              | List\<WebUntisRenderEntryModel\> | List of subjects for the day | [See WebUntisRenderEntryModel] |
| WebUntisRenderEntryModel  | Name                  | string                         | Name of the subject                    | Mathematics            |
|                           | Room                  | string                         | Room where the subject is held         | 101                    |
|                           | StartTime             | DateTime?                      | Start time of the subject              | 2022-03-15T08:00:00    |
|                           | EndTime               | DateTime?                      | End time of the subject                | 2022-03-15T08:45:00    |
|                           | RenderEntryStatus     | WebUntisRenderEntryStatusEnum  | Status of the schedule entry           | TakesPlace             |

## Enum Definitions

### WebUntisSchoolDayEnum
| Value     | Description   |
|-----------|---------------|
| Monday    | Represents Monday |
| Tuesday   | Represents Tuesday |
| Wednesday | Represents Wednesday |
| Thursday  | Represents Thursday |
| Friday    | Represents Friday |
| Unknown   | Represents an unknown or invalid day |

### WebUntisRenderEntryStatusEnum
| Value     | Description   |
|-----------|---------------|
| Unknown   | Status is unknown or not provided |
| TakesPlace | The class or event is scheduled to take place |
| Dropped   | The class or event has been dropped from the schedule |
| Changed   | The class or event has been changed from its original schedule |

## Getting CookieKey and ClassId for Schedule Requests
To retrieve the necessary `cookieKey` and `classId` for requesting schedules:

1. Visit the WebUntis page for your school and select the class you wish to scrape.
2. Once the page has loaded, open your browser's developer tools by pressing `F12`.
3. Navigate to the `Application` section.
4. Under the 'Local Storage' entry, click on the URL of your school's WebUntis page.
5. You should see several keys and values. The required `cookieKey` typically relates to your session, and the `classId` is found within the value for this key.

## Contributing
Contributions are welcome! Please fork the repository, make your changes, and submit a pull request.
