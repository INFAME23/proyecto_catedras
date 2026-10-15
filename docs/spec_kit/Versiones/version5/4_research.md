# Decisiones de diseño — Versión 5: usuarios, login y registro de asistencia

> ✅ **Revisada y construida (2026-10-11)** — ver nota de [2_spec.md](2_spec.md). La versión con
> más decisiones nuevas del proyecto — 8 ADR (D22-D29), frente a 1-4
> por versión anterior.

## D22 — `EnlaceRegistro` no tiene `DELETE`, aunque sí tiene `id` (distinto de D16 y D19)

**Contexto.** A diferencia de `VinculacionAsistente` (D16, v3) y
`Ponencia` (D19, v4), `EnlaceRegistro` **sí** tiene una columna `id`
normal (`id_enlace`, identity). La pregunta no es "cómo se expresa la
llave" (ya está resuelta: es un `id` de toda la vida), sino si, además
de "emitir", debe existir un `DELETE /api/EnlaceRegistro/{id}`.

**Opciones evaluadas:** (a) `DELETE` normal, que haría `CASCADE` hacia
`clave_acceso` y `registro_asistencia` de ese enlace; (b) solo
"revocar" (`PATCH .../revocar`, pone `revocado_en`), sin `DELETE`.

**Decisión: (b).** Un enlace revocado es **evidencia** de que, a
partir de cierto momento, dejó de ser válido — exactamente la misma
lógica que ya cerró `ConsentimientoDatos` (v3) y `LoteCargaAsistente`
(v3) a solo-anexo. Borrarlo de verdad (a) destruiría, en cascada, los
`registro_asistencia` reales que alguien generó usándolo — una
auditoría no puede perder la prueba de cómo se registró una asistencia
real solo porque el enlace que la hizo posible ya caducó. La diferencia
con D16/D19 es que aquí la razón para no tener `DELETE` **no** es "no
hay a qué apuntar sin un `id`" (sí lo hay) ni "no hay vigencia que
proteger" (si la hay, en `revocado_en`) — es, llanamente, que esta
fila es un hecho auditable, el mismo criterio que ya se usó para
`ConsentimientoDatos`, aplicado a una tabla que por coincidencia tiene
`id`.

**Consecuencias.** (+) Ningún registro de asistencia real pierde su
enlace de origen por una limpieza administrativa. (+) Deja un tercer
ejemplo, junto a D16/D19, de que "tiene o no tiene `id`" y "tiene o no
tiene `DELETE`" son dos preguntas independientes — cada tabla del
proyecto responde ambas por su propia razón de negocio, no por copiar
la decisión más parecida en forma. (−) Un enlace emitido por error
(p. ej. a la sesión equivocada) queda ahí para siempre, solo revocado
— aceptado: revocarlo tiene el mismo efecto práctico (deja de servir
para pedir claves nuevas) y es más seguro que permitir borrar
evidencia.

## D23 — BCrypt solo en el servicio, Modelo con `[JsonIgnore]` (Guía IA, Regla 3)

Ver desarrollo completo en §4.1-4.2 de [3_plan.md](3_plan.md). Resumen:
la Guía IA exige que el hash de una clave de usuario viva y se verifique
SOLO en la capa de servicio — ni el controlador ni el repositorio deben
tocar la clave en texto plano más que de paso. Se decidió además que
`ClaveHash` lleve `[JsonIgnore]` en el Modelo en vez de armar un DTO de
respuesta aparte (`UsuarioRespuesta`): es la primera vez que el
proyecto necesita que un campo de un Modelo NUNCA salga en una
respuesta HTTP, y una anotación puntual es menos cambio estructural
que una familia nueva de DTOs de solo-lectura para una sola entidad.
**Consecuencia aceptada:** si una versión futura necesitara, por la
razón que fuera, devolver alguna vez ese campo, tendría que quitar la
anotación en vez de simplemente usar otro DTO — juzgado poco probable
(ningún cliente de esta API debería necesitar nunca el hash de una
clave).

