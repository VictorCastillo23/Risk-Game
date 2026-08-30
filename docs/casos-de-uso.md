# Casos de uso

Describe el comportamiento observable de `Risk.Engine` tal como lo consume `Risk.Web`. Cada caso de uso corresponde a uno o más `GameCommand` procesados por `GameEngine.Execute` (`src/Risk.Engine/GameEngine.cs`); los códigos de error citados son valores de `GameErrorCode` (`src/Risk.Domain/Errors/GameErrorCode.cs`).

## Actores

- **Jugador**: persona humana que juega en modo "hot-seat" (todos los jugadores comparten el mismo navegador/circuito de Blazor, turnándose). Es el único actor implementado hoy — `Risk.AI` no existe todavía.
- **Motor de reglas** (`IGameEngine`): actor de sistema. Valida cada comando contra el estado actual y produce el nuevo `GameState` o un rechazo; nunca confía en que el llamador pre-validó nada.

## UC-01 — Iniciar una partida nueva

**Actor:** Jugador que configura la partida (pantalla `Setup.razor`).
**Precondición:** No hay ninguna partida en curso en la sesión (`GameSessionService.State` es `null`).

**Flujo principal:**
1. El jugador agrega entre 2 y 6 filas de jugador (nombre, color).
2. Confirma el inicio; el sistema llama a `GameSetup.Create(playerCount, mode, dice)` (la firma requiere un `IDiceRoller` desde el ítem 2.1 del roadmap; solo `GameMode.Classic` lo usa realmente, para el roll-off que decide quién arranca).
3. El sistema reparte los 42 territorios al azar y de forma pareja entre los jugadores (1 tropa por territorio, orden aleatorio, asignación round-robin).
4. El sistema calcula el pool de tropas inicial de cada jugador según la cantidad de jugadores (40/35/30/25/20 para 2/3/4/5/6 jugadores) menos los territorios ya recibidos.
5. El sistema arma el mazo estándar de 44 cartas y el turno inicial en fase `Setup` para el jugador 0.
6. El sistema muestra el tablero; continúa en UC-02.

**Flujo alternativo:**
- 2a. Cantidad de jugadores fuera de 2–6 → `InvalidPlayerCount`; la partida no se crea.
- 2b. `GameMode.Classic` (único modo alcanzable desde el navegador hoy, ítem 1.1/2.1): no hay reparto inicial. Los 42 territorios arrancan sin dueño, cada jugador conserva su pool completo de tropas, y el turno inicial queda en fase `Claim` para el ganador del roll-off (`TurnOrder.DetermineFirst`). Cada `ClaimTerritoryCommand` reclama exactamente un territorio con exactamente 1 tropa y rota al siguiente jugador (round-robin); al reclamarse el territorio 42 la fase pasa a `Setup` (en el jugador rotado, no en `players[0]`) y continúa como el flujo principal desde el paso 6.

**Postcondición:** Existe un `GameState` válido. En modos con reparto inicial se registró el evento `TerritoriesAssigned`; en `GameMode.Classic` no se registra ningún evento en la creación (los eventos `TerritoryClaimed`/`PhaseChanged` se emiten a medida que se juega la fase `Claim`).

## UC-02 — Colocar las tropas iniciales (fase Setup)

**Actor:** Jugador en turno.
**Precondición:** Fase `Setup`; el jugador en turno tiene `TroopsRemaining > 0`.

**Flujo principal:**
1. El jugador hace clic en uno de sus propios territorios.
2. El sistema emite `PlaceTroopsCommand(Actor, Territory, Troops: 1)` — en Setup siempre es exactamente 1 tropa por clic.
3. El sistema pasa el turno al siguiente jugador que aún tenga tropas por colocar.
4. Se repite hasta que todos los jugadores agotan su pool inicial.
5. El sistema pasa automáticamente a fase `Reinforce` para el jugador 0 y le calcula su primer refuerzo.

**Flujos alternativos / excepción:**
- Territorio ajeno → `NotOwner`.
- Cantidad distinta de 1, o sin tropas restantes → `InvalidTroopCount`.

**Postcondición:** Los 42 territorios tienen dueño y al menos 1 tropa; arranca el ciclo normal de turnos.

