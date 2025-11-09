using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Project.Models;
using Project.Services.Abstractions;
using Project.Services.Implementations;
using Project.Workers; 
using System.Threading.Tasks;


public class Program
{
    public static async Task Main(string[] args)
    {
        var host = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config.AddJsonFile("settings.json", optional: false, reloadOnChange: true);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<OptimizationOptions>(context.Configuration.GetSection("OptimizationOptions"));
                services.Configure<FilePathsOptions>(context.Configuration.GetSection("FilePaths"));
                services.Configure<MqttOptions>(context.Configuration.GetSection("MqttOptions"));

                services.AddSingleton<IJsonFileService, JsonFileService>();
                services.AddSingleton<IDisplayService, DisplayService>();
                services.AddSingleton<ICalculationService, CalculationService>();
                services.AddSingleton<IOptimizationService, OptimizationService>();
                services.AddSingleton<IMqttService, MqttService>();

                // 注册为托管服务
                services.AddHostedService<MainWorker>();

                services.AddLogging(configure => configure.AddConsole());
            }).Build();

        //var jsonFileService = host.Services.GetRequiredService<IJsonFileService>();
        //var DisplayService = host.Services.GetRequiredService<IDisplayService>();
        //var CalculationService = host.Services.GetRequiredService<ICalculationService>();
        //var OptimizationService = host.Services.GetRequiredService<IOptimizationService>();
        //var logger = host.Services.GetRequiredService<ILogger<Program>>();
        //var filePathsOptions = host.Services.GetRequiredService<IOptions<FilePathsOptions>>().Value;

        //string structureFilePath = filePathsOptions.Structure;
        //string outputFilePath = filePathsOptions.Output;

        //SystemComponents finalComponents = null;
        //SystemComponents components = jsonFileService.DecodeStructure(structureFilePath);
        //logger.LogInformation($"成功解码");


        //DisplayService.DisplayStructure(components);

        //foreach (var station in components.AirCompressionStations)
        //{
        //    station.TempPressure = station.InitialPressure;
        //}

        //CalculationService.GasDuctCalc(ref components);
        //CalculationService.LoadData(ref components);

        //DisplayService.DisplayResults(components);

        //finalComponents = OptimizationService.FindOptimalPressures(components);
        //DisplayService.DisplayResults(finalComponents);


        // 异步运行主机，它将启动所有注册的托管服务
        await host.RunAsync();
    }
}


