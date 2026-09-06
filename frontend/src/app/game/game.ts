import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { Observable } from 'rxjs';
import { Board } from '../board/board';
import { GameApi } from '../game-api';
import { GameMode, GameState } from '../game.models';

@Component({
  selector: 'app-game',
  imports: [Board],
  templateUrl: './game.html',
  styleUrl: './game.css'
})
export class Game implements OnInit {
  private readonly api = inject(GameApi);

  readonly state = signal<GameState | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  /// Clicks are refused while a request is out. Without this a second click sends the
  /// player from the state we have not replaced yet, and the backend rejects a move the
  /// user was entitled to make.
  readonly playable = computed(() => this.state()?.status === 'InProgress' && !this.busy());

  ngOnInit(): void {
    this.newGame('TwoPlayer');
  }

  newGame(mode: GameMode): void {
    this.send(this.api.createGame(mode));
  }

  play(cell: { row: number; column: number }): void {
    const game = this.state();
    if (!game || this.busy()) return;

    this.send(this.api.play(game.gameId, game.currentPlayer, cell.row, cell.column));
  }

  undo(): void {
    const game = this.state();
    if (game) this.send(this.api.undo(game.gameId));
  }

  reset(): void {
    const game = this.state();
    if (game) this.send(this.api.reset(game.gameId));
  }

  resetScoreboard(): void {
    const game = this.state();
    if (!game || this.busy()) return;

    this.busy.set(true);
    this.api.resetScoreboard().subscribe({
      next: scoreboard => {
        this.state.set({ ...this.state()!, scoreboard });
        this.error.set(null);
        this.busy.set(false);
      },
      error: (e: HttpErrorResponse) => this.fail(e)
    });
  }

  statusText(game: GameState): string {
    if (game.status === 'Won') return `${game.winner} wins`;
    if (game.status === 'Draw') return 'Draw';

    return `${game.currentPlayer} to play`;
  }

  position(row: number, column: number): string {
    return `Row ${row + 1}, Column ${column + 1}`;
  }

  private send(request: Observable<GameState>): void {
    this.busy.set(true);
    request.subscribe({
      next: state => {
        this.state.set(state);
        this.error.set(null);
        this.busy.set(false);
      },
      error: (e: HttpErrorResponse) => this.fail(e)
    });
  }

  /// A failed action leaves the board as the server last described it, so the message
  /// explains the rejection without the view drifting from the backend.
  private fail(response: HttpErrorResponse): void {
    this.error.set(response.error?.detail ?? 'The server could not be reached.');
    this.busy.set(false);
  }
}
