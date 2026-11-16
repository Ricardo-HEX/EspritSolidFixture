# Esprit Solid Fixture

Extensión para ESPRIT EDGE 2026.2 destinada a convertir sólidos CAD de utillaje en accesorios de máquina.

El botón grande **Preparación de utillaje** se integra en la pestaña **Inicio**, junto a los comandos de configuración. Antes de abrir la ventana comprueba que el documento tenga una máquina cargada. Si no existe, pregunta si el usuario desea seleccionar una; al aceptar abre un selector en la carpeta de máquinas configurada por el cliente, carga el archivo `.mprj` y continúa automáticamente. Con una máquina válida activa la vista de pieza y abre una ventana flotante no modal.

La ventana sigue la identidad visual de ESPRIT y permite seleccionar un sólido de **Pieza**, un sólido de **Bruto** y uno o varios sólidos de **Utillaje**. Los recuadros tienen fondo blanco en reposo y naranja durante la captura. Utiliza el selector nativo `GetAnyElement` con el filtro `espSolidModel`, por lo que solo permite seleccionar sólidos completos. Se pueden seleccionar varios utillajes consecutivamente y finalizar con `Esc`. Incluye fase, carpeta de salida y botones **Aceptar** y **Cancelar**. Aceptar solo se habilita cuando están definidos pieza, bruto, al menos un utillaje y una ruta CAD válida.

El botón **Selección por ventana** permite añadir varios utillajes de una vez. Al activarlo se limpia la selección gráfica para que el usuario arrastre una ventana normal en la vista de ESPRIT. Al pulsar **Añadir selección**, la extensión importa únicamente los sólidos completos contenidos en el grupo y descarta cualquier cara, arista u otro tipo de objeto.

Al aceptar, la extensión actualiza la primera pieza de trabajo de **Configuración de pieza**: sustituye la geometría de la pieza por el sólido seleccionado y sustituye los materiales existentes por un material de tipo **Sólido** vinculado al bruto elegido. Después genera los GDML y crea el montaje en árbol. Cuando los utillajes no se combinan, los ordena por su cota Z mínima respecto al plano de trabajo: monta el más bajo sobre la máquina, cada accesorio siguiente sobre el anterior y la pieza sobre el último. Cuando se combinan, monta la pieza sobre el único accesorio generado.

La ventana se posiciona automáticamente en la esquina superior izquierda de ESPRIT, debajo de la cinta, y utiliza una cabecera con icono, acento verde y tipografía coherentes con ESPRIT EDGE.

El selector **Destino del montaje** permite trabajar en la configuración actual o crear una nueva configuración. Al elegir **Nueva configuración**, la extensión establece automáticamente Fase 2 y **Cara superior / Giro Y 180° + Z 180°**; al elegir la fase 2 también selecciona el mismo destino. En Fase 2 se pueden dejar vacíos los recuadros de pieza y bruto para reutilizar la pieza de trabajo y el material resultante de la fase anterior. La nueva configuración conserva la máquina, elimina el montaje copiado de la configuración anterior y crea el nuevo árbol.

La salida utiliza la carpeta de accesorios configurada por cada cliente (`espFileTypeFixtures`) y crea dentro `Utillajes_<NombreCAD>`.

Al aceptar se genera un GDML independiente por cada sólido, con el nombre
`Utillaje_<NombreCAD>_Fase_<X>_Solido_<N>.gdml`. La geometría no se desplaza en X ni
en Y: solo se normaliza en Z respecto a la altura mínima del sólido. El adaptador de
accesorio `FA_1` se sitúa a la altura superior, mientras que el adaptador de pieza
`WA_1` recibe el mismo desplazamiento Z que la geometría para conservar el plano
original del conjunto tras normalizar el origen.

El botón **Combinar sólidos** permite crear un único GDML con toda la selección. En
ese modo, el origen se sitúa en el centro de la base del sólido más bajo y `FA_1`
en el centro superior del sólido que alcanza la mayor altura.

El selector **Cara de montaje** permite elegir la cara inferior (Z normal) o la cara
superior. Los GDML conservan siempre la geometría original. En montaje superior la
extensión gira el accesorio raíz 180 grados sobre Y y después 180 grados sobre Z;
todo el árbol de utillajes y la pieza heredan la misma transformación rígida. No se
crean reflejos ni copias temporales. En modo combinado se toma la Z inferior del
conjunto para la geometría y la Z superior para `FA_1`; `WA_1` continúa en el plano
original mediante el mismo desplazamiento Z aplicado a la geometría.

Los utillajes de la lista se pueden retirar seleccionándolos y pulsando `Supr`, o mediante clic derecho y **Eliminar**.

El menú **Eliminar** muestra una X roja. Los sólidos añadidos permanecen seleccionados gráficamente; al seleccionarlos de nuevo durante la captura se desmarcan y se retiran de la lista.
