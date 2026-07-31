import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BirdDetail, BirdFilterOptions, BirdFilterRequest, BirdListItem, PagedResult } from '../models/bird.model';

@Injectable({ providedIn: 'root' })
export class BirdService {
  private http = inject(HttpClient);
  private base = environment.apiUrl;

  getBirds(filter: BirdFilterRequest): Observable<PagedResult<BirdListItem>> {
    let params = new HttpParams();
    Object.entries(filter).forEach(([key, val]) => {
      if (val !== undefined && val !== null && val !== '') {
        params = params.set(key, String(val));
      }
    });
    return this.http.get<PagedResult<BirdListItem>>(`${this.base}/birds`, { params });
  }

  getBird(id: number): Observable<BirdDetail> {
    return this.http.get<BirdDetail>(`${this.base}/birds/${id}`);
  }

  getFilterOptions(): Observable<BirdFilterOptions> {
    return this.http.get<BirdFilterOptions>(`${this.base}/birds/filters`);
  }

  getOrders(): Observable<string[]> {
    return this.http.get<string[]>(`${this.base}/taxonomy/orders`);
  }

  getFamilies(order?: string): Observable<string[]> {
    let params = new HttpParams();
    if (order) params = params.set('order', order);
    return this.http.get<string[]>(`${this.base}/taxonomy/families`, { params });
  }
}
