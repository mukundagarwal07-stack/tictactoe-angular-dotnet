namespace TicTacToe.Api.Domain;

public enum Player { X, O }

public enum GameMode { TwoPlayer, Computer }

public enum GameStatus { InProgress, Won, Draw }

public record Move(int Number, Player Player, int Row, int Column);

public class MoveException(string reason, string message) : Exception(message)
{
    public string Reason { get; } = reason;
}

public class Game
{
    // The eight winning triples, as flat cell indices.
    private static readonly int[][] Lines =
    [
        [0, 1, 2], [3, 4, 5], [6, 7, 8],
        [0, 3, 6], [1, 4, 7], [2, 5, 8],
        [0, 4, 8], [2, 4, 6]
    ];

    private readonly List<Move> _moves = [];

    public Game(GameMode mode)
    {
        Id = Guid.NewGuid();
        Mode = mode;
    }

    public Guid Id { get; }
    public GameMode Mode { get; }
    public Player?[] Cells { get; } = new Player?[9];
    public Player CurrentPlayer { get; private set; } = Player.X;
    public GameStatus Status { get; private set; } = GameStatus.InProgress;
    public Player? Winner { get; private set; }
    public int[]? WinningCells { get; private set; }
    public IReadOnlyList<Move> Moves => _moves;

    /// True once this game's result has been added to the scoreboard, so a
    /// completed game can never be counted twice.
    public bool ScoreRecorded { get; set; }

    // Option A: a finished game is final, so there is nothing to undo.
    public bool CanUndo => _moves.Count > 0 && Status == GameStatus.InProgress;

    public void Play(Player player, int row, int column)
    {
        // Ordered so a move that breaks several rules always reports the same reason.
        if (Status != GameStatus.InProgress)
            throw new MoveException("GameCompleted", "The game is already complete.");

        if (row is < 0 or > 2 || column is < 0 or > 2)
            throw new MoveException("OutOfBounds", "Row and column must be between 0 and 2.");

        if (player != CurrentPlayer)
            throw new MoveException("WrongPlayer", $"It is {CurrentPlayer}'s turn.");

        var index = row * 3 + column;
        if (Cells[index] is not null)
            throw new MoveException("OccupiedCell", "That cell is already taken.");

        Cells[index] = player;
        _moves.Add(new Move(_moves.Count + 1, player, row, column));
        Evaluate();
    }

    public void Undo()
    {
        if (Status != GameStatus.InProgress)
            throw new MoveException("GameCompleted", "The game is complete, so undo is disabled.");

        if (_moves.Count == 0)
            throw new MoveException("NothingToUndo", "There is no move to undo.");

        // In computer mode a turn is a pair of moves, so undo removes the computer's
        // reply along with the human move that prompted it.
        var remove = Mode == GameMode.Computer ? Math.Min(2, _moves.Count) : 1;
        _moves.RemoveRange(_moves.Count - remove, remove);
        Replay();
    }

    public void Reset()
    {
        _moves.Clear();
        ScoreRecorded = false;
        Replay();
    }

    /// Rebuilds board and status from the move list. Undo and reset both work by
    /// replaying what is left rather than trying to reverse each change, which keeps
    /// the derived state (winner, winning cells, whose turn) impossible to get wrong.
    private void Replay()
    {
        Array.Clear(Cells);
        Status = GameStatus.InProgress;
        Winner = null;
        WinningCells = null;
        CurrentPlayer = Player.X;

        foreach (var move in _moves)
            Cells[move.Row * 3 + move.Column] = move.Player;

        Evaluate();
    }

    private void Evaluate()
    {
        var line = LineFor(Cells, Player.X) ?? LineFor(Cells, Player.O);
        if (line is not null)
        {
            Status = GameStatus.Won;
            Winner = Cells[line[0]];
            WinningCells = line.ToArray(); // copy, so callers cannot reach the shared table
            return;
        }

        if (Cells.All(c => c is not null))
        {
            Status = GameStatus.Draw;
            return;
        }

        CurrentPlayer = _moves.Count % 2 == 0 ? Player.X : Player.O;
    }

    /// The completed line for this player, or null. Shared with the computer player,
    /// which uses it to spot its own winning move and to block the opponent's.
    internal static int[]? LineFor(Player?[] cells, Player player) =>
        Lines.FirstOrDefault(line => line.All(i => cells[i] == player));
}
