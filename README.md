# Risk-Game

Una implementación completa y jugable del juego de mesa RISK como aplicación web, usando Blazor Server sobre .NET 8.

## Estado del proyecto

- **`Risk.Domain` + `Risk.Engine` (+ `Risk.Tests`)** — motor de reglas headless completo: mapa clásico de 42 territorios, combate por dados, refuerzos, cartas (escala progresiva + bono +2 por territorio ocupado), fases de turno (`Claim` → `Setup` → `Reinforce` → `Attack` → `Fortify`, más `SelectHeadquarters` solo en modo Capital) y 4 modos de juego (`Classic`, `SecretMission`, `TwoPlayer`, `Capital`).
- **`Risk.Web` (+ `Risk.Web.Tests`)** — interfaz jugable en Blazor Server con dos formas de jugar: hot-seat (una pestaña, turnos en el mismo dispositivo, vía `GameSessionService`) y multijugador en red (cada jugador en su dispositivo con código de mesa, vía `NetworkGameSession` + páginas `Lobby`/`Join`). Tablero SVG sobre el arte acuarela real (`wwwroot/images/world-map.png`, 1340x876) con marcadores por territorio (`Models/TerritoryLayout.cs`), panel por fase, cartas, misiones, dados, log de eventos y pantalla de victoria. Cuentas con Identity + guardado en SQL Server (`/saved`, `SavePanel`); el juego anónimo sigue funcionando sin cuenta.
- **`Risk.AI` (+ `Risk.AI.Tests`)** — bot heurístico determinista que solo ve el `PlayerView` redactado (`Observe`), nunca el `GameState` crudo. Cada asiento marcado IA juega su turno solo (`GameSessionService.AdvanceAiTurns` / `NetworkGameSession.AdvanceAiTurns` vía `BotTurnRunner`).

Suite actual: 948 tests en verde (384 `Risk.Tests` + 206 `Risk.AI.Tests` + 358 `Risk.Web.Tests`).

## Producción

La app está desplegada en Azure App Service: [risk-game-bghugbfnhfhjhmh0.mexicocentral-01.azurewebsites.net](https://risk-game-bghugbfnhfhjhmh0.mexicocentral-01.azurewebsites.net)

## Documentación

- [docs/casos-de-uso.md](docs/casos-de-uso.md) — casos de uso del motor de juego, con flujos y códigos de error.
- [docs/historias-usuario.md](docs/historias-usuario.md) — historias de usuario con criterios de aceptación.
- [docs/diagrama-relacional.md](docs/diagrama-relacional.md) — diagrama entidad-relación del modelo de datos en memoria.
- [CLAUDE.md](CLAUDE.md) — guía de arquitectura y convenciones para trabajar en el repo.

## Cómo jugar

- **Local (hot-seat):** en `/` elegí modo (`Classic`, `SecretMission`, `TwoPlayer`, `Capital`), cantidad de jugadores (exactamente 2 en `TwoPlayer`, 3–5 en el resto), nombre/color por fila y si cada asiento es Humano o IA. `Classic`/`Capital` arrancan reclamando los 42 territorios uno por uno (fase `Claim`); `SecretMission`/`TwoPlayer` reparten de entrada y van directo a `Setup`.
- **En red:** en `/` → "En red" se crea una mesa con código; se comparte el código, cada jugador se une desde `/join` y el anfitrión cura la lista en `/lobby/{codigo}` antes de arrancar. Solo el dispositivo del turno interactúa; el resto ve el tablero en solo lectura.
- **Guardado:** opcional y manual (`SavePanel`). Sin login jugás igual; con cuenta podés guardar en `/saved` y retomar después. Ganar borra la fila guardada.

## Cómo correr el proyecto

```bash
dotnet run --project src/Risk.Web    # levanta la app en https://localhost:xxxx
```

La persistencia usa SQL Server (`Default` connection string, Identity + `RiskDbContext`); sin base configurada el juego local/anónimo igual funciona, solo el guardado devuelve fallo sin romper la partida.

## Comandos

```bash
dotnet build Risk.sln          # compilar todo
dotnet test Risk.sln           # correr toda la suite de tests (xUnit)
dotnet test --filter FullyQualifiedName~AttackCommandTests   # una clase de test
```
