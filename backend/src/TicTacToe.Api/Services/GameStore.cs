using System.Collections.Concurrent;
using TicTacToe.Api.Domain;

namespace TicTacToe.Api.Services;

/// Holds every game of the session and the running scoreboard. In-memory by
/// design, so all of it is lost when the API restarts.
public class GameStore
{
    private readonly ConcurrentDictionary<Guid, Game> _games = new();
    private readonly object _scoreLock = new();

    private int _xWins;
    private int _oWins;
    private int _draws;

    public Game Create(GameMode mode)
    {
        var game = new Game(mode);
        _games[game.Id] = game;
        return game;
    }

    public Game? Find(Guid id) => _games.TryGetValue(id, out var game) ? game : null;

    public (int XWins, int OWins, int Draws) Scoreboard
    {
        get { lock (_scoreLock) return (_xWins, _oWins, _draws); }
    }

    /// Adds a finished game to the scoreboard, once. Called after every move, so
    /// the guard on the game itself is what keeps repeated calls harmless.
    public void RecordIfComplete(Game game)
    {
        lock (_scoreLock)
        {
            if (game.ScoreRecorded || game.Status == GameStatus.InProgress) return;

            if (game.Status == GameStatus.Draw) _draws++;
            else if (game.Winner == Player.X) _xWins++;
            else _oWins++;

            game.ScoreRecorded = true;
        }
    }

    public void ResetScoreboard()
    {
        lock (_scoreLock)
        {
            _xWins = _oWins = _draws = 0;
        }
    }
}
