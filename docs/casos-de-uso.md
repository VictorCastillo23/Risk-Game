# Casos de uso

Describe el comportamiento observable de `Risk.Engine` tal como lo consumen `Risk.Web` (hot-seat y red) y `Risk.AI`. Cada caso de uso corresponde a uno o más `GameCommand` procesados por `GameEngine.Execute` (`src/Risk.Engine/GameEngine.cs`); los códigos de error citados son valores de `GameErrorCode` (`src/Risk.Domain/Errors/GameErrorCode.cs`).

Comandos vigentes (9): `ClaimTerritoryCommand`, `PlaceTroopsCommand`, `PlaceNeutralTroopsCommand` (solo `TwoPlayer`), `TradeCardsCommand`, `AttackCommand`, `OccupyCommand`, `FortifyCommand`, `EndPhaseCommand`, `SelectHeadquartersCommand` (solo `Capital`). Fases vigentes (6): `Claim` → `Setup` → (`SelectHeadquarters` solo en `Capital`) → `Reinforce` → `Attack` → `Fortify`.

## Actores

- **Jugador local**: persona que juega en hot-seat (todos comparten el mismo navegador/circuito, turnándose).
- **Jugador en red**: persona en su propio dispositivo, sentada en una mesa compartida por código (`NetworkGameSession`, páginas `Lobby`/`Join`).
- **Asiento IA**: bot determinista (`Risk.AI`: `BotPlayer` + `BotTurnRunner`) que ocupa un asiento y juega solo su turno desde el `PlayerView` redactado, sin acceso al `GameState` crudo.
- **Motor de reglas** (`IGameEngine`): actor de sistema. Valida cada comando contra el estado actual y produce el nuevo `GameState` o un rechazo; nunca confía en que el llamador pre-validó nada. Su chequeo actor-es-turno es siempre la autoridad final, incluso cuando la red ya filtró al remitente.

## UC-01 — Iniciar una partida nueva

**Actor:** Jugador que configura la partida (pantalla `Setup.razor`, ruta `/`).
**Precondición:** No hay ninguna partida en curso en la sesión (`GameSessionService.State` es `null`).

**Flujo principal (local):**
1. El jugador elige variante (local / en red) y modo (`Classic`, `SecretMission`, `TwoPlayer`, `Capital`).
2. En local agrega filas de jugador (nombre, color, Humano/IA). El rango válido depende del modo (`GameSetup.PlayerCountRange`): exactamente 2 en `TwoPlayer`, 3–5 en el resto. No existe partida de 6.
3. Confirma el inicio; el sistema llama a `GameSetup.Create(playerCount, mode, dice)`.
4. El sistema calcula el pool inicial oficial según cantidad de jugadores (40/35/30/25 para 2/3/4/5 jugadores).
5. Según el modo:
   - `Classic`/`Capital`: los 42 territorios arrancan sin dueño con pools intactos; turno inicial en fase `Claim` para el ganador del roll-off (`TurnOrder.DetermineFirst`). Cada `ClaimTerritoryCommand` reclama exactamente un territorio con 1 tropa y rota (round-robin); al reclamarse el 42 la fase pasa a `Setup`.
   - `SecretMission`: reparte los 42 territorios al azar (1 tropa por territorio, round-robin), reparte una `MissionCard` por jugador y arranca en `Setup`.
   - `TwoPlayer`: reparte entre los 2 jugadores más un tercer ejército neutral (`PlayerState.IsNeutral`); arranca en `Setup` con fase A (tropas propias) y fase B (tropas neutrales vía `PlaceNeutralTroopsCommand`).
6. En `Capital`, tras agotarse `Setup` el turno entra a `SelectHeadquarters` (un cuartel por jugador, write-once); en el resto va directo a `Reinforce` del jugador 0 con su refuerzo ya calculado.

**Flujo alternativo (en red):**
- 2a. El anfitrión crea la mesa (`NetworkGameRegistry.Create`), obtiene un código y reclama su asiento; los demás se unen desde `/join` con el código; el anfitrión cura la lista en `/lobby/{codigo}` (elige jugadores, degrada extras a espectadores, suma/quita bots) y recién ahí arranca — ver UC-14.

