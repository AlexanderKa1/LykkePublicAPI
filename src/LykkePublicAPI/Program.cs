﻿using System.IO;
using Microsoft.AspNetCore.Hosting;

namespace LykkePublicAPI
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var host = new WebHostBuilder()
                .UseKestrel(opts =>
                {
                    opts.ThreadCount = 1;
                })
                .UseContentRoot(Directory.GetCurrentDirectory())
                .UseIISIntegration()
                .UseStartup<Startup>()
                .Build();

            host.Run();
        }
    }
}
