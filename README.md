# Mar Del Plata Transparente

## Desarrollo

La aplicación requiere PostgreSQL y secretos de desarrollo para JWT y Google.

Después de actualizar el código:

```powershell
dotnet ef database update
dotnet run
```

Las obras se guardan en PostgreSQL la primera vez que se consulta cada año. Para
forzar una resincronización desde las fuentes municipales y volver a intentar la
geocodificación de tramos:

```text
GET /api/datos-publicos/obras/anio/2025?actualizar=true
```

La lectura normal (`/obras/anio/{anio}`) usa exclusivamente la copia persistida
una vez sincronizado el año, por lo que no ejecuta scraping ni geocodificación en
cada visita.

Las migraciones incluyen la restricción que impide que una persona apoye dos
veces el mismo reclamo.

## Verificación automática

```powershell
powershell -ExecutionPolicy Bypass -File tests/smoke.ps1
```

La prueba compila el proyecto, valida todos los archivos JavaScript, inicia un
servidor temporal y comprueba autenticación, cabeceras de seguridad, respuestas
404 de la API, límites de consultas y coordenadas de los reclamos.