**Flujos de excepción:**
- Cantidad fuera del rango del modo → `InvalidPlayerCount`; la partida no se crea.

**Postcondición:** Existe un `GameState` válido con `Mode` fijado. En modos con reparto inicial se registró `TerritoriesAssigned`; en `Classic`/`Capital` no hay eventos en la creación (los `TerritoryClaimed`/`PhaseChanged` se emiten al jugar `Claim`).

## UC-02 — Reclamar y colocar las tropas iniciales (fases Claim/Setup/SelectHeadquarters)

**Actor:** Jugador (o IA) en turno.
**Precondición:** Fase `Claim`, `Setup` o `SelectHeadquarters`.

**Flujo principal:**
1. En `Claim`: clic en un territorio sin dueño → `ClaimTerritoryCommand(Actor, Territory, 1)`.
2. En `Setup` (fase A): clic en un territorio propio → `PlaceTroopsCommand(Actor, Territory, 1)` — siempre exactamente 1 por clic. El turno pasa al siguiente con tropas pendientes; al agotarse todos, avanza según el modo (a `SelectHeadquarters` en `Capital`, a `Reinforce` en el resto).
3. En `Setup` fase B (solo `TwoPlayer`): el jugador en turno coloca 1 tropa neutral por clic en cualquier territorio neutral → `PlaceNeutralTroopsCommand`. El neutral nunca toma turno ni recibe refuerzos.
4. En `SelectHeadquarters` (solo `Capital`): clic en un territorio propio → `SelectHeadquartersCommand`. Cada jugador fija su `HeadquartersId` una sola vez; al completar la ronda se revelan (`HeadquartersRevealed`) y arranca `Reinforce`.

**Flujos alternativos / excepción:**
- `Claim` sobre territorio con dueño → rechazo (no reclama).
- `Setup` en territorio ajeno → `NotOwner`; cantidad distinta de 1 o sin pool → `InvalidTroopCount`.
- Comando del neutral o como si fuera turno del neutral → `ActorIsNeutral`.
- Cuartel en territorio ajeno o cuartel repetido → rechazo (incluye `InvalidBonusTerritory` donde aplique).

**Postcondición:** Los 42 territorios tienen dueño y al menos 1 tropa (más el cuartel fijado en `Capital`); arranca el ciclo normal de turnos.

## UC-03 — Reforzar territorios propios (fase Reinforce)

**Actor:** Jugador (o IA) en turno.
**Precondición:** Fase `Reinforce`; `TroopsRemaining` calculado por `Reinforcement.Calculate` (territorios propios ÷ 3, redondeado hacia abajo, mínimo 3, más el bono de cada continente controlado por completo).

**Flujo principal:**
1. El sistema muestra el pool de refuerzo disponible.
2. El jugador reparte esas tropas entre uno o varios territorios propios (uno o más `PlaceTroopsCommand`; en la UI se stagea con clic/re-clic y se confirma en lote).
3. Cuando `TroopsRemaining` llega a 0, el jugador termina la fase (UC-08) y pasa a `Attack`.

**Flujos alternativos / excepción:**
- Terminar la fase con tropas sin colocar → `ReinforcementIncomplete`.
- Territorio ajeno → `NotOwner`; cantidad inválida → `InvalidTroopCount`.

**Postcondición:** Todo el refuerzo del turno quedó distribuido en el tablero.

## UC-04 — Intercambiar cartas por tropas

**Actor:** Jugador en turno. Voluntario durante `Reinforce`; obligatorio si su mano llega a 5+ al empezar `Reinforce`, o si una eliminación lo deja con 6+ en pleno turno (flag `MandatoryTradeDown`).
**Precondición:** El jugador posee un set válido de 3 cartas (`CardSet.IsValid`: mismo símbolo ×3, un símbolo de cada uno, o combinaciones que usan comodines para cubrir el símbolo que falta).

