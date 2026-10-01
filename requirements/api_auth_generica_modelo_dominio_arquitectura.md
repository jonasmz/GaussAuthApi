# API Genérica de Autenticación y Autorización
## Modelo de dominio, arquitectura, stack tecnológico y reglas de desarrollo

## 1. Propósito

Este documento define el modelo de dominio, las reglas funcionales, las decisiones arquitectónicas, el stack tecnológico, las prácticas de desarrollo, los controles de seguridad y las reglas operativas para agentes de IA aplicables a una API genérica de autenticación y autorización.

La API debe poder reutilizarse en múltiples sistemas de negocio sin conocer detalles específicos de esos dominios.

Su responsabilidad se limita a:

- identidad;
- autenticación;
- autorización;
- usuarios;
- credenciales;
- perfiles globales;
- aplicaciones registradas;
- roles;
- permisos;
- sesiones;
- emisión y revocación de credenciales de acceso.

La API no debe contener lógica de negocio perteneciente a las aplicaciones consumidoras.

---

# 2. Objetivos

La solución debe permitir:

- autenticar usuarios mediante correo electrónico;
- administrar usuarios;
- administrar perfiles globales;
- administrar aplicaciones consumidoras;
- asignar usuarios a una o más aplicaciones;
- definir roles por aplicación;
- definir permisos por aplicación;
- asociar permisos a roles;
- asignar roles a usuarios dentro de cada aplicación;
- emitir credenciales de acceso;
- validar el contexto de aplicación;
- revocar sesiones;
- bloquear o deshabilitar usuarios;
- aplicar controles contra abuso y fuerza bruta;
- mantener trazabilidad de eventos relevantes de seguridad;
- funcionar como servicio reutilizable para múltiples proyectos.

---

# 3. Principios generales del dominio

## 3.1. Separación entre identidad y negocio

La API de autenticación no debe conocer conceptos específicos de los sistemas consumidores.

No debe contener entidades como:

- Reserva;
- Turno;
- Pago;
- Cancha;
- Producto;
- Pedido;
- Inventario;
- Factura;
- Cliente comercial.

Cada aplicación de negocio mantiene su propio dominio y su propia persistencia.

## 3.2. Identidad global

Un usuario representa una identidad global dentro del servicio de autenticación.

La misma identidad puede participar en varias aplicaciones.

```text
Usuario
├── Aplicación: Turnos
│   └── Rol: Operador
├── Aplicación: PaymentsTracking
│   └── Rol: Administrador
└── Aplicación: RestoManager
    └── Rol: Cajero
```

## 3.3. Autorización contextual por aplicación

Los roles y permisos deben evaluarse dentro del contexto de una aplicación.

Un usuario con rol `Administrador` en una aplicación no obtiene automáticamente privilegios en otra.

---

# 4. Actores

## 4.1. Usuario

Persona que posee una identidad autenticable.

Puede:

- iniciar sesión;
- cerrar sesión;
- consultar o actualizar su perfil cuando esté permitido;
- cambiar su contraseña;
- recuperar acceso a su cuenta;
- acceder a aplicaciones según sus asignaciones.

## 4.2. Administrador del servicio de identidad

Usuario autorizado para gestionar componentes globales del servicio.

Puede incluir capacidades como:

- crear o deshabilitar usuarios;
- administrar aplicaciones;
- administrar roles;
- administrar permisos;
- asignar usuarios;
- revisar eventos de seguridad.

## 4.3. Aplicación consumidora

Sistema externo que delega autenticación y autorización en esta API.

---

# 5. Entidades principales

## 5.1. User

Representa la identidad principal de una persona.

Atributos conceptuales:

- `Id`
- `Email`
- `EmailNormalized`
- `EmailConfirmed`
- `IsActive`
- `CreatedAt`
- `UpdatedAt`

Las credenciales y mecanismos internos de ASP.NET Core Identity pertenecen a infraestructura y no deben contaminar el dominio.

## 5.2. UserProfile

Representa información global del usuario que puede ser reutilizada por distintas aplicaciones.

Atributos conceptuales:

- `Id`
- `UserId`
- `FirstName`
- `LastName`
- `DisplayName`
- `AvatarReference` opcional
- `Phone` opcional
- `CreatedAt`
- `UpdatedAt`

