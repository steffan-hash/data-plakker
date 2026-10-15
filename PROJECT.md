# Project: [naam invullen]

Dit bestand is projectspecifiek — hoort niet 1-op-1 gekopieerd te worden
naar een ander project. Onderstaande is een eerste versie op basis van een
eerder gesprek; leent zich voor een vervolggesprek om scherper te maken.

## Overzicht
Klein intern dashboard voor logistiek/operations werk. Verwerkt handmatig
geëxporteerde Excel-bestanden (.xlsx) tot een overzicht/rapportage.
Persoonlijk project. Draait op GitHub voor version control, o.a. omdat het
op meerdere plekken (thuis/werk) wordt bewerkt.

## Status
Techkeuze nog niet gemaakt. Bij de eerste sessie: overleg met Claude welke
aanpak het beste past (bijv. Python + Streamlit voor een lokaal dashboard,
of iets simpelers als het echt klein blijft). Zodra dat vastligt, deze
sectie en de commando's hieronder aanvullen.

## Belangrijkste commando's
```
# TODO: invullen zodra tech gekozen is, bijv.
# python -m streamlit run dashboard.py
# pip install -r requirements.txt
```

## Data & bestanden
- Brondata: Excel-bestanden (.xlsx), handmatig geëxporteerd vanuit [naam bronsysteem invullen]
- Omdat het handmatige exports zijn: format kan per export licht verschillen
  (kolomvolgorde, extra tabbladen, lege rijen bovenaan). Ga niet uit van een
  vast format — controleer bij twijfel het bestand eerst voordat je code
  schrijft die ervan uitgaat.
- Input komt in: `data/input/` (map aanmaken als die nog niet bestaat)
- Output/rapportages gaan naar: `data/output/`

## Conventies
- Scripts in `scripts/`
- Output-bestanden met datum in de naam: `rapportage_YYYY-MM-DD.xlsx`

## Context die niet vanzelfsprekend is
[Vul hier aan: bedrijfsspecifieke afkortingen, kolomnamen die niet voor
zich spreken, of vaste regels in het proces die Claude niet kan afleiden
uit de data zelf.]
