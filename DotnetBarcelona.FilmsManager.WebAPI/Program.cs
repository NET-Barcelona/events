using DotnetBarcelona.Actors.Shared;
using DotnetBarcelona.Films.Shared;
using DotnetBarcelona.FilmsManager.Shared;
using DotnetBarcelona.FilmsManager.WebAPI.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var filmsApiUrl = builder.Configuration.GetConnectionString("FilmsApiUrl")
    ?? throw new ArgumentException("FilmsAPIUrl is mandatory");

var actorsApiUrl = builder.Configuration.GetConnectionString("ActorsApiUrl")
    ?? throw new ArgumentException("ActorsApiUrl is mandatory");

builder.Services.AddHttpClient(
    FilmsManagerApiConstants.FilmsApiClient,
    client =>
    {
        client.BaseAddress = new Uri(filmsApiUrl);
    });

builder.Services.AddHttpClient(
    FilmsManagerApiConstants.ActorsApiClient,
    client =>
    {
        client.BaseAddress = new Uri(actorsApiUrl);
    });

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/films", async (IHttpClientFactory httpClientFactory) =>
{
    var filmsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.FilmsApiClient);
    var actorsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.ActorsApiClient);
    var films = await filmsClient.GetFromJsonAsync<FilmDto[]>("/films");
    var actors = new List<ActorDto>();

    foreach (var actorId in films.SelectMany(x => x.Cast).Distinct())
    {
        var actor = await actorsClient.GetFromJsonAsync<ActorDto>($"/actor/{actorId}");

        actors.Add(actor);
    }

    return films.Select(x => new FilmGridDto(
        x.Id,
        x.Name,
        x.ReleaseDate.Year,
        x.Categories.Select(x => x.ToString()).ToArray(),
        actors.Where(y => x.Cast.Contains(y.Id)).Select(y => y.Name).ToArray()));
})
.WithName("GetAllFilms");

app.MapGet("/film/{filmId}", async (IHttpClientFactory httpClientFactory, int filmId) =>
{
    var filmsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.FilmsApiClient);
    var actorsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.ActorsApiClient);
    var film = await filmsClient.GetFromJsonAsync<FilmDto>($"/film/{filmId}");
    var actors = new List<ActorDto>();

    foreach (var actorId in film.Cast)
    {
        var actor = await actorsClient.GetFromJsonAsync<ActorDto>($"/actor/{actorId}");

        actors.Add(actor);
    }

    return new FilmDetailDto(
        film.Id,
        film.Name,
        film.ReleaseDate.Year,
        film.Categories.Select(x => x.ToString()).ToArray(),
        actors);
})
.WithName("GetFilm");

app.MapPost("/film", (IHttpClientFactory httpClientFactory, FilmCreation filmCreation) =>
{
    var filmsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.FilmsApiClient);

    filmsClient.PostAsJsonAsync("/film", filmCreation);

    return Results.Created();
})
.WithName("RegisterFilm");

app.MapDelete("/film/{filmId}", async (IHttpClientFactory httpClientFactory, int filmId) =>
{
    var filmsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.FilmsApiClient);

    return await filmsClient.DeleteAsync($"/film/{filmId}");
});

app.MapGet("/actors", (IHttpClientFactory httpClientFactory) =>
{
    var actorsClient = httpClientFactory.CreateClient(FilmsManagerApiConstants.ActorsApiClient);

    return actorsClient.GetFromJsonAsAsyncEnumerable<ActorDto>("/actors");
})
.WithName("GetAllActors");

app.MapDefaultEndpoints();

app.Run();