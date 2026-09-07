# AI-assisted development notes

The brief allows AI-assisted development and asks for an account of it. This is mine.
I used Claude Code (the CLI) throughout.

My working assumption with these tools is that they are fast at code whose shape is
already decided and unreliable at deciding the shape. So I spent the time up front on the
model and the invariants, wrote those into the prompts as constraints, and kept the
review effort for the three or four places where being subtly wrong would not show up in
an obvious way.

## Converting the requirement into a specification

I read the statement once end to end and turned each numbered requirement into a
behaviour I could assert on. Most of it is unambiguous: a move fills a cell, three in a
line wins, nine filled with no line draws.

The useful part of the exercise was the list of things the brief does not say. I settled
these before writing any code, because they are design decisions and I did not want them
made implicitly:

- **Does reset issue a new game id, or keep it?** The endpoint is
  `POST /api/games/{id}/reset`, which only makes sense if the session outlives the reset.
  The id stays; the frontend never has to swap ids mid-session.
- **In computer mode, is O's reply a second request or part of the human's?** Part of the
  same request. Splitting it puts a step of the turn sequence in the client and leaves a
  render where X has moved and O has not — a state that is not meaningful to a player.
- **Computer-mode undo when only one move has been played?** Remove the single move.
  There is no pair, and refusing would strand the player on a board they cannot clear.
- **Is the scoreboard per game or per session?** `/api/scoreboard` carries no id, so it is
  one scoreboard for the process. That is a statement about scope, not an oversight.
- **Clarification 2.** Option A: undo is disabled once a game is finished. Option B needs
  the scoreboard to be reversible, which means tracking what each game contributed and
  unwinding it — real complexity for a feature nobody asked for. Option A removes the
  class of bug entirely and the brief permits it.

These are in the README under Clarifications, where a reviewer will look for them.

I also fixed two invariants before starting, and they are the reason most of this went
smoothly:

1. The domain type owns every rule. Controllers translate HTTP and nothing else.
2. The frontend derives nothing. If a value is a rule, the response carries it.

Both are testable, and both are things an assistant will quietly violate if you do not say
them.

## The prompts

A note on how these read, because it is the part I would want explained if I were
reviewing someone else's submission. They are longer than a typical prompt and there are
only eight of them. That is deliberate. Asking for "a tic tac toe backend" gets you a
working tic tac toe backend, and none of the decisions in it are yours. Every prompt below
states a constraint I had already settled and why it matters, so what comes back is the
design I chose rather than the most common one on the internet. The failure mode I was
guarding against is not bad code — it is plausible code that quietly answers a question I
had not thought about.

Each one below: the problem I was actually solving, the prompt, and what came back.

**1 — the domain, with the invariants attached**

The first decision is where the rules live, and everything else inherits it. If validation
leaks into controllers you end up asserting game logic through HTTP, and the frontend
starts "helpfully" duplicating a rule or two. So the aggregate owns everything, and the
prompt says so twice.

The second half is the part I would not have written a year ago. A move can be off-board
*and* out of turn at the same time. If the checks run in whatever order is convenient, the
same request can report either reason depending on incidental code structure — and then a
test asserting the reason is testing an accident.

> Build the Game aggregate for tic tac toe. Flat `Player?[9]`, index `row*3+col`. It owns
> move validation, win and draw detection — no rules anywhere else, controllers included.
> Validation runs in a fixed order (completed, bounds, wrong player, occupied) and throws
> with a reason code, because a move can break several rules and the reported reason has
> to be deterministic. Nothing mutates on a rejected move.

Came back close to right. The win check was nested row and column loops; I replaced it
with a static table of the eight triples — shorter, and it gave the computer player a
lookup to reuse instead of writing its own.

**2 — the API surface, shaped around one render path**

The brief says the backend is the source of truth. That is easy to agree with and easy to
half-implement: return a slightly different shape from each endpoint, the client grows a
branch per endpoint, and within an afternoon it is deriving state to fill the gaps.

The fix is structural rather than disciplinary. If every endpoint returns the *same*
object there is one render path and nothing to derive. `canUndo` is the same idea in
miniature: when undo is legal is a game rule, so the server computes it and the button
binds to it. A client that re-derives that rule is a client that can disagree with the
server.

> Contracts and controllers for the seven endpoints. Every game endpoint returns one
> `GameStateResponse` so the client has a single way to render a response. Include
> `canUndo` computed server-side, and embed the scoreboard so a win does not need a second
> round trip. Enums as strings. Rejections as ProblemDetails with the reason code.

I dropped the `IGameStore` interface it added. One implementation, nothing substitutes it
in a test — an interface with a single implementor is a comment pretending to be a type.

**3 — the computer, made deterministic on purpose**