El perfil global no debe almacenar información propia de un negocio específico.

## 5.3. Application

Representa una aplicación autorizada a utilizar el servicio de identidad.

Atributos conceptuales:

- `Id`
- `Code`
- `Name`
- `IsActive`
- `CreatedAt`

Reglas:

- `Code` debe ser único.
- Una aplicación inactiva no debe aceptar nuevas autenticaciones para su contexto.
- Los roles y permisos se definen dentro de una aplicación.

## 5.4. ApplicationMembership

Representa la pertenencia de un usuario a una aplicación.

Atributos conceptuales:

- `Id`
- `UserId`
- `ApplicationId`
- `IsActive`
- `CreatedAt`

Permite diferenciar entre identidad global y autorización para participar en una aplicación concreta.

## 5.5. Role

Representa un conjunto de permisos dentro de una aplicación.

Atributos conceptuales:

- `Id`
- `ApplicationId`
- `Name`
- `NormalizedName`
- `Description`
- `IsActive`

Los nombres de roles pueden repetirse entre aplicaciones, pero no deben colisionar dentro de una misma aplicación.

## 5.6. Permission

Representa una capacidad concreta dentro de una aplicación.

Atributos conceptuales:

- `Id`
- `ApplicationId`
- `Code`
- `Description`
- `IsActive`

Ejemplos:

```text
reservations.read
reservations.create
reservations.cancel
payments.register
users.manage
reports.read
```

## 5.7. RolePermission

Relaciona un rol con uno o más permisos.

Atributos conceptuales:

- `RoleId`
- `PermissionId`

## 5.8. UserRole

Relaciona un usuario con un rol dentro de una aplicación.

Atributos conceptuales:

- `UserId`
- `RoleId`
- `ApplicationId`
- `CreatedAt`

## 5.9. Session

Representa una sesión autenticada.

Atributos conceptuales:

- `Id`
- `UserId`
- `ApplicationId`
- `CreatedAt`
- `ExpiresAt`
- `RevokedAt` opcional
- `LastActivityAt` opcional
- `ClientMetadata` opcional

## 5.10. SecurityEvent

Representa un evento relevante de seguridad.

Ejemplos:

- login exitoso;
- login fallido;
- bloqueo;
- recuperación de contraseña;
- cambio de contraseña;
- revocación de sesión;
- asignación de roles;
- modificación de permisos.

Atributos conceptuales:

- `Id`
- `UserId` opcional
- `ApplicationId` opcional
- `EventType`
- `OccurredAt`
- `Metadata`

Debe evitarse almacenar secretos o información sensible innecesaria.

---

# 6. Relaciones principales

```text
User
├── UserProfile
├── ApplicationMembership
│   └── Application
├── UserRole
│   └── Role
│       └── RolePermission
│           └── Permission
└── Session
```

---

# 7. Reglas de dominio

## DR-001 — Email como identificador de login
El inicio de sesión se realiza mediante correo electrónico. El email debe tratarse de forma normalizada y única para autenticación.

## DR-002 — Usuario activo
Un usuario inactivo no puede autenticarse.

## DR-003 — Aplicación activa
Una aplicación inactiva no puede emitir nuevas sesiones.

## DR-004 — Pertenencia a aplicación
Un usuario debe poseer una membresía activa para acceder al contexto de una aplicación.

## DR-005 — Roles contextualizados
Los roles pertenecen a una aplicación. No existe un rol global que otorgue permisos automáticamente en todas las aplicaciones.

## DR-006 — Permisos contextualizados
Los permisos pertenecen a una aplicación concreta.

## DR-007 — Asignaciones coherentes
No puede asignarse a un usuario un rol perteneciente a una aplicación distinta de la membresía correspondiente.

## DR-008 — Permisos derivados
Los permisos efectivos de un usuario se derivan de los roles activos asignados dentro de la aplicación activa.

## DR-009 — Sesión contextual
Toda sesión debe identificar usuario, aplicación, fecha de creación, fecha de expiración y estado de revocación.

## DR-010 — Sesión revocada
Una sesión revocada no debe seguir otorgando acceso válido.

