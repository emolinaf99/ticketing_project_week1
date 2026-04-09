# Plan de Pruebas — AuthService

> **Proyecto:** TicketRush — Plataforma de Venta de Tickets  
> **Servicio:** AuthService (Registro, Login y Cierre de Sesión)  
> **Arquitectura:** Hexagonal (Ports and Adapters)  
> **Stack tecnológico:** .NET 8 · C# · xUnit · NSubstitute · Entity Framework Core · PostgreSQL · BCrypt.Net · JWT · Testcontainers  
> **Versión:** 1.0  
> **Fecha:** 2026-04-08

---

## Tabla de contenidos

1. [Propósito](#1-propósito)  
2. [Alcance](#2-alcance)  
3. [Los 7 Principios del Testing aplicados](#3-los-7-principios-del-testing-aplicados)  
4. [Estrategia Multinivel](#4-estrategia-multinivel)  
5. [Tipos de Pruebas: Caja Blanca y Caja Negra](#5-tipos-de-pruebas-caja-blanca-y-caja-negra)  
6. [Test Suites](#6-test-suites)  
7. [Test Cases](#7-test-cases)  
8. [Trazabilidad a reglas de negocio y patrones de diseño](#8-trazabilidad-a-reglas-de-negocio-y-patrones-de-diseño)  
9. [Infraestructura de pruebas](#9-infraestructura-de-pruebas)  
10. [Ejecución en Pipeline CI/CD](#10-ejecución-en-pipeline-cicd)  
11. [Criterios de entrada y salida](#11-criterios-de-entrada-y-salida)  
12. [Riesgos y mitigaciones](#12-riesgos-y-mitigaciones)  

---

## 1. Propósito

Este documento constituye el **informe técnico formal** del plan de pruebas del AuthService dentro del ecosistema TicketRush. Su objetivo es:

- Definir la estrategia integral de testing multinivel (Unitario, Componente/Integración, Caja Negra).
- Especificar los Test Suites y Test Cases que protegen las reglas de negocio del servicio de autenticación.
- Justificar cada decisión de prueba bajo los **7 Principios del Testing** (ISTQB Foundation Level).
- Demostrar la diferenciación técnica entre pruebas de **Caja Blanca** y **Caja Negra**.
- Servir como documentación viva del plan de calidad que se defiende durante la auditoría técnica.

---

## 2. Alcance

### 2.1 Dentro de alcance

| Área funcional | Descripción | Reglas de negocio |
|---------------|-------------|-------------------|
| Registro de usuarios | Validación de datos, hash de contraseña, email único, persistencia | RN1 (email único), RN2 (política de contraseña), RN3 (hash bcrypt) |
| Login de usuarios | Autenticación, generación JWT, control de intentos fallidos | RN4 (solo usuarios registrados), RN5 (bloqueo tras 3 intentos) |
| Cierre de sesión | Invalidación del token JWT en cliente, protección de rutas | — |
| Seguridad | Anti-enumeración de usuarios, mitigación de ataques de timing, inyección SQL | — |

### 2.2 Fuera de alcance

- Pruebas de rendimiento/carga (pertenecen al plan global de TicketRush).
- Integración con RabbitMQ (AuthService no publica ni consume eventos en esta feature).
- Pruebas de penetración (pentesting externo).
- Pruebas de UI/Frontend (cubiertas por el plan del FrontendTicketing).

---

## 3. Los 7 Principios del Testing aplicados

### Principio 1: Las pruebas muestran la presencia de defectos, no su ausencia

Los 19 tests implementados en AuthService **no garantizan** ausencia total de errores. Lo que garantizan es que los **caminos críticos del negocio** (registro exitoso, login exitoso, bloqueo por intentos, anti-enumeración) funcionan correctamente. Por ejemplo, `LoginUserUseCaseTests.Fails_WhenUserNotFound_ThrowsInvalidCredentialsException` demuestra que un defecto de seguridad (enumeración de usuarios) fue encontrado y corregido, **pero no prueba que no existan otros vectores de ataque**.

### Principio 2: Las pruebas exhaustivas son imposibles

La combinación de posibles entradas al formulario de registro (email × contraseña × nombres × apellidos) es infinita. Se aplicaron técnicas de **Partición de Equivalencia** y **Análisis de Valores Límite** (documentadas en `TEST_CASES.md`) para reducir el espacio de prueba a clases representativas:

| Técnica | Ejemplo aplicado |
|---------|-----------------|
| Partición de Equivalencia | Emails válidos vs. inválidos (`carlos@dominio.com` vs. `carlos.dominio.com`) |
| Valores Límite | Contraseña de 7 caracteres (bajo el límite) vs. 8 caracteres (en el límite exacto — RN2) |
| Predicción de Errores | Inyección SQL `' OR 1=1 --` en campo de correo (CP-HU2-12) |

### Principio 3: Las pruebas tempranas ahorran tiempo y dinero

Se aplicó **TDD (Red-Green-Refactor)**. Los tests unitarios de `RegisterUserUseCaseTests` y `LoginUserUseCaseTests` se escribieron **antes** de la implementación del código de producción. Evidencia:

- Los mocks de `IUserRepository`, `IPasswordHashingService` y `ILoginAttemptsRepository` se configuraron antes de que existieran los adaptadores reales (`UserRepository`, `BcryptPasswordService`).
- La lógica de bloqueo de cuenta (State pattern: `ActiveState` → `LockedState`) fue diseñada primero como test (`LocksAccount_AfterThreeFailures`) y luego implementada.

### Principio 4: Los defectos se agrupan (Clustering)

En AuthService, los defectos se concentran en dos módulos:

1. **`LoginUserUseCase`** — Orquesta la lógica más compleja: búsqueda de usuario, delegación al patrón State, timing-attack mitigation, registro de intentos, generación JWT. Por eso tiene **5 tests unitarios** (la mayor cantidad).
2. **Entidad `User`** — Contiene validaciones de dominio (`ValidatePasswordFormat`, `Create()`, `RecordFailedAttempt`, `IsLocked`). Sus invariantes se prueban indirectamente en cada test unitario y directamente en los tests de integración.

### Principio 5: Paradoja del Pesticida

Si solo ejecutáramos los mismos 9 tests unitarios indefinidamente, eventualmente dejarían de encontrar defectos nuevos. La mitigación aplicada:

- **Nivel adicional de pruebas de integración** (10 tests) que ejercitan el sistema con PostgreSQL real y BCrypt real, descubriendo errores que los mocks ocultarían (ej. configuración incorrecta de EF Core, mapeo de excepciones en middleware).
- **Rotación de datos de prueba** — Los tests de integración generan usuarios con `Guid.NewGuid()` en cada ejecución, evitando dependencia de datos estáticos.

### Principio 6: Las pruebas dependen del contexto

El contexto de AuthService es un **microservicio de autenticación en un ecosistema de venta de tickets**. Esto define nuestras prioridades:

| Prioridad | Justificación contextual |
|-----------|------------------------|
| **Seguridad > Funcionalidad** | Un fallo de seguridad (enumeración de usuarios, timing attack) tiene más impacto que un error de validación de formato. Por eso existen tests específicos para timing equalization (`dummy_hash` cuando el usuario no existe) y anti-enumeración (mismo HTTP 401 para usuario inexistente y contraseña incorrecta). |
| **Aislamiento de BCrypt en tests unitarios** | BCrypt tarda ~300ms intencionalmente. En el contexto de una suite unitaria que debe ejecutarse en <10 seg, se usa un mock de `IPasswordHashingService` (patrón Strategy ⚠️ — ver DESIGN_PATTERNS.md). |
| **PostgreSQL real en tests de integración** | Usar SQLite in-memory no es suficiente en nuestro contexto porque las constraints de unicidad y el mapeo de tipos difieren. Testcontainers con PostgreSQL 15 refleja fielmente el entorno de producción. |

### Principio 7: La ausencia de errores es una falacia

Que los 19 tests pasen en verde **no significa** que AuthService esté libre de errores. Significa que las **reglas de negocio documentadas** (RN1-RN5) están protegidas. Un escenario no cubierto actualmente: qué sucede si dos solicitudes de registro con el mismo email llegan simultáneamente (race condition). Este riesgo está documentado en la sección 12.

---

## 4. Estrategia Multinivel

La estrategia sigue la **pirámide de testing**, adaptada al contexto del microservicio:

```
           ┌─────────────────┐
           │   Black-Box     │  ← Tests de integración que simulan cliente HTTP real
           │   (Caja Negra)  │     10 tests — AuthService.Infrastructure.Tests
           ├─────────────────┤
           │                 │
           │   Unit Tests    │  ← Caja Blanca: lógica aislada con mocks
           │  (Caja Blanca)  │     9 tests — AuthService.Application.Tests
           │                 │
           └─────────────────┘
```

### 4.1 Nivel 1 — Pruebas Unitarias (Caja Blanca)

| Propiedad | Valor |
|-----------|-------|
| **Proyecto** | `AuthService.Application.Tests` |
| **Framework** | xUnit + NSubstitute |
| **Dependencias reales** | Solo el SUT (Use Cases) y entidades de dominio (`User`) |
| **Dependencias mockeadas** | `IUserRepository`, `IPasswordHashingService`, `IJwtTokenService`, `ILoginAttemptsRepository` |
| **Tiempo objetivo** | < 5 segundos |
| **Ejecutadas en CI** | Job `🧪 Unit Tests — Caja Blanca` |

**¿Por qué son Caja Blanca?**  
Estas pruebas tienen visibilidad total sobre la estructura interna del código: conocen los puertos de salida, configuran sus respuestas mediante mocks, y verifican la secuencia interna de llamadas (ej. que `SaveAsync` NO se invoca si el email ya existe). El tester tiene acceso al código fuente y diseña los casos basándose en las ramas lógicas internas.

### 4.2 Nivel 2 — Pruebas de Integración / Caja Negra

| Propiedad | Valor |
|-----------|-------|
| **Proyecto** | `AuthService.Infrastructure.Tests` |
| **Framework** | xUnit + `WebApplicationFactory<Program>` + Testcontainers.PostgreSql |
| **Dependencias reales** | **Todas**: pipeline ASP.NET Core completo, EF Core, PostgreSQL, BCrypt, JWT |
| **Dependencias mockeadas** | Ninguna |
| **Tiempo objetivo** | < 60 segundos |
| **Ejecutadas en CI** | Job `🌐 Integration Tests — Contratos entre Servicios` |

**¿Por qué son Caja Negra?**  
Estas pruebas interactúan **exclusivamente** a través de la interfaz HTTP pública (`POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout`). No conocen la implementación interna: no saben que se usa BCrypt, no saben que existe un patrón State, no acceden directamente a la base de datos. Solo verifican entradas (request HTTP) y salidas (status code + response body), exactamente como lo haría un cliente externo.

### 4.3 Diferenciación en el Pipeline CI/CD

El archivo `.github/workflows/ci.yml` separa **físicamente** los niveles en Jobs distintos:

```
Job 1: 🔨 Build
    ↓
Job 2: 🧪 Unit Tests — Caja Blanca          (AuthService.Application.Tests)
Job 3: 🔗 Component Tests                    (otras suites)
Job 4: 🌐 Integration Tests                  (AuthService.Infrastructure.Tests)
Job 5: 📦 Black-Box Tests                    (otras suites)
    ↓
Job 6: 🐳 Docker Build & Trivy Scan
```

Esta separación permite:
- **Feedback rápido**: Si un test unitario falla, el desarrollador lo sabe en <2 min sin esperar a que se levante un PostgreSQL.
- **Aislamiento de fallos**: Un fallo en el Job de integración indica un problema de infraestructura (BD, red, configuración), no de lógica de negocio.
- **Shift-Left Quality**: Los merges a `develop` y `main` quedan bloqueados si cualquier Job falla.

---

## 5. Tipos de Pruebas: Caja Blanca y Caja Negra

### 5.1 Pruebas de Caja Blanca — Detalle técnico

Las pruebas de Caja Blanca se encuentran en `AuthService.Application.Tests/`. Verifican la **lógica interna** con conocimiento total de la estructura del código:

| Técnica de Caja Blanca | Ejemplo en AuthService |
|------------------------|----------------------|
| **Cobertura de sentencias** | `RegisterUserUseCaseTests.Success_WhenValid_CreatesUserWithHash` ejercita la secuencia completa: validar → buscar email → hash → crear → guardar |
| **Cobertura de ramas** | `LoginUserUseCaseTests.Fails_WhenUserNotFound` vs. `Fails_WhenPasswordInvalid` → cubren ambas ramas del `if (user == null)` |
| **Verificación de interacciones** | Se verifica que `_userRepo.SaveAsync()` **no** se invoca cuando el email ya existe (`Received(0).SaveAsync(...)`) |
| **Verificación de flujo de datos** | Se confirma que el email se normaliza a minúsculas antes de persistir (`NormalizesEmailToLowercase`) |

### 5.2 Pruebas de Caja Negra — Detalle técnico

Las pruebas de Caja Negra se encuentran en `AuthService.Infrastructure.Tests/`. Verifican el **comportamiento observable** sin conocer la implementación:

| Técnica de Caja Negra | Ejemplo en AuthService |
|-----------------------|----------------------|
| **Partición de Equivalencia** | Email registrado (201) vs. email duplicado (409) — `RegisterFlowTests` |
| **Análisis de Valores Límite** | Contraseña sin mayúscula ni carácter especial → 400 (`Register_WeakPassword_Returns400`) |
| **Transición de Estados** | 3 intentos fallidos → bloqueo → intento correcto sigue denegado (`Login_ThreeFailedAttempts_AccountLocked_Returns401`) |
| **Predicción de Errores** | Email inexistente → mismo 401 que contraseña incorrecta (anti-enumeración: `Login_UnknownEmail_Returns401`) |

---

## 6. Test Suites

### Suite 1: `RegisterUserUseCaseTests` — Caja Blanca (Unitaria)

| ID | Método de test | Tipo | Resultado esperado |
|----|---------------|------|-------------------|
| UT-R-01 | `Success_WhenValid_CreatesUserWithHash` | Happy path | Usuario creado con hash, mensaje y redirect correctos |
| UT-R-02 | `Fails_WhenEmailExists_ThrowsEmailAlreadyExistsException` | Flujo alterno | `EmailAlreadyExistsException`; `SaveAsync` nunca invocado |
| UT-R-03 | `Fails_WhenPasswordInvalid_ThrowsInvalidPasswordException` | Flujo alterno | `InvalidPasswordException`; ni `Hash` ni `SaveAsync` invocados |
| UT-R-04 | `NormalizesEmailToLowercase` | Regla interna | Email guardado en minúsculas |

### Suite 2: `LoginUserUseCaseTests` — Caja Blanca (Unitaria)

| ID | Método de test | Tipo | Resultado esperado |
|----|---------------|------|-------------------|
| UT-L-01 | `Success_ReturnsToken_WhenCredentialsValid` | Happy path | JWT retornado, intento exitoso registrado |
| UT-L-02 | `Fails_WhenUserNotFound_ThrowsInvalidCredentialsException` | Flujo alterno | `InvalidCredentialsException`; dummy hash ejecutado (timing-attack mitigation); intento fallido registrado |
| UT-L-03 | `Fails_WhenPasswordInvalid_RecordsAttempt` | Flujo alterno | `InvalidCredentialsException`; intento fallido registrado para el usuario |
| UT-L-04 | `LocksAccount_AfterThreeFailures` | Regla de negocio (RN5) | `user.IsLocked() == true` tras 3 intentos fallidos |
| UT-L-05 | `ReturnsGenericError_WhenLocked` | Regla de negocio (RN5) | `AccountLockedException`; password verification **nunca** ejecutada |

### Suite 3: `RegisterFlowTests` — Caja Negra (Integración)

| ID | Método de test | Endpoint | HTTP Status | Tipo |
|----|---------------|----------|-------------|------|
| IT-R-01 | `Register_Success_Returns201WithUserInfo` | `POST /api/auth/register` | 201 Created | Happy path |
| IT-R-02 | `Register_DuplicateEmail_Returns409` | `POST /api/auth/register` | 409 Conflict | Flujo alterno (RN1) |
| IT-R-03 | `Register_WeakPassword_Returns400` | `POST /api/auth/register` | 400 Bad Request | Flujo alterno (RN2) |
| IT-R-04 | `Register_PasswordMismatch_Returns400` | `POST /api/auth/register` | 400 Bad Request | Flujo alterno |

### Suite 4: `LoginFlowTests` — Caja Negra (Integración)

| ID | Método de test | Endpoint | HTTP Status | Tipo |
|----|---------------|----------|-------------|------|
| IT-L-01 | `Login_Success_Returns200WithToken` | `POST /api/auth/login` | 200 OK | Happy path E2E (Register → Login) |
| IT-L-02 | `Login_WrongPassword_Returns401` | `POST /api/auth/login` | 401 Unauthorized | Flujo alterno |
| IT-L-03 | `Login_ThreeFailedAttempts_AccountLocked_Returns401` | `POST /api/auth/login` | 401 Unauthorized | Regla de negocio (RN5) |
| IT-L-04 | `Login_UnknownEmail_Returns401` | `POST /api/auth/login` | 401 Unauthorized | Anti-enumeración |
| IT-L-05 | `Logout_WithValidToken_Returns200` | `POST /api/auth/logout` | 200 OK | Happy path (sesión) |
| IT-L-06 | `Logout_WithoutToken_Returns401` | `POST /api/auth/logout` | 401 Unauthorized | Seguridad |

---

## 7. Test Cases

Los Test Cases detallados en formato Gherkin se encuentran en el archivo [`TEST_CASES.md`](./TEST_CASES.md). Aquí se presenta el mapeo entre los Test Cases funcionales y los tests automatizados:

### 7.1 HU1 — Registro de nuevo usuario comprador

| Test Case (Funcional) | Test Automatizado | Nivel | Tipo |
|----------------------|-------------------|-------|------|
| CP-HU1-01: Registro exitoso | UT-R-01 + IT-R-01 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU1-02: Campos vacíos | IT-R-03 (parcial: validación DataAnnotations) | Integración | Caja Negra |
| CP-HU1-03: Formato email inválido | — (validación en frontend/middleware) | — | — |
| CP-HU1-04: Contraseñas no coinciden | IT-R-04 | Integración | Caja Negra |
| CP-HU1-05: Longitud < 8 caracteres (RN2) | UT-R-03 + IT-R-03 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU1-06: Sin mayúscula (RN2) | UT-R-03 (misma partición) | Unitario | Caja Blanca |
| CP-HU1-07: Sin carácter especial (RN2) | UT-R-03 (misma partición) | Unitario | Caja Blanca |
| CP-HU1-08: Email duplicado (RN1) | UT-R-02 + IT-R-02 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU1-09: BCrypt (RN3) | UT-R-01 (verifica que `Hash()` se invoca) | Unitario | Caja Blanca |

### 7.2 HU2 — Inicio y cierre de sesión

| Test Case (Funcional) | Test Automatizado | Nivel | Tipo |
|----------------------|-------------------|-------|------|
| CP-HU2-01: Login exitoso (Comprador) | UT-L-01 + IT-L-01 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU2-02: Login exitoso (Admin) | UT-L-01 (misma lógica, sin distinción de rol) | Unitario | Caja Blanca |
| CP-HU2-03: Campos vacíos | — (validación frontend) | — | — |
| CP-HU2-04: Email inválido | — (validación frontend) | — | — |
| CP-HU2-05: Espacios en email | UT-R-04 (la normalización aplica en registro) | Unitario | Caja Blanca |
| CP-HU2-06: Usuario no registrado (RN4) | UT-L-02 + IT-L-04 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU2-07: Contraseña incorrecta | UT-L-03 + IT-L-02 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU2-08: 2.° intento sin bloqueo (RN5) | UT-L-04 (valida que 2 intentos NO bloquean) | Unitario | Caja Blanca |
| CP-HU2-09: Bloqueo al 3.er intento (RN5) | UT-L-04 + IT-L-03 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU2-10: Login en cuenta bloqueada | UT-L-05 + IT-L-03 | Unitario + Integración | Caja Blanca + Caja Negra |
| CP-HU2-11: Email > 255 chars | — (no automatizado aún) | — | — |
| CP-HU2-12: Inyección SQL | IT-L-04 (EF Core parametriza las queries) | Integración | Caja Negra |
| CP-HU2-13: Token JWT válido | IT-L-05 (usa el token para logout exitoso) | Integración | Caja Negra |
| CP-HU2-14: Token JWT expirado | — (no automatizado; requiere config de tiempo) | — | — |
| CP-HU2-15: Cierre de sesión exitoso | IT-L-05 | Integración | Caja Negra |
| CP-HU2-16: Acceso sin token | IT-L-06 | Integración | Caja Negra |

---

## 8. Trazabilidad a reglas de negocio y patrones de diseño

### 8.1 Reglas de negocio → Tests

| Regla | Descripción | Tests unitarios | Tests integración |
|-------|-------------|----------------|-------------------|
| **RN1** | Correo electrónico único en BD | UT-R-02 | IT-R-02 |
| **RN2** | Política de contraseña (≥8 chars, mayúscula, carácter especial) | UT-R-03 | IT-R-03 |
| **RN3** | Almacenamiento seguro con BCrypt | UT-R-01 (verifica invocación de `Hash()`) | IT-R-01 (implícito: BD real) |
| **RN4** | Acceso solo a usuarios registrados | UT-L-02 | IT-L-04 |
| **RN5** | Bloqueo tras 3 intentos fallidos | UT-L-04, UT-L-05 | IT-L-03 |

### 8.2 Patrones de diseño GoF → Tests

| Patrón | Componente probado | Tests que lo validan |
|--------|-------------------|---------------------|
| **Factory Method** | `User.Create()` | UT-R-01 (crea usuario vía factory), UT-R-03 (rechaza contraseña inválida antes de crear) |
| **Adapter** | `BcryptPasswordService`, `UserRepository`, `LoginAttemptsRepository` | IT-R-01 a IT-R-04, IT-L-01 a IT-L-06 (todos usan adaptadores reales) |
| **Facade** | `RegisterUserUseCase`, `LoginUserUseCase` | UT-R-01 a UT-R-04, UT-L-01 a UT-L-05 (SUT directo) |
| **State** | `ActiveState`, `LockedState` | UT-L-04 (transición Active→Locked), UT-L-05 (comportamiento Locked), IT-L-03 (flujo completo) |
| **Strategy ⚠️** | `IPasswordHashingService` (mock en tests) | UT-L-01 a UT-L-05 (mock); IT-L-01 a IT-L-06 (implementación real BCrypt) |

---

## 9. Infraestructura de pruebas

### 9.1 Fixture de integración: `AuthServiceFactory`

```
AuthServiceFactory (WebApplicationFactory<Program>)
    │
    ├── Levanta Testcontainers PostgreSQL 15
    ├── Reemplaza DbContextOptions con connection string del contenedor
    ├── Ejecuta migraciones EF Core automáticamente (MigrateAsync)
    ├── Comparte el contenedor entre todas las clases [Collection("Integration")]
    └── Limpia el contenedor vía IAsyncLifetime.DisposeAsync
```

### 9.2 Mocks (Nivel unitario)

| Interfaz (Puerto) | Mock vía NSubstitute | Comportamiento configurado |
|-------------------|---------------------|--------------------------|
| `IUserRepository` | `Substitute.For<IUserRepository>()` | `ExistsByEmailAsync` → true/false; `SaveAsync` → void; `FindByEmailAsync` → User o null |
| `IPasswordHashingService` | `Substitute.For<IPasswordHashingService>()` | `Hash()` → string fija; `Verify()` → true/false |
| `IJwtTokenService` | `Substitute.For<IJwtTokenService>()` | `GenerateToken()` → `("fake-jwt", 3600)` |
| `ILoginAttemptsRepository` | `Substitute.For<ILoginAttemptsRepository>()` | `RecordAttemptAsync()` → void |

### 9.3 Estructura de archivos de test

```
authService/
└── tests/
    ├── AuthService.Application.Tests/             ← CAJA BLANCA (Unitarios)
    │   ├── LoginUserUseCaseTests.cs               # 5 tests — Suite 2
    │   ├── RegisterUserUseCaseTests.cs            # 4 tests — Suite 1
    │   └── GlobalUsings.cs
    │
    └── AuthService.Infrastructure.Tests/          ← CAJA NEGRA (Integración)
        ├── AuthServiceFactory.cs                  # Fixture: WebApplicationFactory + Testcontainers
        ├── LoginFlowTests.cs                      # 6 tests — Suite 4
        ├── RegisterFlowTests.cs                   # 4 tests — Suite 3
        └── GlobalUsings.cs
```

---

## 10. Ejecución en Pipeline CI/CD

### 10.1 Estructura del pipeline (`.github/workflows/ci.yml`)

El pipeline está diseñado con **Jobs separados por nivel de prueba**, proporcionando trazabilidad visual directa:

| Job | Nombre | Contiene tests de AuthService | Nivel |
|-----|--------|------------------------------|-------|
| `build` | 🔨 Build — Todos los servicios | Compilación | — |
| `test-unit` | 🧪 Unit Tests — Caja Blanca | `AuthService.Application.Tests` | Unitario |
| `test-component` | 🔗 Component Tests | — (sin tests de componente para AuthService) | Componente |
| `test-integration` | 🌐 Integration Tests — Contratos entre Servicios | `AuthService.Infrastructure.Tests` | Integración |
| `test-blackbox` | 📦 Black-Box Tests — API HTTP Real | — (CrudService) | Black-Box |
| `docker-security` | 🐳 Docker Build & Trivy Scan | Build imagen auth-service + escaneo | Seguridad |

### 10.2 Comandos de ejecución local

```bash
# Solo unitarios (rápido, sin infraestructura)
dotnet test authService/tests/AuthService.Application.Tests/ -c Release

# Solo integración (requiere Docker para Testcontainers)
dotnet test authService/tests/AuthService.Infrastructure.Tests/ -c Release

# Suite completa con reportes TRX
dotnet test authService/tests/AuthService.Application.Tests/ \
  --logger "trx;LogFileName=unit-authservice-app.trx" \
  --results-directory ./test-results/unit -c Release

dotnet test authService/tests/AuthService.Infrastructure.Tests/ \
  --logger "trx;LogFileName=integration-authservice.trx" \
  --results-directory ./test-results/integration -c Release
```

### 10.3 Seguridad Docker

La imagen Docker del AuthService se construye con mejores prácticas de seguridad:

- **Multi-stage build**: imagen base `mcr.microsoft.com/dotnet/aspnet:8.0` (runtime ligero).
- **Usuario no-root**: `authservice` (UID 1001) — no se ejecuta como root.
- **Escaneo Trivy**: El Job `docker-security` ejecuta `aquasecurity/trivy-action` para detectar vulnerabilidades `CRITICAL` y `HIGH` en la imagen.

---

## 11. Criterios de entrada y salida

### 11.1 Criterios de entrada

- [ ] Código compila sin errores (`dotnet build -c Release`).
- [ ] Dependencias de test instaladas (xUnit, NSubstitute, Testcontainers).
- [ ] Docker disponible para tests de integración (Testcontainers requiere Docker daemon).
- [ ] Base de datos PostgreSQL accesible via Testcontainers (puerto dinámico).

### 11.2 Criterios de salida

- [ ] **100% de tests pasan** (19/19 en verde).
- [ ] **0 tests flaky** — cada test produce el mismo resultado en ejecuciones sucesivas.
- [ ] **Todas las reglas de negocio (RN1–RN5) tienen al menos 1 test unitario y 1 test de integración**.
- [ ] Reportes TRX generados y disponibles como artefactos en GitHub Actions.
- [ ] Imagen Docker construida y escaneada sin vulnerabilidades CRITICAL.

---

## 12. Riesgos y mitigaciones

| # | Riesgo | Probabilidad | Impacto | Mitigación |
|---|--------|-------------|---------|------------|
| 1 | **BCrypt lento en tests unitarios** (~300ms por hash) | Alta | Medio | Mock de `IPasswordHashingService` vía NSubstitute (patrón Strategy). Los tests unitarios completan en <1 segundo total. |
| 2 | **Testcontainers falla en CI** (Docker no disponible) | Media | Alto | El runner de GitHub Actions (`ubuntu-latest`) incluye Docker. Se documenta como requisito de CI. |
| 3 | **Race condition en registro** — dos solicitudes simultáneas con el mismo email | Media | Alto | La constraint de unicidad en PostgreSQL (`UNIQUE` en campo email) actúa como última línea de defensa. Test futuro planificado. |
| 4 | **Token JWT expirado no cubierto** | Media | Medio | Requiere configurar tiempo de expiración corto en tests. Se incluye como deuda técnica a cubrir. |
| 5 | **Paradoja del Pesticida** — tests dejan de encontrar defectos | Baja | Medio | Dos niveles distintos (unitario + integración) con datos dinámicos. Revisión periódica de cobertura lógica. |
| 6 | **Migración de esquema rompe repositorios** | Baja | Alto | Tests de integración ejecutan `MigrateAsync()` en cada ejecución, validando el esquema actual contra el código. |

---

## Resumen ejecutivo

```
┌─────────────────────────────────────────────────────────────────┐
│  AUTHSERVICE — PLAN DE PRUEBAS                                  │
│                                                                 │
│  Total tests:        19                                         │
│  ├── Caja Blanca:     9  (AuthService.Application.Tests)       │
│  └── Caja Negra:     10  (AuthService.Infrastructure.Tests)    │
│                                                                 │
│  Reglas cubiertas:   RN1 ✓  RN2 ✓  RN3 ✓  RN4 ✓  RN5 ✓       │
│  Patrones cubiertos: Factory Method ✓  Adapter ✓  Facade ✓    │
│                      State ✓  Strategy ⚠️ ✓                    │
│                                                                 │
│  Pipeline CI/CD:     6 Jobs separados por nivel                │
│  Seguridad Docker:   Multi-stage + no-root + Trivy scan        │
│                                                                 │
│  7 Principios:       Aplicados y justificados en Sección 3     │
└─────────────────────────────────────────────────────────────────┘
```

---

> **Documentos relacionados:**  
> - [`TEST_CASES.md`](./TEST_CASES.md) — Casos de prueba funcionales en formato Gherkin  
> - [`DESIGN_PATTERNS.md`](./DESIGN_PATTERNS.md) — Análisis de patrones GoF aplicados  
> - [`BUSINESS_CONTEXT.md`](../BUSINESS_CONTEXT.md) — Contexto de negocio del proyecto  
> - [`.github/workflows/ci.yml`](../.github/workflows/ci.yml) — Definición del pipeline CI/CD
