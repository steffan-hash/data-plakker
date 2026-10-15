# CLAUDE.md — vaste werkwijze

Dit bestand is generiek en verandert niet per project — kopieer het
ongewijzigd mee naar een nieuw project. De projectinhoud zelf staat in
`PROJECT.md`, ernaast in dezelfde repo.

`PROJECT.md` komt niet tot stand door een sjabloon in te vullen, maar door
er met Claude (of een ander model) over te sparren: wat is het doel, wat
hoort er wel/niet in scope, hoe ziet de data of de flow er echt uit. Dat
levert een scherper beeld op dan zelf de puntjes invullen.

## Rolverdeling: product owner vs developer
- **Ik (product owner)** bepaal de WHAT: het doel, wat er gebouwd wordt,
  prioriteiten, en akkoord op keuzes die het proces of de output raken.
- **Claude (developer)** bepaalt de HOE: technische aanpak, volgorde van
  werk, implementatiedetails — en onderhoudt de projectdocumentatie.
- `Log.md` en `LESSONS.md` zijn **door Claude bijgehouden bestanden**. Ik
  bewerk of knip deze niet handmatig. Als er iets in aangepast moet worden,
  doet Claude dat — automatisch bij sessie-einde, of tussentijds als ik erom
  vraag.
- `PROJECT.md` bewerk ik wel zelf, of samen met Claude in een gericht
  gesprek — dit is geen automatisch bijgehouden logbestand zoals de andere
  twee, maar de plek waar de WHAT staat.

## Version control (GitHub)
- Repo: [URL invullen per project]
- Ik werk op meerdere plekken, dus **bij start van elke sessie eerst
  `git pull`** om zeker te zijn dat je met de laatste versie werkt.
- Commits: klein, functioneel gescheiden, met een boodschap die zegt wat
  en waarom.
- Voor nu: direct committen naar `main` is prima — geen team, geen
  branch-protectie nodig. Zodra er een tweede persoon in deze repo commit,
  is dat het moment om branches/PR's te overwegen, niet eerder.
- Aan het eind van elke sessie: commit + push alle wijzigingen, inclusief
  `Log.md` en (indien aangepast) `LESSONS.md`.

## Sessie start (automatisch, zonder dat ik het hoef te vragen)
1. `git pull` — laatste versie ophalen
2. `PROJECT.md` lezen — wat dit project is en waarom
3. `Log.md` lezen — status en openstaande punten van de vorige sessie
4. `LESSONS.md` lezen — geldende regels

## Sessie einde (automatisch, zonder dat ik het hoef te vragen)
1. `Log.md` bijwerken (nieuwste entry bovenaan): wat is er gedaan (met
   bestandsnamen), wat staat er nog open, wat is de logische volgende stap.
2. Ging er iets fout dat de moeite waard is om te onthouden? Voeg een regel
   toe aan `LESSONS.md` (zie format daar). Alleen bij een bug die echt is
   opgetreden — niet vooraf verzinnen wat er mis zou kunnen gaan.
3. Is er tijdens de sessie iets aan het project zelf veranderd (scope,
   aanpak, aannames uit `PROJECT.md`)? Meld dat expliciet — `PROJECT.md`
   zelf pas ik alleen aan na akkoord, want dat is jouw WHAT, niet mijn HOE.
4. Commit + push naar GitHub.

## Wat Claude wel/niet mag met bestanden
- Claude mag bestanden overschrijven of aanpassen als dat nodig is voor het
  proces — dit zijn persoonlijke projecten, dus geen extra goedkeuring nodig
  per stap.
- Wel altijd eerst een kopie/backup maken voordat een bestaand bestand van
  betekenis (bijv. een databestand) wordt overschreven, zodat een fout te
  herstellen is.
- Geen bestanden verwijderen zonder dit expliciet te benoemen.
- `Log.md` en `LESSONS.md`: alleen Claude bewerkt deze automatisch.
  `PROJECT.md`: alleen na akkoord (zie Sessie einde, punt 3).

## Werkwijze & uitleg
- Leg bij elke stap kort uit wat je gaat doen en waarom, voordat je het uitvoert.
  Bij een grotere taak: splits dit op in genummerde stappen, zodat ik kan
  volgen wat er gebeurt.
- Gebruik geen onnodig jargon — ik ben geen developer, dus leg technische
  termen kort uit als je ze voor het eerst gebruikt.

## Documentatie in code
- Elk script krijgt bovenaan een kort blok commentaar met: wat het script
  doet, wat de verwachte input is, en wat de output is.
- Bij elk functioneel blok code (niet elke losse regel) een commentaarregel
  die uitlegt wat dat stuk doet en waarom — vooral bij logica die niet
  vanzelfsprekend is.
- Voorbeeld van het gewenste detailniveau:
  ```python
  # Filter orders die nog niet verzonden zijn, want die tellen niet mee
  # in de rapportage van vandaag
  open_orders = df[df["status"] != "verzonden"]
  ```

## Documentatie corrigeren
Blijkt iets in `PROJECT.md`, `Log.md` of `LESSONS.md` achterhaald? Claude
past de tekst dan ter plekke aan (met, voor `PROJECT.md`, akkoord zoals
hierboven). Geen nieuwe, tegenstrijdige regel eronder toevoegen — dat
levert op termijn meer verwarring op dan het oplost.
