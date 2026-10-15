// ==========================================================================
//  Servicios/IServicioSede.cs  ·  Proyecto Cátedras · Versión 1
// --------------------------------------------------------------------------
//  Contrato de la capa de NEGOCIO. El controlador depende de esta interfaz
//  (no de la clase concreta). El servicio:
//   · recibe las PETICIONES ya validadas en forma/tipo por el framework,
//   · aplica las reglas de negocio (límite > 0, body de PATCH no vacío),
//   · habla con IRepositorioSede (no sabe qué motor hay),
//   · comunica los problemas con excepciones (ArgumentException -> 400;
//     NoEncontradoExcepcion -> 404). NO conoce HTTP.
//
// --------------------------------------------------------------------------
//  PRINCIPIOS EN ESTE ARCHIVO:
//   · SOLID · I (Segregación de interfaces): contrato PEQUEÑO y cohesionado
//     (solo lo que un cliente de negocio de 'Sede' necesita). El controlador
//     no arrastra métodos que no usa.
//   · SOLID · D (Inversión de dependencias): esta interfaz es la
//     ABSTRACCIÓN de la que depende el controlador; la clase que la cumple
//     es un detalle intercambiable.
//   · SOLID · O (Abierto/Cerrado): se puede sustituir ServicioSede por otra
//     implementación (p. ej. con caché) sin tocar al controlador.
// ==========================================================================

namespace ApiCatedras.Servicios;

using ApiCatedras.Modelos;
using ApiCatedras.Peticiones;

/// <summary>Reglas de negocio del recurso 'Sede'.</summary>
public interface IServicioSede
{
    /// <summary>
    /// Lista sedes. 'limite' nulo -> 1000 por defecto; 'limite' &lt;= 0 ->
    /// ArgumentException (400). Devuelve los datos y el total incluido.
    /// </summary>
    Task<(List<Sede> Datos, int Total)> ListarAsync(int? limite, bool incluirInactivos = false);

    /// <summary>
    /// Devuelve la sede por código. Si no existe lanza NoEncontradoExcepcion
    /// (404).
    /// </summary>
    Task<Sede> ObtenerAsync(string idSede);

    /// <summary>Crea una sede a partir de la petición del POST.</summary>
    Task CrearAsync(SedeCrear peticion);

    /// <summary>
    /// Reemplaza por completo la sede (PUT). Devuelve filas afectadas; si es
    /// 0 lanza NoEncontradoExcepcion (404).
    /// </summary>
    Task<int> ReemplazarAsync(string idSede, SedeReemplazo peticion);

    /// <summary>
    /// Actualiza parcialmente la sede (PATCH). Body sin campos ->
    /// ArgumentException (400); filas afectadas 0 -> NoEncontradoExcepcion
    /// (404).
    /// </summary>
    Task<int> ActualizarAsync(string idSede, SedeActualizar peticion);

    /// <summary>
    /// Elimina la sede. Filas eliminadas 0 -> NoEncontradoExcepcion (404).
    /// </summary>
    Task<int> EliminarAsync(string idSede);
}
