import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BirdFilterOptions, BirdFilterRequest } from '../../core/models/bird.model';

@Component({
  selector: 'app-filter-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="card border-0 shadow-sm mb-4">
      <div class="card-header bg-success text-white">
        <h6 class="mb-0"><i class="bi bi-funnel-fill me-2"></i>Filters</h6>
      </div>
      <div class="card-body">

        <!-- Zoeken -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🔍 Zoeken</label>
          <input type="text" class="form-control form-control-sm"
            placeholder="Naam of wetenschappelijke naam..."
            [(ngModel)]="filter.search" (input)="onFilterChange()">
        </div>

        <!-- Continent -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🌍 Continent</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.continent" (change)="onFilterChange()">
            <option value="">Alle continenten</option>
            <option *ngFor="let c of options?.continents" [value]="c">{{ continentLabel(c) }}</option>
          </select>
        </div>

        <!-- Orde -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🔬 Orde</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.order" (change)="onOrderChange()">
            <option value="">Alle ordes</option>
            <option *ngFor="let o of options?.orders" [value]="o">{{ o }}</option>
          </select>
        </div>

        <!-- Familie -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🧬 Familie</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.family" (change)="onFilterChange()">
            <option value="">Alle families</option>
            <option *ngFor="let f of options?.families" [value]="f">{{ f }}</option>
          </select>
        </div>

        <hr>
        <p class="text-muted small fw-semibold mb-2">Kenmerken</p>

        <!-- Snavel kleur -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🟡 Snavel kleur</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.beakColor" (change)="onFilterChange()">
            <option value="">Alle kleuren</option>
            <option *ngFor="let c of options?.beakColors" [value]="c">{{ c | titlecase }}</option>
          </select>
        </div>

        <!-- Borst kleur -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🔴 Borst kleur</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.breastColor" (change)="onFilterChange()">
            <option value="">Alle kleuren</option>
            <option *ngFor="let c of options?.breastColors" [value]="c">{{ c | titlecase }}</option>
          </select>
        </div>

        <!-- Grootte -->
        <div class="mb-3">
          <label class="form-label fw-semibold">📏 Grootte</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.sizeCategory" (change)="onFilterChange()">
            <option value="">Alle groottes</option>
            <option value="small">Klein</option>
            <option value="medium">Gemiddeld</option>
            <option value="large">Groot</option>
            <option value="very_large">Zeer groot</option>
          </select>
        </div>

        <!-- Habitat -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🌿 Habitat</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.habitatType" (change)="onFilterChange()">
            <option value="">Alle habitats</option>
            <option *ngFor="let h of options?.habitatTypes" [value]="h">{{ h | titlecase }}</option>
          </select>
        </div>

        <!-- Status -->
        <div class="mb-3">
          <label class="form-label fw-semibold">🛡️ Beschermingsstatus</label>
          <select class="form-select form-select-sm" [(ngModel)]="filter.conservationStatus" (change)="onFilterChange()">
            <option value="">Alle statussen</option>
            <option value="LC">LC — Niet bedreigd</option>
            <option value="NT">NT — Bijna bedreigd</option>
            <option value="VU">VU — Kwetsbaar</option>
            <option value="EN">EN — Bedreigd</option>
            <option value="CR">CR — Kritiek bedreigd</option>
          </select>
        </div>

        <button class="btn btn-outline-secondary btn-sm w-100" (click)="resetFilters()">↩ Filters wissen</button>
      </div>
    </div>
  `
})
export class FilterPanelComponent implements OnChanges {
  @Input() options: BirdFilterOptions | null = null;
  @Output() filterChange = new EventEmitter<BirdFilterRequest>();

  filter: BirdFilterRequest = {};

  ngOnChanges(): void { }

  onFilterChange(): void {
    this.filterChange.emit({ ...this.filter, page: 1 });
  }

  onOrderChange(): void {
    this.filter.family = '';
    this.onFilterChange();
  }

  resetFilters(): void {
    this.filter = {};
    this.filterChange.emit({ page: 1 });
  }

  continentLabel(code: string): string {
    const map: Record<string, string> = {
      'EUROPE': '🇪🇺 Europa',
      'AFRICA': '🌍 Afrika',
      'ASIA': '🌏 Azië',
      'NORTH_AMERICA': '🌎 Noord-Amerika',
      'SOUTH_AMERICA': '🌎 Zuid-Amerika',
      'OCEANIA': '🌊 Oceanië',
      'ANTARCTICA': '🧊 Antarctica'
    };
    return map[code] ?? code;
  }
}
