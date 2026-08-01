import { Component, EventEmitter, Input, Output, OnChanges } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { BirdSearchRequest, FilterMeta } from '../../core/models/bird.model';
import { ContinentLabelPipe } from '../../core/pipes/continent-label.pipe';

@Component({
  selector: 'app-filter-panel',
  standalone: true,
  imports: [CommonModule, FormsModule, ContinentLabelPipe],
  styles: [`
    :host { display: block; }
    .panel { background: var(--color-surface); border: 1px solid var(--color-border); border-radius: var(--radius-xl); overflow: hidden; box-shadow: var(--shadow-sm); }
    .panel-header { background: var(--color-primary); color: #fff; padding: .75rem 1rem; display: flex; justify-content: space-between; align-items: center; }
    .panel-title { font-weight: 600; font-size: var(--text-sm); display: flex; align-items: center; gap: .5rem; }
    .btn-reset { font-size: var(--text-xs); padding: .2rem .6rem; border-radius: var(--radius-full); border: 1px solid rgba(255,255,255,.4); color: rgba(255,255,255,.9); background: transparent; cursor: pointer; }
    .btn-reset:hover { background: rgba(255,255,255,.15); }
    .panel-body { padding: 1rem; display: flex; flex-direction: column; gap: .75rem; }
    label { font-size: var(--text-xs); font-weight: 600; color: var(--color-text-muted); display: block; margin-bottom: .25rem; }
    input, select { width: 100%; padding: .4rem .65rem; border: 1.5px solid var(--color-border); border-radius: var(--radius-md); background: var(--color-surface-2); font-size: var(--text-sm); color: var(--color-text); outline: none; }
    input:focus, select:focus { border-color: var(--color-primary); }
  `],
  template: `
    <div class="panel">
      <div class="panel-header">
        <span class="panel-title">
          <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <polygon points="22 3 2 3 10 12.46 10 19 14 21 14 12.46 22 3"/>
          </svg>
          Filters
        </span>
        <button class="btn-reset" (click)="reset()">Wissen</button>
      </div>
      <div class="panel-body">
        <div>
          <label>🔍 Zoeken</label>
          <input placeholder="Naam, soort..." [(ngModel)]="req.search" (ngModelChange)="emit()">
        </div>
        <div>
          <label>🌍 Continent</label>
          <select [(ngModel)]="req.continent" (ngModelChange)="emit()">
            <option value="">Alle continenten</option>
            <option *ngFor="let c of meta?.continents" [value]="c">{{ c | continentLabel }}</option>
          </select>
        </div>
        <div>
          <label>🔬 Orde</label>
          <select [(ngModel)]="req.order" (ngModelChange)="emit()">
            <option value="">Alle ordes</option>
            <option *ngFor="let o of meta?.orders" [value]="o">{{ o }}</option>
          </select>
        </div>
        <div>
          <label>🧬 Familie</label>
          <input placeholder="bijv. Turdidae" [(ngModel)]="req.family" (ngModelChange)="emit()">
        </div>
        <div>
          <label>Genus</label>
          <input placeholder="bijv. Turdus" [(ngModel)]="req.genus" (ngModelChange)="emit()">
        </div>
      </div>
    </div>
  `
})
export class FilterPanelComponent implements OnChanges {
  @Input() meta: FilterMeta | null = null;
  @Input() initial: BirdSearchRequest = {};
  @Output() filterChange = new EventEmitter<BirdSearchRequest>();

  req: BirdSearchRequest = {};

  ngOnChanges(): void {
    this.req = { ...this.initial };
  }

  emit(): void { this.filterChange.emit({ ...this.req, offset: 0 }); }
  reset(): void { this.req = {}; this.emit(); }
}
