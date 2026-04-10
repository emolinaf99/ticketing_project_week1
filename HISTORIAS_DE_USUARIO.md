# Historias de Usuario — Ticketify

**Proyecto:** Ticketify  
**Fecha:** 2026-04-09  
**Versión:** 1.0  
**Metodología:** INVEST + Criterios de Aceptación Given/When/Then

---

## Criterio INVEST

| Letra | Significado | Cómo se aplica en este documento |
|-------|-------------|----------------------------------|
| **I** | Independent | Cada HU puede desarrollarse sin depender de otra incompleta |
| **N** | Negotiable | Los detalles de UX y payload son negociables; el objetivo de negocio no |
| **V** | Valuable | Cada HU entrega valor observable al usuario o al negocio |
| **E** | Estimable | Se estima en Story Points (1, 2, 3, 5, 8) |
| **S** | Small | Entregable en un sprint de 2 semanas por un par de devs |
| **T** | Testable | Cada HU tiene escenarios Given/When/Then verificables |

---

## Módulo 1 — Autenticación

---

### HU-001 · Registro de nuevo usuario

**Como** visitante no registrado en Ticketify,  
**quiero** crear una cuenta con mi nombre, correo y contraseña,  
**para** tener una identidad única que me permita entrar a la fila justa y comprar tickets.

| INVEST | Evaluación |
|--------|-----------|
| Independent | No depende de otra HU pendiente; AuthService es autónomo |
| Negotiable | Campos adicionales (teléfono, avatar) son negociables en sprints futuros |
| Valuable | Sin registro no hay compradores; es la puerta de entrada al negocio |
| Estimable | **3 SP** — endpoint REST + validaciones + hash + test integración |
| Small | Se implementa en 1 sprint con un desarrollador |
| Testable | Sí — escenarios Given/When/Then definidos abajo |

**Criterios de Aceptación:**

```
Escenario 1 — Registro exitoso
  DADO   un visitante con datos válidos (firstName, lastName, email, password, confirmPassword)
  CUANDO envía POST /api/auth/register
  ENTONCES el sistema responde 201 Created
  Y       el usuario queda almacenado en BD con la contraseña hasheada (BCrypt)
  Y       el email se normaliza a minúsculas antes de persistir

Escenario 2 — Email ya registrado
  DADO   un visitante con un email que ya existe en BD
  CUANDO envía POST /api/auth/register
  ENTONCES el sistema responde 409 Conflict
  Y       el body contiene { "message": "Este correo ya está en uso. ¿Deseas iniciar sesión?" }
  Y       no se crea ningún registro nuevo en BD

Escenario 3 — Contraseña que no cumple las reglas
  DADO   un visitante cuya contraseña tiene menos de 8 caracteres o carece de mayúscula o carácter especial
  CUANDO envía POST /api/auth/register
  ENTONCES el sistema responde 400 Bad Request
  Y       el body describe qué regla fue violada

Escenario 4 — Campos obligatorios faltantes
  DADO   un visitante que omite uno o más campos requeridos
  CUANDO envía POST /api/auth/register
  ENTONCES el sistema responde 400 Bad Request
  Y       no se crea ningún registro
```

**Reglas de Negocio:**

- **RN-001-1** El email es el identificador único del usuario; no puede haber dos cuentas con el mismo email (case-insensitive).
- **RN-001-2** La contraseña debe cumplir: mínimo 8 caracteres, al menos 1 mayúscula, al menos 1 carácter especial.
- **RN-001-3** Solo se almacena el hash de la contraseña (BCrypt, work factor ≥ 12); nunca el texto plano.
- **RN-001-4** El email se normaliza a minúsculas al guardar para evitar duplicados por capitalización.
- **RN-001-5** `confirmPassword` debe ser igual a `password`; si difieren se responde 400.

---

### HU-002 · Inicio de sesión

**Como** usuario registrado en Ticketify,  
**quiero** iniciar sesión con mi correo y contraseña,  
**para** obtener un token JWT que me permita acceder a funcionalidades protegidas como la fila justa y la compra de tickets.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Depende de que exista la cuenta (HU-001 completa), no de otra HU en curso |
| Negotiable | El tiempo de expiración del token y el algoritmo son configurables |
| Valuable | Sin login no hay acceso autenticado; bloquea todo el flujo de compra |
| Estimable | **2 SP** — endpoint + JWT generation + intentos fallidos |
| Small | Implementable en 1 sprint |
| Testable | Sí — escenarios below |

