import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { ContinentLabelPipe } from '../../core/pipes/continent-label.pipe';

@Component({
  selector: 'app-map',
  standalone: true,
  imports: [CommonModule, RouterModule, ContinentLabelPipe],
  styles: [`
    :host { display: block; }
    .map-box {
      width: 100%; aspect-ratio: 16/7; min-height: 300px;
      background: linear-gradient(135deg, #1e3a1a 0%, #2d5a27 40%, #3d8035 70%, #1e3a1a 100%);
      border-radius: var(--radius-2xl); overflow: hidden; position: relative;
      display: flex; align-items: center; justify-content: center;
      box-shadow: var(--shadow-lg);
    }
    #map { position: absolute; inset: 0; width: 100%; height: 100%; }
    .map-caption {
      color: rgba(255,255,255,.9); text-align: center; z-index: 2; pointer-events: none;
      h3 { font-family: var(--font-display); font-size: var(--text-xl); }
      p { font-size: var(--text-sm); opacity: .8; margin-top: .35rem; }
    }
    .legend { display: flex; flex-wrap: wrap; gap: .5rem; margin-top: var(--space-3); }
    .legend-item { display: flex; align-items: center; gap: .35rem; font-size: var(--text-xs); color: var(--color-text-muted); }
    .legend-dot { width: 10px; height: 10px; border-radius: 50%; }
  `],
  template: `
    <div class="page-header">
      <div>
        <h1 class="page-title">Verspreidingskaart</h1>
        <p class="page-subtitle">GBIF occurrence data — reële waarnemingspunten wereldwijd</p>
      </div>
    </div>

    <div class="map-box">
      <div id="map"></div>
      <div class="map-caption">
        <h3>🗺️ Interactieve Kaart</h3>
        <p>Leaflet.js + GBIF Occurrence API</p>
        <p style="font-size:.75rem;opacity:.7;margin-top:.5rem">Voeg Leaflet toe aan index.html om de kaart te activeren</p>
      </div>
    </div>

    <div class="legend">
      <div class="legend-item">
        <div class="legend-dot" style="background:var(--color-primary);border:2px solid white"></div>
        Waarnemingspunt (GBIF)
      </div>
      <div class="legend-item">
        <div class="legend-dot" style="background:var(--color-accent);border:2px solid white"></div>
        Broedgebied
      </div>
    </div>

    <div style="margin-top:var(--space-6)">
      <h2 style="font-size:var(--text-lg);font-weight:600;margin-bottom:var(--space-3)">Overzicht per continent</h2>
      <div style="display:grid;grid-template-columns:repeat(auto-fill,minmax(160px,1fr));gap:var(--space-3)">
        <div *ngFor="let c of continents"
          style="background:var(--color-surface);border:1px solid var(--color-border);border-radius:var(--radius-lg);padding:var(--space-4);box-shadow:var(--shadow-sm);cursor:pointer"
          routerLink="/birds" [queryParams]="{continent: c}">
          <div style="font-size:1.5rem;margin-bottom:.25rem">{{ c | continentLabel:'flag' }}</div>
          <div style="font-size:var(--text-sm);font-weight:600">{{ c | continentLabel }}</div>
          <div style="font-size:var(--text-xs);color:var(--color-text-faint);margin-top:.15rem">Bekijk soorten →</div>
        </div>
      </div>
    </div>
  `
})
export class MapComponent {
  private svc = inject(BirdService);
  continents = ['EUROPE','AFRICA','ASIA','NORTH_AMERICA','SOUTH_AMERICA','OCEANIA','ANTARCTICA'];
}
