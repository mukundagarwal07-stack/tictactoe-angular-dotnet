using TicTacToe.Api.Domain;
using Xunit;

namespace TicTacToe.Api.Tests;

public class GameRulesTests
{
    [Fact]
    public void ValidMoveMarksTheCellAndRecordsHistory()
    {
        var game = new Game(GameMode.TwoPlayer);

        game.Play(Player.X, 1, 2);

        Assert.Equal(Player.X, game.Cells[5]);
        Assert.Equal(new Move(1, Player.X, 1, 2), Assert.Single(game.Moves));
    }

    [Fact]
    public void TurnsAlternateBetweenPlayers()
    {
        var game = new Game(GameMode.TwoPlayer);
        Assert.Equal(Player.X, game.CurrentPlayer);

        game.Play(Player.X, 0, 0);
        Assert.Equal(Player.O, game.CurrentPlayer);

        game.Play(Player.O, 1, 1);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public void MoveOnAnOccupiedCellIsRejectedAndLeavesTheTurnAlone()
    {
        var game = new Game(GameMode.TwoPlayer);
        game.Play(Player.X, 0, 0);

        var error = Assert.Throws<MoveException>(() => game.Play(Player.O, 0, 0));

        Assert.Equal("OccupiedCell", error.Reason);
        Assert.Equal(Player.O, game.CurrentPlayer);
        Assert.Single(game.Moves);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 3)]
    public void MoveOutsideTheBoardIsRejected(int row, int column)
    {
        var game = new Game(GameMode.TwoPlayer);

        var error = Assert.Throws<MoveException>(() => game.Play(Player.X, row, column));

        Assert.Equal("OutOfBounds", error.Reason);
        Assert.Empty(game.Moves);
    }

    [Fact]
    public void MoveByTheWrongPlayerIsRejected()
    {
        var game = new Game(GameMode.TwoPlayer);

        var error = Assert.Throws<MoveException>(() => game.Play(Player.O, 0, 0));

        Assert.Equal("WrongPlayer", error.Reason);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public void RowWinIsDetected()
    {
        // X takes the top row while O answers underneath.
        var game = Play(GameMode.TwoPlayer, (0, 0), (1, 0), (0, 1), (1, 1), (0, 2));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Player.X, game.Winner);
        Assert.Equal([0, 1, 2], game.WinningCells);
    }

    [Fact]
    public void ColumnWinIsDetected()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (0, 1), (1, 0), (1, 1), (2, 0));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal(Player.X, game.Winner);
        Assert.Equal([0, 3, 6], game.WinningCells);
    }

    [Fact]
    public void DiagonalWinIsDetected()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (0, 1), (1, 1), (0, 2), (2, 2));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal([0, 4, 8], game.WinningCells);
    }

    [Fact]
    public void AntiDiagonalWinIsDetected()
    {
        var game = Play(GameMode.TwoPlayer, (0, 2), (0, 0), (1, 1), (0, 1), (2, 0));

        Assert.Equal(GameStatus.Won, game.Status);
        Assert.Equal([2, 4, 6], game.WinningCells);
    }

    [Fact]
    public void FullBoardWithNoLineIsADraw()
    {
        var game = FullDraw();

        Assert.Equal(GameStatus.Draw, game.Status);
        Assert.Null(game.Winner);
        Assert.Null(game.WinningCells);
    }

    [Fact]
    public void MovesAfterCompletionAreRejected()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (1, 0), (0, 1), (1, 1), (0, 2));

        var error = Assert.Throws<MoveException>(() => game.Play(Player.O, 2, 2));

        Assert.Equal("GameCompleted", error.Reason);
        Assert.Equal(5, game.Moves.Count);
    }

    [Fact]
    public void ResetClearsTheGameButNotTheRecordedFlagOfALaterGame()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (1, 0), (0, 1), (1, 1), (0, 2));
        game.ScoreRecorded = true;

        game.Reset();

        Assert.All(game.Cells, Assert.Null);
        Assert.Empty(game.Moves);
        Assert.Equal(GameStatus.InProgress, game.Status);
        Assert.Null(game.Winner);
        Assert.Null(game.WinningCells);
        Assert.Equal(Player.X, game.CurrentPlayer);
        Assert.False(game.ScoreRecorded);
    }

    /// Plays the given cells in order, alternating from X.
    internal static Game Play(GameMode mode, params (int Row, int Column)[] moves)
    {
        var game = new Game(mode);

        foreach (var (row, column) in moves)
            game.Play(game.CurrentPlayer, row, column);

        return game;
    }

    /// X O X / X O O / O X X — every cell filled, no line.
    internal static Game FullDraw() => Play(GameMode.TwoPlayer,
        (0, 0), (0, 1), (0, 2), (1, 1), (1, 0), (1, 2), (2, 1), (2, 0), (2, 2));
}
