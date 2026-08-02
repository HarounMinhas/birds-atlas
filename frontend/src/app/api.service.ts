import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  BirdDetail,
  BirdListResponse,
  OccurrencePoint,
  TaxonomyOptions
} from './models';

export interface BirdQuery {
  q: string;
  page: number;
  pageSize: number;
  continent: string;
  order: string;
  family: string;
  genus: string;
  sort: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = '/api';

  getBirds(query: BirdQuery): Observable<BirdListResponse> {
    let params = new HttpParams()
      .set('page', query.page)
      .set('pageSize', query.pageSize)
      .set('sort', query.sort);

    if (query.q.trim()) params = params.set('q', query.q.trim());
    if (query.continent) params = params.set('continent', query.continent);
    if (query.order) params = params.set('order', query.order);
    if (query.family) params = params.set('family', query.family);
    if (query.genus) params = params.set('genus', query.genus);

    return this.http.get<BirdListResponse>(`${this.baseUrl}/birds`, { params });
  }

  getBird(id: number): Observable<BirdDetail> {
    return this.http.get<BirdDetail>(`${this.baseUrl}/birds/${id}`);
  }

  getOccurrences(id: number, limit = 300): Observable<OccurrencePoint[]> {
    return this.http.get<OccurrencePoint[]>(`${this.baseUrl}/birds/${id}/occurrences`, {
      params: new HttpParams().set('limit', limit)
    });
  }

  getTaxonomyOptions(): Observable<TaxonomyOptions> {
    return this.http.get<TaxonomyOptions>(`${this.baseUrl}/taxonomy/options`);
  }
}
