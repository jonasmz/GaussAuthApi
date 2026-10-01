# Roadmap de Features — API Genérica de Autenticación y Autorización

## 1. Propósito

Este documento define el roadmap funcional y técnico de alto nivel para la construcción incremental de una API genérica y reutilizable de autenticación y autorización.

El roadmap organiza el trabajo en features independientes y secuenciales que posteriormente deberán desarrollarse mediante GitHub Spec-Kit usando el flujo:

```text
constitution
→ specify
→ clarify
→ plan
→ tasks
→ analyze
→ implement
```

Cada feature de este roadmap debe convertirse en una especificación independiente.

Este documento no reemplaza `spec.md`, `plan.md` ni `tasks.md`. Su función es definir el orden, alcance y dependencias generales de las capacidades que compondrán el producto.

---

# 2. Principios del roadmap

El roadmap debe respetar las siguientes reglas:

- Las features se organizan por capacidad funcional, no por capas técnicas.
- Cada feature debe producir una capacidad útil y verificable.
- Las dependencias deben mantenerse explícitas.
- Las features deben ser lo suficientemente acotadas para evitar implementaciones excesivamente grandes.
- Las decisiones aún abiertas en la constitución no deben fijarse prematuramente.
- No deben agregarse capacidades especulativas por YAGNI.
- No deben ejecutarse agentes en paralelo por defecto.
- El orden de implementación debe reducir retrabajo y dependencia circular.
- La seguridad debe introducirse desde las primeras features y endurecerse progresivamente.

---

# 3. Vista general

```text
001-foundation
    ↓
002-users-profiles
    ↓
003-applications-memberships
    ↓
004-roles-permissions
    ↓
005-authentication-login
    ↓
006-sessions-access
    ↓
007-password-management
    ↓
008-authorization-contract
    ↓
009-security-audit
    ↓
011-admin-operations
    ↓
012-hardening-release

010-file-profile-support
    └── opcional, después de 002
```

---

# 4. Feature 001 — Foundation

## Identificador

```text
001-foundation
```

## Objetivo

Establecer la base técnica, estructural y arquitectónica del proyecto sobre la cual se desarrollarán todas las features posteriores.

## Alcance

Debe incluir:

- creación de la solución .NET 10;
- estructura de proyectos;
- arquitectura hexagonal;
- organización por vertical slices;
- proyectos o módulos de Domain, Application, Infrastructure y API;
- configuración de ASP.NET Core Web API;
- Entity Framework Core;
- PostgreSQL 17;
- configuración inicial de ASP.NET Core Identity como infraestructura;
- composición de dependencias;
- configuración base;
- manejo inicial de errores;
- logging estructurado;
- nullable reference types;
- convenciones de proyecto;
- migración inicial de infraestructura si corresponde;
- validación de conectividad con PostgreSQL.

## Fuera de alcance

No debe incluir:

- login funcional;
- emisión de tokens;
- roles de negocio;
- permisos;
- recuperación de contraseña;
- sesiones funcionales;
- operaciones administrativas;
- archivos de perfil.

## Dependencias

Ninguna.

## Criterios de finalización

La feature se considera completa cuando:

- la solución compila;
- la API inicia correctamente;
- existe conexión funcional a PostgreSQL 17;
- EF Core puede ejecutar una migración básica;
- ASP.NET Core Identity está integrado únicamente en Infrastructure;
- Domain no depende de ASP.NET Core, EF Core, Identity ni PostgreSQL;
- Program.cs no contiene registros DI desorganizados;
- la solución respeta un tipo top-level por archivo;
- existe una prueba mínima que valide la correcta composición o inicialización cuando corresponda.

## Capacidad disponible al finalizar

Existe una plataforma backend ejecutable y preparada para implementar features reales sin comprometer la arquitectura definida.

---

# 5. Feature 002 — Users and Profiles

## Identificador

```text
002-users-profiles
```

## Objetivo

Implementar el concepto de usuario global y su perfil reutilizable.

## Alcance

Debe incluir:

- creación de usuario;
- consulta de usuario;
- activación e inactivación;
- perfil global;
- nombre;
- apellido;
- display name;
- teléfono opcional;
- avatar reference opcional, sin carga de archivo todavía;
- timestamps;
- email como identificador de login;
- normalización del email;
- unicidad de email;
- integración con Identity sin contaminar el dominio.

## Fuera de alcance

No incluye:

- login;
- aplicaciones;
- memberships;
- roles;
- permisos;
- sesiones;
- password reset;
- archivos físicos.