## DR-011 — Cambio de estado del usuario
Deshabilitar un usuario debe impedir nuevas autenticaciones. La política para invalidar inmediatamente sesiones existentes debe definirse explícitamente.

## DR-012 — Cambio de credenciales
El cambio o restablecimiento de contraseña debe poder invalidar sesiones existentes según la política de seguridad definida.

## DR-013 — Perfil separado de identidad
Los datos de perfil no deben mezclarse con las estructuras internas de autenticación cuando no pertenezcan conceptualmente a Identity.

## DR-014 — Dominio de negocio fuera del servicio
No se almacenan en esta API datos específicos de las aplicaciones consumidoras.

## DR-015 — No compartir bases de negocio
La API de autenticación debe utilizar su propia base de datos. Las APIs de negocio no deben consultar directamente las tablas internas de autenticación.

## DR-016 — Integración mediante contratos
La relación entre la API de autenticación y las APIs de negocio debe realizarse mediante tokens, claims, APIs y contratos explícitos, no mediante acceso directo a tablas.

## DR-017 — Menor privilegio
Los permisos concedidos deben respetar el principio de menor privilegio.

## DR-018 — Trazabilidad
Los eventos críticos de seguridad deben poder auditarse.

---

# 8. Estados principales

## Usuario
```text
Active
Inactive
Locked
```

## Aplicación
```text
Active
Inactive
```

## Membresía
```text
Active
Inactive
```

## Rol
```text
Active
Inactive
```

## Permiso
```text
Active
Inactive
```

## Sesión
```text
Active
Expired
Revoked
```

---

# 9. Flujos principales

## 9.1. Login

1. El cliente identifica la aplicación.
2. El usuario envía correo electrónico y contraseña.
3. El sistema normaliza el email.
4. Se valida existencia y estado del usuario.
5. Se valida pertenencia activa a la aplicación.
6. Se valida la credencial mediante ASP.NET Core Identity.
7. Se aplican controles de rate limiting y bloqueo.
8. Se obtienen roles y permisos efectivos.
9. Se crea una sesión.
10. Se emite la credencial de acceso.
11. Se registra el evento de seguridad.

## 9.2. Logout

1. El usuario solicita cerrar sesión.
2. El sistema identifica la sesión.
3. La sesión queda revocada.
4. Se registra el evento.

## 9.3. Recuperación de contraseña

1. El usuario solicita recuperación.
2. El sistema aplica rate limiting.
3. Se genera el mecanismo de recuperación correspondiente.
4. No se debe revelar innecesariamente si un email existe.
5. El usuario completa el restablecimiento.
6. Se registra el evento.
7. Las sesiones existentes podrán invalidarse según política.

## 9.4. Asignación de usuario a aplicación

1. Un administrador selecciona usuario y aplicación.
2. Se crea o activa la membresía.
3. Se asignan roles permitidos.
4. Se registran los cambios de seguridad.

## 9.5. Asignación de roles

1. Se identifica usuario.
2. Se identifica aplicación.
3. Se verifica membresía activa.
4. Se selecciona un rol de esa misma aplicación.
5. Se crea la asignación.
6. Se registra el evento.

## 9.6. Evaluación de autorización

La aplicación consumidora debe poder determinar identidad, aplicación, roles, permisos relevantes y estado de sesión cuando la estrategia elegida lo requiera.

La autorización específica del negocio se realiza en la API consumidora.

---

# 10. Stack tecnológico

- ASP.NET Core
- .NET 10
- C#
- Entity Framework Core
- ASP.NET Core Identity
- PostgreSQL 17

La persistencia principal utilizará PostgreSQL 17 y las migraciones se administrarán mediante EF Core Migrations.

Si el servicio requiere subida de archivos, se almacenarán localmente y deberán accederse mediante una abstracción de almacenamiento.

---

# 11. Metodología SDD

El desarrollo utilizará GitHub Spec-Kit.

```text
constitution
    ↓
specify
    ↓
clarify
    ↓
plan
    ↓
tasks
    ↓
analyze
    ↓
implement
```

Las especificaciones deben ser la fuente de verdad de cada feature. Las decisiones globales deben residir en la constitución y documentación arquitectónica.

---

# 12. Arquitectura

## 12.1. Arquitectura hexagonal

La solución utilizará Ports and Adapters.

