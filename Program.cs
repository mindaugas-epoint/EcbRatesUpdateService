using System.Reflection;
using Database_Access_Layer;
using SendEmail;
using Serilog;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace EcbRatesUpdateService
{
    class Program
    {
        public static void Main(string[] args)
        {
            string connectionString;
            var environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            IConfiguration config = new ConfigurationBuilder()
                    .SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location))
                    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
                    .Build();

            DBConfig dbConfig = config.GetSection("DbConfig").Get<DBConfig>();
            string seqServerUlr = config.GetSection("SeqConfig")["ServerUrl"].ToString();
           
            var assemblyInfo = Assembly.GetExecutingAssembly().GetName();
            var host = Host.CreateDefaultBuilder(args)
                .UseSerilog((context, provider, loggerConfiguration) => 
                {
                    loggerConfiguration
                    .ReadFrom.Configuration(context.Configuration)
                    .Enrich.FromLogContext()
                    .Enrich.WithProperty("Assembly", $"{assemblyInfo.Name}")
                    .WriteTo.Console()
                    .WriteTo.Seq(seqServerUlr);
                })
                .ConfigureServices(services =>
                { 
                    if (dbConfig.SqlServerType == "mysql")
                    {
                        connectionString = $"Server={dbConfig.ServerName};Database={dbConfig.DatabaseName};User ID={dbConfig.UserName};Password={dbConfig.Password};CHARSET=utf8;";
                        services.AddSingleton<IDbContext>(sp => new MySqlDB(connectionString));
                    } 
                    else
                    {
                        connectionString = $"Data Source={dbConfig.ServerName};Initial Catalog={dbConfig.DatabaseName};Persist Security Info=True;Encrypt=True;TrustServerCertificate=True;User ID={dbConfig.UserName};Password={dbConfig.Password}";
                        services.AddSingleton<IDbContext>(sp => new MsSqlDB(connectionString));
                    }
                    services.AddSingleton<ISendEmail, SendEmailUsingSendGrid>();
                    services.AddSingleton<IEcbRatesUpdate, EcbRatesUpdate>();
                    services.Configure<EcbRatesUpdateWorker>(config);
                    services.AddHostedService<EcbRatesUpdateWorker>();
                })
                .Build();

            host.Run();   
        }

    }
}

