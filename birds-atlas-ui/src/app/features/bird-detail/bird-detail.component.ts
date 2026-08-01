import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { BirdService } from '../../core/services/bird.service';
import { BirdDetail } from '../../core/models/bird.model';

@Component({
  selector: 'app-bird-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="container py-4" *ngIf="bird; else spinner">
      <a routerLink="/birds" class="btn btn-sm btn-outline-secondary mb-3">‹ Terug</a>

      <div class="row g-4">
        <div class="col-md-4">
          <img [src]="bird.imageUrl || 'assets/bird-placeholder.svg'"
            [alt]="bird.commonName"
            class="img-fluid rounded-3 shadow w-100" style="max-height:380px;object-fit:cover"
            loading="lazy">
          <p class="text-muted mt-1" style="font-size:.68rem">Foto via iNaturalist (CC)</p>

          <!-- Geluiden -->
          <div *ngIf="bird.sounds.length > 0" class="mt-3">
            <h6 class="fw-semibold">🔊 Geluiden <span class="badge bg-secondary">Xeno-canto</span></h6>
            <div *ngFor="let s of bird.sounds" class="mb-2">
              <audio controls class="w-100" style="height:36px">
                <source [src]="s.url">
              </audio>
              <p class="text-muted mb-0" style="font-size:.65rem">{{ s.attribution }} &mdash; {{ s.license }}</p>
            </div>
          </div>
        </div>

        <div class="col-md-8">
          <h1 class="fw-bold">{{ bird.commonName }}</h1>
          <p class="fst-italic text-muted fs-5 mb-1">{{ bird.scientificName }}</p>

          <span class="badge fs-6 mb-3" [ngClass]="statusClass(bird.conservationStatus)">
            {{ bird.conservationStatus ?? 'NE' }} &mdash; {{ statusLabel(bird.conservationStatus) }}
          </span>

          <p *ngIf="bird.description" class="text-secondary">{{ bird.description }}</p>

          <!-- Taxonomie -->
          <div class="row g-2 mb-3">
            <div class="col-4" *ngFor="let f of [
              {label:'Orde',    value:bird.order},
              {label:'Familie', value:bird.family},
              {label:'Genus',   value:bird.genus}
            ]">
              <div class="border rounded p-2 small">
                <span class="text-muted d-block">{{ f.label }}</span>
                <strong>{{ f.value || '—' }}</strong>
              </div>
            </div>
          </div>

          <!-- Verspreiding -->
          <h6 class="fw-semibold">🗺️ Verspreiding</h6>
          <div class="d-flex flex-wrap gap-2 mb-3">
            <span *ngFor="let c of bird.continents" class="badge bg-success">{{ contLabel(c) }}</span>
            <span *ngIf="!bird.continents.length" class="text-muted small">Niet beschikbaar</span>
          </div>

          <!-- Links -->
          <div class="d-flex gap-2 mt-3">
            <a *ngIf="bird.wikipediaUrl" [href]="bird.wikipediaUrl" target="_blank"
              class="btn btn-outline-dark btn-sm">📖 Wikipedia</a>
            <a [href]="'https://www.gbif.org/species/' + bird.gbifKey" target="_blank"
              class="btn btn-outline-success btn-sm">GBIF</a>
            <a *ngIf="bird.inatTaxonId" [href]="'https://www.inaturalist.org/taxa/' + bird.inatTaxonId" target="_blank"
              class="btn btn-outline-secondary btn-sm">iNaturalist</a>
          </div>
        </div>
      </div>
    </div>

    <ng-template #spinner>
      <div class="text-center py-5">
        <div class="spinner-border text-success"></div>
        <p class="mt-2 text-muted small">Vogeldata ophalen...</p>
      </div>
    </ng-template>
  `
})
export class BirdDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private svc = inject(BirdService);
  bird: BirdDetail | null = null;

  ngOnInit(): void {
    const key = Number(this.route.snapshot.paramMap.get('id'));
    this.svc.getDetail(key).subscribe(b => this.bird = b);
  }

  contLabel(c: string): string {
    return ({
      EUROPE:'\ud83c\uddea\ud83c\uddfa Europa', AFRICA:'\ud83c\udf0d Afrika', ASIA:'\ud83c\udf0f Azi\u00eb',
      NORTH_AMERICA:'\ud83c\udf0e Noord-Amerika', SOUTH_AMERICA:'\ud83c\udf0e Zuid-Amerika', OCEANIA:'\ud83c\udf0a Oceani\u00eb'
    } as Record<string, string>)[c] ?? c;
  }
  statusClass(s: string | null): string {
    return ({ LC:'bg-success', NT:'bg-info text-dark', VU:'bg-warning text-dark', EN:'bg-danger', CR:'bg-dark' } as any)[s ?? ''] ?? 'bg-secondary';
  }
  statusLabel(s: string | null): string {
    return ({ LC:'Niet bedreigd', NT:'Bijna bedreigd', VU:'Kwetsbaar', EN:'Bedreigd', CR:'Kritiek' } as any)[s ?? ''] ?? 'Niet ge\u00ebvalueerd';
  }
}
