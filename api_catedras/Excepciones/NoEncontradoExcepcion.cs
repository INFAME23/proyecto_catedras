// ==========================================================================
//  Excepciones/NoEncontradoExcepcion.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  El NEGOCIO comunica los problemas con EXCEPCIONES; el CONTROLADOR las
//  traduce a códigos HTTP (Artículo 3 de la constitución):
//
//     ArgumentException        -> 400  (regla de negocio: límite <= 0,
//                                        body vacío en PATCH)
//     NoEncontradoExcepcion    -> 404  (esta clase: el recurso no existe)
//     NpgsqlException y demás   -> 500  (error del motor)
//
//  Se usa 'ArgumentException' (que ya existe en .NET) para el 400 y esta
//  excepción propia para el 404. Así el servicio no sabe nada de HTTP:
//  solo lanza; el controlador decide el número.
// ==========================================================================

namespace ApiCatedras.Excepciones;

/// <summary>
/// Se lanza cuando se pide una sede (u otro recurso) que no existe.
/// El controlador la convierte en HTTP 404.
/// </summary>
public class NoEncontradoExcepcion : Exception
{
    // ': base(mensaje)' pasa el texto a la clase Exception, que lo guarda
    // en la propiedad .Message (la que lee el controlador para el 'detalle').
    public NoEncontradoExcepcion(string mensaje) : base(mensaje)
    {
    }
}