## D24 — `EnlaceRegistro` y `ClaveAcceso` no son CRUD: son verbos de negocio sobre funciones que ya existen

**Contexto.** A diferencia de toda tabla anterior del proyecto, estas
dos no se crean con un `INSERT` que la API componga: el **cómo** ya
está resuelto desde v1 en `sp_emitir_enlace`/`fn_emitir_enlace_api`,
`sp_solicitar_clave`/`fn_solicitar_clave_api` y
`sp_validar_clave_y_registrar`/`fn_validar_clave_api` — funciones que
calculan tokens, ventanas de tiempo, canal, claves aleatorias, hashes
con sal y validan hasta 6 reglas de negocio numeradas cada una.

**Opciones evaluadas:** (a) tratar estas tablas como CRUD de todos
modos — un `POST /api/EnlaceRegistro` que arme el `INSERT` él mismo,
reimplementando en C# lo que el SP ya hace; (b) exponer un verbo de
negocio por función (`emitir`, `revocar`, `solicitar`, `validar`), que
simplemente invoque la función SQL y traduzca su resultado.

**Decisión: (b).** (a) significaría reimplementar en C#, con más
riesgo de errores, reglas que la BD ya resuelve de forma probada
(cálculo de ventana con los parámetros de `parametro`, revocación
automática del enlace anterior, derivación de `canal` según la
modalidad, hash con sal, conteo de intentos) — exactamente el tipo de
duplicación que D9 (desde v2) y D20 (v4) ya vienen evitando, llevado
aquí a su caso más extremo: toda una regla de negocio de seguridad,
no solo una restricción de integridad. El "verbo HTTP que corresponde"
ya no es un sustituto natural de "la operación de negocio que ocurre"
— por eso las rutas se llaman `/emitir`, `/revocar`, `/solicitar`,
`/validar` en vez de forzarlas sobre `POST`/`PUT`/`PATCH` genéricos.

**Consecuencias.** (+) Cero reimplementación de lógica de seguridad ya
probada. (+) Las rutas documentan la intención de negocio mejor que un
CRUD genérico (mismo argumento que ya usó D16 para "cerrar" en vez de
un `PATCH` anónimo, llevado un paso más allá: aquí ni siquiera hay
"columnas que cambian", hay una operación completa). (−) Estas 2
entidades rompen, más que ninguna otra del proyecto, el molde de 5/6
verbos — aceptado porque la alternativa (forzar el molde) escondería,
no simplificaría, lo que realmente pasa.

## D25 — Las excepciones de negocio de la BD caen en el mismo 500 que cualquier otro error de PostgreSQL

Ver desarrollo completo en §4.4 de [3_plan.md](3_plan.md). Resumen de
las opciones: (a) agregar un mapeo nuevo de `SqlState` → código HTTP
(p. ej. `no_data_found` → 404) para que los mensajes de `RN-01`/`RN-15`
etc. se sientan más "correctos" por fuera; (b) dejar que caigan en el
mismo `catch (NpgsqlException ex)` que cualquier otro error de la BD,
igual que el resto del proyecto desde v1.

**Decisión: (b).** El proyecto nunca ha mapeado por `SqlState` — el
404 de este proyecto es, por definición desde v1, "el SERVICIO buscó
algo por `ObtenerPorIdAsync` y no lo encontró", nunca una traducción
de lo que la BD decide llamar internamente "no encontrado". Agregar un
mapeo nuevo solo para v5 crearía una inconsistencia real: el mismo
error de BD (`PostgresException`) respondería 404 en unos endpoints y
500 en otros, según de qué `SqlState` viniera — una regla más difícil
de explicar y de mantener que "cualquier error de la BD es 500,
siempre". **Consecuencia aceptada:** el mensaje de error que ve el
cliente en estos casos (`"RN-01: no hay ningún asistente..."`) es
técnicamente un 500 aunque describa, en español, un "no encontrado" —
se documenta aquí y en RF149-150 de [2_spec.md](2_spec.md)
explícitamente para que nadie lo lea como un bug al implementar.

