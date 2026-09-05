using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/scoreboard")]
public class ScoreboardController(GameStore store) : ControllerBase
{
    /// <summary>Returns wins and draws for the current session.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ScoreboardView), StatusCodes.Status200OK)]
    public ScoreboardView Get() => Current();

    /// <summary>Clears the scoreboard. Games in progress are not affected.</summary>
    [HttpPost("reset")]
    [ProducesResponseType(typeof(ScoreboardView), StatusCodes.Status200OK)]
    public ScoreboardView Reset()
    {
        store.ResetScoreboard();
        return Current();
    }

    private ScoreboardView Current()
    {
        var (x, o, draws) = store.Scoreboard;
        return new ScoreboardView(x, o, draws);
    }
}
