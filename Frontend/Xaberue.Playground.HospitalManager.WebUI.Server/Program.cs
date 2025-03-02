using Grpc.Net.Client;
using Microsoft.FluentUI.AspNetCore.Components;
using Xaberue.Playground.HospitalManager.Doctors;
using Xaberue.Playground.HospitalManager.Doctors.Shared;
using Xaberue.Playground.HospitalManager.ServiceDefaults;
using Xaberue.Playground.HospitalManager.Shared;
using Xaberue.Playground.HospitalManager.WebUI.Server.Components;
using Xaberue.Playground.HospitalManager.WebUI.Server.Configuration;

var builder = WebApplication.CreateBuilder(args);

var grpcEnabled = bool.Parse(builder.Configuration["EnableGrpc"]!);

builder.AddServiceDefaults();
builder.AddRedisOutputCache("cache");

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddFluentUIComponents();

var patientsApiUrl = builder.Configuration.GetConnectionString("PatientsAPIUrl")
    ?? throw new ArgumentException("PatientsAPIUrl is mandatory");

var doctorsApiUrl = builder.Configuration.GetConnectionString("DoctorsApiUrl")
    ?? throw new ArgumentException("DoctorsApiUrl is mandatory");

builder.Services.AddHttpClient(
    HospitalManagerApiConstants.PatientsApiClient,
    client =>
    {
        client.BaseAddress = new Uri(patientsApiUrl);
    });

builder.Services.AddHttpClient(
    HospitalManagerApiConstants.DoctorsApiClient,
    client =>
    {
        client.BaseAddress = new Uri(doctorsApiUrl);
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
    app.MapOpenApi();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseAntiforgery();

var group = app.MapGroup("/api");

group.MapGet("/doctors", async (IHttpClientFactory httpClientFactory) =>
{
    if (grpcEnabled)
    {
        using var doctorsChannel = GrpcChannel.ForAddress(doctorsApiUrl);

        var doctorsClient = new Doctors.DoctorsClient(doctorsChannel);
        var response = await doctorsClient.GetAllAsync(new GetAllDoctorsRequest());

        return response.Doctors.Select(x => new DoctorGridViewModel(
                x.Id,
                $"{x.Name} {x.Surname}",
                DateTime.Parse(x.HiringDate)
            ));
    }
    else
    {
        var doctorsClient = httpClientFactory.CreateClient(HospitalManagerApiConstants.DoctorsApiClient);
        var doctors = await doctorsClient.GetFromJsonAsync<DoctorDto[]>("/doctors") ?? [];

        return doctors.Select(x => new DoctorGridViewModel(
               x.Id,
               $"{x.Name} {x.Surname}",
               x.HiringDate
           ));
    }
})
.WithName("GetAllFilms");

app.MapDefaultEndpoints();

app.UseOutputCache();

app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(Xaberue.Playground.HospitalManager.WebUI.Client._Imports).Assembly);

app.Run();