**Criterios de Aceptación:**

```
Escenario 1 — Login exitoso
  DADO   un usuario registrado con credenciales correctas
  CUANDO envía POST /api/auth/login con { email, password }
  ENTONCES el sistema responde 200 OK
  Y       el body contiene { token, expiresIn }
  Y       el token es un JWT firmado HS256 con claims: sub (GUID), email, iat, exp

Escenario 2 — Contraseña incorrecta
  DADO   un usuario registrado que ingresa contraseña incorrecta
  CUANDO envía POST /api/auth/login
  ENTONCES el sistema responde 401 Unauthorized
  Y       el body contiene { "message": "Credenciales inválidas" } (mensaje genérico)
  Y       el sistema registra el intento fallido en BD

Escenario 3 — Cuenta bloqueada tras 3 fallos consecutivos
  DADO   un usuario que ha fallado 3 veces consecutivas sin un login exitoso intermedio
  CUANDO intenta hacer login nuevamente
  ENTONCES el sistema responde 401 Unauthorized
  Y       el body contiene { "message": "Credenciales inválidas" } (no revela el bloqueo)
  Y       la cuenta permanece bloqueada durante 15 minutos desde el tercer fallo

Escenario 4 — Usuario inexistente
  DADO   un email que no existe en BD
  CUANDO envía POST /api/auth/login
  ENTONCES el sistema responde 401 con el mismo mensaje genérico (no revela inexistencia)

Escenario 5 — Cierre de sesión
  DADO   un usuario autenticado con token vigente
  CUANDO envía POST /api/auth/logout
  ENTONCES el sistema responde 200 OK con { "message": "Logout exitoso" }
  Y       el cliente elimina el token del almacenamiento local
```

**Reglas de Negocio:**

- **RN-002-1** Tras 3 intentos fallidos consecutivos la cuenta se bloquea automáticamente por 15 minutos (configurable vía `LockoutSettings.LockoutDurationMinutes`).
- **RN-002-2** Un login exitoso reinicia el contador de fallos a 0.
- **RN-002-3** Los mensajes de error de autenticación son siempre genéricos para evitar user enumeration.
- **RN-002-4** El token JWT tiene expiración de 60 minutos por defecto (configurable vía `JwtSettings.ExpirationMinutes`).
- **RN-002-5** El `sub` del JWT es el GUID del usuario, nunca un ID numérico secuencial.
- **RN-002-6** El desbloqueo es automático al transcurrir el tiempo de bloqueo; no requiere acción manual del administrador.

---

### HU-003 · Protección de rutas por autenticación

**Como** usuario no autenticado que intenta acceder a una ruta protegida,  
**quiero** ser redirigido automáticamente a la pantalla de login,  
**para** entender que necesito identificarme antes de continuar.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Se implementa como guard en el router, independiente de cualquier servicio |
| Negotiable | El destino de redirección post-login es configurable |
| Valuable | Protege el negocio: ningún usuario anónimo puede comprar o entrar a una cola |
| Estimable | **1 SP** — route guard ya existe, se extiende con las rutas nuevas |
| Small | Tarea de configuración, no de desarrollo extenso |
| Testable | Sí — intentar navegar a ruta protegida sin token |

**Criterios de Aceptación:**

```
Escenario 1 — Acceso a ruta protegida sin sesión
  DADO   un usuario que no ha iniciado sesión
  CUANDO navega a /queue/:eventId/:ticketId o /admin
  ENTONCES es redirigido automáticamente a /login
  Y       no se hace ninguna llamada a la API protegida

Escenario 2 — Token expirado presentado en API
  DADO   un usuario cuyo JWT ha expirado
  CUANDO realiza cualquier llamada a un endpoint [Authorize]
  ENTONCES el servidor responde 401 Unauthorized
  Y       el frontend redirige al usuario a /login

Escenario 3 — Acceso a /login ya autenticado
  DADO   un usuario con sesión vigente
  CUANDO navega a /login o /register
  ENTONCES es redirigido automáticamente a / (página principal)
```

**Reglas de Negocio:**

- **RN-003-1** Las rutas `/queue/*` y `/admin/*` requieren autenticación obligatoria.
- **RN-003-2** Un 401 recibido desde cualquier servicio provoca cierre de sesión en el cliente y redirección a login.
- **RN-003-3** El token se almacena en `localStorage` bajo la clave `auth_token`; si no existe o está expirado, el usuario se considera no autenticado.

---

