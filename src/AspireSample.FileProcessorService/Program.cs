using AspireSample.FileProcessorService;

var builder = Host.CreateApplicationBuilder(args);
// Register our configuration object.
builder.Services.Configure<ServiceConfiguration>(builder.Configuration.GetSection(nameof(ServiceConfiguration)));
builder.Services.Configure<ConnectionStrings>(builder.Configuration.GetSection(nameof(ConnectionStrings)));
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
