using Serilog;
using Serilog.Events;
using WorkerServiceIntegracaoSygnal.Util;


namespace WorkerServiceIntegracaoSygnal
{
    public class Program
    {
        public static void Main(string[] args)
        {
            string localGravacaoLog = Config.GetAppSettings("LocalArquivoLog");

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(localGravacaoLog, rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7, outputTemplate: "{Message}{NewLine}")
                .CreateLogger();

            try
            {
                Log.Information("Inicializando o serviço...");
                CreateHostBuilder(args).Build().Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Erro fatal na inicialização do serviço");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHostedService<Worker>();
                });
    }
}
