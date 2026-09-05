using Microsoft.AspNetCore.Mvc;
using TicTacToe.Api.Contracts;
using TicTacToe.Api.Domain;
using TicTacToe.Api.Services;

namespace TicTacToe.Api.Controllers;

[ApiController]
[Route("api/games")]
public class GamesController(GameStore store) : ControllerBase
{
    /// <summary>Starts a new game in the given mode.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status201Created)]
    public ActionResult<GameStateResponse> Create(CreateGameRequest request)
    {
        // Non-null: [ApiController] returns 400 on the Required attributes before we get here.
        var game = store.Create(request.Mode!.Value);
        return CreatedAtAction(nameof(Get), new { id = game.Id }, Present(game));
    }

    /// <summary>Returns the current state of a game.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GameStateResponse> Get(Guid id) =>
        store.Find(id) is { } game ? Present(game) : NotFound();

    /// <summary>
    /// Plays a move. In computer mode the reply from O is played in the same
    /// request, so one call always returns a state the player can act on.
    /// </summary>
    [HttpPost("{id:guid}/moves")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GameStateResponse> Play(Guid id, MoveRequest request)
    {
        if (store.Find(id) is not { } game) return NotFound();

        try
        {
            game.Play(request.Player!.Value, request.Row!.Value, request.Column!.Value);

            if (game.Mode == GameMode.Computer && game.Status == GameStatus.InProgress)
            {
                var cell = ComputerPlayer.SelectMove(game);
                game.Play(Player.O, cell / 3, cell % 3);
            }
        }
        catch (MoveException e)
        {
            return Rejected(e);
        }

        store.RecordIfComplete(game);
        return Present(game);
    }

    /// <summary>Takes back the last move, or the last pair in computer mode.</summary>
    [HttpPost("{id:guid}/undo")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GameStateResponse> Undo(Guid id)
    {
        if (store.Find(id) is not { } game) return NotFound();

        try
        {
            game.Undo();
        }
        catch (MoveException e)
        {
            return Rejected(e);
        }

        return Present(game);
    }

    /// <summary>Clears the board for a fresh game, keeping the scoreboard.</summary>
    [HttpPost("{id:guid}/reset")]
    [ProducesResponseType(typeof(GameStateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<GameStateResponse> Reset(Guid id)
    {
        if (store.Find(id) is not { } game) return NotFound();

        game.Reset();
        return Present(game);
    }

    private ActionResult<GameStateResponse> Rejected(MoveException e)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "The move was rejected.",
            Detail = e.Message
        };
        problem.Extensions["reason"] = e.Reason;

        return BadRequest(problem);
    }

    private GameStateResponse Present(Game game)
    {
        var (x, o, draws) = store.Scoreboard;

        return new GameStateResponse(
            game.Id,
            game.Mode,
            game.Status,
            game.Cells.ToArray(),
            game.CurrentPlayer,
            game.Winner,
            game.WinningCells,
            game.CanUndo,
            game.Moves.Select(m => new MoveView(m.Number, m.Player, m.Row, m.Column)).ToList(),
            new ScoreboardView(x, o, draws));
    }
}
