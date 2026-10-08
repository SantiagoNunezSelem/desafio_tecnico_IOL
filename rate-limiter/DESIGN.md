
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


# Alternativas consideradas (librería de Microsoft)

Encontre en la pagina oficial de Microsoft la libreria "System.Threading.RateLimiting" que permite implementar de forma mucho mas sencilla los metodos de un rate limiter.

Por interpretacion propia asumo que la utilzacion de esta libreria reduciria la complejidad del desafio, y por lo tanto, el sentido del mismo.

Es por ello que decidi no utilizarla para este desafio, pero en un ambiente productivo seria recomendable utilizarla.

# Ejecutar el proyecto

[completar al final del proyecto]

