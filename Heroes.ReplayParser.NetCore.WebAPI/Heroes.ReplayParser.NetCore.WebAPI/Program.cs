using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;

namespace Heroes.ReplayParser.NetCore.WebAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var configurations = new ConfigurationBuilder()
		        .AddCommandLine(args)
		        .Build();
            new WebHostBuilder()
		        .UseConfiguration(configurations) 
		        .UseKestrel()
		        .UseContentRoot(Directory.GetCurrentDirectory())
		        .UseStartup<Startup>()
		        .Build()
		        .Run();
        }
    }
}