## D26 — El único caso donde la API revalida algo que el `CHECK` de la BD ya permitiría

Ver desarrollo completo en §4.5 de [3_plan.md](3_plan.md). Resumen:
`chk_registro_clave` acepta `origen IN ('MANUAL','IMPORTADO')` SIN
exigir `fk_clave`, pero por construcción lógica (`OR`) también
"permitiría" un `origen: 'QR'` sin `fk_clave` si nadie más lo evita —
el `CHECK` fue escrito para garantizar que un registro NO manual
siempre tenga su clave, no para prohibir que un registro se autodeclare
"QR" sin una. El servicio cierra ese hueco de forma declarativa (lista
cerrada de 2 valores permitidos en el `POST` directo) porque dejarlo
pasar produciría una mentira de auditoría silenciosa — distinto de los
casos D9 (FK/CHECK que SÍ reflejan fielmente la regla completa, y por
eso no hace falta repetirlos en C#).

## D27 — El login responde `401`, el primer código nuevo del proyecto desde v1

**Contexto.** Hasta v4, el proyecto solo usa `200`/`204`/`400`/`404`/
`422`/`500`. Un intento de login con clave incorrecta o usuario
inactivo no es ninguno de esos: no es un error de formato (422), no es
una regla de negocio violada sobre datos que de otro modo se habrían
guardado (400), no es "ese usuario no existe como recurso" en el
sentido de un `GET /api/Usuario/{id}` (404 sería engañoso: el usuario
sí existe, solo que la clave no coincide, y revelar esa diferencia por
el código HTTP sería una fuga de información), y ciertamente no es un
error de la BD (500).

**Opciones evaluadas:** (a) reusar 400 ("clave incorrecta" como si
fuera una validación de negocio más); (b) adoptar 401 (`Unauthorized`),
el código HTTP estándar para "credenciales inválidas", el mismo tanto
si el usuario no existe como si la clave no coincide o la cuenta está
inactiva (para no revelar CUÁL de las tres pasó).

**Decisión: (b).** 401 es semánticamente correcto y estándar — este es
exactamente el caso para el que existe. Reusar 400 habría sido fiel a
"no agregar nada nuevo" pero técnicamente incorrecto, y el propio
Artículo 8 de la constitución (contratos al pie de la letra) no exige
nunca quedarse corto cuando el HTTP estándar ya tiene la respuesta
correcta — solo exige no inventar convenciones propias cuando ya existe
una. **Consecuencia aceptada:** el sobre de error sigue siendo
`{estado, mensaje}` igual que los demás códigos (sin inventar una forma
nueva solo para 401), y el mensaje es genérico ("usuario o clave
incorrectos") sin distinguir las 3 causas posibles, precisamente para
no filtrar si un `usuario` existe o no.

## D28 — Por qué no hay autorización por rol dentro de la API

**Contexto.** v5 agrega roles (`rol_por_usuario`) y el login los
devuelve, pero ninguna spec pide que la API bloquee un endpoint según
el rol de quien lo llama.

**Opciones evaluadas:** (a) agregar un middleware de autorización
(`[Authorize(Roles = "ADMIN")]` o equivalente) que exija un token por
petición y verifique el rol antes de ejecutar cada acción; (b) la API
sigue respondiendo todos sus endpoints sin exigir sesión, y es el
FRONT (Flask) quien decide, con la sesión que ÉL guarda tras el login,
qué panel mostrarle a cada rol.

**Decisión: (b).** (a) exigiría un mecanismo de sesión por petición
(un JWT firmado, una cookie de sesión validada por la API, o similar)
que el Artículo 2 no permite agregar sin que una spec lo pida con una
razón real — y ninguna lo hace: el objetivo didáctico de v5 es mostrar
un login real con clave hasheada (BCrypt) y un flujo de auto-registro
con clave de un solo uso, no construir un sistema de autorización de
producción. La API sigue siendo, en ese sentido, tan "abierta" como
desde v1 — una limitación conocida del proyecto didáctico, no un
descuido de esta versión.

**Consecuencias.** (+) Cero paquetes nuevos de autenticación por
token (más allá de `BCrypt.Net-Next`, que es para hashear, no para
sesiones). (+) El alcance de v5 se mantiene enfocado en los 2 flujos
reales que pedía el mapa de versiones. (−) Cualquier cliente puede
llamar cualquier endpoint sin credenciales — documentado aquí y en
§2 de [2_spec.md](2_spec.md) ("No incluye") para que no se lea como una
vulnerabilidad pasada por alto, sino como un límite explícito del
proyecto.

## D29 — Dos mecanismos de autenticación distintos, no uno

**Contexto.** `usuario`+`clave_hash` (BCrypt) y
`enlace_registro`+`clave_acceso` (OTP por correo, sha256) podrían
parecer "la misma idea" (alguien prueba que es quien dice ser) y
tentar a resolverlos con un solo concepto ("toda persona tiene una
clave").

**Opciones evaluadas:** (a) un solo mecanismo — todo el mundo
(personal Y asistentes) usa `usuario`/clave BCrypt para todo,
incluida la asistencia; (b) dos mecanismos separados: `usuario` solo
para personal que administra el sistema (login clásico, de uso
repetido), `clave_acceso` solo para asistentes que se autorregistran
puntualmente en una sesión (OTP de un solo uso, sin cuenta que
mantener).

**Decisión: (b), ya implícita en el diseño de la BD, no una elección
libre de la API.** `usuario.fk_asistente` es `UNIQUE`
(`uq_usuario_persona`): la BD ya asume que la mayoría de los 14.811
asistentes **nunca** tendrá una cuenta `usuario` — solo el personal
administrativo la necesita. Pedirle una clave BCrypt permanente a
cada asistente que asiste a una sola cátedra abierta sería fricción
sin beneficio real (nadie vuelve a loguearse con ella); una clave de 6
dígitos que expira en minutos, enviada al correo que ya se tiene
registrado, resuelve exactamente el problema real ("demostrar que el
correo es suyo, una vez, para esta sesión") sin pedirle que recuerde
una contraseña más. La API solo refleja esa separación que el modelo
de datos ya decidió — documentarla aquí es para que una versión futura
no intente "unificar" los dos flujos pensando que es una simplificación
cuando en realidad resuelven problemas distintos.

**Consecuencias.** (+) Cada mecanismo tiene la fricción que su propio
caso de uso justifica (permanente y reutilizable para el personal;
desechable y sin registro previo de cuenta para el asistente). (−) El
proyecto tiene, desde v5, dos formas de "probar quién soy" en paralelo
— aceptado porque son, de hecho, dos problemas distintos con dos
dueños distintos (`usuario` vs `asistente`), no una sola idea resuelta
dos veces.

## Ajustes al borrador (2026-10-08, al revisar la spec contra la BD real y la v4 cerrada)

No son ADR nuevos (no cambian ninguna decisión D22-D29), pero explican por
qué esta spec difiere del borrador original:

1. **Numeración.** El borrador usaba D21-D28 y RNF11-RNF13, pero la v4 ya
   tiene D19-D21 y RNF10-RNF11. Todo ADR desde D21 y todo RNF desde RNF11
   subió una posición (v5: D22-D29, RNF12-RNF14; los borradores de v6 y v7
   se desplazaron igual para que sus referencias cruzadas sigan siendo
   correctas).
2. **Fechas.** Los Modelos usan `DateTime` UTC, no `DateTimeOffset` (ADR D21
   de v4): es lo que ya hacen todos los Modelos construidos.
3. **`inet`.** `ip` e `ip_solicitud` son `inet`; Npgsql las entrega como
   `IPAddress`, que Dapper no convierte a `string`. Se leen con `host(ip)`
   y las peticiones validan la IP en el servicio (400 si no es válida).
4. **Intentos de clave.** Leyendo `sp_validar_clave_y_registrar`: el
   `UPDATE intentos + 1` va seguido de `RAISE EXCEPTION`, y la excepción
   revierte la sentencia entera, así que el contador no se acumula y la
   clave no se anula tras 3 fallos (RF153). **Consecuencia aceptada:**
   dentro de sus 10 minutos de vida, una clave de 6 dígitos admite
   intentos sin límite. No se corrige porque (a) el Artículo 5 prohíbe
   tocar el script y (b) reimplementarlo en C# repetiría lo que D24
   evita. Se confirma en la Fase 44; si esa prueba mostrara que
   `intentos` sí sube, este punto y RF153 se revierten.
5. **Conteos de ejemplo.** `registro_asistencia` tiene **168** filas (167
   `clave_acceso`); el borrador decía 167 en dos sitios.
6. **Cuenta de ejemplo.** `admin.catedras` (id 1, asistente 14809, roles
   `ADMIN` y `CONSULTA` vigentes) trae un hash BCrypt de una clave que
   nadie conoce. No se toca la BD: el quickstart fija una clave nueva con
   `PATCH /api/Usuario/1` (RF136), paso que se repite tras cada
   `down -v`.
7. **Front.** El borrador hablaba de un «selector de rol de prueba» que en
   el código nunca existió (son placeholders). El autorregistro se
   construye sobre `routes/registro.py` (ya registrado y enlazado en el
   navbar) en vez de crear `autorregistro.py`, y `/admin` queda protegido
   por rol desde el front (D28 solo dice que la API no autoriza).
8. **Datos de ejemplo del quickstart verificados contra el script:**
   `1000090` (asistente 1, vinculación `ESTUDIANTE` vigente, programa
   `M0221` activo), asistente 2 (igual) y asistente 100 (sin `usuario`);
   modalidad `PRESENCIAL` → canal `QR`. Todos los enlaces de ejemplo
   vencieron en agosto de 2026: las pruebas de "solicitar"/"validar"
   necesitan una sesión nueva alrededor de la hora actual.
9. **`origen` del registro manual: 400, no 422.** El borrador ponía un
   `[RegularExpression]` en `RegistroAsistenciaCrear.Origen` *y* la
   validación del servicio; con la anotación, `origen: "QR"` habría dado
   422 y nunca habría llegado al servicio, contradiciendo RF156 y el
   criterio 6 (400). Queda solo la validación del servicio (D26).
10. **Nombres JSON en snake_case.** El borrador mostraba los bodies y las
    respuestas en camelCase (`fkAsistente`, `idClave`, `userAgent`), pero
    v1-v4 usan snake_case con `[JsonPropertyName]` (`fk_sesion`, `id_sesion`);
    con camelCase, un `POST` habría dado 422 por «campo obligatorio». Se
    alinearon `2_spec.md`, `6_contracts.md`, `7_quickstart.md` y la guía. No
    cambian los parámetros de query (`?fkSesion=`), los de ruta (`{idEnlace}`)
    ni `filasEliminadas`, que ya eran así en v1-v4.
11. **`usuario` y Dapper.** La columna `usuario` mapea a la propiedad
    `NombreUsuario`; Dapper no lo adivina, así que el SQL del repositorio
    usa `usuario AS nombre_usuario`.
12. **`ClaveHash` sin `required` (hallazgo de la Fase 43).** El borrador
    declaraba `public required string ClaveHash` con `[JsonIgnore]`;
    `System.Text.Json` (.NET 8+) lo rechaza al serializar
    (`InvalidOperationException … is marked required but does not specify a
    setter`), lo que habría dado 500 en toda respuesta con un `Usuario`. Se
    cambió a `public string ClaveHash { get; set; } = ""`. No cambia ninguna
    decisión (D23 sigue igual: el hash nunca se serializa).