## Módulo 2 — Gestión de Eventos (Administrador)

---

### HU-004 · Crear evento

**Como** administrador de Ticketify,  
**quiero** registrar un nuevo evento (nombre, fecha, lugar, capacidad),  
**para** que los usuarios puedan verlo en el catálogo y adquirir tickets.

| INVEST | Evaluación |
|--------|-----------|
| Independent | El CRUD de eventos es autónomo en `crud-service` |
| Negotiable | Campos opcionales (imagen, descripción larga) se pueden agregar después |
| Valuable | Sin eventos no hay tickets; es el primer eslabón del ciclo de negocio |
| Estimable | **2 SP** — endpoint POST + validación + persistencia |
| Small | Un sprint |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Creación exitosa
  DADO   un administrador autenticado con datos de evento válidos
  CUANDO envía POST /api/events con { name, date, venue, totalTickets }
  ENTONCES el sistema responde 201 Created
  Y       el evento queda registrado en BD con status activo
  Y       el body contiene el ID asignado al evento

Escenario 2 — Datos inválidos
  DADO   un administrador que omite campos obligatorios o ingresa fecha en el pasado
  CUANDO envía POST /api/events
  ENTONCES el sistema responde 400 Bad Request con detalle de los campos inválidos

Escenario 3 — Evento duplicado (mismo nombre y fecha)
  DADO   un administrador que intenta crear un evento con nombre y fecha idénticos a uno existente
  CUANDO envía POST /api/events
  ENTONCES el sistema responde 409 Conflict
```

**Reglas de Negocio:**

- **RN-004-1** La fecha del evento no puede ser en el pasado al momento de la creación.
- **RN-004-2** `totalTickets` debe ser un entero positivo mayor a 0.
- **RN-004-3** El sistema genera automáticamente los registros de tickets individuales (uno por capacidad) al crear el evento.
- **RN-004-4** Un evento recién creado tiene estado `active` y todos sus tickets en `available`.

---

### HU-005 · Consultar catálogo de eventos

**Como** usuario (comprador o administrador) de Ticketify,  
**quiero** ver la lista de eventos disponibles con su nombre, fecha y tickets restantes,  
**para** decidir qué evento me interesa y acceder a su detalle.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Es una consulta de solo lectura, sin dependencias de flujo |
| Negotiable | Filtros, paginación y orden son mejoras negociables |
| Valuable | Es la pantalla de inicio; sin ella el usuario no puede comenzar el flujo de compra |
| Estimable | **1 SP** — GET con proyección simple |
| Small | Mínimo viable en pocas horas |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Lista de eventos disponibles
  DADO   que existen eventos registrados en el sistema
  CUANDO cualquier usuario (autenticado o no) accede a GET /api/events
  ENTONCES el sistema responde 200 OK
  Y       el body contiene la lista de eventos con: id, name, date, availableTickets, reservedTickets, paidTickets

Escenario 2 — Sin eventos registrados
  DADO   que no hay eventos en BD
  CUANDO se consulta GET /api/events
  ENTONCES el sistema responde 200 OK con lista vacía []
```

**Reglas de Negocio:**

- **RN-005-1** Los contadores `availableTickets`, `reservedTickets` y `paidTickets` se calculan en tiempo real a partir del estado de los tickets.
- **RN-005-2** Solo se muestran eventos con estado `active`; eventos archivados o cancelados se excluyen.

---

### HU-006 · Consultar detalle de un evento

**Como** usuario de Ticketify,  
**quiero** ver el detalle de un evento específico con todos sus tickets y sus estados,  
**para** seleccionar el ticket que deseo y entrar a la fila justa.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Solo requiere el ID del evento, sin flujos previos |
| Negotiable | El nivel de detalle de cada ticket (sección, fila) es negociable |
| Valuable | Punto de decisión de compra — donde el usuario elige el ticket |
| Estimable | **2 SP** — GET evento + tickets con contadores |
| Small | Un sprint |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Detalle de evento existente
  DADO   un evento con ID válido
  CUANDO el usuario accede a /events/:id
  ENTONCES la UI muestra nombre del evento, fecha, tickets disponibles/reservados/pagados
  Y       la lista de tickets muestra cada uno con su estado (available, reserved, paid)
  Y       los tickets con estado 'available' muestran el botón "Reservar"

Escenario 2 — Evento no encontrado
  DADO   un ID de evento que no existe
  CUANDO el usuario accede a /events/:id
  ENTONCES el sistema responde 404 Not Found
  Y       la UI muestra un mensaje de error amigable

