// ==========================================================================
//  Peticiones/UsuarioConRolesCrear.cs  ·  Proyecto Cátedras · Guía IA, Regla 1
// --------------------------------------------------------------------------
//  Body del POST /api/Usuario/con-roles: crear un usuario como MAESTRO-DETALLE.
//
//  La cuenta SE CREA, no se busca: el body trae los datos de la persona
//  (nombres, apellidos, correo...), los de la cuenta (usuario, clave) y la
//  lista de roles. Todo se guarda en UNA transacción (sp_guardar_usuario).
//
//   · Persona:  nombres, apellidos, al menos un correo (lo exige el CHECK
//               chk_asistente_correo; aquí se valida antes -> 400), celular.
//   · Cuenta:   usuario (4-40, minúsculas/dígitos/._-) y clave (mín. 8, texto
//               plano: el SERVICIO la hashea, ADR D23).
//   · Roles:    al menos UNO, sin repetir. El rol debe existir en el catálogo
//               'rol' (si no, la FK lo rechaza -> 500 y no se crea nada).
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>Body del POST /api/Usuario/con-roles.</summary>
public class UsuarioConRolesCrear
{
    // --- La persona ---------------------------------------------------------

    [Required(ErrorMessage = "El campo 'nombres' es obligatorio.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El campo 'nombres' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("nombres")]
    public string? Nombres { get; set; }

    [Required(ErrorMessage = "El campo 'apellidos' es obligatorio.")]
    [StringLength(100, MinimumLength = 1, ErrorMessage = "El campo 'apellidos' debe tener entre 1 y 100 caracteres.")]
    [JsonPropertyName("apellidos")]
    public string? Apellidos { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_institucional' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", ErrorMessage = "El campo 'correo_institucional' no tiene formato de correo.")]
    [JsonPropertyName("correo_institucional")]
    public string? CorreoInstitucional { get; set; }

    [StringLength(254, ErrorMessage = "El campo 'correo_personal' admite hasta 254 caracteres.")]
    [RegularExpression("^[^@\\s]+@[^@\\s]+\\.[^@\\s]+$", ErrorMessage = "El campo 'correo_personal' no tiene formato de correo.")]
    [JsonPropertyName("correo_personal")]
    public string? CorreoPersonal { get; set; }

    [StringLength(20, MinimumLength = 1, ErrorMessage = "El campo 'celular' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("celular")]
    public string? Celular { get; set; }

    // --- La cuenta ----------------------------------------------------------

    [Required(ErrorMessage = "El campo 'usuario' es obligatorio.")]
    [RegularExpression("^[a-z0-9._-]{4,40}$",
        ErrorMessage = "El campo 'usuario' debe tener de 4 a 40 caracteres: minúsculas, dígitos, punto, guion y guion bajo.")]
    [JsonPropertyName("usuario")]
    public string? Usuario { get; set; }

    [Required(ErrorMessage = "El campo 'clave' es obligatorio.")]
    [MinLength(8, ErrorMessage = "El campo 'clave' debe tener al menos 8 caracteres.")]
    [JsonPropertyName("clave")]
    public string? Clave { get; set; }

    /// <summary>¿Cuenta activa? Opcional (por defecto true).</summary>
    [JsonPropertyName("activo")]
    public bool? Activo { get; set; }

    /// <summary>true = en su primer ingreso debe cambiar la clave que le entregó el administrador (D54).</summary>
    [JsonPropertyName("debe_cambiar_clave")]
    public bool? DebeCambiarClave { get; set; }

    // --- El detalle: los roles ----------------------------------------------

    /// <summary>Roles que recibe la cuenta. Al menos uno (lo valida el servicio -> 400).</summary>
    [Required(ErrorMessage = "El campo 'roles' es obligatorio.")]
    [JsonPropertyName("roles")]
    public List<DetalleRolInicial>? Roles { get; set; }
}

/// <summary>Un rol que nace junto con su usuario. Sin 'fk_usuario' (lo fija el SP). Nace vigente.</summary>
public class DetalleRolInicial
{
    [Required(ErrorMessage = "El campo 'fk_rol' es obligatorio.")]
    [StringLength(20, MinimumLength = 1, ErrorMessage = "El campo 'fk_rol' debe tener entre 1 y 20 caracteres.")]
    [JsonPropertyName("fk_rol")]
    public string? FkRol { get; set; }

    /// <summary>Fecha de inicio "AAAA-MM-DD". Opcional (por defecto, hoy).</summary>
    [JsonPropertyName("fecha_ini")]
    public DateOnly? FechaIni { get; set; }
}
