export interface BirdSummary {
  gbifKey: number;
  commonName: string;
  scientificName: string;
  order: string;
  family: string;
  conservationStatus: string | null;
  thumbnailUrl: string | null;
  continents: string[];
}

export interface BirdDetail {
  gbifKey: number;
  inatTaxonId: number | null;
  commonName: string;
  scientificName: string;
  order: string;
  family: string;
  genus: string;
  conservationStatus: string | null;
  description: string | null;
  wikipediaUrl: string | null;
  imageUrl: string | null;
  thumbnailUrl: string | null;
  continents: string[];
  characteristics: Characteristic[];
  sounds: MediaItem[];
}

export interface Characteristic { key: string; label: string; value: string; }
export interface MediaItem { url: string; source: string; attribution: string | null; license: string | null; }

export interface PagedResult<T> {
  items: T[];
  offset: number;
  limit: number;
  total: number;
}

export interface FilterMeta { continents: string[]; orders: string[]; }

export interface BirdSearchRequest {
  continent?: string;
  order?: string;
  family?: string;
  genus?: string;
  search?: string;
  offset?: number;
  limit?: number;
}