The brief gives the priority list, so the logic was settled before I typed anything. The
only real decision was the corner tie-break, and it is a trade-off worth being explicit
about: random corners feel more like an opponent, fixed corners are testable. I took
testable — "the computer took a corner" asserted over a *set* of acceptable answers is a
weaker test than an assertion on a value.

> Computer player: win, block, centre, corner, any free cell, in that priority. Reuse the
> win-line lookup from the aggregate for the win and block checks. Corners in a fixed
> order, not random — I want a given board to always produce the same move so the tests
> assert on a value rather than a set.

That is the trade-off I would expect to be asked about, and the answer is that this is a
rules exercise, not a game AI exercise.

**4 — undo, which is where the actual thinking went**

The requirement I spent longest on, and the only one where I specified the implementation
rather than the behaviour.

The obvious way to undo a move is to reverse it: clear the cell, flip the player back. It
is what I would have written by hand at speed, and it is subtly wrong — it restores the
board while leaving `Status`, `Winner` and `WinningCells` describing a game that no longer
exists. Worse, it *looks* fine and passes a naive test, because a two-player undo from an
in-progress game has nothing stale to expose. You only see it after undoing a completed
game.

The alternative is to drop the moves and recompute from the history that remains. It
rebuilds nine cells — free at this size — and it cannot drift, because derived state is
never carried forward, only recomputed. So I asked for that directly rather than asking
for undo and reviewing what came back:

> Undo. Two-player drops one move, computer mode drops the pair. Do not reverse state:
> drop the moves from the history and recompute board, status, winner, winning cells and
> current player by replaying what is left. Also handle computer mode with a single move
> in the history — there is no pair to drop, so remove the one move rather than failing.

The last sentence is there because the brief describes computer-mode undo as removing a
pair, and a pair is not always what exists. The API will let you play one move and undo
it. "Remove two" throws on a one-element list; refusing strands the player on a board they
cannot clear. Neither is what a user wants, so the rule is *up to* two.

**5 — the score-once guard**

A genuine correction, and the bug I would point at if asked what I got wrong.

The scoreboard was being written wherever a completed game was observed — which included
the read path. Play a game, refetch it, and the score went up again. Nothing looked broken
while playing; it only shows up if you reload.

The fix is not "add a check", it is deciding *what event* scores a game. A game is scored
by the move that completes it, not by anyone noticing it is complete. Once that is stated,
the flag follows.

> The scoreboard is being incremented wherever a completed game is observed. It needs a
> flag on the game so a result is counted exactly once, at the point the move completes
> it — never on a read, never on a reset.

`ApiTests` covers it now: win a game, read it back, assert the total has not moved.

**6 — the frontend, with the same invariant restated**

I had already said the frontend derives nothing. I said it again here, and I was right to,
because the first version of the game component worked out the current player from
`moves.length`.

That is worth dwelling on, because it is the most instructive moment in the build. The
model was not being careless — deriving the turn from the move count is *correct*, and in
a frontend-only tic tac toe it is what you would write. It is wrong here only because of a
constraint that lives in my head and in the brief's Clarification 1. Constraints that are
obvious to you are invisible to the tool, and those are exactly the ones worth repeating.

> Angular 20 standalone with signals. `GameApi` with one method per endpoint. The game
> component replaces its whole state with each response and derives nothing — no computing
> whose turn it is, no local win check. Board component takes cells, winning cells and an
> interactive flag; it works nothing out itself.

**7 — tests, named against the brief**

I gave it the brief's own list rather than "write tests", so coverage traces back to the
requirement instead of to whatever felt worth testing. The API-level tests are deliberate:
domain tests would not have caught the model-binding problem found in the review pass
below, because that bug lives between HTTP and the domain.

> Backend tests for the matrix in the brief — valid and invalid moves, turn switching, row
> and column and both diagonals, draw, reset, undo in both modes, computer selection per
> rule, move after completion, scoreboard update and update-once. API-level tests through
> WebApplicationFactory for the contract. Frontend: service calls and the board's
> rendering and clicks.

Skeletons were useful, several assertions were not — see below.

**8 — a review pass over the finished code**

By this point everything worked and every test was green, which is the least reliable
moment to trust your own judgement about a codebase. I had been looking at it for two
days and had stopped seeing it.

So I turned the tool around and pointed it at the finished code as a reviewer rather than
an author. Two details in the prompt did the work. Splitting it into three independent
passes stops one concern crowding out the others — a single "review this" prompt reliably
returns style notes. And demanding a concrete failure scenario per finding is the filter:
anything that cannot be stated as "these inputs produce this wrong output" is speculation,
and asking for it up front kills most false positives before I have to read them.

> Review this as if it were someone else's merge request. Three passes: bugs, security,
> and requirements compliance against the brief. High signal only — a concrete failure
> scenario for every finding, and say so plainly if a category is clean rather than
> padding the list.

