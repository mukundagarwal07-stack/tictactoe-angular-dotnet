using System.ComponentModel.DataAnnotations;
using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Contracts;

// Required, so an empty body is a 400 rather than a move nobody asked for, and
// EnumDataType because the JSON binder otherwise accepts any number as an enum value.
// Row and column are only checked for presence here: the board's bounds are a game rule,
// and the domain reports them with the same reason codes as every other rejection.
public record CreateGameRequest([Required, EnumDataType(typeof(GameMode))] GameMode? Mode);

public record MoveRequest(
    [Required, EnumDataType(typeof(Player))] Player? Player,
    [Required] int? Row,
    [Required] int? Column);

public record MoveView(int Number, Player Player, int Row, int Column);

public record ScoreboardView(int XWins, int OWins, int Draws);

public record GameStateResponse(
    Guid GameId,
    GameMode Mode,
    GameStatus Status,
    Player?[] Board,
    Player CurrentPlayer,
    Player? Winner,
    int[]? WinningCells,
    bool CanUndo,
    IReadOnlyList<MoveView> Moves,
    ScoreboardView Scoreboard);
