using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using SmartTravelPlanner.Client;
using SmartTravelPlanner.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
var baseUrl = builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl is required.");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(60) });
builder.Services.AddScoped<ITravelApiClient, TravelApiClient>();
builder.Services.AddScoped<IWorkflowLiveClient, SignalRWorkflowLiveClient>();
builder.Services.AddMudServices();
await builder.Build().RunAsync();
