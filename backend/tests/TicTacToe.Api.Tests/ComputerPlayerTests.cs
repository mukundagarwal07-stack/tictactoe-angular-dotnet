using TicTacToe.Api.Domain;
using Xunit;

namespace TicTacToe.Api.Tests;

public class ComputerPlayerTests
{
    [Fact]
    public void TakesTheWinWhenOneIsAvailable()
    {
        // Both sides are one move from a line: O wins at 2, X would win at 5.
        // Winning has to beat blocking.
        var game = Board("OO." +
                         "XX." +
                         "...");

        Assert.Equal(2, ComputerPlayer.SelectMove(game));
    }

    [Fact]
    public void BlocksWhenXIsAboutToWin()
    {
        var game = Board("XX." +
                         "O.." +
                         "...");

        Assert.Equal(2, ComputerPlayer.SelectMove(game));
    }

    [Fact]
    public void TakesTheCentreWhenNothingIsUrgent()
    {
        var game = Board("X.." +
                         "..." +
                         "...");

        Assert.Equal(4, ComputerPlayer.SelectMove(game));
    }

    [Fact]
    public void TakesACornerWhenTheCentreIsGone()
    {
        var game = Board("..." +
                         ".X." +
                         "...");

        Assert.Equal(0, ComputerPlayer.SelectMove(game));
    }

    [Fact]
    public void FallsBackToAnyFreeCell()
    {
        // Nothing to win, nothing to block, centre and every corner gone — only the
        // last rule can produce a move here.
        var game = Board("XOX" +
                         ".XO" +
                         "OXO");

        Assert.Equal(3, ComputerPlayer.SelectMove(game));
    }

    [Fact]
    public void SelectingAMoveLeavesTheBoardUntouched()
    {
        // The win and block checks try a piece in each free cell; none of that may show
        // up on the real board.
        var game = Board("XX." +
                         "O.." +
                         "...");
        var before = game.Cells.ToArray();

        ComputerPlayer.SelectMove(game);

        Assert.Equal(before, game.Cells);
    }

    /// Builds a game from a nine-character board of 'X', 'O' and '.'.
    private static Game Board(string cells)
    {
        var game = new Game(GameMode.Computer);

        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] == '.') continue;
            game.Cells[i] = cells[i] == 'X' ? Player.X : Player.O;
        }

        return game;
    }
}
