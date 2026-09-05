namespace TicTacToe.Api.Domain;

/// Picks O's move using the fixed priority from the specification: win, block,
/// centre, corner, anything. Corners are tried in a set order rather than at
/// random so that a given board always produces the same move and the rules stay
/// testable.
public static class ComputerPlayer
{
    private static readonly int[] Corners = [0, 2, 6, 8];

    public static int SelectMove(Game game)
    {
        // A copy, because the win and block checks try a piece in each free cell to see
        // what it would produce. That must never be visible on the real board.
        var cells = game.Cells.ToArray();

        return Completes(cells, Player.O)
            ?? Completes(cells, Player.X)
            ?? FreeCentre(cells)
            ?? FreeCorner(cells)
            ?? FirstFree(cells);
    }

    /// The cell that would give this player a line, if there is one.
    private static int? Completes(Player?[] cells, Player player)
    {
        for (var i = 0; i < cells.Length; i++)
        {
            if (cells[i] is not null) continue;

            cells[i] = player;
            var wins = Game.LineFor(cells, player) is not null;
            cells[i] = null;

            if (wins) return i;
        }

        return null;
    }

    private static int? FreeCentre(Player?[] cells) => cells[4] is null ? 4 : null;

    private static int? FreeCorner(Player?[] cells)
    {
        foreach (var i in Corners)
            if (cells[i] is null) return i;

        return null;
    }

    private static int FirstFree(Player?[] cells)
    {
        for (var i = 0; i < cells.Length; i++)
            if (cells[i] is null) return i;

        throw new InvalidOperationException("The board is full.");
    }
}
