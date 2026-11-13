// ==========================================================================
//  Peticiones/ValidacionGeo.cs  ·  Subversión v7.2 · RF-S11
// --------------------------------------------------------------------------
//  Regla de varios campos que una anotación sola no puede expresar: la
//  latitud y la longitud van JUNTAS o ninguna (una coordenada sola no ubica
//  nada; es la misma regla 'chk_sede_geo_par' de la base de datos).
//
//  Las peticiones de la sede la usan desde 'IValidatableObject.Validate':
//  ASP.NET la ejecuta junto con las anotaciones ([Range], [Required]...) y,
//  si falla, el cliente recibe 422 con el campo señalado (formato de Program.cs)
//  y la base de datos no se toca.
// ==========================================================================

namespace ApiCatedras.Peticiones;

using System.ComponentModel.DataAnnotations;

/// <summary>Validación compartida de las coordenadas de una sede.</summary>
public static class ValidacionGeo
{
    /// <summary>Devuelve un error si llegó solo una de las dos coordenadas.</summary>
    public static IEnumerable<ValidationResult> LatitudYLongitudJuntas(decimal? latitud, decimal? longitud)
    {
        // 'yield return' entrega los errores de uno en uno; si no hay, la lista queda vacía = válido.
        if (latitud is not null && longitud is null)
            yield return new ValidationResult(
                "La longitud es obligatoria cuando se envía la latitud (van juntas o ninguna).",
                new[] { nameof(SedeCrear.Longitud) });

        if (longitud is not null && latitud is null)
            yield return new ValidationResult(
                "La latitud es obligatoria cuando se envía la longitud (van juntas o ninguna).",
                new[] { nameof(SedeCrear.Latitud) });
    }
}
