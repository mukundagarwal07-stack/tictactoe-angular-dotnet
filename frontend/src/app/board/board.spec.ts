import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Board } from './board';
import { Player } from '../game.models';

describe('Board', () => {
  let fixture: ComponentFixture<Board>;

  const cells = (): HTMLButtonElement[] =>
    Array.from(fixture.nativeElement.querySelectorAll('.cell'));

  beforeEach(async () => {
    await TestBed.configureTestingModule({ imports: [Board] }).compileComponents();

    fixture = TestBed.createComponent(Board);
    fixture.componentRef.setInput('cells', new Array<Player | null>(9).fill(null));
    fixture.detectChanges();
  });

  it('renders nine cells', () => {
    expect(cells().length).toBe(9);
  });

  it('shows the marks that are on the board', () => {
    fixture.componentRef.setInput('cells', ['X', null, 'O', null, null, null, null, null, null]);
    fixture.detectChanges();

    expect(cells()[0].textContent?.trim()).toBe('X');
    expect(cells()[2].textContent?.trim()).toBe('O');
  });

  it('highlights the winning line', () => {
    fixture.componentRef.setInput('cells', ['X', 'X', 'X', null, null, null, null, null, null]);
    fixture.componentRef.setInput('winningCells', [0, 1, 2]);
    fixture.detectChanges();

    expect(cells()[0].classList).toContain('win');
    expect(cells()[3].classList).not.toContain('win');
  });

  it('emits the row and column of an empty cell', () => {
    const played: { row: number; column: number }[] = [];
    fixture.componentInstance.play.subscribe(move => played.push(move));

    cells()[5].click();

    expect(played).toEqual([{ row: 1, column: 2 }]);
  });

  it('does not emit for a cell that is already taken', () => {
    fixture.componentRef.setInput('cells', ['X', null, null, null, null, null, null, null, null]);
    fixture.detectChanges();

    let emitted = false;
    fixture.componentInstance.play.subscribe(() => (emitted = true));

    cells()[0].click();

    expect(emitted).toBeFalse();
    expect(cells()[0].disabled).toBeTrue();
  });

  it('disables every cell once the game is over', () => {
    fixture.componentRef.setInput('interactive', false);
    fixture.detectChanges();

    expect(cells().every(cell => cell.disabled)).toBeTrue();
  });
});