## Dependencias

- 001-foundation

## Criterios de finalización

- Se puede crear un usuario válido.
- No pueden existir identidades duplicadas según la regla de email.
- Un usuario puede activarse o desactivarse.
- Existe perfil global separado de los datos internos de Identity.
- Domain permanece independiente de Identity.
- Persistencia y mapping están implementados.
- Existen pruebas esenciales de invariantes de usuario.

## Capacidad disponible al finalizar

El servicio puede almacenar y administrar identidades globales y perfiles, aunque todavía no puede autenticarlas.

---

# 6. Feature 003 — Applications and Memberships

## Identificador

```text
003-applications-memberships
```

## Objetivo

Permitir que la API funcione como servicio genérico para múltiples aplicaciones consumidoras.

## Alcance

Debe incluir:

- entidad Application;
- código único de aplicación;
- nombre;
- estado activo/inactivo;
- ApplicationMembership;
- asociación usuario-aplicación;
- activación e inactivación de memberships;
- validación de pertenencia activa;
- aislamiento lógico entre aplicaciones.

## Fuera de alcance

No incluye:

- roles;
- permisos;
- login completo;
- tokens;
- sesiones.

## Dependencias

- 002-users-profiles

## Criterios de finalización

- Se puede registrar una aplicación.
- El código de aplicación es único.
- Se puede asociar un usuario a una aplicación.
- Una membership puede activarse o desactivarse.
- Un usuario puede pertenecer a múltiples aplicaciones.
- Una aplicación inactiva no puede considerarse válida para nuevas operaciones de acceso.
- Existen pruebas esenciales de aislamiento y membresía.

## Capacidad disponible al finalizar

El servicio ya puede distinguir identidades globales de su participación en aplicaciones concretas.

---

# 7. Feature 004 — Roles and Permissions

## Identificador

```text
004-roles-permissions
```

## Objetivo

Implementar el modelo de autorización reusable basado en roles y permisos contextualizados por aplicación.

## Alcance

Debe incluir:

- Role;
- Permission;
- RolePermission;
- UserRole;
- roles por aplicación;
- permisos por aplicación;
- asignación de permisos a roles;
- asignación de roles a usuarios;
- validación de coherencia de aplicación;
- resolución de permisos efectivos.

## Fuera de alcance

No incluye:

- login;
- emisión de credenciales;
- sesiones;
- enforcement HTTP definitivo en consumidores.

## Dependencias

- 003-applications-memberships

## Criterios de finalización

- Se pueden crear roles dentro de una aplicación.
- Se pueden crear permisos dentro de una aplicación.
- Se pueden asociar permisos a roles.
- Se pueden asignar roles a usuarios con membership válida.
- No se puede asignar un rol de una aplicación a un usuario fuera de ese contexto.
- Los permisos efectivos pueden calcularse correctamente.
- Roles de aplicaciones distintas permanecen aislados.

## Capacidad disponible al finalizar

El servicio dispone de un modelo completo de autorización contextual aunque todavía no exista autenticación operativa.

---

# 8. Feature 005 — Authentication Login

## Identificador

```text
005-authentication-login
```

## Objetivo

Implementar el inicio de sesión por correo electrónico dentro del contexto de una aplicación.

## Alcance

Debe incluir:

- login por email;
- contraseña;
- selección o identificación de aplicación;
- validación de usuario activo;
- validación de aplicación activa;
- validación de membership activa;
- validación de credenciales con ASP.NET Core Identity;
- lockout donde corresponda;
- rate limiting del login;
- respuesta uniforme ante credenciales inválidas;
- registro mínimo de intentos relevantes de seguridad.

## Fuera de alcance

No incluye todavía:

- refresh tokens;
- estrategia avanzada de sesiones;
- OAuth 2.0;
- OpenID Connect;
- login social;
- MFA.

## Dependencias

- 002-users-profiles
- 003-applications-memberships

## Criterios de finalización

- Un usuario válido puede autenticarse por email.
- Un usuario inactivo no puede autenticarse.
- Una aplicación inactiva no permite login.
- Una membership inactiva impide login en esa aplicación.
- Credenciales inválidas no filtran información sensible.
- El endpoint tiene rate limiting.
- Lockout funciona según política configurable.
- Se registran eventos mínimos de login exitoso y fallido.

## Capacidad disponible al finalizar

