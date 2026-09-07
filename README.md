# Tic Tac Toe

## Project overview

A browser Tic Tac Toe game with an Angular frontend and a .NET Web API backend. Two
people can play each other, or one person can play the computer. The backend owns the
game: it validates every move, decides the winner, keeps the move history and holds the
scoreboard. The frontend renders whatever the backend sends back.

## Tech stack

| | |
| --- | --- |
| Frontend | Angular 20 (standalone components, signals), TypeScript |
| Backend | .NET 8 Web API, C# |
| API | REST over HTTP, JSON |
| Storage | In-memory, for the lifetime of the API process |
| Backend tests | xUnit, `WebApplicationFactory` |
| Frontend tests | Karma and Jasmine |

## Features

- 3 x 3 board; empty cells are clickable, filled cells are locked
- Turn indicator, with X and O alternating after every valid move
- Win detection on rows, columns and both diagonals, with the winning line highlighted
- Draw detection when the board fills with no line
- Move history showing move number, player and position
- Undo, which behaves differently in each mode (see below)
- Session scoreboard for X wins, O wins and draws, served by the backend
- Two player mode and a computer opponent
- Reset game (keeps the score) and reset scoreboard, as separate actions

## How to run it

Two terminals. The backend first, since the frontend calls it on startup.

### Backend

Needs the .NET 8 SDK.

```
cd backend
dotnet run --project src/TicTacToe.Api
```

Runs on `http://localhost:5175`. Swagger UI is at `http://localhost:5175/swagger` and is
the quickest way to try the API without the frontend.

### Frontend

Needs Node 20.19+, 22.12+ or 24+.

```
cd frontend
npm install
npm start
```

Runs on `http://localhost:4200`. The API base URL is in `src/environments/environment.ts`
if you need to point it somewhere else.

## API

| Method | Endpoint | Purpose | Success | Errors |
| --- | --- | --- | --- | --- |
| POST | `/api/games` | Start a game. Body: `{ "mode": "TwoPlayer" }` or `"Computer"` | 201 | 400 |
| GET | `/api/games/{id}` | Current game state | 200 | 404 |
| POST | `/api/games/{id}/moves` | Play a move. Body: `{ "player": "X", "row": 0, "column": 2 }` | 200 | 400, 404 |
| POST | `/api/games/{id}/undo` | Take back the last move, or the last pair in computer mode | 200 | 400, 404 |
| POST | `/api/games/{id}/reset` | Clear the board, keep the game id and the score | 200 | 404 |
| GET | `/api/scoreboard` | Wins and draws for this session | 200 | |
| POST | `/api/scoreboard/reset` | Clear the scoreboard | 200 | |

`POST /api/games` returns the new game's URL in a `Location` header. An unknown game id
is a 404 on every `/api/games/{id}` route.

Every game endpoint returns the same shape, so the frontend has one way to render a
response. Playing X at the top left in computer mode:

```
POST /api/games/3579f3ac-1de8-4da6-84df-c0e55b2fac75/moves
{ "player": "X", "row": 0, "column": 0 }
```

```json
{
  "gameId": "3579f3ac-1de8-4da6-84df-c0e55b2fac75",
  "mode": "Computer",
  "status": "InProgress",
  "board": ["X", null, null, null, "O", null, null, null, null],
  "currentPlayer": "X",
  "winner": null,
  "winningCells": null,
  "canUndo": true,
  "moves": [
    { "number": 1, "player": "X", "row": 0, "column": 0 },
    { "number": 2, "player": "O", "row": 1, "column": 1 }
  ],
  "scoreboard": { "xWins": 0, "oWins": 0, "draws": 0 }
}
```

`board` is nine cells in row order, so index 5 is row 2, column 3. `winningCells` holds
the three indices to highlight. `status` is `InProgress`, `Won` or `Draw`.

A rejected move comes back as a 400 with the reason attached:

```json
{
  "title": "The move was rejected.",
  "status": 400,
  "detail": "That cell is already taken.",
  "reason": "OccupiedCell"
}
```

Reasons are `OccupiedCell`, `WrongPlayer`, `OutOfBounds`, `GameCompleted` and
`NothingToUndo`. A request that is malformed rather than an illegal move — a missing
field, or a mode the API does not define — is rejected by model validation before it
reaches the game, and comes back as a standard `ValidationProblemDetails` instead.

## How to run the tests

```
cd backend
dotnet test
```

42 tests covering move validation, turn switching, wins on every line, draws, reset,
undo in both modes, the computer's move choice, scoreboard updates for wins and draws,
request validation and the API contract itself.

