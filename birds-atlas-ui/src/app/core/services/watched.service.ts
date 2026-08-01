import { Injectable, signal, computed } from '@angular/core';
import { BirdSummary } from '../models/bird.model';

/** In-memory watchlist — no localStorage (iframe sandbox). */
@Injectable({ providedIn: 'root' })
export class WatchedService {
  private _list = signal<BirdSummary[]>([]);

  readonly list   = this._list.asReadonly();
  readonly count  = computed(() => this._list().length);
  readonly gbifKeys = computed(() => new Set(this._list().map(b => b.gbifKey)));

  isWatched(gbifKey: number): boolean {
    return this.gbifKeys().has(gbifKey);
  }

  toggle(bird: BirdSummary): void {
    if (this.isWatched(bird.gbifKey)) {
      this._list.set(this._list().filter(b => b.gbifKey !== bird.gbifKey));
    } else {
      this._list.set([...this._list(), bird]);
    }
  }

  remove(gbifKey: number): void {
    this._list.set(this._list().filter(b => b.gbifKey !== gbifKey));
  }
}