## UC-03 — Reforzar territorios propios (fase Reinforce)

**Actor:** Jugador en turno.
**Precondición:** Fase `Reinforce`; `TroopsRemaining` calculado por `Reinforcement.Calculate` (territorios propios ÷ 3, redondeado hacia abajo, mínimo 3, más el bono de cada continente que controla por completo).

**Flujo principal:**
1. El sistema muestra el pool de refuerzo disponible.
2. El jugador reparte esas tropas entre uno o varios territorios propios (uno o más `PlaceTroopsCommand`).
3. Cuando `TroopsRemaining` llega a 0, el jugador termina la fase (UC-08) y pasa a `Attack`.

**Flujos alternativos / excepción:**
- Terminar la fase con tropas sin colocar → `ReinforcementIncomplete`.
- Territorio ajeno → `NotOwner`; cantidad inválida → `InvalidTroopCount`.

**Postcondición:** Todo el refuerzo del turno quedó distribuido en el tablero.

## UC-04 — Intercambiar cartas por tropas

**Actor:** Jugador en turno. Voluntario durante `Reinforce`; obligatorio en cualquier fase si su mano llega a 5+ cartas.
**Precondición:** El jugador posee un set válido de 3 cartas (`CardSet.IsValid`: mismo símbolo ×3, un símbolo de cada uno, o combinaciones que usan comodines para cubrir el símbolo que falta).

**Flujo principal:**
1. El jugador selecciona 3 cartas de su mano y emite `TradeCardsCommand`.
2. El sistema valida que las cartas estén en su mano y que formen un set válido.
3. El sistema calcula el bono según el número de canje de la partida (`CardTradeBonus`: 4, 6, 8, 10, 12, 15 tropas para los primeros 6 canjes, luego +5 por cada canje adicional).
4. Suma el bono al pool de refuerzo del jugador y descuenta las 3 cartas de su mano.

**Flujos alternativos / excepción:**
- Set inválido, o cartas que no están en la mano → `InvalidCardSet`.
- Mano con 5+ cartas y el jugador intenta cualquier comando que no sea `TradeCardsCommand`/`OccupyCommand` → `MandatoryTradeRequired` (el motor bloquea toda otra acción hasta que canjee).

**Postcondición:** Mano reducida en 3, `TroopsRemaining` incrementado, contador `TradesCompleted` avanzado.

## UC-05 — Atacar un territorio enemigo adyacente

**Actor:** Jugador en turno, fase `Attack`.
**Precondición:** El territorio de origen es propio y tiene 2+ tropas; el destino es de otro jugador y es adyacente al origen (incluye las rutas marítimas clásicas, no solo fronteras terrestres).

**Flujo principal:**
1. El jugador elige origen, destino y cantidad de dados de ataque (1 a 3, sin dejar el origen en 0 tropas).
2. El sistema tira los dados del atacante y hasta 2 dados del defensor (según sus tropas disponibles).
3. `BattleResolver` ordena cada tirada de mayor a menor y compara par a par; un empate lo gana el defensor.
4. El sistema aplica las bajas a cada bando y registra `BattleResolved`.

**Flujo alternativo — conquista (5a):**
- Si el defensor queda en 0 tropas, el territorio pasa al atacante con 0 tropas, se registra `TerritoryConquered`, se marca `ConqueredThisTurn`, y se abre una ocupación pendiente que bloquea cualquier otro comando hasta resolver UC-06.
- Si esa conquista deja al defensor sin ningún territorio, es eliminado (UC-11: transfiere toda su mano al atacante).
- Si el atacante pasa a controlar los 42 territorios, la partida termina (UC-10).

**Flujos de excepción:**
- Origen no propio o destino no enemigo → `NotOwner`.
- No adyacentes → `NotAdjacent`.
- Dados fuera de 1–3 → `InvalidDiceCount`.
- Dados que dejarían el origen sin tropas → `InsufficientTroops`.

**Postcondición:** Siempre queda registrado un `BattleResolved`; `TerritoryConquered`/`PlayerEliminated`/`GameWon` son condicionales al resultado.

## UC-06 — Ocupar un territorio recién conquistado

**Actor:** Jugador en turno.
**Precondición:** Existe una ocupación pendiente (`TurnState.PendingOccupation`) creada por UC-05.

