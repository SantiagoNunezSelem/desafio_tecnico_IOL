
# Introduccion

A cotinuacion voy a detallar mi paso a paso para poder llevar a cabo el desafio tecnico para la entrevista de SSr Back-End Developer en IOL.
Estoy realmente muy contento de tener esta posibilidad.

# Proyecto: Rate Limiter

Antes de comenzar me descargue los dos libros mencionados en el desafio tecnico.
"System Design Interview An Insider’s Guide by Alex Xu"
"John Ousterhout - A Philosophy of Software Design, 2nd Edition (2021, Yaknyam Press)"
Para seleccionar el proyecto a desarrollar lei el indice de "System Design Interview" y me decidi por realizar el desafio de la unidad 4, el "Rate Limiter".
Seleccione este proyecto debido a que de las opciones disponibles es el problema que requiere comprender conceptos que me resultan familiares.

# Requisitos

Rechazar los request que superan el límite configurado con respuestas de baja latencia y utilizando poca memoria.
Informar al cliente cuando recibe un rechazo (HTTP 429 y headers con el estado del límite).
Las reglas deben ser configurables y que el algoritmo se pueda elegir (voy a implementar 2 para comparar).

# Utilizacion de IA

En el transcurso del desafío me acompaño utilizando Claude Code. La idea es aprovecharla para escribir gran parte del código, y que mi rol sea controlar los avances, comprender las implementaciones, corregir cuando haga falta y tomar las decisiones estructurales.

Para poder controlar los avances y gestionar las decisiones, el código se desarrolla en 4 etapas. En cada una reviso el código, hago preguntas hasta entenderlo y dejo comentarios propios en lo que necesite aclarar.

# Etapas de desarrollo

## 1) Estructura base del proyecto con algoritmo "Token Bucket"
Se implementa la estructura base del proyecto que incluye la funcionalidad del algoritmo Token Bucket.
Se implementan tambien los test correspondientes utilizando XUnit.

## 2) Algoritmo "Sliding window counter"
Se implementa un segundo algoritmo de rate limiter para poder compararlos en escenarios diferentes.
Se implementa una regla de configuracion para seleccionar cual utilizar.
Se implementan los test correspondientes.

## 3) Limitador por IP
Se implementa un registro que mantiene un limitador independiente por cliente (usuario o IP). De esta forma el consumo de uno no afecta a los demas.
Funciona con cualquiera de los dos algoritmos.
Se implementa también la limpieza de los limitadores inactivos para controlar el uso de memoria.
Se implementan los test correspondientes. En estos test se van a incluir escenarios de concurrencia.

## 4) Middleware HTTP
Se implementa una API web con ASP.NET Core y un middleware que consulta al registro de limitadores en cada request entrante antes de que llegue a los endpoints.
Se implementa el proceso para aceptar o rechazar request, indicandole al usuario el estado e informacion correspondiente.
Se implementan los test correspondientes, incluyendo los necesarios para levantar la API en memoria.

# Arquitectura

La solución tiene tres proyectos:

- `src/RateLimiter.Core`: la lógica del rate limiter. No depende de ASP.NET ni de HTTP.
- `src/RateLimiter.Api`: la API web con el middleware y un endpoint de ejemplo (`GET /ping`).
- `tests/RateLimiter.Tests`: los tests de los dos proyectos anteriores.

Recorrido de un request:

1. El cliente envía un request a la API, por ejemplo `GET /ping`.
2. El request llega primero a `RateLimitMiddleware`, que obtiene la IP del cliente.
3. El middleware le pregunta a `RateLimiterRegistry` si ese cliente puede hacer el request.
4. El registro busca el rate limiter de ese cliente. Si no existe, lo crea.
5. El rate limiter (`TokenBucket` o `SlidingWindowCounter`, según la configuración) decide si el request se acepta y devuelve un `RateLimitDecision`.
6. El middleware lee esa decisión y agrega los headers con el estado del límite.
7. Si el request fue aceptado, sigue hacia el endpoint y se responde 200.
8. Si fue rechazado, el middleware responde 429 y el request no llega al endpoint.

