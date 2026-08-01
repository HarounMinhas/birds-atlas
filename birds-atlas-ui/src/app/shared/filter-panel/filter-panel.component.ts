import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BirdSearchRequest, FilterMeta } from '../../core/models/bird.model';

@Component({
  selector: 'app-filter-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card border-0 shadow-sm">
      <div class="card-header bg-success text-white d-flex justify-content-between align-items-center">
        <span class="fw-semibold small"><i class="bi bi-funnel-fill me-2"></i>Filters</span>
        <button class="btn btn-sm btn-outline-light py-0" (click)="reset()">Wissen</button>
      </div>
      <div class="card-body p-3">

        <div class="mb-3">
          <label class="form-label small fw-semibold mb-1">🔍 Zoeken</label>
          <input class="form-control form-control-sm" placeholder="Naam of soort..."
            [(ngModel)]="req.search" (input)="emit()">
        </div>

        <div class="mb-3">
          <label class="form-label small fw-semibold mb-1">🌍 Continent</label>
          <select class="form-select form-select-sm" [(ngModel)]="req.continent" (change)="emit()">
            <option value="">Alle continenten</option>
            <option *ngFor="let c of meta?.continents" [value]="c">{{ label(c) }}</option>
          </select>
        </div>

        <div class="mb-3">
          <label class="form-label small fw-semibold mb-1">🔬 Orde</label>
          <select class="form-select form-select-sm" [(ngModel)]="req.order" (change)="emit()">
            <option value="">Alle ordes</option>
            <option *ngFor="let o of meta?.orders" [value]="o">{{ o }}</option>
          </select>
        </div>

        <div class="mb-3">
          <label class="form-label small fw-semibold mb-1">🧬 Familie</label>
          <input class="form-control form-control-sm" placeholder="bijv. Turdidae"
            [(ngModel)]="req.family" (input)="emit()">
        </div>

        <div class="mb-3">
          <label class="form-label small fw-semibold mb-1">Genus</label>
          <input class="form-control form-control-sm" placeholder="bijv. Turdus"
            [(ngModel)]="req.genus" (input)="emit()">
        </div>

      </div>
    </div>
  `
})
export class FilterPanelComponent implements OnChanges {
  @Input() meta: FilterMeta | null = null;
  @Output() filterChange = new EventEmitter<BirdSearchRequest>();

  req: BirdSearchRequest = {};
  ngOnChanges(): void {}
  emit(): void { this.filterChange.emit({ ...this.req, offset: 0 }); }
  reset(): void { this.req = {}; this.emit(); }

  label(c: string): string {
    return ({
      EUROPE: '🇪🇺 Europa', AFRICA: '🌍 Afrika', ASIA: '🌏 Azië',
      NORTH_AMERICA: '🌎 Noord-Amerika', SOUTH_AMERICA: '🌎 Zuid-Amerika',
      OCEANIA: '🌊 Oceanië', ANTARCTICA: '🧊 Antarctica'
    } as Record<string, string>)[c] ?? c;
  }
}
