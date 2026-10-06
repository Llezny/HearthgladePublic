# Edytor POI w scenie

Narzędzie do układania punktów zainteresowania (`PoiSO`: obozy, porty) jak sceny, zamiast
ręcznego wpisywania `offset`/`rotation` w listach assetu. Kod: `Gameplay/Map/Poi/PoiAuthoring.cs`
(komponenty rysujące, tylko edytor) i `Editor/PoiEditorTool.cs` (narzędzie, inspektory).

## Jak używać

1. Zaznacz asset `PoiSO` w Project i kliknij **Edit layout in scene** w inspektorze albo
   `Tools > POI > Edit selected POI in scene`. Otwiera się scena
   `Assets/Scenes/Authoring/PoiEditor.unity` (tworzona przy pierwszym użyciu, w `.gitignore`:
   to kopia robocza, źródłem prawdy jest asset).
2. Elementy to **instancje prefabów** pod obiektem `POI layout`. Przesuwasz, obracasz,
   duplikujesz (Ctrl+D) i usuwasz je zwykłymi narzędziami Unity. W trakcie ruchu przyciągają
   się do komórek mapy (`snapStep`, domyślnie pół komórki; obrót co `rotationStep` = 15°).
3. Dodawanie: pole **Piece (prefab)** w inspektorze `POI layout` (ląduje na środku widoku),
   albo przeciągnij prefab do sceny i kliknij **Adopt prefab instances dragged into the scene**
   (albo przeciągnij go w Hierarchy na `POI layout`).
4. **Ścieżki** (malowane w terenie, bez obiektów): przyciski **Path** i **Round plaza**. Ścieżka to
   obiekt z dwoma końcami `From`/`To` (te same miejsca = okrągły plac), `halfWidth` i wyglądem
   (`look`). Po zaznaczeniu ścieżki w Scene view są uchwyty końców i szerokości.
5. **Save to POI asset** zapisuje układ do `PoiSO` (`pieces` i `paths`; reszta pól assetu bez
   zmian). Zapis zapisuje też Ctrl+S sceny (`saveToAssetWhenSceneIsSaved`). Przełączenie na inny POI
   z niezapisanymi zmianami pyta, co zrobić.

## Co widać w Scene view

- Siatka komórek mapy (0.3675 j.; mocniejsza linia co 5), osie witryny (E = +x, N = +z), okrąg
  `clearRadius`.
- **Wyspa mapy**, na której stoi POI (mapa, której `perlinNoiseConfig.Pois` go zawiera, seed 12345):
  woda na niebiesko, ląd na zielono, plus znacznik startu gracza. Dla POI losowo rozmieszczanych
  (obóz) to układ własny POI, a wyspa jest tylko poglądowa (bez losowego obrotu witryny).
- Ścieżki w kolorze wyglądu; przycisk **Refresh island** liczy wyspę od nowa.

## Zasady i ograniczenia

- Do zapisu liczy się: prefab, pozycja x/z w komórkach (zaokrąglona do 0.01), obrót Y względem
  prefabu. Wysokość i skala instancji są ignorowane (builder bierze je z prefabu).
- Obiekty niebędące instancjami prefabów są pomijane przy zapisie (ostrzeżenie w konsoli).
- Pola poza układem (miejsce, odstępy, loot, kotwica) edytujesz dalej w inspektorze assetu.
- `HarborAssetBuilder` wypełnia układ portu tylko wtedy, gdy POI nie ma jeszcze elementów;
  `Tools > Ports > Reset harbour layouts to the defaults` przywraca wbudowane układy.
