<div align="center">

# CourseClaimer.Wisedu

**CROSS PLATFORM** Auto course claiming software for Wisedu sites.

[![Language](https://img.shields.io/github/languages/top/MadLongTom/CourseClaimer.Wisedu
)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/github/license/MadLongTom/CourseClaimer.Wisedu.svg?logo=git&logoColor=red)](https://github.com/MadLongTom/CourseClaimer.Wisedu/blob/master/LICENSE.txt)
[![Repo Size](https://img.shields.io/github/repo-size/MadLongTom/CourseClaimer.Wisedu.svg?logo=github&logoColor=green&label=repo)](https://github.com/MadLongTom/CourseClaimer.Wisedu)
[![Commit Date](https://img.shields.io/github/last-commit/MadLongTom/CourseClaimer.Wisedu/master.svg?logo=github&logoColor=green&label=commit)](https://github.com/MadLongTom/CourseClaimer.Wisedu)

</div>

## *Important Notice*

Due to the traffic limitation of nginx and linux connection pool, if your account > 80, consider using **Legacy Mode** in <code>appsettings.json</code> 

## Annotations

![image](https://github.com/user-attachments/assets/9c51eaeb-f426-4f00-aa3a-a23e7311cd33)

## OpenTelemetry

Use **<code>Prometheus</code>** to manage tracing and metrics, modify <code>prometheus.yml</code>

```yml
# my global config
global:
  scrape_interval: 15s # Set the scrape interval to every 15 seconds. Default is every 1 minute.
  evaluation_interval: 15s # Evaluate rules every 15 seconds. The default is every 1 minute.
  # scrape_timeout is set to the global default (10s).

# Alertmanager configuration
alerting:
  alertmanagers:
    - static_configs:
        - targets:
          # - alertmanager:9093

# Load rules once and periodically evaluate them according to the global 'evaluation_interval'.
rule_files:
  # - "first_rules.yml"
  # - "second_rules.yml"

# A scrape configuration containing exactly one endpoint to scrape:
# Here it's Prometheus itself.
scrape_configs:
  # The job name is added as a label `job=<job_name>` to any timeseries scraped from this config.
  - job_name: "prometheus"

    # metrics_path defaults to '/metrics'
    # scheme defaults to 'http'.

    static_configs:
      - targets: ["IP:Port"]

```

## Configuration

In <code>appsettings.json</code>, edit your hostadresss, login port and database provider.

| Parameter                | Description                                                                                             |
|--------------------------|---------------------------------------------------------------------------------------------------------|
| BasePath                 | string: Host Address of the wisedu website                                                              |
| AuthPath                 | string: Login API path                                                                                  |
| DBProvider               | string: Provider of ClaimDbContext, SQLite/SQLServer/PosegreSQL are supported                           |
| DBProvider_CAP           | string: Provider of MessageBus Persistence,SQLite/InMemory/PostgreSQL are supported                     |
| ReLoginDelayMilliseconds | int: How many milliseconds does it take for an account to perform a re login after logging in elsewhere |
| CapTakeNum               | int: How many available accounts will simultaneously claim one course                                   |
| QuartzDelayMilliseconds  | int: Duration of scheduled tasks in non Legacy Mode                                                     |
| CronSchedule             | string: Time expression for Quartz tasks                                                                |
| UseQuartz                | bool: Use scheduled tasks                                                                               |
| LegacyMode               | bool: Use Add or MQ based List?                                                                         |
| PGSQL                    | string: connectionstring of ClaimDbContext (DBProvider=PostgreSQL)                                      |
| PGSQL_CAP                | string: connectionstring of MessageBus (DBProvider_CAP=PostgreSQL)                                      |
| GlobalExceptionList      | string: courses written in there will be discarded globally                                             |
| LimitListMillSeconds     | int: minimum delay of List API                                                                          |
| LimitAddMillSeconds      | int: minimum delay of Add API                                                                           |

And configure the AesKey

```csharp
builder.Services.AddSingleton<Aes>(inst =>
{
    var util = Aes.Create();
    util.Key = "MWMqg2tPcDkxcm11"u8.ToArray();
    return util;
});
```

## Build

```shell
cd ./CourseClaimer.Wisedu
dotnet restore
dotnet run
```

## Publish

```shell
dotnet publish
```

## Docker support

A ubuntu docker img with dotnet 8 sdk and opencv

for running this interestring software in linux docker

using <code>docker build -t heujwxk .</code> in cli to build docker img

and then use <code>docker-compose up -d</code> to start the project

the data will be saved in the db as the compose goes.

the docker-compose.yml just give a example, though it could be run.

## Usage

Add your information to the table tab, separating categories and courses with half width commas