**Flujo principal:**
1. El jugador selecciona 3 cartas de su mano y emite `TradeCardsCommand` (opcionalmente indicando en qué territorio propio cobra el bono de ocupación).
2. El sistema valida que las cartas estén en su mano y que formen un set válido.
3. El sistema calcula el bono según el número de canje de la partida (`CardTradeBonus`: 4, 6, 8, 10, 12, 15 tropas para los primeros 6 canjes, luego +5 por cada canje adicional).
4. Si alguna carta canjeada nombra un territorio que el jugador ocupa, suma +2 tropas extra colocadas directamente ahí (`TerritoryTradeBonus.Troops`).
5. Suma el bono al pool del jugador y descuenta las 3 cartas de su mano (`TradesCompleted` avanza; el flag de canje obligatorio se limpia al quedar en ≤4).

**Flujos alternativos / excepción:**
- Set inválido, o cartas que no están en la mano → `InvalidCardSet`.
- Territorio de bono inválido (ajeno o no nombrado por el set) → `InvalidBonusTerritory`.
- Mano con 5+ cartas (o flag armado) y el jugador intenta cualquier comando que no sea `TradeCardsCommand`/`OccupyCommand` → `MandatoryTradeRequired` (bloquea hasta canjear; también bloquea el propio `EndPhaseCommand`).

**Postcondición:** Mano reducida en 3, `TroopsRemaining` incrementado, contador `TradesCompleted` avanzado.

## UC-05 — Atacar un territorio enemigo adyacente

**Actor:** Jugador (o IA) en turno, fase `Attack`.
**Precondición:** El origen es propio y tiene 2+ tropas; el destino es de otro jugador (o neutral en `TwoPlayer`) y es adyacente al origen (incluye las rutas marítimas clásicas, no solo fronteras terrestres).

**Flujo principal:**
1. El jugador elige origen, destino y dados de ataque (1 a 3, sin dejar el origen en 0 tropas).
2. El sistema tira los dados del atacante y hasta 2 del defensor (según sus tropas).
3. `BattleResolver` ordena cada tirada de mayor a menor y compara par a par; un empate lo gana el defensor.
4. El sistema aplica las bajas a cada bando y registra `BattleResolved`.

**Flujo alternativo — conquista (5a):**
- Si el defensor queda en 0 tropas, el territorio pasa al atacante con 0 tropas, se registra `TerritoryConquered`, se marca `ConqueredThisTurn`, y se abre una ocupación pendiente que bloquea cualquier otro comando hasta resolver UC-06.
- Si esa conquista deja al defensor sin ningún territorio, es eliminado (UC-11: transfiere toda su mano al atacante; puede armar canje obligatorio).
- Si se cumple la regla de victoria del modo (UC-10), la partida termina.

**Flujos de excepción:**
- No es tu turno / fase incorrecta → `NotYourTurn` / `WrongPhase`.
- Origen no propio o destino no enemigo → `NotOwner`.
- No adyacentes → `NotAdjacent`.
- Dados fuera de 1–3 → `InvalidDiceCount`.
- Dados que dejarían el origen sin tropas → `InsufficientTroops`.
- Suplantar al ejército neutral → `ActorIsNeutral`.

**Postcondición:** Siempre queda registrado un `BattleResolved`; `TerritoryConquered`/`PlayerEliminated`/`GameWon` son condicionales al resultado.

## UC-06 — Ocupar un territorio recién conquistado

**Actor:** Jugador (o IA) en turno.
**Precondición:** Existe una ocupación pendiente (`TurnState.PendingOccupation`) creada por UC-05.

**Flujo principal:**
1. Mientras hay ocupación pendiente, el motor rechaza cualquier comando que no sea `OccupyCommand` (`OccupationPending`).
2. El jugador decide cuántas tropas mover: mínimo, la cantidad de dados usados en el ataque ganador; máximo, las tropas del origen menos 1.
3. El sistema mueve las tropas al territorio conquistado (`TerritoryOccupied`) y limpia la ocupación pendiente.

**Flujo de excepción:**
- Sin ocupación pendiente → `NoPendingOccupation`.
- Cantidad fuera de rango → `InvalidTroopCount`.

**Postcondición:** El territorio conquistado queda con las tropas movidas; el turno vuelve a aceptar cualquier comando de `Attack`.

## UC-07 — Reagrupar tropas (Fortify)

**Actor:** Jugador (o IA) en turno, fase `Fortify`. Como máximo una vez por turno.
**Precondición:** No usó `Fortify` todavía este turno; origen y destino son propios; existe una cadena de territorios propios que los conecta (BFS restringido a territorios del jugador, no adyacencia directa).

