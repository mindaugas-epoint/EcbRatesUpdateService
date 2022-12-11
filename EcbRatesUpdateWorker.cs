using Database_Access_Layer;
using EcbRates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using SendEmail;
using Serilog;

namespace EcbRatesUpdateService
{
    public class EcbRatesUpdateWorker : BackgroundService
    {
        private readonly IDbContext _DbContext;
        private readonly ILogger _Logger;
        private readonly ISendEmail _SendEmail;
        private readonly IConfiguration _Config;
        private static IEcbRates _EcbRates;

        public EcbRatesUpdateWorker(IDbContext dbContext, ILogger logger, ISendEmail sendEmail, IEcbRates ecbRates, IConfiguration config)
        {
            _DbContext = dbContext;
            _Logger = logger;
            _SendEmail = sendEmail;
            _EcbRates= ecbRates;
            _Config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            DBConfig dbConfig = _Config.GetSection("DbConfig").Get<DBConfig>();
            EmailConfig emailConfig = new EmailConfig(_Config.GetSection("EmailConfig")["EmailFrom"],
                                                        _Config.GetSection("EmailConfig")["SendgridApiKey"],
                                                        _Config.GetSection("EmailConfig:EmailReceivers").Get<IEnumerable<EmailReceiver>>());

            DateTime startTime = DateTime.Parse(_Config.GetSection("ServiceSettings")["StartTime"], System.Globalization.CultureInfo.CurrentCulture);
            DateTime stopTime = DateTime.Parse(_Config.GetSection("ServiceSettings")["StopTime"], System.Globalization.CultureInfo.CurrentCulture);

            while (!stoppingToken.IsCancellationRequested)
            {
                DateTime currentTime = DateTime.Now;

                if (currentTime > startTime && currentTime < stopTime)
                {
                    _Logger.Information("ECB rates update started.");
                    try
                    {
                        if (dbConfig.SqlServerType == "mysql")
                        {
                            await _EcbRates.UpdateEcbRatesOneByOne(_DbContext);
                        }
                        else
                        {
                            await _EcbRates.UpdateEcbRates(_DbContext);
                        }

                        _Logger.Information("ECB rates update successfully completed.");
                        try
                        {
                            bool emailSuccess = await _SendEmail.SendEmail(emailConfig, "ECB rates update - SUCCESS", "ECB rate update successfully completed.");
                            if (emailSuccess)
                                _Logger.Information("Successful ECB rates update Email Notification message sent.");
                        }
                        catch (Exception ex)
                        {
                            _Logger.Error(ex.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        startTime = currentTime.AddMinutes(5);
                        stopTime = currentTime.AddMinutes(6);

                        _Logger.Error(ex.ToString());

                        _Logger.Information("Next DB Update retry at {ServiceStartStopTime.StartTime}");

                        try
                        {
                            bool emailSuccess = await _SendEmail.SendEmail(emailConfig, "eKeitykla DbUpdate - FAILURE", "eKeitykla DB Update failed.");
                            if (emailSuccess)
                                _Logger.Information("Failed eKeitykla DB Update Email Notification message sent.");
                        }
                        catch
                        {
                            _Logger.Error(ex.ToString());
                        }
                    }
                }
                await Task.Delay(60000, stoppingToken);
            }


        }
    }
}
