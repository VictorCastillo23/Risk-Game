# Historias de usuario

Formato `Como <rol>, quiero <acción>, para <beneficio>`, agrupadas por épica y con criterios de aceptación verificables contra `Risk.Engine`/`Risk.Web`/`Risk.AI`. Complementan los flujos detallados en [casos-de-uso.md](casos-de-uso.md).

Rangos vigentes: exactamente 2 jugadores en `TwoPlayer`, 3–5 en `Classic`/`SecretMission`/`Capital` (no hay partidas de 6). Pools iniciales: 40/35/30/25 para 2/3/4/5 jugadores.

## Épica: Configuración de partida

### HU-01 — Armar la partida
Como jugador que organiza la partida, quiero cargar jugadores con nombre, color y tipo (Humano/IA) y elegir el modo, para empezar una partida con mis amigos o contra bots en el mismo dispositivo.

**Criterios de aceptación:**
- Fuera del rango del modo (`TwoPlayer` = 2, resto = 3–5), el sistema rechaza el inicio (`InvalidPlayerCount`) y no crea partida.
- Puedo elegir entre `Classic`, `SecretMission`, `TwoPlayer` y `Capital` desde el setup.
- Cada fila puede marcarse Humano o IA; en `TwoPlayer` además aparece el ejército neutral sintetizado.
- Cada jugador arranca con el pool oficial (40/35/30/25 según cantidad).

### HU-01b — Reclamar territorios en Classic/Capital
Como jugador en fase Claim, quiero reclamar territorios vacíos de a uno, para armar mi posición inicial según el reglamento clásico.

**Criterios de aceptación:**
- Cada reclamo es exactamente 1 tropa en un territorio sin dueño (`ClaimTerritoryCommand`); el turno rota y el 42 cierra la fase.
- Quien arranca se define por roll-off de dados (`TurnOrder.DetermineFirst`).

## Épica: Reparto inicial de tropas (Setup)

### HU-02 — Colocar mi primera tropa en cada territorio
Como jugador en fase Setup, quiero hacer clic en uno de mis territorios para reforzarlo con una tropa, para completar el reparto inicial antes de que arranque el juego real.

**Criterios de aceptación:**
- Cada clic coloca exactamente 1 tropa; no se puede elegir otra cantidad.
- Solo puedo colocar en territorios que ya son míos (`NotOwner` si no).
- En `TwoPlayer` fase B coloco tropas neutrales de a 1 (`PlaceNeutralTroopsCommand`); el neutral nunca toma turno.
- Cuando todos terminan, la partida entra a `SelectHeadquarters` (solo `Capital`) o a `Reinforce` del jugador 0 con su refuerzo calculado.

### HU-02b — Elegir mi cuartel general (Capital)
Como jugador en modo Capital, quiero elegir un territorio propio como cuartel secreto, para tener un objetivo propio que defender y rivales que cazar.

**Criterios de aceptación:**
- Un cuartel por jugador, write-once (`SelectHeadquartersCommand`), revelados todos a la vez (`HeadquartersRevealed`).
- Capturar un cuartel rival lo registra (`HeadquartersCaptured`); perder el propio no elimina, solo entrega la carta.

## Épica: Fase de refuerzo (Reinforce)

### HU-03 — Recibir tropas según mi territorio
Como jugador al empezar mi turno, quiero recibir automáticamente mi pool de refuerzo, para tener tropas nuevas que distribuir antes de atacar.

**Criterios de aceptación:**
- El pool es como mínimo 3, o territorios propios ÷ 3 redondeado hacia abajo si es mayor.
- Se suma el bono de cada continente que controlo por completo (América del Norte +5, América del Sur +2, Europa +5, África +3, Asia +7, Oceanía +2).

### HU-04 — Repartir mi refuerzo en el mapa
Como jugador en fase Reinforce, quiero distribuir mis tropas nuevas entre varios de mis territorios, para prepararme según dónde planeo atacar o defender.

**Criterios de aceptación:**
- Solo puedo reforzar territorios que ya son míos.
- No puedo terminar la fase Reinforce mientras me queden tropas sin colocar (`ReinforcementIncomplete`).

## Épica: Cartas

### HU-05 — Canjear un set de cartas por tropas
Como jugador con un set válido de 3 cartas, quiero canjearlas por tropas de refuerzo, para reforzar mi posición sin depender solo de mis territorios.