Escenario 3 — Filtro por estado de ticket
  DADO   que la vista está cargada con múltiples tickets en distintos estados
  CUANDO el usuario selecciona un filtro (Disponibles / Reservados / Pagados / Todos)
  ENTONCES la lista se filtra en el cliente sin hacer nueva llamada a la API
```

**Reglas de Negocio:**

- **RN-006-1** Un ticket en estado `reserved` con `expires_at` en el pasado se muestra como `available` en la UI (el backend lo libera eventualmente).
- **RN-006-2** El botón "Reservar" solo se habilita para tickets en estado `available`.

---

## Módulo 3 — Fila Justa (Fair Queue)

---

### HU-007 · Entrar a la fila justa de un ticket

**Como** usuario autenticado en Ticketify,  
**quiero** entrar a la cola de un ticket disponible y que el sistema me asigne una posición justa basada en el orden de llegada,  
**para** tener la certeza de que mi lugar está garantizado y que bots o acaparadores no me pueden desplazar.

| INVEST | Evaluación |
|--------|-----------|
| Independent | El FairQueueService es un microservicio autónomo con su propia BD |
| Negotiable | El tiempo de turno (180 s) y el algoritmo de estimación de espera son configurables |
| Valuable | Núcleo de la disrupción — sin esto el producto no se diferencia del mercado |
| Estimable | **5 SP** — POST /enter + lógica FIFO + activación inmediata si primero + SSE notif |
| Small | Implementable en 1 sprint con foco exclusivo |
| Testable | Sí — escenarios con múltiples actores simultáneos |

**Criterios de Aceptación:**

```
Escenario 1 — Primer usuario en cola
  DADO   un usuario autenticado y un ticket sin nadie en cola
  CUANDO hace clic en "Reservar" y el sistema llama POST /api/queue/enter con { eventId, ticketId }
  ENTONCES el sistema responde 200/201 con { position: 1, status: "active", turnExpiresAt: <ISO> }
  Y       la UI muestra directamente el PaymentModal con countdown de 3 minutos
  Y       FairQueueService publica ticket.reserved a RabbitMQ para que ReservationService procese la reserva

Escenario 2 — Usuario entra a cola con personas adelante
  DADO   un ticket que ya tiene N personas en cola
  CUANDO un nuevo usuario autenticado llama POST /api/queue/enter
  ENTONCES el sistema responde 200/201 con { position: N+1, status: "waiting", totalInQueue: N+1 }
  Y       la UI muestra "Posición #N+1 de N+1 — Espera estimada: X min"

Escenario 3 — Idempotencia (mismo usuario entra dos veces)
  DADO   un usuario que ya tiene posición en la cola del ticket (waiting o active)
  CUANDO llama nuevamente a POST /api/queue/enter para el mismo ticket
  ENTONCES el sistema responde 200 con la posición existente (no crea duplicado)
  Y       la restricción UNIQUE (ticket_id, user_id) en BD garantiza la integridad

Escenario 4 — Usuario no autenticado intenta entrar
  DADO   un usuario sin JWT válido
  CUANDO llama a POST /api/queue/enter
  ENTONCES el sistema responde 401 Unauthorized
  Y       el frontend redirige a /login

Escenario 5 — Ticket no disponible
  DADO   un ticket con estado diferente a 'available' (reserved, paid)
  CUANDO un usuario intenta entrar a su cola
  ENTONCES el sistema responde 409 Conflict con mensaje explicativo