El sistema puede autenticar usuarios correctamente dentro de una aplicación, pero todavía no dispone de un ciclo completo de sesión reusable.

---

# 9. Feature 006 — Sessions and Access

## Identificador

```text
006-sessions-access
```

## Objetivo

Incorporar el ciclo de vida de sesiones y el mecanismo de acceso entre Auth API y APIs consumidoras.

## Alcance

Debe incluir:

- entidad Session;
- creación de sesión;
- expiración;
- revocación;
- identificación de usuario;
- identificación de aplicación;
- emisión de credencial de acceso;
- validación de credencial;
- propagación de roles/permisos relevantes;
- logout mediante revocación.

La estrategia concreta de token debe definirse durante `clarify` / `plan` de esta feature.

## Fuera de alcance

No debe asumir de antemano:

- JWT;
- refresh token;
- rotación;
- OIDC;
- OAuth completo.

## Dependencias

- 005-authentication-login
- 004-roles-permissions

## Criterios de finalización

- Un login exitoso crea una sesión.
- Toda sesión identifica usuario y aplicación.
- Las sesiones expiran.
- Las sesiones pueden revocarse.
- Una sesión revocada no autoriza nuevas operaciones.
- Logout invalida la sesión correspondiente.
- La credencial emitida permite identificar el contexto autorizado.
- Roles/permisos pueden propagarse de forma segura conforme al diseño elegido.

## Capacidad disponible al finalizar

Una aplicación externa ya puede autenticar al usuario mediante Auth API y recibir una credencial reutilizable para acceder a servicios protegidos.

---

# 10. Feature 007 — Password Management

## Identificador

```text
007-password-management
```

## Objetivo

Completar el ciclo de vida de credenciales de contraseña.

## Alcance

Debe incluir:

- cambio de contraseña autenticado;
- solicitud de recuperación;
- reset de contraseña;
- tokens o mecanismos temporales de recuperación;
- rate limiting;
- protección contra user enumeration;
- eventos de seguridad;
- política de invalidación de sesiones después de cambio/reset según especificación.

## Fuera de alcance

No incluye:

- MFA;
- passkeys;
- social login;
- OAuth externo.

## Dependencias

- 005-authentication-login
- 006-sessions-access

## Criterios de finalización

- Un usuario puede cambiar su contraseña.
- Se puede iniciar recuperación sin revelar existencia de cuentas.
- Un reset válido cambia la credencial.
- Un token de reset inválido o expirado no funciona.
- Rate limiting protege los endpoints públicos.
- Se registra auditoría mínima de cambios/reset.
- Se aplica la política definida sobre sesiones existentes.

## Capacidad disponible al finalizar

El servicio dispone de un ciclo de contraseña completo y utilizable en producción básica.

---

# 11. Feature 008 — Authorization Contract

## Identificador

```text
008-authorization-contract
```

## Objetivo

Definir y exponer el contrato estable mediante el cual las APIs consumidoras reciben o resuelven identidad, aplicación, roles y permisos.

## Alcance

Debe incluir:

- contrato de identidad autenticada;
- identificador estable de usuario;
- contexto de aplicación;
- roles;
- permisos;
- validación de credenciales;
- mecanismos necesarios para que Business APIs no accedan a la base de Auth;
- ejemplos o contratos de integración.

## Fuera de alcance

No incluye:

- reglas de negocio de aplicaciones consumidoras;
- autorización de reservas, pagos, inventarios, etc.;
- SDKs complejos salvo necesidad explícita.

## Dependencias

- 004-roles-permissions
- 006-sessions-access

## Criterios de finalización

- Una API externa puede identificar al usuario autenticado.
- Puede identificar la aplicación.
- Puede resolver roles y permisos relevantes.
- No necesita acceder a tablas de Auth.
- El contrato queda documentado y estable.
- Existe una prueba de integración mínima demostrando aislamiento entre Auth y consumidor.

## Capacidad disponible al finalizar

La API de autenticación ya puede utilizarse de manera real por otra API de negocio.

---

# 12. Feature 009 — Security and Audit

## Identificador

```text
009-security-audit
```

## Objetivo

Completar los controles transversales de seguridad y trazabilidad necesarios para una primera versión robusta.

## Alcance

Debe incluir:

- SecurityEvent;
- auditoría de eventos críticos;
- revisión de rate limiting;
- revisión de lockout;
- errores seguros;
- logging estructurado;
- protección de datos sensibles;
- límites de request;
- sanitización contextual;
- validación de entradas externas;
- revisión de configuración y secretos;
- revisión de endpoints anónimos.