**Flujo principal:**
1. El jugador elige origen, destino y cantidad de tropas (dejando al menos 1 en el origen).
2. El sistema valida la conectividad con `ConnectivityRules.HasFriendlyPath`.
3. Mueve las tropas (`TroopsFortified`) y marca `FortifyUsed = true`.

**Flujos de excepción:**
- Ya usó Fortify este turno → `FortifyAlreadyUsed`.
- Sin camino propio que conecte ambos territorios → `NoFriendlyPath`.
- Territorio de origen o destino ajeno → `NotOwner`.

**Postcondición:** A lo sumo un movimiento de tropas por turno queda aplicado.

## UC-08 — Finalizar la fase actual

**Actor:** Jugador (o IA) en turno.

**Flujo principal:**
1. El jugador pide terminar su fase actual (`EndPhaseCommand`).
2. `Reinforce → Attack → Fortify → siguiente jugador activo en Reinforce` (salta eliminados y al neutral; en `TwoPlayer` el neutral nunca es turno).
3. Al pasar de `Attack` a `Fortify`: nada (la carta se roba al salir de `Fortify`). Al pasar de `Fortify` al siguiente jugador: si el saliente conquistó al menos un territorio este turno y el mazo no está vacío, roba exactamente una carta (aunque haya conquistado varias); se resetean `ConqueredThisTurn`, `FortifyUsed` y el flag de canje; se calcula el refuerzo del entrante.

**Flujo de excepción:**
- Terminar `Reinforce` con `TroopsRemaining > 0` → `ReinforcementIncomplete`.
- Terminar con canje obligatorio pendiente (5+ cartas o flag armado) → `MandatoryTradeRequired`.

## UC-09 — Consultar el estado de la partida (vista redactada)

**Actor:** Jugador o IA (en la UI, vía `GameSessionService.ObserveCurrentPlayer`).

**Flujo principal:**
1. El sistema arma un `PlayerView` a partir del `GameState`: la mano completa del jugador que consulta (más su misión efectiva en `SecretMission`), las manos ajenas reducidas a un simple conteo, y el tablero/turno público.
2. En red, cada circuito solo obtiene la vista de su propio asiento (`NetworkGameSession.Observe(seat)`); espectadores/sin-asiento no tienen vista de mano por construcción.
3. Tras el fin (`Won`), `WinnerMission()` expone la misión efectiva del ganador para el reveal de `VictoryScreen`; antes del fin devuelve `null` por tipo, sin depender de que el llamador "se porte bien".

**Postcondición:** Ningún cliente puede ver información oculta de otro jugador — esto es estructural (el modelo expuesto no contiene esas cartas), no una regla que se pueda saltear.

## UC-10 — Ganar la partida

**Trigger:** Consecuencia de UC-05/UC-08 según la `IVictoryRule` del modo (`VictoryRules`):
- `Classic`: el atacante controla los 42 territorios.
- `SecretMission`: el jugador completa su `MissionCard` (conquistar continentes, ocupar N territorios o eliminar un ejército; `MissionResolution`) y la revela.
- `Capital`: captura todos los cuarteles rivales mientras conserva el propio (`HeadquartersCaptured`).
- `TwoPlayer`: elimina al adversario (al neutral no hace falta eliminarlo).

**Postcondición:** `GameState.Status` pasa a `Won(ganador)`; se registra `GameWon`; el motor rechaza cualquier comando posterior con `GameOver` salvo iniciar una partida nueva (UC-12). En partidas guardadas, ganar borra la fila (ver UC-15).

## UC-11 — Ser eliminado

**Trigger:** Sub-flujo de UC-05: una conquista deja al defensor sin ningún territorio.

**Flujo principal:**
1. El sistema marca al jugador como eliminado y le vacía la mano.
2. Transfiere toda esa mano al atacante que lo eliminó (si lo deja en 6+, arma `MandatoryTradeDown`).
3. Registra `PlayerEliminated`.
4. A partir de aquí, la rotación de turnos (UC-08) salta siempre a este jugador.

## UC-12 — Reiniciar la sesión (nueva partida)

**Actor:** Jugador, desde la pantalla de victoria (`VictoryScreen.razor`).

