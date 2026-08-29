# Recrear el juego "Risk" en Blazor (.NET)

---

## Contexto y objetivo

Quiero que construyas una implementación completa y jugable del juego de mesa **Risk** (Riesgo/TEG) como una aplicación web usando **Blazor Server** sobre **.NET 8**. La aplicación debe soportar partidas con una mezcla de **jugadores humanos** (en el mismo navegador, modo hot-seat: cada humano juega su turno desde el mismo dispositivo) y **jugadores controlados por IA**.

Trabajemos de forma incremental: primero la arquitectura y el motor de reglas, después la IA, y al final el pulido visual. Antes de escribir código, propón la estructura de carpetas/proyectos y espera mi confirmación (o procede si es razonable, pero explícala brevemente en el commit/resumen).

---

## Stack técnico

- **.NET 8**, **Blazor Server** (para poder usar SignalR y mantener el estado de la partida en el servidor, evitando duplicar lógica en el cliente).
- Arquitectura en capas, en proyectos separados dentro de una solución:
  - `Risk.Domain` — clase de librería (sin dependencias de UI) con las entidades y reglas del juego (territorios, continentes, jugadores, cartas, fases de turno, combate).
  - `Risk.Engine` — motor de partida: aplica las reglas del `Domain`, valida movimientos, gestiona el flujo de turnos y expone una API clara (comandos/eventos) que la UI y la IA puedan consumir.
  - `Risk.AI` — estrategias de IA que consumen el `Engine` igual que lo haría un jugador humano (sin trucos ni acceso a información oculta que un humano no tendría).
  - `Risk.Web` — proyecto Blazor Server con la interfaz de usuario.
  - `Risk.Tests` — pruebas unitarias (xUnit) para el motor de reglas, sobre todo combate, tarjetas y condiciones de victoria.

---

## Reglas del juego a implementar

Implementa las reglas clásicas de Risk:

### Mapa
- 42 territorios agrupados en 6 continentes (América del Norte, América del Sur, Europa, África, Asia, Oceanía), con sus adyacencias reales (incluyendo las conexiones marítimas clásicas, ej. Alaska-Kamchatka).
- Bono de tropas por controlar un continente completo (usa los valores estándar del juego).

### Configuración inicial
- Entre 2 y 6 jugadores (humanos + IA, mezclados).
- Reparto de territorios inicial (aleatorio y equitativo) y colocación inicial de tropas por turnos.

### Turno de un jugador (3 fases)
1. **Refuerzo**: calcular tropas nuevas (territorios ÷ 3, redondeado hacia abajo, mínimo 3, + bonos de continente + posibles bonos por canje de cartas) y colocarlas en territorios propios.
2. **Ataque**: atacar territorios adyacentes enemigos. Resolución de combate con dados:
   - Atacante lanza hasta 3 dados (según tropas disponibles, dejando al menos 1 en el territorio de origen).
   - Defensor lanza hasta 2 dados (según tropas disponibles).
   - Comparar dados de mayor a menor; empates favorecen al defensor.
   - Si el atacante conquista el territorio, debe mover al menos tantas tropas como dados usó en el ataque ganador (permitir elegir cuántas, respetando el mínimo).
   - Al conquistar al menos un territorio en el turno, el jugador recibe una carta al final del turno.
3. **Fortificación**: mover tropas una vez por turno entre dos territorios propios conectados por una cadena de territorios propios.

### Cartas
- Tres tipos de símbolo (infantería, caballería, artillería) + comodines.
- Canje de sets (3 iguales, o 1 de cada tipo, o con comodín) por tropas extra, siguiendo la escala progresiva estándar de Risk.
- Si un jugador tiene 5+ cartas al empezar su turno, debe canjear un set obligatoriamente.

### Eliminación y victoria
- Un jugador eliminado (pierde todos sus territorios) entrega todas sus cartas al jugador que lo eliminó.
- Condición de victoria: un jugador controla todos los territorios del mapa. (Opcional/fase futura: modo por objetivos.)

---

## Requisitos de arquitectura del motor (`Risk.Engine`)