## Fuera de alcance

No incluye:

- SIEM;
- observabilidad empresarial avanzada;
- retenciones regulatorias no especificadas;
- infraestructura cloud.

## Dependencias

- 005-authentication-login
- 006-sessions-access
- 007-password-management
- 008-authorization-contract

## Criterios de finalización

- Todos los endpoints públicos críticos tienen controles explícitos.
- No se registran contraseñas, secretos ni tokens completos.
- Los errores de producción no exponen internals.
- Existen límites de request razonables.
- Los eventos relevantes de seguridad son auditables.
- Se revisan las principales superficies de abuso.
- Las pruebas esenciales de seguridad pasan.

## Capacidad disponible al finalizar

La API dispone de controles de seguridad coherentes y trazabilidad suficiente para uso real controlado.

---

# 13. Feature 010 — File Profile Support (Opcional)

## Identificador

```text
010-file-profile-support
```

## Estado

Opcional.

No forma parte del MVP mientras no exista un requisito concreto de carga de archivos.

## Objetivo

Permitir almacenar archivos de perfil, por ejemplo avatar, de manera segura y local.

## Alcance

Si se activa esta feature:

- almacenamiento local;
- puerto de almacenamiento;
- adapter de filesystem;
- validación de tipos;
- magic bytes;
- allow-list;
- límite de tamaño;
- nombres físicos controlados por servidor;
- normalización;
- path traversal prevention;
- gestión de archivos huérfanos;
- reemplazo y eliminación.

## Dependencias

- 002-users-profiles

## Criterios de finalización

- Solo se aceptan tipos permitidos.
- La firma real del archivo se valida.
- No se confía en extensión ni MIME del cliente.
- El nombre físico lo controla el servidor.
- No hay ejecución de archivos subidos.
- Existe política de limpieza de archivos huérfanos.

## Capacidad disponible al finalizar

Los perfiles pueden contener archivos físicos locales administrados de forma segura.

---

# 14. Feature 011 — Administrative Operations

## Identificador

```text
011-admin-operations
```

## Objetivo

Exponer las operaciones necesarias para administrar el servicio de identidad sin manipulación directa de base de datos.

## Alcance

Debe incluir operaciones administrativas para:

- usuarios;
- perfiles;
- aplicaciones;
- memberships;
- roles;
- permisos;
- asignaciones de roles;
- estados activos/inactivos.

La propia API debe proteger estas operaciones mediante autorización adecuada.

## Fuera de alcance

No incluye:

- frontend administrativo;
- reporting complejo;
- gestión de negocio de aplicaciones consumidoras.

## Dependencias

- 002-users-profiles
- 003-applications-memberships
- 004-roles-permissions
- 006-sessions-access
- 008-authorization-contract
- 009-security-audit

## Criterios de finalización

- El servicio puede administrarse íntegramente mediante endpoints protegidos.
- Las operaciones administrativas requieren permisos explícitos.
- Las modificaciones relevantes generan auditoría.
- No es necesario modificar la base manualmente para operación normal.

## Capacidad disponible al finalizar

La Auth API puede operarse y mantenerse mediante sus propios contratos administrativos.

---

# 15. Feature 012 — Hardening and First Reusable Release

## Identificador

```text
012-hardening-release
```

## Objetivo

Consolidar el sistema para generar una primera versión reusable y estable.

## Alcance

Debe incluir:

- revisión completa contra constitution;
- revisión de todas las migraciones;
- revisión de contratos;
- pruebas esenciales end-to-end;
- pruebas de aislamiento entre aplicaciones;
- validación de seguridad;
- revisión de configuraciones;
- documentación mínima de despliegue;
- documentación de integración para consumidores;
- revisión de dependencias;
- eliminación de código muerto;
- revisión de TODOs y decisiones temporales;
- análisis final con Spec-Kit.

## Fuera de alcance

No incluye nuevas capacidades funcionales importantes.

## Dependencias

Todas las features obligatorias anteriores.

## Criterios de finalización

- `/speckit.analyze` no presenta violaciones críticas de constitución.
- Todas las pruebas esenciales pasan.
- La API compila y ejecuta sin errores.
- Las migraciones son reproducibles.
- Los contratos de integración están documentados.
- Una aplicación consumidora puede autenticarse y autorizarse usando el servicio.
- No existen dependencias tecnológicas no justificadas.
- No existen secretos en repositorio.
- La versión puede considerarse primera release reutilizable.

