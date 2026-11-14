# Esprit Solid Fixture

Extensión para ESPRIT EDGE 2026.2 destinada a convertir sólidos CAD de utillaje en accesorios de máquina.

El botón grande **Preparación de utillaje** se integra en la pestaña **Inicio**, junto a los comandos de configuración. Al pulsarlo activa la vista de pieza y abre una ventana flotante no modal para seleccionar uno o varios sólidos de utillaje.

La ventana sigue la identidad visual de ESPRIT. El recuadro de utillajes tiene fondo blanco en reposo y naranja durante la captura. Utiliza el selector nativo `GetAnyElement` con el filtro `espSolidModel`, por lo que solo permite seleccionar sólidos completos. Se pueden seleccionar varios consecutivamente y finalizar con `Esc`. Incluye fase, carpeta de salida y botones **Aceptar** y **Cancelar**. Aceptar solo se habilita con al menos un utillaje y una ruta CAD válida.

La ventana se posiciona automáticamente en la esquina superior izquierda de ESPRIT, debajo de la cinta, y utiliza una cabecera con icono, acento verde y tipografía coherentes con ESPRIT EDGE.

La salida utiliza la carpeta de accesorios configurada por cada cliente (`espFileTypeFixtures`) y crea dentro `Utillajes_<NombreCAD>`.

Al aceptar se genera un GDML independiente por cada sólido, con el nombre
`Utillaje_<NombreCAD>_Fase_<X>_Solido_<N>.gdml`. La geometría no se desplaza en X ni
en Y: solo se normaliza en Z respecto a la altura mínima del sólido. El adaptador de
accesorio `FA_1` se sitúa a la altura superior, mientras que el adaptador de pieza
`WA_1` permanece exactamente en el plano original, sin transformación.

El botón **Combinar sólidos** permite crear un único GDML con toda la selección. En
ese modo, el origen se sitúa en el centro de la base del sólido más bajo y `FA_1`
en el centro superior del sólido que alcanza la mayor altura.

El selector **Cara de montaje** permite elegir la cara inferior (Z normal) o la cara
superior (Z invertido). En montaje superior ESPRIT crea copias temporales y refleja
físicamente los sólidos respecto al plano XY global; después los exporta, normaliza
solo su coordenada Z y elimina las copias. X e Y se mantienen sin desplazamiento. En
modo combinado se toma la Z inferior del conjunto para la geometría y la Z superior
para `FA_1`; `WA_1` continúa en el plano original.

Los utillajes de la lista se pueden retirar seleccionándolos y pulsando `Supr`, o mediante clic derecho y **Eliminar**.

El menú **Eliminar** muestra una X roja. Los sólidos añadidos permanecen seleccionados gráficamente; al seleccionarlos de nuevo durante la captura se desmarcan y se retiran de la lista.