- El estado de la partida (`GameState`) debe ser inmutable o gestionado de forma que cada acción se aplique a través de comandos explícitos (ej. `AttackCommand`, `FortifyCommand`, `TradeCardsCommand`), devolviendo el nuevo estado o un resultado de error si la acción es inválida.
- Toda validación de reglas (¿es adyacente?, ¿tiene suficientes tropas?, ¿es su turno?, ¿es su fase?) debe vivir en el motor, nunca en la UI.
- El motor debe emitir eventos/registro de la partida (log) para poder mostrar un historial de jugadas en la UI (quién atacó a quién, resultado de dados, territorios conquistados, cartas canjeadas, etc.).
- Los dados deben usar un generador aleatorio inyectable (interfaz `IDiceRoller`) para poder testear el motor de forma determinista.

---

## IA (`Risk.AI`)

- Empieza con una IA basada en heurísticas simples (no necesita machine learning):
  - Priorizar completar continentes que ya controla parcialmente.
  - Reforzar fronteras (territorios propios adyacentes a enemigos).
  - Atacar cuando tiene ventaja numérica clara (ej. 3:1 o mejor), evitando ataques suicidas.
  - Canjear cartas apenas sea posible o conveniente.
  - Fortificar moviendo tropas de la retaguardia hacia las fronteras.
- Diseña la interfaz `IAiStrategy` de forma que en el futuro se puedan añadir otras estrategias (agresiva, defensiva, aleatoria) sin tocar el motor.
- La IA debe jugar su turno completo de forma automática, con una pequeña pausa/animación en la UI para que sea legible por el humano (no instantáneo).

---

## Interfaz de usuario (`Risk.Web`)

- **Pantalla de configuración de partida**: elegir número de jugadores (2-6), para cada uno indicar si es humano o IA (y opcionalmente dificultad de la IA), y color.
- **Tablero principal**: mapa del mundo en SVG interactivo, con los territorios coloreados según su dueño y mostrando el número de tropas. Click en un territorio para seleccionarlo como origen/destino de una acción según la fase actual.
- Indicador claro de: jugador en turno, fase actual (Refuerzo/Ataque/Fortificación), tropas disponibles para colocar.
- Panel/animación de tiradas de dados al resolver un combate (mostrar los dados de atacante y defensor y el resultado).
- Panel de cartas del jugador humano en turno, con opción de canjear un set.
- Panel de historial/log de la partida (scrolleable).
- Cuando le toca a un jugador humano, el sistema debe esperar su input; cuando le toca a la IA, debe jugar automáticamente y mostrar sus acciones en el log y el tablero.
- Mensaje/pantalla de victoria al finalizar la partida.

---

## Calidad y pruebas

- Escribe pruebas unitarias (xUnit) en `Risk.Tests` cubriendo al menos:
  - Cálculo de refuerzos (con y sin bonos de continente).
  - Resolución de combate con distintos números de dados y empates.
  - Validación de adyacencia y de fases del turno (rechazar acciones fuera de fase).
  - Canje de cartas y su escala progresiva.
  - Condición de victoria y eliminación de jugadores.
- Usa nombres de clases/métodos en inglés (convención habitual en C#), pero los textos visibles en la UI pueden estar en español.
- Comenta las decisiones de diseño no triviales.

---

## Plan de trabajo sugerido (hazlo por fases, confirmando conmigo entre fases si es posible)

1. Crear la solución y los proyectos vacíos con las referencias correctas.
2. Modelar el dominio: territorios, continentes, adyacencias, jugadores, cartas.
3. Implementar el motor de turnos y combate, con pruebas unitarias.
4. Implementar el canje de cartas y las condiciones de victoria/eliminación.
5. Implementar la IA básica.
6. Construir la UI de configuración de partida.
7. Construir el tablero SVG interactivo y conectar con el motor vía Blazor Server.
8. Añadir el log de partida, animaciones de dados y pantalla de victoria.
9. Pulido visual y revisión final de reglas.

Al terminar cada fase, dame un resumen breve de qué se implementó y cómo probarlo localmente (`dotnet run`, etc.).