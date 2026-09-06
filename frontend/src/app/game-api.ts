import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../environments/environment';
import { GameMode, GameState, Player, Scoreboard } from './game.models';

@Injectable({ providedIn: 'root' })
export class GameApi {
  private readonly http = inject(HttpClient);
  private readonly base = environment.apiBaseUrl;

  createGame(mode: GameMode): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games`, { mode });
  }

  getGame(id: string): Observable<GameState> {
    return this.http.get<GameState>(`${this.base}/games/${id}`);
  }

  play(id: string, player: Player, row: number, column: number): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/moves`, { player, row, column });
  }

  undo(id: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/undo`, null);
  }

  reset(id: string): Observable<GameState> {
    return this.http.post<GameState>(`${this.base}/games/${id}/reset`, null);
  }

  resetScoreboard(): Observable<Scoreboard> {
    return this.http.post<Scoreboard>(`${this.base}/scoreboard/reset`, null);
  }
}