```

**Reglas de Negocio:**

- **RN-007-1** Una identidad (user_id del JWT) puede ocupar máximo **una posición** por ticket. La restricción `UNIQUE (ticket_id, user_id)` es inviolable.
- **RN-007-2** La posición se asigna por estricto orden de llegada (timestamp `entered_at`); no hay prioridades por historial o tipo de cuenta.
- **RN-007-3** Si el usuario entrante es `position = 1`, su turno se activa inmediatamente (status = `active`) y se publica `ticket.reserved` a RabbitMQ.
- **RN-007-4** El turno activo tiene duración de **180 segundos** (configurable vía `QueueSettings.TurnDurationSeconds`).
- **RN-007-5** Solo usuarios con JWT válido y claim `sub` (GUID) pueden entrar a la cola.

---

### HU-008 · Ver posición en la cola en tiempo real

**Como** usuario en espera dentro de la fila justa de Ticketify,  
**quiero** ver mi posición actualizada automáticamente sin recargar la página,  
**para** saber cuánto tiempo falta para mi turno y mantener la confianza en el proceso.

| INVEST | Evaluación |
|--------|-----------|
| Independent | El SSE hub es interno al FairQueueService; no modifica otros servicios |
| Negotiable | La frecuencia de actualización y el diseño visual del indicador son negociables |
| Valuable | La transparencia en tiempo real es el diferenciador central del producto |
| Estimable | **3 SP** — SSE endpoint + hub Channel<T> + notificación a todos los waiting |
| Small | Un sprint focalizado en SSE |
| Testable | Sí — múltiples actores con verificación de mensajes SSE |

**Criterios de Aceptación:**

```
Escenario 1 — Actualización cuando avanza la cola
  DADO   un usuario en posición #3
  CUANDO el usuario #1 completa su pago y el sistema avanza la cola
  ENTONCES el usuario en #3 recibe un evento SSE con { position: 2, status: "waiting" }
  Y       la UI actualiza el número de posición sin recarga de página
  Y       el tiempo estimado se recalcula (posición - 1) × (turno/2)

Escenario 2 — Activación de turno via SSE
  DADO   un usuario en posición #2
  CUANDO el usuario #1 abandona o expira su turno
  ENTONCES el usuario en #2 recibe SSE con { status: "active", turnExpiresAt: <ISO> }
  Y       la UI oculta el panel de posición y muestra el PaymentModal con countdown

Escenario 3 — Múltiples usuarios en cola simultánea
  DADO   5 usuarios en cola para el mismo ticket
  CUANDO cualquiera de ellos avanza, abandona o expira
  ENTONCES los 4 restantes reciben actualizaciones SSE correctas con sus posiciones ajustadas
  Y       no hay interferencia entre los streams de distintos usuarios

Escenario 4 — Reconexión ante error de red
  DADO   un usuario cuya conexión SSE se interrumpe
  CUANDO el cliente detecta el error (onerror)
  ENTONCES muestra indicador "Reconectando..."
  Y       el cliente puede consultar GET /api/queue/{ticketId}/position para recuperar estado actual
```

**Reglas de Negocio:**

- **RN-008-1** El SSE usa el patrón `Channel<T>` en memoria; no hay broker externo para la cola de posiciones (sí para `ticket.reserved`).
- **RN-008-2** El token JWT se pasa como query param `?access_token=` exclusivamente para el endpoint SSE, ya que `EventSource` del browser no soporta headers personalizados.
- **RN-008-3** La estimación de espera se calcula como `(position - 1) × (TurnDurationSeconds / 2)` segundos.
- **RN-008-4** El stream SSE se cierra automáticamente en el servidor cuando el estado es terminal (`completed`, `cancelled`, `timed_out`).
- **RN-008-5** Cada `ticketId` tiene su propio conjunto de canales SSE; los streams de tickets distintos son completamente independientes.

---

### HU-009 · Completar el pago cuando llega el turno

**Como** usuario cuyo turno acaba de activarse en la fila justa de Ticketify,  
**quiero** ver el formulario de pago con un countdown visible y completar la compra antes de que expire el tiempo,  
**para** asegurar el ticket que me corresponde por haber esperado en la fila.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Reutiliza PaymentModal existente; no modifica PaymentService |
| Negotiable | El método de pago (tarjeta, PSE) y el diseño del formulario son negociables |
| Valuable | Convierte la espera en ingresos; es el cierre del ciclo de negocio |
| Estimable | **3 SP** — integración PaymentModal con countdown + manejo de estados SSE |
| Small | Un sprint |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Pago exitoso dentro del countdown
  DADO   un usuario con status='active' y countdown en ejecución
  CUANDO completa el formulario de pago y lo envía
  ENTONCES el sistema procesa el pago vía Producer → RabbitMQ → PaymentService
  Y       al llegar status='paid' via SSE, la UI muestra "Pago aprobado — Ticket #X comprado"
  Y       el ticket queda en estado 'paid' en BD

Escenario 2 — Pago rechazado por el servicio de pagos
  DADO   un usuario que completa el formulario pero PaymentService rechaza el pago
  CUANDO llega status='released' via SSE del CrudService
  ENTONCES la UI muestra "Pago rechazado — el ticket fue liberado"
  Y       el ticket vuelve a estado 'available'
  Y       FairQueueService avanza la cola al siguiente usuario

Escenario 3 — Countdown llega a cero sin pago
  DADO   un usuario con status='active' cuyo turno expira sin pago
  CUANDO TurnExpirationWorker detecta turn_expires_at < NOW()
  ENTONCES el usuario recibe SSE con status='timed_out'
  Y       la UI muestra "Tu turno venció — vuelve a intentarlo"
  Y       FairQueueService avanza la cola al siguiente usuario automáticamente

Escenario 4 — Usuario cierra el PaymentModal durante su turno activo
  DADO   un usuario con turno activo que cierra el modal de pago
  CUANDO el modal emite el evento 'close'
  ENTONCES la UI muestra diálogo de confirmación: "¿Abandonar la fila? Perderás tu turno"
  Y       si confirma, se llama DELETE /api/queue/{ticketId}/leave y la cola avanza
```

