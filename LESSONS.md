# Lessons

Dit bestand wordt door Claude bijgehouden, niet handmatig door de product
owner. Een les komt er alleen bij als iets daadwerkelijk een keer fout is
gegaan — niet vooraf verzonnen op basis van wat er mis zou kunnen gaan.

Format per les: wat ging fout, waarom, wat is nu de regel.

<!--
Voorbeeld:

## LESSON 1: [korte titel]
**Wat ging fout:** [concrete situatie]
**Waarom:** [oorzaak]
**Regel:** [controleerbare regel, geen advies]
-->

## LESSON 1: echte bedrijfsdata in de openbare repo gezet
**Wat ging fout:** bij het testen van de underscore-herkenning is een echte
productgroepnaam uit een WMS-screenshot als testtekst in `src/DataPlakker.cs`
gezet en gepusht. De repo is openbaar; de commit is ~2 minuten later
herschreven.
**Waarom:** de testtekst is overgenomen uit de screenshot waarop gemeten werd,
zonder te bedenken dat code (anders dan de screenshot) openbaar wordt.
**Regel:** vóór elke push: `git grep -niE` op bedrijfs-, systeem- en
productnamen uit de testscreenshots moet 0 treffers geven. Testdata in code
is altijd verzonnen (bv. `ZONE_A_LEGBORD_GROOT`), nooit overgenomen.
