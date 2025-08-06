using WorkerService1;
IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService() //  enable running as a Windows Service
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();     // For console testing
        logging.AddEventLog();    // For Windows Event Viewer logs
    })
    .Build();
await host.RunAsync();