// ==========================================================================
//  Excepciones/CodigoRecuperacionInvalidoExcepcion.cs  ·  Cátedras · Subversión v7.1
// --------------------------------------------------------------------------
//  El código de recuperación de clave no sirvió: no existe, ya se usó, venció,
//  es incorrecto o se agotaron los intentos. El servicio la lanza (no sabe de
//  HTTP) y el controlador la traduce a 400. 'Estado' es el que devolvió la BD
//  (NO_EXISTE, USADO, EXPIRADO, INCORRECTO, AGOTADO); sirve para el log y las
//  pruebas, NO para mostrárselo a la persona: el front muestra siempre el
//  mismo mensaje para no revelar qué cuentas existen (ADR D27).
// ==========================================================================

namespace ApiCatedras.Excepciones;

/// <summary>El código de recuperación no es válido. El controlador la convierte en HTTP 400.</summary>
public class CodigoRecuperacionInvalidoExcepcion : Exception
{
    public string Estado { get; }

    public CodigoRecuperacionInvalidoExcepcion(string estado)
        : base($"El código de recuperación no es válido ({estado}).")
    {
        Estado = estado;
    }
}
