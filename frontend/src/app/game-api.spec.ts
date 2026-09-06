import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { environment } from '../environments/environment';
import { GameApi } from './game-api';
import { GameState } from './game.models';

describe('GameApi', () => {
  const base = environment.apiBaseUrl;
  const id = '11111111-1111-1111-1111-111111111111';

  let api: GameApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()]
    });

    api = TestBed.inject(GameApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('posts the mode when creating a game', () => {
    api.createGame('Computer').subscribe();

    const request = http.expectOne(`${base}/games`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ mode: 'Computer' });
    request.flush({} as GameState);
  });

  it('posts the player and cell when playing a move', () => {
    api.play(id, 'X', 1, 2).subscribe();

    const request = http.expectOne(`${base}/games/${id}/moves`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ player: 'X', row: 1, column: 2 });
    request.flush({} as GameState);
  });

  it('posts to the undo endpoint', () => {
    api.undo(id).subscribe();

    const request = http.expectOne(`${base}/games/${id}/undo`);
    expect(request.request.method).toBe('POST');
    request.flush({} as GameState);
  });

  it('posts to the reset endpoint', () => {
    api.reset(id).subscribe();

    const request = http.expectOne(`${base}/games/${id}/reset`);
    expect(request.request.method).toBe('POST');
    request.flush({} as GameState);
  });

  it('posts to the scoreboard reset endpoint', () => {
    api.resetScoreboard().subscribe();

    const request = http.expectOne(`${base}/scoreboard/reset`);
    expect(request.request.method).toBe('POST');
    request.flush({ xWins: 0, oWins: 0, draws: 0 });
  });
});
