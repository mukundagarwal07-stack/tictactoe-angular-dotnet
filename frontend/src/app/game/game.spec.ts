import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../../environments/environment';
import { Game } from './game';
import { GameState } from '../game.models';

describe('Game', () => {
  const base = environment.apiBaseUrl;

  const state = (over: Partial<GameState> = {}): GameState => ({
    gameId: 'g1',
    mode: 'TwoPlayer',
    status: 'InProgress',
    board: new Array(9).fill(null),
    currentPlayer: 'X',
    winner: null,
    winningCells: null,
    canUndo: false,
    moves: [],
    scoreboard: { xWins: 0, oWins: 0, draws: 0 },
    ...over
  });

  let game: Game;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    game = TestBed.createComponent(Game).componentInstance;
    http = TestBed.inject(HttpTestingController);

    game.ngOnInit();
    http.expectOne(`${base}/games`).flush(state());
  });

  afterEach(() => http.verify());

  it('renders the state the backend returned', () => {
    expect(game.state()?.currentPlayer).toBe('X');
    expect(game.statusText(state())).toBe('X to play');
  });

  it('reports the winner rather than a turn once the game is won', () => {
    expect(game.statusText(state({ status: 'Won', winner: 'O' }))).toBe('O wins');
    expect(game.statusText(state({ status: 'Draw' }))).toBe('Draw');
  });

  it('ignores a second click while a move is still in flight', () => {
    game.play({ row: 0, column: 0 });
    game.play({ row: 0, column: 1 });

    // One request, so the second click cannot send a stale current player.
    const request = http.expectOne(`${base}/games/g1/moves`);
    expect(request.request.body).toEqual({ player: 'X', row: 0, column: 0 });
    request.flush(state({ currentPlayer: 'O', canUndo: true }));

    expect(game.playable()).toBeTrue();
  });

  it('shows the reason a move was rejected', () => {
    game.play({ row: 0, column: 0 });

    http.expectOne(`${base}/games/g1/moves`).flush(
      { detail: 'That cell is already taken.' },
      { status: 400, statusText: 'Bad Request' });

    expect(game.error()).toBe('That cell is already taken.');
    expect(game.playable()).toBeTrue();
  });

  it('formats a move position the way the history table shows it', () => {
    expect(game.position(0, 0)).toBe('Row 1, Column 1');
    expect(game.position(2, 1)).toBe('Row 3, Column 2');
  });
});