**Criterios de aceptación:**
- Un set válido es 3 cartas del mismo símbolo, una de cada símbolo, o cualquier combinación que usa comodines para cubrir lo que falta.
- El bono sube en escala fija (4, 6, 8, 10, 12, 15 tropas) para los primeros 6 canjes de la partida, y luego +5 por cada canje adicional.
- Si una carta canjeada nombra un territorio que ocupo, recibo +2 tropas extra ahí (`TerritoryTradeBonus`); territorio de bono inválido → `InvalidBonusTerritory`.
- Intentar canjear cartas que no tengo en la mano, o un set inválido, se rechaza sin gastar ninguna carta.

### HU-06 — Verme obligado a canjear si tengo demasiadas cartas
Como jugador con 5 o más cartas en la mano, quiero que el sistema me bloquee cualquier otra acción hasta que canjee un set, para respetar el límite clásico de mano de Risk.

**Criterios de aceptación:**
- Con 5+ cartas al empezar `Reinforce`, o con flag armado tras una eliminación que me deja en 6+, cualquier comando que no sea canjear (o resolver una ocupación pendiente) se rechaza con `MandatoryTradeRequired` — incluido terminar la fase.
- El bloqueo se levanta al quedar en ≤4 cartas.

## Épica: Ataque

### HU-07 — Atacar un territorio enemigo
Como jugador en fase Attack, quiero atacar un territorio enemigo adyacente al mío eligiendo cuántos dados tirar, para intentar conquistarlo.

**Criterios de aceptación:**
- Solo puedo atacar territorios adyacentes (frontera terrestre o las rutas marítimas clásicas: Alaska-Kamchatka, Groenlandia-Islandia, Brasil-Norte de África, etc.).
- Puedo tirar entre 1 y 3 dados, siempre dejando al menos 1 tropa en mi territorio de origen.
- El defensor tira automáticamente hasta 2 dados según sus tropas disponibles.
- En cada par de dados comparado, un empate lo gana el defensor.

### HU-08 — Ocupar el territorio que acabo de conquistar
Como jugador que acaba de ganar una batalla, quiero elegir cuántas tropas mover al territorio conquistado, para decidir cuánta fuerza dejo atrás y cuánta avanzo.

**Criterios de aceptación:**
- El mínimo a mover es la cantidad de dados que usé para ganar el ataque; el máximo es todo menos 1 tropa del origen.
- Mientras no resuelva esta ocupación, no puedo emitir ningún otro comando (`OccupationPending`).

### HU-09 — Eliminar a un rival y quedarme con sus cartas
Como jugador que conquista el último territorio de un rival, quiero que ese jugador quede eliminado y su mano pase a la mía, para seguir jugando con la ventaja que le gané en el tablero.

**Criterios de aceptación:**
- El jugador eliminado pierde toda posibilidad de jugar y se lo salta en la rotación de turnos.
- Toda su mano de cartas se transfiere íntegra a quien lo eliminó (si lo deja en 6+, debe canjear hasta ≤4 antes de seguir).

### HU-10 — Ganar la partida según el modo
Como jugador, quiero que el sistema declare mi victoria según el modo elegido, sin arbitraje manual.

**Criterios de aceptación:**
- `Classic`: controlar los 42 territorios en el mismo comando que completó la conquista.
- `SecretMission`: completar mi misión (continentes / N territorios / eliminar ejército) y revelarla en la pantalla de victoria.
- `Capital`: capturar todos los cuarteles rivales conservando el propio (al neutral no hace falta eliminarlo en `TwoPlayer`: basta eliminar al adversario).
- Tras `Won`, ningún comando posterior se acepta (`GameOver`), salvo iniciar una partida nueva.

## Épica: Fortificación

### HU-11 — Reagrupar tropas entre mis territorios
Como jugador en fase Fortify, quiero mover tropas de un territorio mío a otro conectado por territorios propios, para reforzar mi frontera antes de terminar el turno.

**Criterios de aceptación:**
- Solo puedo hacer este movimiento una vez por turno (`FortifyAlreadyUsed` en el segundo intento).
- Origen y destino deben conectarse por una cadena continua de territorios míos, no solo ser adyacentes directamente.
- Siempre debe quedar al menos 1 tropa en el territorio de origen.

## Épica: Ritmo de turno

### HU-12 — Avanzar de fase con un solo botón
Como jugador, quiero terminar mi fase actual con una sola acción, para no tener que recordar manualmente el orden Reinforce → Attack → Fortify.