**Reglas de Negocio:**

- **RN-009-1** El countdown es calculado en el cliente a partir de `turnExpiresAt` recibido en el SSE; se actualiza cada segundo con `setInterval`.
- **RN-009-2** La expiración del turno en el servidor es enforced por `TurnExpirationWorker` (polling cada 10 segundos); el cliente solo la visualiza.
- **RN-009-3** Un turno expirado libera el ticket: `ticket.status.changed` con `newStatus='released'` es consumido por FairQueueService para avanzar la cola.
- **RN-009-4** El pago rechazado (20% simulado) libera el ticket y avanza la cola; el usuario debe volver al inicio si quiere intentarlo.
- **RN-009-5** El flujo de pago (Producer → RabbitMQ → PaymentService → CrudService) no cambia; la Fila Justa es transparente para esos servicios.

---

### HU-010 · Salir voluntariamente de la cola

**Como** usuario en espera en la fila justa de Ticketify,  
**quiero** poder abandonar la cola en cualquier momento antes de que llegue mi turno,  
**para** no bloquear el acceso de otros usuarios que sí desean el ticket.

| INVEST | Evaluación |
|--------|-----------|
| Independent | Es un endpoint DELETE simple que no impacta otros flujos |
| Negotiable | El mensaje de confirmación y la navegación post-salida son negociables |
| Valuable | Sin salida voluntaria, los usuarios inactivos bloquean toda la cola |
| Estimable | **2 SP** — DELETE endpoint + avance de cola si era el activo + SSE notif |
| Small | Un sprint |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Salida voluntaria en estado waiting
  DADO   un usuario en cola con status='waiting'
  CUANDO hace clic en "Abandonar fila" y confirma el diálogo
  ENTONCES el sistema llama DELETE /api/queue/{ticketId}/leave
  Y       el sistema responde 204 No Content
  Y       la cola avanza (posiciones de los siguientes usuarios se reducen en 1)
  Y       los usuarios restantes reciben actualización SSE con sus nuevas posiciones
  Y       el usuario es redirigido a /events/:eventId

Escenario 2 — Cierre de pestaña o navegación
  DADO   un usuario en cola (waiting o active) que cierra la pestaña o navega fuera de /queue
  CUANDO se dispara el hook onUnmounted del componente
  ENTONCES el frontend llama DELETE /api/queue/{ticketId}/leave automáticamente (sin confirmación)
  Y       el sistema libera la posición

Escenario 3 — Salida cuando el turno ya está activo
  DADO   un usuario con status='active' que decide abandonar
  CUANDO llama DELETE /api/queue/{ticketId}/leave
  ENTONCES su entrada se marca como 'cancelled'
  Y       el sistema avanza al siguiente en espera y le notifica via SSE
  Y       el ticket vuelve a 'available' para el siguiente
