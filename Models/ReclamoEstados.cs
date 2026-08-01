namespace ReclamosMDP.API.Models
{
    public static class ReclamoEstados
    {
        public const string Recibido = "Recibido";
        public const string Revision = "En revisión";
        public const string Proceso = "En proceso";
        public const string Solucionado = "Solucionado";
        public const string Rechazado = "Rechazado";


        public static readonly string[] Todos =
        {
            Recibido,
            Revision,
            Proceso,
            Solucionado,
            Rechazado
        };
    }
}