using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Domain;
using Xunit;

namespace TicTacToe.Api.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly JsonSerializerOptions Json =
        new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    private readonly HttpClient _client;

    public ApiTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.CreateClient();

        // The scoreboard is shared by every test in this class, so start from zero.
        _client.PostAsync("/api/scoreboard/reset", null).GetAwaiter().GetResult();
    }

    [Fact]
    public async Task CreatingAGameReturnsAnEmptyBoard()
    {
        var response = await _client.PostAsJsonAsync("/api/games", new CreateGameRequest(GameMode.TwoPlayer), Json);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var game = await Read(response);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.All(game.Board, Assert.Null);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public async Task UnknownGameReturnsNotFound()
    {
        var response = await _client.GetAsync($"/api/games/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AMoveIsAppliedAndReturnedInHistory()
    {
        var game = await NewGame(GameMode.TwoPlayer);

        game = await Move(game.GameId, Player.X, 0, 0);

        Assert.Equal(Player.X, game.Board[0]);
        Assert.Equal(Player.O, game.CurrentPlayer);
        Assert.Equal(new MoveView(1, Player.X, 0, 0), Assert.Single(game.Moves));
    }

    [Fact]
    public async Task AnOccupiedCellIsRejectedWithAReason()
    {
        var game = await NewGame(GameMode.TwoPlayer);
        await Move(game.GameId, Player.X, 1, 1);

        var response = await _client.PostAsJsonAsync(
            $"/api/games/{game.GameId}/moves", new MoveRequest(Player.O, 1, 1), Json);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("OccupiedCell", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"player":"X"}""")]
    [InlineData("""{"player":"X","row":5,"column":0}""")]
    public async Task IncompleteOrOutOfRangeMoveRequestsAreRejected(string body)
    {
        var game = await NewGame(GameMode.TwoPlayer);

        var response = await _client.PostAsync($"/api/games/{game.GameId}/moves",
            new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AnUndefinedGameModeIsRejected()
    {
        // The JSON binder will happily produce (GameMode)99 without the enum check.
        var response = await _client.PostAsync("/api/games",
            new StringContent("{\"mode\":99}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ComputerRepliesWithinTheSameRequest()
    {
        var game = await NewGame(GameMode.Computer);

        game = await Move(game.GameId, Player.X, 0, 0);

        Assert.Equal(2, game.Moves.Count);
        Assert.Equal(Player.O, game.Moves[1].Player);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public async Task UndoInComputerModeRemovesBothMoves()
    {
        var game = await NewGame(GameMode.Computer);
        game = await Move(game.GameId, Player.X, 0, 0);

        var response = await _client.PostAsync($"/api/games/{game.GameId}/undo", null);
        game = await Read(response);

        Assert.Empty(game.Moves);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.False(game.CanUndo);
    }

    [Fact]
    public async Task UndoWithNoMovesIsRejected()
    {
        var game = await NewGame(GameMode.TwoPlayer);

        var response = await _client.PostAsync($"/api/games/{game.GameId}/undo", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("NothingToUndo", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task ResetClearsTheBoardAndKeepsTheGameId()
    {
        var game = await NewGame(GameMode.TwoPlayer);
        await Move(game.GameId, Player.X, 0, 0);

        var response = await _client.PostAsync($"/api/games/{game.GameId}/reset", null);
        var reset = await Read(response);

        Assert.Equal(game.GameId, reset.GameId);
        Assert.Empty(reset.Moves);
        Assert.All(reset.Board, Assert.Null);
        Assert.Equal(Player.X, reset.CurrentPlayer);
    }

    [Fact]
    public async Task WinningUpdatesTheScoreboardOnceAndSurvivesAReset()
    {
        var game = await WinForX();

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Player.X, game.Winner);
        Assert.Equal([0, 1, 2], game.WinningCells);
        Assert.Equal(1, game.Scoreboard.XWins);

        // Re-reading a finished game must not count it again.
        var reread = await Read(await _client.GetAsync($"/api/games/{game.GameId}"));
        Assert.Equal(1, reread.Scoreboard.XWins);

        var reset = await Read(await _client.PostAsync($"/api/games/{game.GameId}/reset", null));
        Assert.Equal(1, reset.Scoreboard.XWins);
    }

    [Fact]
    public async Task AnOWinAndADrawAreCountedSeparately()
    {
        var game = await NewGame(GameMode.TwoPlayer);
        // O takes the middle column while X is blocked across the top.
        await Move(game.GameId, Player.X, 0, 0);
        await Move(game.GameId, Player.O, 0, 1);
        await Move(game.GameId, Player.X, 0, 2);
        await Move(game.GameId, Player.O, 1, 1);
        await Move(game.GameId, Player.X, 1, 0);
        game = await Move(game.GameId, Player.O, 2, 1);

        Assert.Equal(Player.O, game.Winner);
        Assert.Equal(1, game.Scoreboard.OWins);

        game = await Read(await _client.PostAsync($"/api/games/{game.GameId}/reset", null));
        foreach (var (row, column) in new[] { (0, 0), (0, 1), (0, 2), (1, 1), (1, 0), (1, 2), (2, 1), (2, 0), (2, 2) })
            game = await Move(game.GameId, game.CurrentPlayer, row, column);

        Assert.Equal(GameStatus.Draw, game.Status);
        Assert.Equal(new ScoreboardView(0, 1, 1), game.Scoreboard);
    }

    [Fact]
    public async Task TheScoreboardEndpointReturnsTheSameTotals()
    {
        var game = await WinForX();

        var scoreboard = await _client.GetFromJsonAsync<ScoreboardView>("/api/scoreboard", Json);

        Assert.Equal(game.Scoreboard, scoreboard);
    }

    [Fact]
    public async Task TheComputerStopsPlayingOnceTheGameIsDecided()
    {
        var game = await NewGame(GameMode.Computer);

        // Play X into the first free cell each turn until the game is decided. The
        // computer answers every move, so this ends in a win for O or a draw.
        while (game.Status == GameStatus.InProgress)
        {
            var free = Array.FindIndex(game.Board, cell => cell is null);
            game = await Move(game.GameId, Player.X, free / 3, free % 3);
        }

        // The deciding move is the last one on the board: the computer does not answer it.
        if (game.Status == GameStatus.Won)
            Assert.Equal(game.Winner, game.Moves[^1].Player);

        // A further move is refused outright rather than drawing another computer reply.
        var after = await _client.PostAsJsonAsync($"/api/games/{game.GameId}/moves",
            new MoveRequest(Player.X, 0, 0), Json);
        Assert.Equal(HttpStatusCode.BadRequest, after.StatusCode);

        var unchanged = await Read(await _client.GetAsync($"/api/games/{game.GameId}"));
        Assert.Equal(game.Moves.Count, unchanged.Moves.Count);
    }

    [Fact]
    public async Task ResetScoreboardClearsTheTotals()
    {
        await WinForX();

        var response = await _client.PostAsync("/api/scoreboard/reset", null);
        var scoreboard = await response.Content.ReadFromJsonAsync<ScoreboardView>(Json);

        Assert.Equal(new ScoreboardView(0, 0, 0), scoreboard);
    }

    private async Task<GameStateResponse> WinForX()
    {
        var game = await NewGame(GameMode.TwoPlayer);

        await Move(game.GameId, Player.X, 0, 0);
        await Move(game.GameId, Player.O, 1, 0);
        await Move(game.GameId, Player.X, 0, 1);
        await Move(game.GameId, Player.O, 1, 1);

        return await Move(game.GameId, Player.X, 0, 2);
    }

    private async Task<GameStateResponse> NewGame(GameMode mode) =>
        await Read(await _client.PostAsJsonAsync("/api/games", new CreateGameRequest(mode), Json));

    private async Task<GameStateResponse> Move(Guid id, Player player, int row, int column) =>
        await Read(await _client.PostAsJsonAsync($"/api/games/{id}/moves", new MoveRequest(player, row, column), Json));

    private static async Task<GameStateResponse> Read(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<GameStateResponse>(Json))!;
    }
}
