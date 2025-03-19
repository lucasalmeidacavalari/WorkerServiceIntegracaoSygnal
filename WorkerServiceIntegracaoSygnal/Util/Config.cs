using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WorkerServiceIntegracaoSygnal.Util
{
    public class Config
    {
        public static string GetConnectionStrings(string attribute)
        {
            var value = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build().GetSection("ConnectionString")[attribute];
            return value;
        }
        public static string GetAppSettings(string attribute)
        {
            var value = new ConfigurationBuilder()
                        .AddJsonFile("appsettings.json")
                        .Build().GetSection("ServiceConfigurations")[attribute];
            return value;
        }
    }
}