Clases de `RateLimiter.Core`:

- `IRateLimiter`: la interfaz que implementan los dos algoritmos.
- `TokenBucket` y `SlidingWindowCounter`: los algoritmos. Cada instancia limita a un solo cliente.
- `RateLimitDecision`: el resultado de consultar a un rate limiter (aceptado o no, límite, restantes y tiempo de espera).
- `RateLimiterOptions`: la regla de configuración (algoritmo, límite y ventana).
- `RateLimiterFactory`: crea el algoritmo que indica la regla.
- `RateLimiterRegistry`: mantiene un rate limiter por cliente y elimina los inactivos.

# Decisiones

## Token Bucket

- Recarga sin timer. El libro muestra un proceso que agrega tokens cada cierto tiempo. En su lugar, el bucket guarda el momento de la última recarga y, cuando llega un request, calcula los tokens ganados desde entonces. El resultado es el mismo y un bucket sin uso no consume CPU.
- Los tokens recargados se guardan solo cuando un request es aprobado. Si no hay tokens suficientes, el request se rechaza y el guardado se posterga hasta el siguiente request aprobado. No se pierde nada, porque la recarga se calcula siempre a partir del tiempo transcurrido desde el último guardado. De esta forma se evitan escrituras que no aportan valor y, sobre todo, errores de redondeo: guardar una recarga parcial en cada rechazo acumulaba decimales (sumar diez veces 0,1 no da exactamente 1) y un cliente podía quedar rechazado un instante de más. Lo detectó un test.

## Sliding Window Counter

- Toma dos contadores por usuario. Se guarda la cantidad de requests de la ventana actual y de la anterior. Con ellos se estima un aproximado de request que hubo en la ventana deslizante. Este metodo optimiza el uso de la memoria mucho mejor que el "sliding window log" ya que en lugar de guardar un timestamp por request, guarda 2 contadores por cliente.
- Decidi que el estimado se redondea hacia arriba. Un request se acepta solo si el estimado no supera el límite.
- Las ventanas se cuentan desde que se crea la instancia, no desde el minuto en punto del reloj. Así no dependemos de la fecha y hora del sistema.

## Comparación de los algoritmos

- Parámetros. El token bucket usa la capacidad y los tokens por segundo. El sliding window counter usa el límite (capacidad de ventana deslizante) y la duración de la ventana.
- Ráfagas. El token bucket permite una ráfaga de request de hasta la capacidad y luego recarga de forma continua. El sliding window counter suaviza: después de una ventana llena libera un lugar cada (ventana / límite).
- Precisión. El token bucket es exacto. El sliding window counter es aproximado.
- Memoria por cliente. El token bucket guarda cant tokens y el instante donde se guardo los tokens. El sliding window counter guarda dos contadores (cant tokens en ventana actual y anterior) y el número de ventana (index).

Con 5 requests por minuto: Un cliente que estuvo inactivo puede enviar 5 requests juntos en los dos algoritmos. 
El token bucket acepta la ráfaga y además devuelve un token cada 12 segundos, por lo que en un minuto puede aceptar hasta 10 requests. 
El sliding window counter mantiene el total cerca de 5 en cualquier minuto.

## Decisiones comunes

- Una interfaz, `IRateLimiter`, con un solo método.
- Una sola regla de configuración para los dos algoritmos: "`Limit` requests cada `Window`". `RateLimiterFactory` la traduce a los parámetros de cada algoritmo. En el token bucket, la capacidad es `Limit` y se recarga por completo en una `Window`.
- Un `lock` por limitador. Leer el estado, decidir y actualizar tiene que ser un solo paso. Sino dos requests simultáneos podrían generar races conditions. Como cada cliente va a tener su propio limitador, clientes distintos no se bloquean entre sí.
- Los tests usan un reloj falso que avanza solo cuando se lo indican, por lo que no esperan tiempo real.

## Registro de limitadores por cliente