**Flujo principal:**
1. El jugador confirma "nueva partida".
2. `GameSessionService.Reset()` limpia `State`, `Players`, `LastEvents` y `OwnerUserId` (y abandona la mesa en red sin destruirla para los demás).
3. La UI vuelve a la pantalla de configuración (UC-01).

## UC-13 — Jugar con asientos IA

**Actor:** Asiento IA (bot) + jugador humano que lo configuró.
**Precondición:** Al menos una fila con `IsAi` en `Setup`, o bot sumado al lobby en red.

**Flujo principal:**
1. Tras `Start`/`Execute`/`LoadFrom`, `AdvanceAiTurns` drena todos los asientos IA consecutivos (`BotTurnRunner.RunTurn`: `Observe → Decide → Execute` en loop) hasta llegar a un humano o al fin del juego.
2. `BotPlayer.DecideNextCommand(PlayerView, BotMemory)` es pura y respeta el mismo orden de validación del motor (ocupación pendiente → canje obligatorio → fase); decide entre 7 estrategias (`Claim`, `Setup`, `Headquarters`, `Reinforce`, `Attack`, `Occupy`, `Fortify`) con scoring (`TerritoryScoring`, `CombatOdds`, `MissionScoring`/`CapitalScoring`, constantes en `BotWeights`).
3. Un `Rejected` del motor nunca se enmascara con otro comando: se publica como `AiTurnFailure.Rejected` (o `BudgetExhausted` si se agota el presupuesto) y se muestra junto al tablero; nunca se reintenta solo.

**Postcondición:** O el turno vuelve a un humano, o la partida termina, o queda un fallo IA visible y trazable como defecto.

## UC-14 — Jugar en red (multidispositivo)

**Actor:** Jugadores en red (anfitrión + invitados), con o sin asientos IA.
**Precondición:** Mesa creada (`NetworkGameRegistry.Create` → código), asientos reclamados (`ClaimSeat`/`Join`), lobby curado y partida arrancada.

**Flujo principal:**
1. El anfitrión crea la mesa y comparte el código; cada invitado se une desde `/join` (reclamo por nombre, con recupero de asiento caído y bookmark para recarga).
2. El anfitrión cura el roster en el lobby (selecciona jugadores, degrada extras a espectadores, suma/quita bots) y arranca con modo elegido.
3. Cada circuito juega desde `Game.razor`: solo el dispositivo del turno ve paneles activos; los demás (y espectadores) ven el tablero en solo lectura con "Esperando a …".
4. Cada comando viaja por `NetworkGameSession.Dispatch`: chequeo actor-es-asiento (seguridad de red) y luego `engine.Execute` como autoridad de regla. Mano y misión solo vía `Observe(asiento-propio)`.

**Postcondición:** Una `GameState` compartida por código, con garantía de información oculta por ruteo (cada circuito solo renderiza su propia vista).

## UC-15 — Guardar y retomar la partida (cuenta opcional)

**Actor:** Jugador con o sin cuenta (Identity + `RiskDbContext` en SQL Server).
**Precondición:** Partida en curso en `GameSessionService` (hot-seat; en red el guardado lo drivea el anfitrión).

**Flujo principal:**
1. El jugador pulsa "Guardar partida" (`SavePanel`, acción manual, nunca automática). Sin login se le pide autenticarse (flujo anónimo-con-stash: la partida se guarda en `ProtectedSessionStorage` y se completa tras el login vía `RehydrateAndSaveAsync`).
2. `SaveAsync` persiste un `GameSnapshot` (JSON versionado vía `GameSnapshotSerializer`) como fila por usuario (`EfGameStore`): nuevo (`Created`) o sobrescrito (`Overwritten`, con confirmación previa vía `GetSaveSummaryAsync`).
3. Desde `/saved` el jugador retoma (`ResumeAsync` → `LoadFrom`, bypaseando `GameSetup.Create`, reconstruyendo bots).
4. Al ganar con save asociado, la fila se borra (`DeleteSaveIfWonAsync`, best-effort).

**Garantías:** el juego anónimo nunca se ve afectado (`OwnerUserId null`, cero llamadas al store salvo save/resume); cualquier fallo del store devuelve `Failed`/`null` sin voltear el circuito ni tocar la partida en curso.
