
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
Permitir compartir el estado entre varias instancias utilizando Redis. Esto sera opcional para el usario ya que requiere tener docker instalado.

# Utilizacion de IA

En el transcurso del desafío me acompaño utilizando Claude Code. La idea es aprovecharla para escribir gran parte del código, y que mi rol sea controlar los avances, comprender las implementaciones, corregir cuando haga falta y tomar las decisiones estructurales.

Para poder controlar los avances y gestionar las decisiones, el código se desarrolla en 5 etapas. En cada una reviso el código, hago preguntas hasta entenderlo y dejo comentarios propios en lo que necesite aclarar.

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

## 5) Implementar Redis
Se implementa un almacenamiento de estado en Redis, para que varias instancias de la aplicacion compartan los mismos contadores y el limite se aplique sobre el total de requests, no por instancia.
Debido a que desconozco si la persona que pruebe mi proyecto tendra intalado Docker, esta opcion se activara por configuracion. Por default esta opcion no estara habilitada.
Se implementan también los test correspondientes.

# Arquitectura

[completar al final del proyecto]

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


# Alternativas consideradas (librería de Microsoft)

Encontre en la pagina oficial de Microsoft la libreria "System.Threading.RateLimiting" que permite implementar de forma mucho mas sencilla los metodos de un rate limiter.
Por interpretacion propia asumo que la utilzacion de esta libreria reduciria la complejidad del desafio, y por lo tanto, el sentido del mismo.
Es por ello que decidi no utilizarla para este desafio, pero en un ambiente productivo seria recomendable utilizarla.

# Ejecutar el proyecto

[completar al final del proyecto]