- Un limitador por cliente, creado con el primer request de ese cliente.
- Un `Dictionary` protegido por un `lock`, en lugar de un `ConcurrentDictionary`. El lock cubre solo buscar el limitador y marcarlo como usado. El consumo se hace afuera, con el lock propio de cada rate limiter. Con un `ConcurrentDictionary` la limpieza podría eliminar un limitador justo cuando otro request lo está por usar, y ese request se perdería. El costo es que todos los requests pasan por un mismo lock, aunque por muy poco tiempo. Si eso se volviera un cuello de botella, se podría dividir el diccionario en varios, cada uno con su lock.
- Un rate limiter se elimina cuando pasa dos ventanas sin requests. En ese tiempo los dos algoritmos vuelven a su estado inicial.
- La limpieza no usa un timer ni un hilo en segundo plano. Se ejecuta durante un request, como mucho una vez cada dos ventanas. El costo es que ese request recorre todo el diccionario, y que si no llega ningún request no se libera memoria.

## Middleware HTTP

- El rate limiter es un middleware y no código dentro de cada endpoint. Se ejecuta antes que cualquier endpoint, por lo que un request rechazado no llega a consumir recursos de la aplicación y los endpoints no saben que existe.
- El cliente se identifica por su dirección IP. Si la API estuviera detrás de un proxy, todos los requests llegarían con la IP del proxy y habría que leer el header `X-Forwarded-For`, confiando solo en proxies conocidos. No lo implementé porque depende de la infraestructura. Con usuarios autenticados convendría usar el id de usuario.
- Si no se puede obtener la IP, esos requests comparten un mismo límite. Preferí eso antes que dejarlos sin limitar.
- Los headers `X-Ratelimit-Limit` y `X-Ratelimit-Remaining` se envían en todas las respuestas. En un rechazo se responde HTTP 429 con `X-Ratelimit-Retry-After` y también `Retry-After`, que es el header estándar de HTTP. Los segundos se redondean hacia arriba para que el cliente no reintente antes de tiempo.
- La regla se lee de `appsettings.json` al iniciar. Una regla inválida impide que la aplicación arranque, en lugar de fallar con el primer request. Cambiar la regla requiere reiniciar la aplicación.
- El registro es un singleton: una única instancia compartida por todos los requests. Por eso el estado es por instancia de la aplicación; con varias instancias cada una aplicaría su propio límite.
- Hay dos tipos de tests. Los del middleware lo prueban aislado, con un request armado a mano y el reloj falso. Los de la API la levantan completa en memoria y verifican que la configuración, el middleware y el endpoint están bien conectados.


# Alternativas consideradas (librería de Microsoft)

Encontre en la pagina oficial de Microsoft la libreria "System.Threading.RateLimiting" que permite implementar de forma mucho mas sencilla los metodos de un rate limiter.
Por interpretacion propia asumo que la utilzacion de esta libreria reduciria la complejidad del desafio, y por lo tanto, el sentido del mismo.
Es por ello que decidi no utilizarla para este desafio, pero en un ambiente productivo seria recomendable utilizarla.

# Ejecutar el proyecto

Se necesita el SDK de .NET 8 o superior. Todos los comandos se ejecutan desde la carpeta `rate-limiter`.

Compilar:

```
dotnet build
```

Ejecutar los tests:

```
dotnet test
```

Levantar la API (queda escuchando en `http://localhost:5277`):

```
dotnet run --project src/RateLimiter.Api
```

Enviar un request desde otra terminal:

```
curl -i http://localhost:5277/ping
```

Con la configuración por defecto se permiten 5 requests cada 10 segundos por IP. Los primeros responden `200` con los headers `X-Ratelimit-Limit` y `X-Ratelimit-Remaining`. Al superar el límite responde `429` con `X-Ratelimit-Retry-After`.

La regla se cambia en `src/RateLimiter.Api/appsettings.json`:

```json
"RateLimiter": {
  "Algorithm": "TokenBucket",
  "Limit": 5,
  "Window": "00:00:10"
}
```

- `Algorithm`: `TokenBucket` o `SlidingWindowCounter`.
- `Limit`: cantidad de requests permitidos por ventana.
- `Window`: duración de la ventana, en formato `hh:mm:ss`.

