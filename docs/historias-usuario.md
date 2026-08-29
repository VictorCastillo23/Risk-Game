# Historias de usuario

Formato `Como <rol>, quiero <acción>, para <beneficio>`, agrupadas por épica y con criterios de aceptación verificables contra `Risk.Engine`/`Risk.Web`. Complementan los flujos detallados en [casos-de-uso.md](casos-de-uso.md).

## Épica: Configuración de partida

### HU-01 — Armar la partida
Como jugador que organiza la partida, quiero cargar entre 2 y 6 jugadores con nombre y color, para empezar una partida de Risk con mis amigos en el mismo dispositivo.

**Criterios de aceptación:**
- Con menos de 2 o más de 6 jugadores, el sistema rechaza el inicio (`InvalidPlayerCount`) y no crea partida.
- Al confirmar, los 42 territorios quedan repartidos al azar y lo más parejo posible entre todos los jugadores.
- Cada jugador arranca con el pool de tropas oficial según cantidad de jugadores (40/35/30/25/20).

## Épica: Reparto inicial de tropas (Setup)

### HU-02 — Colocar mi primera tropa en cada territorio
Como jugador en fase Setup, quiero hacer clic en uno de mis territorios para reforzarlo con una tropa, para completar el reparto inicial antes de que arranque el juego real.

**Criterios de aceptación:**
- Cada clic coloca exactamente 1 tropa; no se puede elegir otra cantidad.
- Solo puedo colocar en territorios que ya son míos.
- Cuando agoto mi pool inicial, el turno pasa al siguiente jugador con tropas pendientes.
- Cuando todos terminan, la partida entra en la fase Reinforce del jugador 1, con su refuerzo ya calculado.

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
- Intentar canjear cartas que no tengo en la mano, o un set inválido, se rechaza sin gastar ninguna carta.

### HU-06 — Verme obligado a canjear si tengo demasiadas cartas
Como jugador con 5 o más cartas en la mano, quiero que el sistema me bloquee cualquier otra acción hasta que canjee un set, para respetar el límite clásico de mano de Risk.

**Criterios de aceptación:**
- Con 5+ cartas, cualquier comando que no sea canjear (o resolver una ocupación pendiente) se rechaza con `MandatoryTradeRequired`.
- Esto aplica sin importar en qué fase esté, incluido el caso en que una eliminación me suma la mano de otro jugador y me deja con 5+ cartas en medio de mi propio turno.

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
- Mientras no resuelva esta ocupación, no puedo emitir ningún otro comando.

### HU-09 — Eliminar a un rival y quedarme con sus cartas
Como jugador que conquista el último territorio de un rival, quiero que ese jugador quede eliminado y su mano pase a la mía, para seguir jugando con la ventaja que le gané en el tablero.

**Criterios de aceptación:**
- El jugador eliminado pierde toda posibilidad de jugar y se lo salta en la rotación de turnos.
- Toda su mano de cartas se transfiere íntegra a quien lo eliminó.

### HU-10 — Ganar la partida
Como jugador que controla el mapa entero, quiero que el sistema declare mi victoria automáticamente, para que la partida termine sin que nadie tenga que arbitrar a mano.

**Criterios de aceptación:**
- Al controlar los 42 territorios, el estado pasa a "ganada" en el mismo comando que completó la conquista.
- Ningún comando posterior se acepta, salvo iniciar una partida nueva.

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
- El sistema no me deja terminar Reinforce si me quedan tropas sin colocar.
- Al terminar Fortify, si conquisté algo este turno y quedan cartas en el mazo, recibo una carta automáticamente antes de que empiece el turno del siguiente jugador.

## Épica: Información y tablero

### HU-13 — Ver el tablero y mi mano sin espiar a nadie
Como jugador, quiero ver el estado completo del tablero y mi propia mano, pero solo la cantidad de cartas de mis rivales, para que la partida sea justa sin depender de la honestidad de nadie.

**Criterios de aceptación:**
- Mi vista siempre incluye mi mano completa.
- La vista de cualquier otro jugador se reduce a un número (su cantidad de cartas), nunca a los valores reales.

### HU-14 — Ver el registro de lo que pasó en la partida
Como jugador, quiero un historial legible de los eventos de la partida (ataques, conquistas, canjes, eliminaciones), para entender cómo se llegó a la situación actual del tablero.

**Criterios de aceptación:**
- Cada acción que cambia el estado deja un evento en el log (`LogPanel`), con una descripción entendible del hecho, no solo el nombre técnico del evento.

## Épica: Cierre de partida

### HU-15 — Empezar una partida nueva después de ganar
Como jugador, quiero volver a la pantalla de configuración después de que termina una partida, para poder jugar otra ronda sin recargar la aplicación.

**Criterios de aceptación:**
- Desde la pantalla de victoria, "nueva partida" limpia toda la sesión (estado, jugadores, últimos eventos) y vuelve a Setup.
