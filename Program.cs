using System.Reflection;
using Database_Access_Layer;
using EcbRates;
using Logger;
using SendEmail;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EcbRatesUpdateService
{
    class Program
    {
        public class DBConfig
        {
            public string SqlServerType { get; set; }
            public string ServerName { get; set; }
            public string DatabaseName { get; set; }
            public string UserName { get; set; }
            public string Password { get; set; }
        }

        public static class ServiceStartStopTime
        {
            public static DateTime StartTime { get; set; }
            public static DateTime StopTime { get; set; }
        }

        class TimerState
        {
            public int Counter;
        }

        private static bool _manualStart;
        private static bool _retryOnFailedUpdate;
        private static ISendEmail _sendEmailService;
        private static IDbContext _dbContext;
        private static ILogger _logger;
        private static IEcbRates _ecbRates;
        private static IConfiguration _config;
        private static string _sqlServerType;

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            string connectionString;
            _config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .Build();

            DBConfig dbConfig = _config.GetSection("DbConfig").Get<DBConfig>();
            _sqlServerType = dbConfig.SqlServerType;
           
      
            var hostBuilder = Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureAppConfiguration((context, builder) =>
                {
                    builder.SetBasePath(Directory.GetCurrentDirectory());
                })
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ISendEmail, SendEmailUsingSendGrid>();
                    if (_sqlServerType == "mysql")
                    {
                        connectionString = $"Server={dbConfig.ServerName};Database={dbConfig.DatabaseName};User ID={dbConfig.UserName};Password={dbConfig.Password};CHARSET=utf8;";
                        services.AddSingleton<IDbContext>(sp => new MySqlDB(connectionString));
                    } 
                    else
                    {
                        connectionString = $"Data Source={dbConfig.ServerName};Initial Catalog={dbConfig.DatabaseName};Persist Security Info=True;Encrypt=True;TrustServerCertificate=True;User ID={dbConfig.UserName};Password={dbConfig.Password}";
                        services.AddSingleton<IDbContext>(sp => new MsSqlDB(connectionString));
                    }
                    
                    services.AddSingleton<ILogger, SeriLog>();
                    services.AddSingleton<IEcbRates, EcbRatesRep>();
                });

            return hostBuilder;
        }

        static async Task Main(string[] args)
        {
            var host = CreateHostBuilder(args).Build();
            using var serviceScope = host.Services.CreateScope();
            var provider = serviceScope.ServiceProvider;

            _sendEmailService = provider.GetRequiredService<ISendEmail>();
            _dbContext = provider.GetRequiredService<IDbContext>();
            _logger = provider.GetRequiredService<ILogger>();
            _ecbRates = provider.GetRequiredService<IEcbRates>();

            if (args.Length > 0)
            {
                if (args[0] == "now")
                {
                    _logger.WriteLog("Information", "Service started manualy for one time update.");

                    _manualStart = true;

                    await StartService(null);
                }
                else
                {
                    _logger.WriteLog("Error", "Wrong parameter provided. App terminated.");
                    Environment.Exit(-1);
                }
            }
            else
            {
                _logger.WriteLog("Information", "Service started.");

                _manualStart = false;

                var timer = new PeriodicTimer(TimeSpan.FromSeconds(60));
                while (await timer.WaitForNextTickAsync())
                {
                    await StartService(null);
                }
            }

        }

        static async Task StartService(object? timerState)
        {
            DateTime currentTime = DateTime.Now;

            EmailConfig emailConfig = new EmailConfig(_config.GetSection("EmailConfig")["EmailFrom"], 
                                                        _config.GetSection("EmailConfig")["SendgridApiKey"],
                                                        _config.GetSection("EmailConfig:EmailReceivers").Get<IEnumerable<EmailReceiver>>());

            if (!_retryOnFailedUpdate)
            {
                GetUpdateTime();
            }


            if ((currentTime > ServiceStartStopTime.StartTime && currentTime < ServiceStartStopTime.StopTime) || _manualStart)
            {
                _logger.WriteLog("Information", "ECB rates update started.");
                try
                {
                    if (_sqlServerType == "mysql")
                    {
                        await _ecbRates.UpdateEcbRatesOneByOne(_dbContext);
                    } 
                    else
                    {
                        await _ecbRates.UpdateEcbRates(_dbContext);
                    }

                    // Log success
                    _logger.WriteLog("Information", "ECB rates update successfully completed.");

                    // Send email success
                    try
                    {
                        bool emailSuccess = await _sendEmailService.SendEmail(emailConfig, "ECB rates update - SUCCESS", "ECB rate update successfully completed.");
                        if (emailSuccess)
                            _logger.WriteLog("Information", "Successful ECB rates update Email Notification message sent.");
                    }
                    catch (Exception ex)
                    {
                        _logger.WriteLog("Error", ex.ToString());
                    }

                    _retryOnFailedUpdate = false;
                }
                catch (Exception ex)
                {
                    _retryOnFailedUpdate = true;

                    // set start/stop time +5min on failed DBUpdate
                    ServiceStartStopTime.StartTime = currentTime.AddMinutes(5);
                    ServiceStartStopTime.StopTime = currentTime.AddMinutes(6);

                    // Log error
                    _logger.WriteLog("Error", ex.ToString());

                    _logger.WriteLog("Information", $"Next DB Update retry at {ServiceStartStopTime.StartTime}");

                    // Send email error
                    try
                    {
                        bool emailSuccess = await _sendEmailService.SendEmail(emailConfig, "eKeitykla DbUpdate - FAILURE", "eKeitykla DB Update failed.");
                        if (emailSuccess)
                            _logger.WriteLog("Information", "Failed eKeitykla DB Update Email Notification message sent.");
                    } 
                    catch 
                    {
                        _logger.WriteLog("Error", ex.ToString());
                    }
                }
            }
        }

        static void GetUpdateTime()
        {
            string startTime = _config.GetSection("ServiceSettings")["StartTime"];
            string stopTime = _config.GetSection("ServiceSettings")["StopTime"];

            ServiceStartStopTime.StartTime = DateTime.Parse(startTime, System.Globalization.CultureInfo.CurrentCulture);
            ServiceStartStopTime.StopTime = DateTime.Parse(stopTime, System.Globalization.CultureInfo.CurrentCulture);

        }

    }
}