```
cd frontend
npm test
```

16 tests covering the API service calls, the board component's rendering and clicks, and
the game component's status text, error handling and in-flight guard. They run headless,
so Chrome needs to be installed but no window opens.

## AI tools and prompt summary

I used Claude Code (the CLI) while building this. The approach was to decide the design
first and use the tool for the parts whose shape was already settled.

I worked out the specification from the problem statement myself, including the questions
it leaves open — whether reset keeps the game id, whether the computer's reply is its own
request, what undo does in computer mode with a single move on the board. Those are under
Clarifications above. I also fixed two invariants before writing anything: the domain type
owns every rule, and the frontend derives nothing. Both went into the prompts as
constraints rather than being left to be inferred, which is most of the reason there was
not much to correct afterwards.

What I did correct was worth the attention. Undo I specified as replay-from-history up
front, because reverse-patching is the obvious implementation and it is wrong in a way
tests rarely catch. The scoreboard was being recorded on the read path, so refetching a
finished game inflated the total; that became a flag on the game and an API test that
refetches and asserts the score has not moved. One generated test passed for the wrong
reason — the winning cell in its fixture was already occupied — so it never exercised the
priority it claimed to.

I checked the game rules against a truth table written by hand, traced every undo path
myself, and curled all seven endpoints to confirm the responses match what this README
documents rather than trusting that code and docs agreed. The sample response above is a
real one.

`docs/ai-workflow.md` has the detail: the prompts, what each produced, and what changed.

## Design decisions

**The backend owns the game.** Every action is a request, and the component replaces its
whole state with the response. The frontend has no copy of the rules — it cannot decide
whose turn it is or whether someone has won, because it never computes those things. That
keeps the two halves from drifting apart.

**Undo replays instead of reversing.** Taking a move back drops it from the history and
then rebuilds the board, status, winner and current player from the moves that are left.
Reversing each change individually would mean carefully unwinding the win state and the
turn, which is easy to get subtly wrong. Replaying nine cells costs nothing and is
obviously correct.

**The computer is deterministic.** It follows the priority in the specification — win,
block, centre, corner, anything — and when it takes a corner it always tries them in the
same order rather than picking at random. A given board always produces the same move,
which makes the behaviour testable. It plays a reasonable game and cannot be beaten
carelessly, but it does not look ahead, so it is not unbeatable.

**In computer mode, one click is one request.** The move endpoint plays X and then
immediately plays O's reply, returning a single state. The alternative was a second call
from the frontend, which would leave the UI briefly showing a half-finished turn and
would put part of the turn sequence in the client.

**The scoreboard is embedded in the game response.** `GET /api/scoreboard` exists as well,
but returning the scores alongside the game means the UI stays consistent after a win
without a second round trip.

**`canUndo` is computed on the server.** The button binds straight to it, so the rule for
when undo is allowed lives in one place.

## Clarifications and assumptions

**Clarification 2 — Option A.** Once a game is won or drawn, undo is disabled and the
result stands. The scoreboard is never adjusted downwards, so there is no reconciliation
to get wrong.

Points the brief left open, and what I decided:

- **Reset keeps the game id.** The endpoint is `POST /api/games/{id}/reset`, so the
  session survives the reset and the frontend does not have to swap ids mid-game.
- **The computer's reply is part of the human's request**, as above.
- **Computer-mode undo with only one move on the board** removes that single move rather
  than failing. There is no pair to remove, and refusing would leave the player stuck.
- **The scoreboard is process-wide, not per game.** `/api/scoreboard` takes no id, so
  there is one scoreboard for the session, shared by every game.
- **Move validation happens in a fixed order** — completed game, then off-board, then
  wrong player, then occupied cell. A move can break more than one rule, and the order
  makes the reported reason predictable.
- **Changing mode starts a new game** and leaves the scoreboard alone.

## Known limitations

- Everything is held in memory. Restarting the API loses all games and the scoreboard.
- There is one scoreboard for the whole process, so two browsers hitting the same API
  share it.
- Games are never cleaned up; each one stays in the dictionary until the process ends.
- The computer follows fixed rules rather than searching, so it can be drawn with and
  occasionally beaten.
- CORS allows `http://localhost:4200` only, which is enough to run this locally.

## Future improvements

- Persist games and scores, with SQLite as the obvious next step
- Scope the scoreboard to a player or a session rather than the process
- Minimax for an unbeatable computer, with a difficulty setting
- Undo after completion (Option B), rolling the score back with it
- Expire finished games so the store does not grow