```text
               ┌──────────────────────┐
               │      API / HTTP      │
               └──────────┬───────────┘
                          │
                          ▼
               ┌──────────────────────┐
               │     Application      │
               └──────────┬───────────┘
                          │
                          ▼
               ┌──────────────────────┐
               │        Domain        │
               └──────────┬───────────┘
                          │
                          ▼
               ┌──────────────────────┐
               │        Ports         │
               └──────────┬───────────┘
                          │
                          ▼
               ┌──────────────────────┐
               │    Infrastructure    │
               │ Identity / EF / FS   │
               └──────────────────────┘
```

## 12.2. Dominio agnóstico

El dominio no debe depender de ASP.NET Core, ASP.NET Core Identity, Entity Framework Core, PostgreSQL, JWT, filesystem, DTOs HTTP ni controladores.

## 12.3. Identity como infraestructura

No modelar:

```text
Domain.User : IdentityUser
```

Preferir:

```text
Domain / Application
        ↓
Identity Port
        ↓
Infrastructure Adapter
        ↓
ASP.NET Core Identity
```

## 12.4. Vertical Slices

```text
Features/
├── Authentication/
│   ├── Login/
│   ├── Logout/
│   └── Refresh/
├── Users/
│   ├── Create/
│   ├── Disable/
│   └── GetProfile/
├── Applications/
├── Roles/
├── Permissions/
├── Memberships/
└── PasswordRecovery/
```

---

# 13. Principios de diseño

## SOLID
Se utilizan como guía de diseño, sin convertirlos en una excusa para abstraer de más.

## YAGNI
No implementar capacidades que no tengan un requisito actual.

## KISS
Preferir soluciones simples, explícitas y mantenibles.

## Pragmatismo
Evaluar necesidad real, claridad, mantenimiento, coste, seguridad e impacto arquitectónico.

---

# 14. Estrategia de pruebas

No se busca una suite extensa. Se implementarán las pruebas esenciales.

Prioridades:

- login;
- usuario inactivo;
- aplicación inactiva;
- membresía inválida;
- asignaciones de roles;
- permisos efectivos;
- aislamiento entre aplicaciones;
- expiración y revocación de sesiones;
- recuperación de contraseña;
- rate limiting crítico;
- invariantes del dominio;
- seguridad de subida de archivos si existe.

No se establecen porcentajes artificiales de cobertura.

---

# 15. Convenciones de C#

## 15.1. Un tipo por archivo

Cada tipo top-level debe vivir en su propio archivo.

Aplica a class, record, interface, enum, struct y delegate.

## 15.2. Nombre de archivo

Convención:

```text
<nombre>.<tipo>.cs
```

Ejemplos:

```text
user.entity.cs
session.entity.cs
userStatus.enum.cs
loginRequest.dto.cs
loginResponse.dto.cs
identityService.interface.cs
login.handler.cs
login.command.cs
getProfile.query.cs
```

## 15.3. Convenciones .NET

Se seguirán las convenciones recomendadas por el equipo de .NET para naming, async/await, CancellationToken, nullable reference types, exceptions, logging, configuration, dependency injection, disposal de recursos y APIs HTTP.

---

# 16. Inyección de dependencias

Se utilizará el contenedor de DI de ASP.NET Core salvo necesidad concreta.

`Program.cs` no debe convertirse en una lista extensa de registros.

Preferir:

```csharp
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddIdentityModule(builder.Configuration)
    .AddApiServices();
```

---

# 17. Seguridad

## 17.1. Rate limiting

Debe aplicarse especialmente sobre login, recuperación de contraseña, restablecimiento, confirmación de email, reenvíos, endpoints anónimos y operaciones costosas.

## 17.2. Bloqueo

Deben aprovecharse mecanismos de lockout de Identity cuando corresponda.

## 17.3. Validación de entrada

Toda entrada externa se considera no confiable.

Se debe validar formato, longitud, límites y normalización, y rechazar datos inválidos.

## 17.4. Límites de request

Definir límites para body, strings, colecciones, paginación, archivos, cantidad de archivos y metadata.

---

# 18. Subida de archivos

Si se habilitan archivos:

