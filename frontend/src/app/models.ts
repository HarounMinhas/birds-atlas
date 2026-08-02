export interface BirdSummary {
  id: number;
  commonName: string;
  englishName: string;
  scientificName: string;
  family: string;
  order: string;
  genus: string;
  imageUrl: string | null;
  photoAttribution: string | null;
  photoLicense: string | null;
  iucnStatus: string;
  observationCount: number;
  gbifKey: number | null;
}

export interface BirdListResponse {
  page: number;
  pageSize: number;
  total: number;
  isEstimate: boolean;
  items: BirdSummary[];
}

export interface TaxonomyOptions {
  orders: string[];
  families: string[];
  genera: string[];
}

export interface BirdTaxonomy {
  kingdom: string;
  phylum: string;
  className: string;
  order: string;
  family: string;
  genus: string;
  species: string;
  gbifKey: number | null;
}

export interface AudioRecording {
  id: string;
  commonName: string;
  scientificName: string;
  recordist: string;
  type: string;
  country: string;
  length: string;
  fileUrl: string;
  licenseUrl: string;
  sourceUrl: string;
}

export interface BirdDetail extends BirdSummary {
  wikipediaSummary: string | null;
  wikipediaUrl: string | null;
  inaturalistUrl: string;
  gbifUrl: string | null;
  continents: string[];
  taxonomy: BirdTaxonomy;
  recordings: AudioRecording[];
}

export interface OccurrencePoint {
  key: number;
  latitude: number;
  longitude: number;
  country: string;
  locality: string;
  eventDate: string | null;
  sourceUrl: string;
}

export type MainView = 'discover' | 'map' | 'favorites' | 'lifelist' | 'profile';

export interface StoredBird extends BirdSummary {
  continents?: string[];
}
