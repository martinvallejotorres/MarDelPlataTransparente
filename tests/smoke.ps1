param(
    [int]$Port = 5199,
    [string]$NodePath = $env:NODE_EXE
)

$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Net.Http
$projectPath = Split-Path -Parent $PSScriptRoot
$baseUrl = "http://127.0.0.1:$Port"
$process = $null

function Assert-Equal($Expected, $Actual, [string]$Message) {
    if ($Expected -ne $Actual) {
        throw "$Message. Esperado: $Expected. Obtenido: $Actual"
    }
}

try {
    Push-Location $projectPath

    dotnet build --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "La compilación falló."
    }

    if ([string]::IsNullOrWhiteSpace($NodePath)) {
        $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
        if ($nodeCommand) {
            $NodePath = $nodeCommand.Source
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($NodePath)) {
        Get-ChildItem -LiteralPath "wwwroot/js" -Filter "*.js" | ForEach-Object {
            & $NodePath --check $_.FullName
            if ($LASTEXITCODE -ne 0) {
                throw "JavaScript inválido: $($_.Name)"
            }
        }
    }
    else {
        Write-Warning "Node.js no está disponible; se omite la validación sintáctica de JavaScript."
    }

    $env:ASPNETCORE_ENVIRONMENT = "Development"
    $env:Logging__EventLog__LogLevel__Default = "None"

    $processArgs = @{
        FilePath = "dotnet"
        ArgumentList = @("run", "--no-build", "--no-launch-profile", "--urls", $baseUrl)
        WorkingDirectory = $projectPath
        WindowStyle = "Hidden"
        PassThru = $true
    }
    $process = Start-Process @processArgs

    $handler = [System.Net.Http.HttpClientHandler]::new()
    $handler.AllowAutoRedirect = $false
    $client = [System.Net.Http.HttpClient]::new($handler)

    $ready = $false
    for ($attempt = 0; $attempt -lt 40; $attempt++) {
        try {
            $response = $client.GetAsync("$baseUrl/").GetAwaiter().GetResult()
            if ([int]$response.StatusCode -eq 200) {
                $ready = $true
                break
            }
        }
        catch {
            Start-Sleep -Milliseconds 250
        }
    }

    if (-not $ready) {
        throw "La aplicación no inició a tiempo."
    }

    Assert-Equal 200 ([int]$response.StatusCode) "La portada no respondió"
    if (-not $response.Headers.Contains("Content-Security-Policy")) {
        throw "Falta Content-Security-Policy."
    }
    if (-not $response.Headers.Contains("X-Content-Type-Options")) {
        throw "Falta X-Content-Type-Options."
    }
    $homeContent = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    if ($homeContent -notmatch 'buscarObraHistorial') {
        throw "Faltan los filtros del historial de obras."
    }

    $reclamosResponse = $client.GetAsync("$baseUrl/api/reclamos").GetAwaiter().GetResult()
    Assert-Equal 200 ([int]$reclamosResponse.StatusCode) "Falló la API de reclamos"
    $reclamosJson = $reclamosResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult()
    $reclamos = $reclamosJson | ConvertFrom-Json
    foreach ($reclamo in $reclamos) {
        if ($reclamo.latitud -lt -38.20 -or $reclamo.latitud -gt -37.70 -or
            $reclamo.longitud -lt -57.85 -or $reclamo.longitud -gt -57.30) {
            throw "El reclamo $($reclamo.id) tiene coordenadas fuera del área admitida."
        }
    }

    $adminResponse = $client.GetAsync("$baseUrl/api/admin").GetAwaiter().GetResult()
    Assert-Equal 401 ([int]$adminResponse.StatusCode) "El panel admin quedó expuesto"

    $missingResponse = $client.GetAsync("$baseUrl/api/no-existe").GetAwaiter().GetResult()
    Assert-Equal 404 ([int]$missingResponse.StatusCode) "Una ruta API inexistente no devuelve 404"
    $missingType = $missingResponse.Content.Headers.ContentType.MediaType
    Assert-Equal "application/json" $missingType "El 404 de API no es JSON"

    $invalidMonth = $client.GetAsync("$baseUrl/api/datos-publicos/obras?anio=2026&mes=13").GetAwaiter().GetResult()
    Assert-Equal 400 ([int]$invalidMonth.StatusCode) "No se rechazó un mes inválido"

    $longPeriod = $client.GetAsync("$baseUrl/api/datos-publicos/obras/periodo?anioDesde=2025&mesDesde=1&anioHasta=2026&mesHasta=1").GetAwaiter().GetResult()
    Assert-Equal 400 ([int]$longPeriod.StatusCode) "No se rechazó un período excesivo"

    $comparisonResponse = $client.GetAsync("$baseUrl/api/datos-publicos/obras/comparacion-tramos?anioDesde=2024").GetAwaiter().GetResult()
    Assert-Equal 200 ([int]$comparisonResponse.StatusCode) "Fallo la comparacion historica de tramos"

    $categoriasResponse = $client.GetAsync("$baseUrl/api/datos-publicos/categorias").GetAwaiter().GetResult()
    Assert-Equal 200 ([int]$categoriasResponse.StatusCode) "Fallo el catalogo de categorias"
    $categorias = ($categoriasResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).categorias
    Assert-Equal 18 $categorias.Count "El catalogo no contiene todas las categorias oficiales"

    $administracionResponse = $client.GetAsync("$baseUrl/api/datos-publicos/administracion-publica?anio=2026&planta=todas").GetAwaiter().GetResult()
    Assert-Equal 200 ([int]$administracionResponse.StatusCode) "Fallo Administracion Publica"
    $administracion = ($administracionResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json).resumen
    if ($administracion.totalAgentes -le 0 -or $administracion.tendencia.Count -ne 3) {
        throw "El resumen de Administracion Publica esta incompleto."
    }

    $delegacionesResponse = $client.GetAsync("$baseUrl/api/datos-publicos/administracion-publica/delegaciones").GetAwaiter().GetResult()
    Assert-Equal 200 ([int]$delegacionesResponse.StatusCode) "Fallo el GeoJSON de delegaciones"
    $delegaciones = $delegacionesResponse.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
    Assert-Equal "FeatureCollection" $delegaciones.type "La capa de delegaciones no es GeoJSON"
    if ($delegaciones.features.Count -le 0) {
        throw "La capa de delegaciones no contiene poligonos."
    }

    Write-Host "Smoke tests correctos."
}
finally {
    if ($process -and -not $process.HasExited) {
        Stop-Process -Id $process.Id
        $process.WaitForExit()
    }

    if ($client) {
        $client.Dispose()
    }
    if ($handler) {
        $handler.Dispose()
    }

    Pop-Location
}