```

**Reglas de Negocio:**

- **RN-010-1** Un usuario solo puede salir de su propia cola (el `user_id` del JWT debe coincidir con el de la entrada).
- **RN-010-2** Si el usuario en estado `active` abandona, se debe hacer la operación de avance de cola de inmediato para no dejar el ticket en limbo.
- **RN-010-3** Un usuario que abandona la cola puede volver a entrar si el ticket sigue disponible, pero ocupa la última posición (no recupera la anterior).
- **RN-010-4** La salida por cierre de pestaña es silenciosa (no muestra confirmación); la salida manual requiere confirmación explícita.

---

## Módulo 4 — Reserva y Pago (Flujo Asíncrono)

---

### HU-011 · Procesamiento asíncrono de reserva de ticket

**Como** sistema de Ticketify (actor interno),  
**quiero** procesar la reserva de un ticket de forma asíncrona a través de RabbitMQ,  
**para** desacoplar el FairQueueService del ReservationService y garantizar la resiliencia del flujo.

| INVEST | Evaluación |
|--------|-----------|
| Independent | ReservationService consume de RabbitMQ sin dependencias síncronas |
| Negotiable | El tiempo de expiración de reserva es configurable |
| Valuable | Garantiza que el ticket quede reservado para el usuario activo en la fila |
| Estimable | **3 SP** — consumer + lógica de reserva + actualización de estado con optimistic locking |
| Small | Un sprint |
| Testable | Sí — publicar mensaje de prueba y verificar estado en BD |

**Criterios de Aceptación:**

```
Escenario 1 — Reserva exitosa
  DADO   que FairQueueService publica { ticketId, eventId, userId, email } a ticket.reserved
  CUANDO ReservationService consume el mensaje
  ENTONCES el ticket cambia de 'available' a 'reserved' en BD
  Y       se registra reserved_by, reserved_at y expires_at (180 segundos desde ahora)
  Y       CrudService publica ticket.status.changed vía SSE para notificar al frontend

Escenario 2 — Ticket ya no disponible (race condition)
  DADO   que dos mensajes llegan casi simultáneamente para el mismo ticket
  CUANDO ReservationService intenta reservar el ya tomado
  ENTONCES el optimistic locking (version check) rechaza el segundo mensaje
  Y       el ticket permanece en el estado del primero que lo tomó

Escenario 3 — Expiración de reserva sin pago
  DADO   un ticket en estado 'reserved' cuyo expires_at ha vencido
  CUANDO el worker de expiración detecta el vencimiento
  ENTONCES el ticket cambia a 'released'
  Y       CrudService publica ticket.status.changed con newStatus='released'
  Y       FairQueueService detecta el evento y avanza la cola al siguiente usuario
```

**Reglas de Negocio:**

- **RN-011-1** Un ticket solo puede estar reservado por un usuario a la vez; el campo `version` previene condiciones de carrera.
- **RN-011-2** La reserva expira automáticamente tras `reservationDurationSeconds` (180 s por defecto).
- **RN-011-3** El `ReservationService` es idempotente: si recibe el mismo `ticketId` dos veces, el segundo mensaje no genera un estado inconsistente.

---

### HU-012 · Procesamiento asíncrono de pago

**Como** usuario de Ticketify que ha completado el formulario de pago,  
**quiero** que mi pago sea procesado de forma confiable y recibir confirmación en tiempo real,  
**para** saber si el ticket quedó asegurado a mi nombre o si fue rechazado.

| INVEST | Evaluación |
|--------|-----------|
| Independent | PaymentService consume mensajes de RabbitMQ sin dependencias síncronas del front |
| Negotiable | La lógica de aprobación/rechazo y el porcentaje de rechazo simulado son configurables |
| Valuable | El pago es la fuente de ingresos; sin confirmación el usuario no tiene certeza |
| Estimable | **3 SP** — consumer + lógica aprobado/rechazado + publicación de resultado |
| Small | Un sprint |
| Testable | Sí — publicar mensajes de prueba y verificar estado final |

**Criterios de Aceptación:**

```
Escenario 1 — Pago aprobado
  DADO   un ticket en estado 'reserved' y un mensaje de pago válido en la cola
  CUANDO PaymentService procesa el mensaje y aprueba el pago
  ENTONCES el ticket cambia de 'reserved' a 'paid' en BD
  Y       CrudService emite SSE con status='paid' al frontend del usuario
  Y       la UI muestra confirmación de compra exitosa

Escenario 2 — Pago rechazado
  DADO   un ticket en estado 'reserved' y un mensaje de pago que falla validación
  CUANDO PaymentService rechaza el pago (20% de probabilidad simulada)
  ENTONCES el ticket cambia de 'reserved' a 'released'
  Y       CrudService emite SSE con status='released' al frontend
  Y       FairQueueService detecta 'released' y avanza la cola
  Y       la UI muestra mensaje "Pago rechazado"

Escenario 3 — Mensaje de pago duplicado o expirado
  DADO   un mensaje cuyo ticketId ya tiene estado 'paid' o 'released'
  CUANDO PaymentService lo intenta procesar
  ENTONCES el mensaje se descarta silenciosamente (idempotencia)
  Y       el estado del ticket no cambia
