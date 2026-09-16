# Spec: risk-web-real-map-art

## Capability: board-map-rendering (NEW)

Risk.Web's board renders ownership, troop counts, selection, continent
bonuses, and sea-route connections as an SVG overlay atop the real
watercolor map artwork (`wwwroot/images/world-map.png`, 1340x876), using 42
hand-placed pixel-coordinate markers instead of hex polygons.
Presentation-only; game rules stay in `Risk.Engine`.

### Requirement: Map background layer
The board SHALL render the map PNG as a fixed SVG `<image>` background at native 1340x876, with all interactive elements composited above it.

#### Scenario: Board loads with real artwork
- GIVEN a game is in progress
- WHEN the board component renders
- THEN the SVG viewBox SHALL be `0 0 1340 876`
- AND `world-map.png` SHALL render as the base layer beneath all markers, lines, and labels

### Requirement: Territory marker placement
Each of the 42 territories SHALL render as a marker centered at its hand-placed coordinate, replacing per-territory hex polygons.

#### Scenario: All markers render inside their territory
- GIVEN the layout defines one coordinate per territory in `WorldMap`
- WHEN the board renders
- THEN exactly 42 markers SHALL be visible
- AND each marker SHALL fall within its territory's painted region in the artwork

### Requirement: Ownership color and contrast
A marker's fill MUST reflect its territory's current owner color, or a distinct unclaimed color when unowned, and MUST hold at least 3:1 contrast (WCAG 1.4.11) against the locally adjacent map pixels.

#### Scenario: Owned territory marker color
- GIVEN a territory is owned by a player
- WHEN the board renders that marker
- THEN its fill SHALL match that player's assigned color
- AND it SHALL measure at least 3:1 contrast against the immediately adjacent artwork

#### Scenario: Unclaimed territory marker color
- GIVEN a territory has no owner
- WHEN the board renders that marker
- THEN it SHALL render in a distinct unclaimed color, not any player's color

### Requirement: Troop count and reinforcement badge
Each marker MUST display its territory's troop count as legible text, and MUST show a distinguishable badge when reinforcements are staged but not yet placed there.

#### Scenario: Troop count visible
- GIVEN a territory has N troops
- WHEN the board renders
- THEN N SHALL render on or immediately adjacent to that marker, legible at its rendered size

#### Scenario: Pending reinforcement badge
- GIVEN a player has staged but undispatched reinforcements on a territory
- WHEN the board renders
- THEN a badge visually distinct from the troop count SHALL render on or near that marker

### Requirement: Marker size and density exception
Markers SHOULD be at least 24x24 CSS px. A marker MAY render smaller only where a dense cluster would otherwise overlap a neighbor, per the WCAG 2.5.8 "Essential" exception, invoked per-territory and documented, never as a blanket exemption.

#### Scenario: Dense cluster marker
- GIVEN a territory sits in a densely packed continent region
- WHEN a 24x24px marker would overlap a neighboring marker
- THEN the marker MAY render smaller than 24x24px
- AND it MUST NOT visually overlap the neighboring marker

### Requirement: Click and right-click interaction contract
Clicking or right-clicking a territory marker MUST continue invoking the board's existing `EventCallback<TerritoryId>` parameters (`OnTerritoryClick`, `OnTerritoryRightClick`) with that territory's id. This public component contract MUST NOT change.

#### Scenario: Left click raises OnTerritoryClick
- GIVEN the board is not locked
- WHEN a user clicks a territory's marker
- THEN `OnTerritoryClick` SHALL invoke with that territory's `TerritoryId`

#### Scenario: Right click raises OnTerritoryRightClick
- GIVEN the board is not locked
- WHEN a user right-clicks a territory's marker
- THEN `OnTerritoryRightClick` SHALL invoke with that territory's `TerritoryId`
- AND the browser's default context menu SHALL NOT appear

### Requirement: Selection highlight
The currently selected territory MUST render with a highlight visually distinguishable from unselected markers.

#### Scenario: Selected territory highlighted
- GIVEN a territory is the current selection
- WHEN the board renders
- THEN that marker SHALL render with a highlight not present on other markers

### Requirement: Locked board during pending occupation
While `Locked` is true (a conquest awaits occupation resolution), the board MUST disable pointer interaction on all markers.

#### Scenario: Board locked during occupation prompt
- GIVEN a conquest is awaiting occupation resolution
- WHEN the board renders with `Locked` true
- THEN markers SHALL NOT respond to click or right-click
- AND the board SHALL apply pointer-events-none (or equivalent) styling

### Requirement: Sea-route lines only
The board SHALL render exactly the 5 named sea routes (Alaska–Kamchatka, Greenland–Iceland, Brazil–NorthAfrica, WesternEurope–NorthAfrica, Siam–Indonesia) as lines between marker coordinates. No land-adjacency line SHALL render.

#### Scenario: Only sea routes render
- GIVEN the board renders territory connection lines
- WHEN counting rendered lines
- THEN exactly 5 lines SHALL be present, one per named sea route
- AND no line SHALL connect two land-adjacent territories

### Requirement: Continent bonus labels
Each continent MUST display a "+bonus" label positioned so it reads as belonging to that continent, with no background halo shape rendered behind it. (Design note: a literal territory-coordinate bounding-box anchor is infeasible on real cartography, since continent bounding boxes overlap — see `design.md`'s "Deviation from spec wording." The intent — the label visually belongs to its continent — is satisfied by 6 hand-placed anchors and verified by a nearest-centroid test.)

#### Scenario: Continent bonus label renders
- GIVEN a continent has a fixed reinforcement bonus
- WHEN the board renders
- THEN a "+{bonus}" label SHALL render positioned so it reads as belonging to that continent
- AND no rectangle or shape SHALL render behind the label as a halo