Security came back clean, which I wanted stated explicitly rather than padded. The other
two passes found five things worth fixing:

- **No in-flight guard on the frontend.** Two quick clicks in two-player mode both sent
  the player from the state that had not been replaced yet, so the second was rejected
  with "It is O's turn" for a move the user was entitled to make. Reproduces reliably on
  a slow connection. Fixed with a `busy` signal, and a component test that asserts a
  second click during a request sends nothing.
- **The computer mutated the live board.** `Completes` tried a piece in each free cell to
  see what it would produce, writing into the real array and clearing it after. Invisible
  single-threaded, wrong in principle, and it made the board briefly show a phantom mark.
  It now works on a copy. `WinningCells` and the board in the response are copied too.
- **A test that passed for the wrong reason.** `FallsBackToAnyFreeCell` used a board where
  the "any free cell" answer was also the blocking move, so it never reached the branch it
  claimed to test — deleting the fallback entirely would not have failed it. I searched
  for a position where no win and no block exist and every corner and the centre are gone,
  and used that instead.
- **An empty request body was a legal move.** `{}` bound to X at row 0, column 0 and got
  played. The request records now require their fields, and reject an undefined enum —
  `{"mode": 99}` was creating a game whose mode matched neither branch.
- **My first fix for that broke the error contract.** I had added `[Range(0, 2)]` to row
  and column, which made model validation reject off-board moves before the domain saw
  them, so the API stopped returning `OutOfBounds` and the frontend showed "the server
  could not be reached" instead of the real reason. Caught by curling the endpoint against
  what the README documents. Bounds are a game rule, so they belong in the domain: the
  attributes now only assert that a field was sent.

The last one is the reason I check documented behaviour against a running server rather
than against the code. A change that improves one thing and quietly breaks another is
exactly what a green test suite will not tell you about.

## What the AI generated

Scaffolding, the DTO records, controller plumbing and attributes, the store, most of the
templates and CSS, test skeletons, and a first draft of the README structure.

## What I changed by hand

- **Replaced the generated `TakesTheWin` test.** It set up a board where O's winning cell
  was already occupied, so it passed without ever exercising the priority. The version in
  the repo puts both sides one move from a line, so it fails if blocking ever beats
  winning. A test that passes for the wrong reason is worse than no test.
- **Split the undo rejection into two reasons.** Undo after a win was reporting
  `NothingToUndo` when the history was full and the real cause was that the game was
  finished. Found by curling the endpoint, not from a failing test.
- **Moved score recording off the read path** and added the `ScoreRecorded` guard.
- **Dropped the `IGameStore` interface** and a mapping layer with nothing to map.
- **Fixed the corner order** to be deterministic.
- **Removed comments that restated the method name**, and defensive null checks the
  domain model makes unreachable.
- **Added the in-flight guard, the board copies, the request validation and the honest
  fallback test** — all from the review pass below.

## What I reviewed carefully

Win and draw detection against a truth table I wrote out by hand — all eight lines plus a
full board with no line. Every undo path traced for both modes, including the empty and
single-move cases. The validation order.

And the API itself: I curled each of the seven endpoints and compared the actual responses
against what the README documents, rather than assuming code and docs agreed. The sample
response in the README is a real one, pasted from a running server. Then I ran the whole
thing in a browser and walked the acceptance criteria in order, which is how the undo
reason-code problem surfaced.

## Assumptions

The open questions above, decided as described and written up in the README.

## Trade-offs

- **Replay on undo** rebuilds nine cells per undo. Irrelevant at this size, and it buys
  code that is correct by construction rather than by careful maintenance.
- **In-memory storage** is what the brief allows and keeps setup to two commands. It costs
  persistence: a restart loses everything.
- **A rule-based computer** is testable and quick to explain, but does not search, so it
  is not unbeatable. Minimax is perhaps thirty lines more and is in the future work list.
- **The scoreboard embedded in the game response** duplicates a little of
  `/api/scoreboard`, in exchange for the UI never needing a second call to stay consistent
  after a win.
- **No CI, no Docker.** Neither is asked for, and both are noise in something meant to be
  cloned and run locally.

## Where it helped and where it did not

Fast for work whose shape was already decided — scaffolding, the DTO layer, Angular
wiring, test skeletons. That is most of the typing and none of the design.

Least useful exactly where the problem was subtle. Reverse-patching undo is the obvious
implementation and it is quietly wrong; the score-once bug sat on the read path where
nothing was obviously misbehaving. Neither would have been caught by asking the tool to
check its own work, because both look correct unless you already know what the right
answer is. That is the part that does not transfer — the tool writes faster than I do, but
it cannot tell me which three things are worth being careful about.