```

**Reglas de Negocio:**

- **RN-012-1** El 20% de pagos es rechazado en la simulación actual (configurable para integraciones reales).
- **RN-012-2** Una vez el ticket está en `paid`, no puede volver a ningún estado anterior.
- **RN-012-3** El resultado del pago se comunica vía RabbitMQ → CrudService → SSE al frontend; no hay polling.

---

## Módulo 5 — Administración

---

### HU-013 · Gestión de tickets (administrador)

**Como** administrador de Ticketify,  
**quiero** consultar los tickets de un evento, ver sus estados y crear tickets adicionales,  
**para** gestionar el inventario de entradas y atender situaciones operativas.

| INVEST | Evaluación |
|--------|-----------|
| Independent | CRUD de tickets es autónomo en crud-service |
| Negotiable | Edición de precios y categorías son mejoras futuras |
| Valuable | El administrador necesita visibilidad completa del inventario para operar |
| Estimable | **2 SP** — GET + POST bulk + actualización de estado |
| Small | Un sprint |
| Testable | Sí |

**Criterios de Aceptación:**

```
Escenario 1 — Consultar tickets de un evento
  DADO   un administrador autenticado
  CUANDO accede a GET /api/events/:eventId/tickets
  ENTONCES ve la lista completa de tickets con su estado actual

Escenario 2 — Crear tickets adicionales en lote
  DADO   un administrador que necesita ampliar el aforo
  CUANDO envía POST /api/tickets/bulk con { eventId, quantity }
  ENTONCES el sistema crea N nuevos tickets en estado 'available'
  Y       responde 201 con la lista de tickets creados

Escenario 3 — Liberar ticket manualmente
  DADO   un administrador que detecta una reserva bloqueada
  CUANDO actualiza el estado del ticket a 'available' via PUT /api/tickets/:id/release
  ENTONCES el ticket cambia a 'available'
  Y       cualquier cola justa activa para ese ticket avanza automáticamente
```

**Reglas de Negocio:**

- **RN-013-1** Solo usuarios con rol de administrador pueden crear o liberar tickets manualmente.
- **RN-013-2** Un ticket en estado `paid` no puede ser liberado manualmente.
- **RN-013-3** La creación masiva de tickets está limitada a 500 por petición para prevenir saturación.

---

## Resumen de Historias de Usuario

| ID | Título | Módulo | SP | Prioridad |
|----|--------|--------|----|-----------|
| HU-001 | Registro de nuevo usuario | Autenticación | 3 | P1 |
| HU-002 | Inicio de sesión | Autenticación | 2 | P1 |
| HU-003 | Protección de rutas | Autenticación | 1 | P1 |
| HU-004 | Crear evento | Gestión Eventos | 2 | P1 |
| HU-005 | Consultar catálogo de eventos | Gestión Eventos | 1 | P1 |
| HU-006 | Consultar detalle de evento | Gestión Eventos | 2 | P1 |
| HU-007 | Entrar a la fila justa | Fila Justa | 5 | P1 |
| HU-008 | Ver posición en tiempo real | Fila Justa | 3 | P1 |
| HU-009 | Completar pago en turno activo | Fila Justa | 3 | P1 |
| HU-010 | Salir voluntariamente de la cola | Fila Justa | 2 | P2 |
| HU-011 | Reserva asíncrona de ticket | Reserva/Pago | 3 | P1 |
| HU-012 | Procesamiento asíncrono de pago | Reserva/Pago | 3 | P1 |
| HU-013 | Gestión de tickets (admin) | Administración | 2 | P2 |
| | **TOTAL** | | **32 SP** | |

---

## Glosario de Estados de Ticket

| Estado | Descripción |
|--------|-------------|
| `available` | Ticket libre, aceptando nuevas entradas a la fila justa |
| `reserved` | Ticket reservado para el usuario activo en la fila; en espera de pago |
| `paid` | Ticket comprado exitosamente; estado terminal positivo |
| `released` | Ticket liberado por pago rechazado o expiración; vuelve a aceptar cola |
| `cancelled` | Ticket cancelado por el administrador; fuera de circulación |

## Glosario de Estados de Cola (fair_queue)

| Estado | Descripción |
|--------|-------------|
| `waiting` | Usuario en espera; no es su turno aún |
| `active` | Es el turno del usuario; tiene 180 s para pagar |
| `completed` | Usuario completó el pago exitosamente |
| `cancelled` | Usuario abandonó la cola o el ticket fue adquirido por otro |
| `timed_out` | El usuario no pagó dentro del tiempo de turno |