**Flujo principal:**
1. Mientras hay ocupación pendiente, el motor rechaza cualquier comando que no sea `OccupyCommand` (`OccupationPending`).
2. El jugador decide cuántas tropas mover: mínimo, la cantidad de dados usados en el ataque ganador; máximo, las tropas del origen menos 1.
3. El sistema mueve las tropas al territorio conquistado y limpia la ocupación pendiente.

**Flujo de excepción:**
- Sin ocupación pendiente → `NoPendingOccupation`.
- Cantidad fuera de rango → `InvalidTroopCount`.

**Postcondición:** El territorio conquistado queda con las tropas movidas; el turno vuelve a aceptar cualquier comando de `Attack`.

## UC-07 — Reagrupar tropas (Fortify)

**Actor:** Jugador en turno, fase `Fortify`. Como máximo una vez por turno.
**Precondición:** No usó `Fortify` todavía este turno; origen y destino son propios; existe una cadena de territorios propios que los conecta (BFS restringido a territorios del jugador, no adyacencia directa).

**Flujo principal:**
1. El jugador elige origen, destino y cantidad de tropas (dejando al menos 1 en el origen).
2. El sistema valida la conectividad con `ConnectivityRules.HasFriendlyPath`.
3. Mueve las tropas y marca `FortifyUsed = true`.

**Flujos de excepción:**
- Ya usó Fortify este turno → `FortifyAlreadyUsed`.
- Sin camino propio que conecte ambos territorios → `NoFriendlyPath`.
- Territorio de origen o destino ajeno → `NotOwner`.

**Postcondición:** A lo sumo un movimiento de tropas por turno queda aplicado.

## UC-08 — Finalizar la fase actual

**Actor:** Jugador en turno.

**Flujo principal:**
1. El jugador pide terminar su fase actual (`EndPhaseCommand`).
2. `Reinforce → Attack → Fortify → siguiente jugador activo en Reinforce`.
3. Al pasar de `Fortify` al siguiente jugador: si el jugador saliente conquistó al menos un territorio este turno y el mazo no está vacío, roba una carta; se resetean `ConqueredThisTurn` y `FortifyUsed`; se salta a cualquier jugador eliminado; se calcula el refuerzo del jugador entrante.

**Flujo de excepción:**
- Terminar `Reinforce` con `TroopsRemaining > 0` → `ReinforcementIncomplete`.

## UC-09 — Consultar el estado de la partida (vista redactada)

**Actor:** Jugador (cualquiera; en la UI, siempre el jugador en turno vía `GameSessionService.ObserveCurrentPlayer`).

**Flujo principal:**
1. El sistema arma un `PlayerView` a partir del `GameState`: la mano completa del jugador que consulta, las manos ajenas reducidas a un simple conteo, y el tablero/turno público.

**Postcondición:** Ningún cliente puede ver información oculta de otro jugador — esto es estructural (el modelo de datos que se expone no contiene esas cartas), no una regla que se pueda saltear.

## UC-10 — Ganar la partida

**Trigger:** Consecuencia de UC-05 cuando, tras una conquista, el atacante controla los 42 territorios.

**Postcondición:** `GameState.Status` pasa a `Won(ganador)`; se registra `GameWon`; el motor rechaza cualquier comando posterior con `GameOver` salvo iniciar una partida nueva (UC-12).

## UC-11 — Ser eliminado

**Trigger:** Sub-flujo de UC-05: una conquista deja al defensor sin ningún territorio.

**Flujo principal:**
1. El sistema marca al jugador como eliminado y le vacía la mano.
2. Transfiere toda esa mano al atacante que lo eliminó.
3. Registra `PlayerEliminated`.
4. A partir de aquí, la rotación de turnos (UC-08) salta siempre a este jugador.

## UC-12 — Reiniciar la sesión (nueva partida)

**Actor:** Jugador, desde la pantalla de victoria (`VictoryScreen.razor`).

**Flujo principal:**
1. El jugador confirma "nueva partida".
2. `GameSessionService.Reset()` limpia `State`, `Players` y `LastEvents`.
3. La UI vuelve a la pantalla de configuración (UC-01).
