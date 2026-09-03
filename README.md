# Risk-Game

Una implementación completa y jugable del juego de mesa RISK como aplicación web, usando Blazor Server sobre .NET 8.

## Estado del proyecto

- **`Risk.Domain`** y **`Risk.Engine`** — motor de reglas completo (mapa clásico de 42 territorios, combate, refuerzos, cartas, fases de turno).
- **`Risk.Web`** — interfaz jugable en Blazor Server (tablero SVG con grilla hexagonal, panel por fase, cartas, log de eventos, pantalla de victoria).
- **`Risk.AI`** — no implementada todavía; el motor expone `Observe`/`PlayerView` con vista redactada precisamente para que un cliente de IA se pueda sumar sin acceso a información oculta.

## Producción

La app está desplegada en Azure App Service: [risk-game-bghugbfnhfhjhmh0.mexicocentral-01.azurewebsites.net](https://risk-game-bghugbfnhfhjhmh0.mexicocentral-01.azurewebsites.net)

## Documentación

- [docs/casos-de-uso.md](docs/casos-de-uso.md) — casos de uso del motor de juego, con flujos y códigos de error.
- [docs/historias-usuario.md](docs/historias-usuario.md) — historias de usuario con criterios de aceptación.
- [docs/diagrama-relacional.md](docs/diagrama-relacional.md) — diagrama entidad-relación del modelo de datos en memoria.
- [CLAUDE.md](CLAUDE.md) — guía de arquitectura y convenciones para trabajar en el repo.

## Cómo correr el proyecto

```bash
dotnet run --project src/Risk.Web    # levanta la app en https://localhost:xxxx
```

## Comandos

```bash
dotnet build Risk.sln          # compilar todo
dotnet test Risk.sln           # correr toda la suite de tests (xUnit)
dotnet test --filter FullyQualifiedName~AttackCommandTests   # una clase de test
```
