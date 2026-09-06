using TicTacToe.Api.Domain;
using Xunit;
using static TicTacToe.Api.Tests.GameRulesTests;

namespace TicTacToe.Api.Tests;

public class UndoTests
{
    [Fact]
    public void TwoPlayerUndoRemovesOnlyTheLastMove()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (1, 1));

        game.Undo();

        Assert.Equal(Player.X, Assert.Single(game.Moves).Player);
        Assert.Null(game.Cells[4]);
        Assert.Equal(Player.O, game.CurrentPlayer);
    }

    [Fact]
    public void ComputerUndoRemovesThePairAndHandsTheTurnBackToX()
    {
        var game = new Game(GameMode.Computer);
        game.Play(Player.X, 0, 0);
        var reply = ComputerPlayer.SelectMove(game);
        game.Play(Player.O, reply / 3, reply % 3);

        game.Undo();

        Assert.Empty(game.Moves);
        Assert.All(game.Cells, Assert.Null);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public void ComputerUndoWithASingleMoveRemovesJustThatMove()
    {
        // Reachable if the computer has not replied yet; there is no pair to drop.
        var game = new Game(GameMode.Computer);
        game.Play(Player.X, 0, 0);

        game.Undo();

        Assert.Empty(game.Moves);
        Assert.Equal(Player.X, game.CurrentPlayer);
    }

    [Fact]
    public void UndoIsRefusedOnceAGameIsWon()
    {
        var game = Play(GameMode.TwoPlayer, (0, 0), (1, 0), (0, 1), (1, 1), (0, 2));
        Assert.Equal(GameStatus.Won, game.Status);
        game.ScoreRecorded = true;

        // Option A means undo is refused here, so the recorded result stands.
        Assert.Equal("GameCompleted", Assert.Throws<MoveException>(game.Undo).Reason);
        Assert.Equal(GameStatus.Won, game.Status);
        Assert.True(game.ScoreRecorded);
    }

    [Fact]
    public void UndoIsUnavailableOnAFreshGame()
    {
        var game = new Game(GameMode.TwoPlayer);

        Assert.False(game.CanUndo);
        Assert.Equal("NothingToUndo", Assert.Throws<MoveException>(game.Undo).Reason);
    }

    [Fact]
    public void UndoIsUnavailableAfterADraw()
    {
        var game = FullDraw();

        Assert.False(game.CanUndo);
        Assert.Equal("GameCompleted", Assert.Throws<MoveException>(game.Undo).Reason);
    }
}
