import { Pipe, PipeTransform } from '@angular/core';

const MAP: Record<string, string> = {
  EUROPE:        '🇪🇺 Europa',
  AFRICA:        '🌍 Afrika',
  ASIA:          '🌏 Azië',
  NORTH_AMERICA: '🌎 Noord-Amerika',
  SOUTH_AMERICA: '🌎 Zuid-Amerika',
  OCEANIA:       '🌊 Oceanië',
  ANTARCTICA:    '🧊 Antarctica',
};

const FLAGS: Record<string, string> = {
  EUROPE:'🇪🇺', AFRICA:'🌍', ASIA:'🌏',
  NORTH_AMERICA:'🌎', SOUTH_AMERICA:'🌎', OCEANIA:'🌊', ANTARCTICA:'🧊'
};

@Pipe({ name: 'continentLabel', standalone: true })
export class ContinentLabelPipe implements PipeTransform {
  transform(value: string, mode: 'full' | 'flag' = 'full'): string {
    return mode === 'flag' ? (FLAGS[value] ?? value) : (MAP[value] ?? value);
  }
}
