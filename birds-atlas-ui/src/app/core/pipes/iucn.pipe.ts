import { Pipe, PipeTransform } from '@angular/core';

const LABELS: Record<string, string> = {
  LC: 'Niet bedreigd',    NT: 'Bijna bedreigd',
  VU: 'Kwetsbaar',       EN: 'Bedreigd',
  CR: 'Kritiek bedreigd', EW: 'Uitgestorven in wild',
  EX: 'Uitgestorven',    DD: 'Onvoldoende data',
};

@Pipe({ name: 'iucnLabel', standalone: true })
export class IucnLabelPipe implements PipeTransform {
  transform(status: string | null): string {
    if (!status) return 'Niet geëvalueerd';
    return LABELS[status.toUpperCase()] ?? status;
  }
}

@Pipe({ name: 'iucnClass', standalone: true })
export class IucnClassPipe implements PipeTransform {
  transform(status: string | null): string {
    const s = status?.toUpperCase() ?? 'NE';
    const map: Record<string,string> = {
      LC:'badge-lc', NT:'badge-nt', VU:'badge-vu',
      EN:'badge-en', CR:'badge-cr'
    };
    return map[s] ?? 'badge-ne';
  }
}
