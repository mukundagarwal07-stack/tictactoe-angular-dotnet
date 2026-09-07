import { Component, input, output } from '@angular/core';
import { Player } from '../game.models';

@Component({
  selector: 'app-board',
  templateUrl: './board.html',
  styleUrl: './board.css'
})
export class Board {
  readonly cells = input.required<(Player | null)[]>();
  readonly winningCells = input<number[] | null>(null);
  readonly interactive = input(true);

  readonly play = output<{ row: number; column: number }>();

  label(index: number): string {
    return `Row ${Math.floor(index / 3) + 1}, Column ${(index % 3) + 1}`;
  }

  isWinning(index: number): boolean {
    return this.winningCells()?.includes(index) ?? false;
  }

  select(index: number): void {
    if (!this.interactive() || this.cells()[index]) return;

    this.play.emit({ row: Math.floor(index / 3), column: index % 3 });
  }
}