**Criterios de aceptación:**
- El sistema no me deja terminar Reinforce si me quedan tropas sin colocar, ni si debo canje obligatorio.
- Al terminar Fortify, si conquisté algo este turno y quedan cartas en el mazo, recibo exactamente una carta antes del siguiente turno (aunque haya conquistado varias).

## Épica: Información y tablero

### HU-13 — Ver el tablero y mi mano sin espiar a nadie
Como jugador, quiero ver el estado completo del tablero y mi propia mano (y mi misión en `SecretMission`), pero solo la cantidad de cartas de mis rivales, para que la partida sea justa sin depender de la honestidad de nadie.

**Criterios de aceptación:**
- Mi vista siempre incluye mi mano completa (vía `PlayerView`).
- La vista de cualquier otro jugador se reduce a un número (su cantidad de cartas), nunca a los valores reales; en red cada dispositivo solo recibe su propia vista.

### HU-13b — Ver el mapa real con mis territorios
Como jugador, quiero ver el tablero acuarela real con un marcador por territorio pintado con mi color, para ubicarme sin una grilla abstracta.

**Criterios de aceptación:**
- El tablero renderiza `world-map.png` (1340x876) con un marcador por territorio desde `TerritoryLayout` (coordenadas hechas a mano, separadas del grafo de adyacencia del dominio).
- Anillos con contraste WCAG (casos de arte oscuro usan aro claro) y radio que cumple el piso táctil.

### HU-14 — Ver el registro de lo que pasó en la partida
Como jugador, quiero un historial legible de los eventos de la partida (ataques, conquistas, canjes, eliminaciones, reclamos, cuarteles), para entender cómo se llegó a la situación actual del tablero.

**Criterios de aceptación:**
- Cada acción que cambia el estado deja un evento en el log (`LogPanel`), con una descripción entendible del hecho, no solo el nombre técnico del evento.
- El panel de dados conserva la última batalla aunque después lleguen comandos que no son de ataque.

## Épica: Rivales IA

### HU-16 — Jugar contra bots en mi mesa
Como jugador, quiero marcar asientos como IA al armar la partida (local o en red), para completar la mesa sin esperar a nadie.

**Criterios de aceptación:**
- Cada asiento IA juega su turno solo tras `Start`/cada comando (`AdvanceAiTurns`), hasta devolver el turno a un humano o terminar la partida.
- El bot solo decide desde su vista redactada; un comando ilegal suyo nunca se esconde: se muestra como fallo IA (`Rejected`/`límite de comandos`) sin reintento automático.

## Épica: Multijugador en red

### HU-17 — Armar una mesa con código y jugar cada uno en su dispositivo
Como anfitrión, quiero crear una mesa con código, compartirlo y curar quién juega, para no turnarnos un solo teclado.

**Criterios de aceptación:**
- Crear mesa da un código; unirse con código + nombre reclama un asiento (`Join`); recargar reengancha por bookmark sin perder el asiento.
- El lobby deja elegir jugadores, degradar extras a espectadores y sumar/quitar bots antes de arrancar.
- Solo el dispositivo del turno interactúa; el resto (y espectadores) ven el tablero en solo lectura con "Esperando a …".

## Épica: Cuentas y guardado

### HU-18 — Guardar y retomar mi partida con cuenta
Como jugador con cuenta, quiero guardar mi partida y retomarla después, sin que el juego anónimo se vea afectado.

**Criterios de aceptación:**
- Guardar es manual (`SavePanel`); sin login se pide autenticarse y la partida pendiente se completa tras el login sin perderse.
- Segundo guardado sobrescribe el anterior (con confirmación previa si ya había save).
- Desde `/saved` retomo donde quedé (incluidos bots); al ganar, la fila guardada se borra.
- Sin cuenta juego igual: ningún fallo de base voltea la partida en curso.

## Épica: Cierre de partida

### HU-15 — Empezar una partida nueva después de ganar
Como jugador, quiero volver a la pantalla de configuración después de que termina una partida, para poder jugar otra ronda sin recargar la aplicación.

**Criterios de aceptación:**
- Desde la pantalla de victoria (con misión revelada en `SecretMission`), "nueva partida" limpia toda la sesión (estado, jugadores, últimos eventos, ownership de guardado) y vuelve a Setup. En red abandona la mesa sin destruirla para los demás.
