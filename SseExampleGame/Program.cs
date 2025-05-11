using System.Net.ServerSentEvents;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


// Хранилище игроков: playerId → Player
var players = new Dictionary<string, Player>();

app.MapGet("/", () => Results.Redirect("/index.html"));

// SSE: стримим всем клиентам массив всех игроков под событием "update"
app.MapGet("/game/stream", (CancellationToken ct) =>
{
    async IAsyncEnumerable<SseItem<List<PlayerInfo>>> StreamAll([EnumeratorCancellation] CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var list = players
                .Select(kvp => new PlayerInfo(
                    kvp.Key,
                    kvp.Value.Name,
                    kvp.Value.Color,
                    kvp.Value.X,
                    kvp.Value.Y))
                .ToList();

            //var json = JsonSerializer.Serialize(snapshot);
            yield return new SseItem<List<PlayerInfo>>(list, eventType: "update");

            await Task.Delay(100, token);
        }
    }

    return TypedResults.ServerSentEvents(
        StreamAll(ct),
        eventType: "update"
    );
});

// Join: принимаем { name, color }, возвращаем { playerId }
app.MapPost("/game/join", ([FromBody] PlayerJoin join) =>
{
    if (string.IsNullOrWhiteSpace(join.Name))
        return Results.BadRequest(new { error = "Invalid name" });
    if (string.IsNullOrWhiteSpace(join.Color))
        return Results.BadRequest(new { error = "Invalid color" });

    var id = Guid.NewGuid().ToString();
    players[id] = new Player(join.Name.Trim(), join.Color, 200, 200);
    return Results.Ok(new { playerId = id });
});

// Move: обновляем позицию одного игрока
app.MapPost("/game/move/{id}/{dir}", (string id, string dir) =>
{
    if (!players.TryGetValue(id, out var pl))
        return Results.BadRequest(new { error = "Unknown player" });

    const double step = 10;
    var (x, y) = (pl.X, pl.Y);
    (x, y) = dir.ToLower() switch
    {
        "up" => (x, y - step),
        "down" => (x, y + step),
        "left" => (x - step, y),
        "right" => (x + step, y),
        _ => (x, y)
    };

    players[id] = new Player(pl.Name, pl.Color, x, y);
    return Results.Ok();
});

app.UseDefaultFiles();
app.UseStaticFiles();
app.Run();


// Модель игрока
record Player(string Name, string Color, double X, double Y);
record PlayerInfo(string id, string name, string color, double x, double y);
// DTO для join
record PlayerJoin(string Name, string Color);