## Capacidad disponible al finalizar

Existe una API genérica de autenticación/autorización reusable por múltiples proyectos.

---

# 16. MVP recomendado

El MVP funcional recomendado incluye:

```text
001-foundation
002-users-profiles
003-applications-memberships
004-roles-permissions
005-authentication-login
006-sessions-access
007-password-management
008-authorization-contract
009-security-audit
012-hardening-release
```

`010-file-profile-support` queda fuera salvo necesidad real.

`011-admin-operations` puede incluirse antes de la primera release si se requiere administración completa por API.

---

# 17. Hitos funcionales

## Hito A — Base técnica

Después de:

```text
001
```

Resultado:

- arquitectura lista;
- stack operativo;
- DB funcional.

---

## Hito B — Modelo de identidad multi-aplicación

Después de:

```text
002
003
004
```

Resultado:

- usuarios;
- perfiles;
- aplicaciones;
- memberships;
- roles;
- permisos.

Todavía no existe login completo.

---

## Hito C — Autenticación operativa

Después de:

```text
005
006
```

Resultado:

- login;
- sesión;
- credencial de acceso;
- logout;
- expiración;
- revocación.

---

## Hito D — Servicio reusable

Después de:

```text
007
008
```

Resultado:

- recuperación de contraseña;
- contrato de autorización;
- integración real con APIs consumidoras.

---

## Hito E — Seguridad y administración

Después de:

```text
009
011
```

Resultado:

- auditoría;
- hardening;
- administración completa.

---

## Hito F — Release

Después de:

```text
012
```

Resultado:

- primera versión reusable del servicio.

---

# 18. Reglas para transformar el roadmap en specs

Cada feature debe desarrollarse mediante un ciclo independiente de Spec-Kit.

Ejemplo:

```text
/speckit.specify 001-foundation
/speckit.clarify
/speckit.plan
/speckit.tasks
/speckit.analyze
/speckit.implement
```

Después de completar y validar una feature se continúa con la siguiente.

No deben combinarse varias features del roadmap dentro de una misma especificación salvo que una revisión explícita demuestre que la separación no aporta valor.

Las decisiones abiertas deben resolverse únicamente cuando la feature correspondiente las necesite.

Ejemplo:

La estrategia concreta de tokens NO debe decidirse durante 001-foundation.

Debe resolverse durante:

```text
006-sessions-access
```

---

# 19. Decisiones que deben permanecer fuera del roadmap por ahora

No deben convertirse todavía en features salvo requisito explícito:

- OAuth 2.0 server;
- OpenID Connect provider;
- MFA;
- social login;
- passkeys;
- SSO;
- federación;
- LDAP;
- Active Directory;
- almacenamiento cloud;
- distributed cache;
- message broker;
- event sourcing;
- API Gateway;
- multi-region;
- Kubernetes.

Estas capacidades pueden incorporarse en futuras versiones si aparece una necesidad concreta.

---

# 20. Orden recomendado de ejecución

```text
001-foundation
002-users-profiles
003-applications-memberships
004-roles-permissions
005-authentication-login
006-sessions-access
007-password-management
008-authorization-contract
009-security-audit
011-admin-operations
012-hardening-release
```

Feature opcional:

```text
010-file-profile-support
```

Puede introducirse después de 002 cuando exista una necesidad real.

---

# 21. Criterio para modificar el roadmap

El roadmap puede evolucionar con mayor frecuencia que la constitución.

Se puede:

- dividir una feature demasiado grande;
- mover una feature opcional;
- agregar una nueva capacidad;
- modificar dependencias;
- posponer elementos.

No debe modificarse para introducir silenciosamente decisiones que contradigan la constitución.

Cuando una nueva necesidad afecte reglas permanentes del proyecto, primero debe evaluarse si requiere una enmienda de la constitución.

---

# 22. Resultado esperado

Al completar este roadmap, el sistema debe proporcionar un servicio de identidad independiente capaz de:

- administrar usuarios globales;
- administrar perfiles;
- registrar múltiples aplicaciones;
- asociar usuarios a aplicaciones;
- administrar roles y permisos por aplicación;
- autenticar mediante email;
- gestionar sesiones;
- revocar acceso;
- administrar contraseñas;
- exponer contratos reutilizables de autorización;
- registrar eventos de seguridad;
- ser consumido por múltiples APIs de negocio sin compartir su base de datos;
- evolucionar sin introducir dependencias de dominios específicos.
