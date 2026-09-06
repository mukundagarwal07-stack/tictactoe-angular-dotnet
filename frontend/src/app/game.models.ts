export type Player = 'X' | 'O';
export type GameMode = 'TwoPlayer' | 'Computer';
export type GameStatus = 'InProgress' | 'Won' | 'Draw';

export interface Move {
  number: number;
  player: Player;
  row: number;
  column: number;
}

export interface Scoreboard {
  xWins: number;
  oWins: number;
  draws: number;
}

export interface GameState {
  gameId: string;
  mode: GameMode;
  status: GameStatus;
  board: (Player | null)[];
  currentPlayer: Player;
  winner: Player | null;
  winningCells: number[] | null;
  canUndo: boolean;
  moves: Move[];
  scoreboard: Scoreboard;
}
