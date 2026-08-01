import { CommonModule } from '@angular/common';
import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import * as L from 'leaflet';
import { Subscription, finalize } from 'rxjs';
import { ApiService } from './api.service';
import {
  BirdDetail,
  BirdSummary,
  MainView,
  OccurrencePoint,
  StoredBird,
  TaxonomyOptions
} from './models';

interface NavItem {
  view: MainView;
  icon: string;
  label: string;
}

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './app.component.html',
  styleUrl: './app.component.css'
})
export class AppComponent implements OnInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly subscriptions = new Subscription();
  private searchTimer?: ReturnType<typeof setTimeout>;
  private map?: L.Map;
  private readonly mapLayer = L.layerGroup();
  private mapRequestToken = 0;

  readonly navItems: NavItem[] = [
    { view: 'discover', icon: 'grid', label: 'Ontdek soorten' },
    { view: 'map', icon: 'map', label: 'Kaart' },
    { view: 'favorites', icon: 'heart', label: 'Favorieten' },
    { view: 'lifelist', icon: 'sound', label: 'Mijn lifelist' },
    { view: 'profile', icon: 'user', label: 'Profiel' }
  ];

  readonly continents = [
    { code: '', label: 'Alle' },
    { code: 'AFRICA', label: 'Afrika' },
    { code: 'ASIA', label: 'Azie' },
    { code: 'EUROPE', label: 'Europa' },
    { code: 'NORTH_AMERICA', label: 'Noord-Amerika' },
    { code: 'SOUTH_AMERICA', label: 'Zuid-Amerika' },
    { code: 'OCEANIA', label: 'Oceanie' }
  ];

  readonly iucnLabels: Record<string, string> = {
    LC: 'Least Concern',
    NT: 'Near Threatened',
    VU: 'Vulnerable',
    EN: 'Endangered',
    CR: 'Critically Endangered',
    EW: 'Extinct in the Wild',
    EX: 'Extinct',
    DD: 'Data Deficient',
    NE: 'Not Evaluated'
  };

  activeView: MainView = 'discover';
  birds: BirdSummary[] = [];
  favorites: StoredBird[] = [];
  seen: StoredBird[] = [];
  comparisonIds: number[] = [];
  taxonomy: TaxonomyOptions = { orders: [], families: [], genera: [] };

  query = '';
  continent = '';
  order = '';
  family = '';
  genus = '';
  sort = 'observations';
  page = 1;
  readonly pageSize = 24;
  total = 0;
  totalIsEstimate = false;

  loading = false;
  error = '';
  showFilters = false;
  mobileSearchOpen = false;
  toast = '';

  selectedBird: BirdDetail | null = null;
  detailLoading = false;
  detailError = '';

  mapLoading = false;
  mapError = '';
  mapSpeciesNames: string[] = [];
  pagesViewed = 0;

  ngOnInit(): void {
    this.restoreLocalState();
    this.loadTaxonomy();
    this.loadBirds(false);
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.map?.remove();
  }

  get visibleBirds(): BirdSummary[] {
    if (this.activeView === 'favorites') return this.favorites;
    if (this.activeView === 'lifelist') return this.seen;
    return this.birds;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.total / this.pageSize));
  }

  get currentTitle(): string {
    switch (this.activeView) {
      case 'favorites': return 'Mijn favorieten';
      case 'lifelist': return 'Mijn lifelist';
      case 'map': return 'Verspreidingskaart';
      case 'profile': return 'Mijn vogelprofiel';
      default: return `${this.total.toLocaleString('nl-NL')} soorten`;
    }
  }

  get uniqueSeenContinents(): string[] {
    return [...new Set(this.seen.flatMap((bird) => bird.continents ?? []))];
  }

  get uniqueSeenFamilies(): number {
    return new Set(this.seen.map((bird) => bird.family).filter(Boolean)).size;
  }

  setView(view: MainView): void {
    this.activeView = view;
    this.mobileSearchOpen = false;
    if (view === 'map') {
      window.setTimeout(() => this.renderMap(), 0);
    }
  }

  onSearchInput(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadBirds(true), 350);
  }

  selectContinent(code: string): void {
    this.continent = code;
    this.loadBirds(true);
  }

  applyFilters(): void {
    this.showFilters = false;
    this.loadBirds(true);
  }

  clearFilters(): void {
    this.order = '';
    this.family = '';
    this.genus = '';
    this.continent = '';
    this.loadBirds(true);
  }

  changePage(nextPage: number): void {
    if (nextPage < 1 || nextPage > this.totalPages || nextPage === this.page) return;
    this.page = nextPage;
    this.loadBirds(false);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  openDetail(id: number): void {
    this.detailLoading = true;
    this.detailError = '';
    this.selectedBird = null;
    this.pagesViewed += 1;
    localStorage.setItem('birdatlas:pagesViewed', String(this.pagesViewed));

    this.subscriptions.add(
      this.api.getBird(id)
        .pipe(finalize(() => this.detailLoading = false))
        .subscribe({
          next: (bird) => {
            this.selectedBird = bird;
            this.refreshStoredBird(bird);
          },
          error: () => this.detailError = 'De soortinformatie kon niet worden geladen.'
        })
    );
  }

  closeDetail(): void {
    this.selectedBird = null;
    this.detailError = '';
  }

  toggleFavorite(bird: BirdSummary | BirdDetail, event?: Event): void {
    event?.stopPropagation();
    const index = this.favorites.findIndex((item) => item.id === bird.id);
    if (index >= 0) {
      this.favorites.splice(index, 1);
      this.showToast('Verwijderd uit favorieten');
    } else {
      this.favorites.unshift(this.toStoredBird(bird));
      this.showToast('Toegevoegd aan favorieten');
    }
    this.persistBirds('birdatlas:favorites', this.favorites);
  }

  toggleSeen(bird: BirdSummary | BirdDetail, event?: Event): void {
    event?.stopPropagation();
    const index = this.seen.findIndex((item) => item.id === bird.id);
    if (index >= 0) {
      this.seen.splice(index, 1);
      this.showToast('Verwijderd uit lifelist');
    } else {
      this.seen.unshift(this.toStoredBird(bird));
      this.showToast('Gemarkeerd als gezien');
    }
    this.persistBirds('birdatlas:seen', this.seen);
  }

  isFavorite(id: number): boolean {
    return this.favorites.some((bird) => bird.id === id);
  }

  isSeen(id: number): boolean {
    return this.seen.some((bird) => bird.id === id);
  }

  toggleComparison(bird: BirdSummary, event?: Event): void {
    event?.stopPropagation();
    const index = this.comparisonIds.indexOf(bird.id);
    if (index >= 0) {
      this.comparisonIds.splice(index, 1);
    } else if (this.comparisonIds.length < 4) {
      this.comparisonIds.push(bird.id);
    } else {
      this.showToast('Je kunt maximaal vier soorten vergelijken');
      return;
    }
    localStorage.setItem('birdatlas:comparison', JSON.stringify(this.comparisonIds));
    if (this.activeView === 'map') this.renderMap();
  }

  isCompared(id: number): boolean {
    return this.comparisonIds.includes(id);
  }

  showBirdOnMap(bird: BirdSummary | BirdDetail): void {
    if (!this.comparisonIds.includes(bird.id)) {
      this.comparisonIds = [bird.id, ...this.comparisonIds].slice(0, 4);
    }
    localStorage.setItem('birdatlas:comparison', JSON.stringify(this.comparisonIds));
    this.closeDetail();
    this.setView('map');
  }

  retry(): void {
    this.loadBirds(false);
  }

  handleImageError(event: Event): void {
    const image = event.target as HTMLImageElement;
    image.style.display = 'none';
  }

  statusLabel(status: string): string {
    return this.iucnLabels[status] ?? status;
  }

  continentLabel(code: string): string {
    return this.continents.find((item) => item.code === code)?.label ?? code.replaceAll('_', ' ');
  }

  trackBird(_: number, bird: BirdSummary): number {
    return bird.id;
  }

  loadBirds(resetPage: boolean): void {
    if (resetPage) this.page = 1;
    this.loading = true;
    this.error = '';

    this.subscriptions.add(
      this.api.getBirds({
        q: this.query,
        page: this.page,
        pageSize: this.pageSize,
        continent: this.continent,
        order: this.order,
        family: this.family,
        genus: this.genus,
        sort: this.sort
      })
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (response) => {
          this.birds = response.items;
          this.total = response.total;
          this.totalIsEstimate = response.isEstimate;
        },
        error: () => {
          this.birds = [];
          this.error = 'Birds Atlas kan de databronnen nu niet bereiken. Controleer of de API draait.';
        }
      })
    );
  }

  private loadTaxonomy(): void {
    this.subscriptions.add(
      this.api.getTaxonomyOptions().subscribe({
        next: (options) => this.taxonomy = options,
        error: () => {
          this.taxonomy = { orders: [], families: [], genera: [] };
        }
      })
    );
  }

  private renderMap(): void {
    const container = document.getElementById('atlas-map');
    if (!container) return;

    if (!this.map) {
      this.map = L.map(container, { zoomControl: true, minZoom: 2 }).setView([20, 5], 2);
      L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 18,
        attribution: '&copy; OpenStreetMap contributors'
      }).addTo(this.map);
      this.mapLayer.addTo(this.map);
    } else {
      this.map.invalidateSize();
    }

    this.mapLayer.clearLayers();
    this.mapError = '';
    this.mapSpeciesNames = [];

    const selectedBirds = this.resolveMapBirds();
    if (!selectedBirds.length) {
      this.mapLoading = false;
      this.mapError = 'Selecteer een of meer soorten via de vergelijkknop op een vogelkaart.';
      return;
    }

    const token = ++this.mapRequestToken;
    this.mapLoading = true;
    const allCoordinates: L.LatLngExpression[] = [];
    let completed = 0;
    const palette = ['#1f6a3c', '#c4672c', '#476c9b', '#7b4f8d'];

    selectedBirds.forEach((bird, index) => {
      this.subscriptions.add(
        this.api.getOccurrences(bird.id, 300).subscribe({
          next: (points) => {
            if (token !== this.mapRequestToken) return;
            this.mapSpeciesNames.push(bird.commonName || bird.scientificName);
            this.addOccurrencePoints(points, bird, palette[index], allCoordinates);
          },
          error: () => undefined,
          complete: () => {
            if (token !== this.mapRequestToken) return;
            completed += 1;
            if (completed === selectedBirds.length) {
              this.mapLoading = false;
              if (allCoordinates.length && this.map) {
                this.map.fitBounds(L.latLngBounds(allCoordinates), { padding: [24, 24], maxZoom: 6 });
              } else {
                this.mapError = 'Voor deze selectie zijn geen GBIF-punten met coordinaten gevonden.';
              }
            }
          }
        })
      );
    });
  }

  private addOccurrencePoints(
    points: OccurrencePoint[],
    bird: BirdSummary,
    color: string,
    allCoordinates: L.LatLngExpression[]
  ): void {
    for (const point of points) {
      const latLng: L.LatLngExpression = [point.latitude, point.longitude];
      allCoordinates.push(latLng);
      const marker = L.circleMarker(latLng, {
        radius: 4,
        weight: 1,
        color,
        fillColor: color,
        fillOpacity: 0.62
      });
      const place = [point.locality, point.country].filter(Boolean).join(', ');
      marker.bindPopup(
        `<strong>${this.escapeHtml(bird.commonName || bird.scientificName)}</strong><br>` +
        `${this.escapeHtml(place || 'GBIF occurrence')}<br>` +
        `<a href="${point.sourceUrl}" target="_blank" rel="noopener">Bekijk bij GBIF</a>`
      );
      marker.addTo(this.mapLayer);
    }
  }

  private resolveMapBirds(): BirdSummary[] {
    const allBirds = [...this.birds, ...this.favorites, ...this.seen];
    const unique = new Map(allBirds.map((bird) => [bird.id, bird]));
    const selected = this.comparisonIds
      .map((id) => unique.get(id))
      .filter((bird): bird is BirdSummary => Boolean(bird));

    if (selected.length) return selected;
    return this.birds.slice(0, 1);
  }

  private restoreLocalState(): void {
    this.favorites = this.readStoredBirds('birdatlas:favorites');
    this.seen = this.readStoredBirds('birdatlas:seen');
    this.pagesViewed = Number(localStorage.getItem('birdatlas:pagesViewed') ?? 0);
    try {
      const parsed: unknown = JSON.parse(localStorage.getItem('birdatlas:comparison') ?? '[]');
      this.comparisonIds = Array.isArray(parsed)
        ? parsed.filter((id): id is number => typeof id === 'number').slice(0, 4)
        : [];
    } catch {
      this.comparisonIds = [];
    }
  }

  private readStoredBirds(key: string): StoredBird[] {
    try {
      const parsed: unknown = JSON.parse(localStorage.getItem(key) ?? '[]');
      return Array.isArray(parsed) ? parsed as StoredBird[] : [];
    } catch {
      return [];
    }
  }

  private persistBirds(key: string, birds: StoredBird[]): void {
    localStorage.setItem(key, JSON.stringify(birds));
  }

  private toStoredBird(bird: BirdSummary | BirdDetail): StoredBird {
    return {
      id: bird.id,
      commonName: bird.commonName,
      englishName: bird.englishName,
      scientificName: bird.scientificName,
      family: bird.family,
      order: bird.order,
      genus: bird.genus,
      imageUrl: bird.imageUrl,
      photoAttribution: bird.photoAttribution,
      photoLicense: bird.photoLicense,
      iucnStatus: bird.iucnStatus,
      observationCount: bird.observationCount,
      gbifKey: bird.gbifKey,
      continents: 'continents' in bird ? bird.continents : undefined
    };
  }

  private refreshStoredBird(bird: BirdDetail): void {
    const refresh = (items: StoredBird[], key: string): void => {
      const index = items.findIndex((item) => item.id === bird.id);
      if (index >= 0) {
        items[index] = this.toStoredBird(bird);
        this.persistBirds(key, items);
      }
    };
    refresh(this.favorites, 'birdatlas:favorites');
    refresh(this.seen, 'birdatlas:seen');
  }

  private showToast(message: string): void {
    this.toast = message;
    window.setTimeout(() => {
      if (this.toast === message) this.toast = '';
    }, 2400);
  }

  private escapeHtml(value: string): string {
    return value.replace(/[&<>'"]/g, (character) => ({
      '&': '&amp;',
      '<': '&lt;',
      '>': '&gt;',
      "'": '&#039;',
      '"': '&quot;'
    })[character] ?? character);
  }
}