- no confiar únicamente en extensión, nombre o Content-Type;
- validar firma o magic bytes cuando corresponda;
- usar allow-list de tipos;
- impedir path traversal;
- normalizar nombres;
- generar nombres internos seguros;
- controlar archivos huérfanos;
- almacenar fuera de ubicaciones ejecutables.

---

# 19. Logging y auditoría

El logging debe ser estructurado.

No registrar contraseñas, tokens completos, secretos, credenciales ni datos sensibles innecesarios.

Los eventos de seguridad relevantes deben auditarse.

---

# 20. Configuración y secretos

Los secretos no deben residir en código, repositorio, archivos versionados ni imágenes de contenedor.

---

# 21. Migraciones

Los cambios de esquema deben gestionarse mediante EF Core Migrations.

Las migraciones deben ser versionadas, revisables, reproducibles y trazables.

---

# 22. Reglas para agentes de IA

Estas reglas son obligatorias.

## AR-001 — Sin forks en background
No lanzar forks de agentes en background.

## AR-002 — Sin paralelización implícita
No ejecutar agentes en paralelo de forma predeterminada.

## AR-003 — Paralelización solo con necesidad explícita
Solo se permitirá cuando exista necesidad concreta, contexto suficiente, aislamiento claro, ausencia de trabajo superpuesto y justificación del coste.

## AR-004 — Control de tokens
No generar consumo adicional mediante agentes duplicados, análisis repetidos, implementaciones paralelas o forks innecesarios.

## AR-005 — Contexto obligatorio
Cualquier agente delegado debe recibir constitution, spec, plan, tasks, reglas de dominio, arquitectura y estado actual cuando corresponda.

## AR-006 — Evitar duplicación
Antes de implementar, revisar el estado existente.

## AR-007 — Spec-Kit como fuente de verdad
Los artefactos de Spec-Kit gobiernan la implementación. El agente no puede modificar unilateralmente requisitos para simplificar el trabajo.

---

# 23. Criterio para nuevas dependencias

No incorporar una librería o tecnología sin necesidad concreta.

Evaluar necesidad, mantenimiento, seguridad, soporte, complejidad, compatibilidad y alternativas nativas de .NET.

Aplica especialmente a MediatR, AutoMapper, FluentValidation, buses, brokers, caches, storage cloud, librerías de resultados y frameworks adicionales de autenticación.

---

# 24. Decisiones todavía abiertas

Este documento no fija todavía:

- formato concreto de access token;
- JWT vs alternativa;
- refresh tokens;
- duración de sesiones;
- rotación de tokens;
- revocación inmediata;
- proveedor de email;
- confirmación de email;
- estrategia de MFA;
- OAuth 2.0 / OpenID Connect;
- login social;
- passkeys;
- política exacta de contraseña;
- duración de lockout;
- infraestructura de despliegue;
- reverse proxy;
- Docker;
- observabilidad avanzada.

Estas decisiones deberán introducirse mediante especificaciones futuras cuando exista una necesidad concreta.

---

# 25. Resumen normativo

```text
Metodología
├── SDD
└── GitHub Spec-Kit

Stack
├── ASP.NET Core
├── .NET 10
├── C#
├── EF Core
├── ASP.NET Core Identity
└── PostgreSQL 17

Dominio
├── User
├── UserProfile
├── Application
├── ApplicationMembership
├── Role
├── Permission
├── RolePermission
├── UserRole
├── Session
└── SecurityEvent

Arquitectura
├── Hexagonal
├── Vertical Slices
├── dominio agnóstico
└── Identity como infraestructura

Diseño
├── SOLID
├── YAGNI
├── KISS
└── pragmatismo

Testing
└── pruebas esenciales

C#
├── un tipo top-level por archivo
├── nombre de archivo indica tipo
├── convenciones .NET
└── DI agrupada

Seguridad
├── rate limiting
├── lockout
├── validación
├── límites
├── sanitización contextual
├── uploads seguros
└── auditoría

Agentes
├── sin forks en background
├── sin paralelización implícita
├── contexto suficiente
├── control de tokens
└── Spec-Kit como fuente de verdad
```

Este documento constituye la base funcional y técnica de la API genérica de autenticación y autorización y debe utilizarse como referencia para elaborar la constitución y las futuras especificaciones de GitHub Spec-Kit.
